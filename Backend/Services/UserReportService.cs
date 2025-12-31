using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// 用户报表服务实现
/// </summary>
public class UserReportService : IUserReportService
{
    private readonly PlayLinkerDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenEncryptionService _encryptionService;
    private readonly ILogger<UserReportService> _logger;
    private const int STEAM_PLATFORM_ID = 1;
    private const string STEAM_API_BASE = "https://api.steampowered.com";

    public UserReportService(
        PlayLinkerDbContext context,
        IHttpClientFactory httpClientFactory,
        ITokenEncryptionService encryptionService,
        ILogger<UserReportService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户报表概览
    /// </summary>
    public async Task<UserReportOverviewDto> GetUserReportOverviewAsync(int userId)
    {
        var result = new UserReportOverviewDto();

        // 获取用户资料
        result.Profile = await GetUserProfileAsync(userId);

        // 获取游戏库统计
        result.GameLibrary = await GetGameLibraryStatsAsync(userId);

        // 获取成就统计
        result.Achievements = await GetAchievementStatsAsync(userId);

        // 获取最近游玩记录
        result.RecentPlayed = await GetRecentPlayedGamesAsync(userId, 10);

        // 获取愿望单
        result.Wishlist = await GetWishlistAsync(userId);

        return result;
    }

    /// <summary>
    /// 获取用户资料摘要（仅从数据库获取，不调用Steam API）
    /// </summary>
    private async Task<UserProfileSummaryDto> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return new UserProfileSummaryDto { UserId = userId };
        }

        var profile = new UserProfileSummaryDto
        {
            UserId = userId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl
        };

        // 获取Steam绑定信息（仅从数据库）
        var steamBinding = await _context.UserPlatformBindings
            .Include(b => b.PlayerPlatform)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);

        if (steamBinding != null)
        {
            profile.SteamId = steamBinding.PlatformUserId;
            profile.SteamProfileName = steamBinding.PlayerPlatform?.ProfileName;
            profile.Country = steamBinding.PlayerPlatform?.Country;
            profile.AccountCreated = steamBinding.PlayerPlatform?.AccountCreated?.ToString("yyyy-MM-dd");
            // Steam等级、徽章、好友数需要通过同步功能获取
        }

        return profile;
    }

    /// <summary>
    /// 获取游戏库详细统计
    /// </summary>
    public async Task<GameLibrarySummaryDto> GetGameLibraryStatsAsync(int userId)
    {
        var result = new GameLibrarySummaryDto();

        // 获取Steam绑定
        var steamBinding = await _context.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);

        if (steamBinding == null)
        {
            return result;
        }

        // 从数据库获取游戏库数据
        var libraryGames = await _context.UserPlatformLibraries
            .Include(l => l.Game)
                .ThenInclude(g => g.GameGenres)
                    .ThenInclude(gg => gg.Genre)
            .Where(l => l.PlatformUserId == steamBinding.PlatformUserId && l.PlatformId == STEAM_PLATFORM_ID)
            .ToListAsync();

        result.TotalGames = libraryGames.Count;
        result.TotalPlaytimeMinutes = libraryGames.Sum(g => g.PlaytimeMinutes);
        result.TotalPlaytimeFormatted = FormatPlaytime(result.TotalPlaytimeMinutes);
        result.PlayedGames = libraryGames.Count(g => g.PlaytimeMinutes > 0);
        result.NeverPlayedGames = libraryGames.Count(g => g.PlaytimeMinutes == 0);

        // 最近2周游玩时长（暂时跳过Steam API调用，需要同步时获取）
        // result.RecentPlaytimeMinutes = 0;

        // 按类型统计游戏时长
        var genrePlaytime = libraryGames
            .SelectMany(l => l.Game.GameGenres.Select(gg => new { Genre = gg.Genre.Name, l.PlaytimeMinutes }))
            .GroupBy(x => x.Genre)
            .Select(g => new PlaytimeByGenreDto
            {
                Genre = g.Key,
                PlaytimeMinutes = g.Sum(x => x.PlaytimeMinutes),
                GameCount = g.Count()
            })
            .OrderByDescending(x => x.PlaytimeMinutes)
            .Take(10)
            .ToList();

        var totalGenrePlaytime = genrePlaytime.Sum(x => x.PlaytimeMinutes);
        foreach (var genre in genrePlaytime)
        {
            genre.Percentage = totalGenrePlaytime > 0 ? Math.Round((double)genre.PlaytimeMinutes / totalGenrePlaytime * 100, 1) : 0;
        }
        result.PlaytimeByGenre = genrePlaytime;

        // TOP 10 最常玩游戏
        result.TopPlayedGames = libraryGames
            .OrderByDescending(l => l.PlaytimeMinutes)
            .Take(10)
            .Select(l => new TopPlayedGameDto
            {
                GameId = l.GameId,
                GameName = l.Game.Name,
                HeaderImage = l.Game.HeaderImage,
                PlaytimeMinutes = l.PlaytimeMinutes,
                PlaytimeFormatted = FormatPlaytime(l.PlaytimeMinutes),
                LastPlayed = l.LastPlayed?.ToString("yyyy-MM-dd HH:mm"),
                AchievementsUnlocked = l.AchievementsUnlocked,
                AchievementsTotal = l.AchievementsTotal
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// 获取成就详细统计
    /// </summary>
    public async Task<AchievementSummaryDto> GetAchievementStatsAsync(int userId)
    {
        var result = new AchievementSummaryDto();

        // 获取用户所有成就
        var userAchievements = await _context.UserAchievements
            .Include(ua => ua.Achievement)
                .ThenInclude(a => a.Game)
            .Where(ua => ua.UserId == userId)
            .ToListAsync();

        result.TotalAchievements = userAchievements.Count;
        result.UnlockedAchievements = userAchievements.Count(ua => ua.Unlocked);
        result.CompletionRate = result.TotalAchievements > 0 
            ? Math.Round((double)result.UnlockedAchievements / result.TotalAchievements * 100, 1) 
            : 0;

        // 计算完美游戏数
        var gameAchievementStats = userAchievements
            .GroupBy(ua => ua.Achievement.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                Total = g.Count(),
                Unlocked = g.Count(ua => ua.Unlocked)
            })
            .ToList();

        result.PerfectGames = gameAchievementStats.Count(g => g.Total > 0 && g.Total == g.Unlocked);

        // 最近解锁的成就
        result.RecentUnlocks = userAchievements
            .Where(ua => ua.Unlocked && ua.UnlockTime.HasValue)
            .OrderByDescending(ua => ua.UnlockTime)
            .Take(10)
            .Select(ua => new RecentAchievementDto
            {
                AchievementId = ua.AchievementId,
                AchievementName = ua.Achievement.AchievementName,
                DisplayName = ua.Achievement.DisplayName,
                Description = ua.Achievement.Description,
                IconUnlocked = ua.Achievement.IconUnlocked,
                GameId = ua.Achievement.GameId,
                GameName = ua.Achievement.Game.Name,
                UnlockTime = ua.UnlockTime?.ToString("yyyy-MM-dd HH:mm")
            })
            .ToList();

        // 各游戏成就进度
        result.GameProgress = await _context.UserPlatformLibraries
            .Include(l => l.Game)
            .Where(l => l.AchievementsTotal > 0)
            .OrderByDescending(l => l.AchievementsUnlocked)
            .Take(20)
            .Select(l => new GameAchievementProgressDto
            {
                GameId = l.GameId,
                GameName = l.Game.Name,
                HeaderImage = l.Game.HeaderImage,
                TotalAchievements = l.AchievementsTotal ?? 0,
                UnlockedAchievements = l.AchievementsUnlocked ?? 0,
                CompletionRate = l.AchievementsTotal > 0 
                    ? Math.Round((double)(l.AchievementsUnlocked ?? 0) / l.AchievementsTotal.Value * 100, 1) 
                    : 0
            })
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 获取最近游玩记录（优先从数据库获取）
    /// </summary>
    public async Task<List<RecentPlayedGameDto>> GetRecentPlayedGamesAsync(int userId, int count = 10)
    {
        var steamBinding = await _context.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);

        if (steamBinding == null)
        {
            return new List<RecentPlayedGameDto>();
        }

        // 优先从数据库获取（避免API超时）
        var dbGames = await _context.UserPlatformLibraries
            .Include(l => l.Game)
            .Where(l => l.PlatformUserId == steamBinding.PlatformUserId && l.PlatformId == STEAM_PLATFORM_ID && l.LastPlayed.HasValue)
            .OrderByDescending(l => l.LastPlayed)
            .Take(count)
            .ToListAsync();

        return dbGames.Select(l => new RecentPlayedGameDto
            {
                GameId = l.GameId,
                GameName = l.Game.Name,
                HeaderImage = l.Game.HeaderImage,
                PlaytimeMinutes = l.PlaytimeMinutes,
                LastPlayed = l.LastPlayed?.ToString("yyyy-MM-dd HH:mm")
            })
            .ToList();
    }

    /// <summary>
    /// 获取愿望单（暂时返回空，需要同步时获取）
    /// </summary>
    public async Task<WishlistSummaryDto> GetWishlistAsync(int userId)
    {
        var result = new WishlistSummaryDto();

        // 愿望单数据需要通过同步功能从Steam获取
        // 这里暂时返回空数据，避免API超时
        await Task.CompletedTask;

        return result;
    }


    /// <summary>
    /// 从Steam同步用户数据
    /// </summary>
    public async Task<SyncResultDto> SyncFromSteamAsync(int userId)
    {
        var result = new SyncResultDto { SyncTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };

        try
        {
            var steamBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);

            if (steamBinding == null)
            {
                result.Message = "未绑定Steam账号";
                return result;
            }

            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                result.Message = "未找到Steam API Key";
                return result;
            }

            var steamId = steamBinding.PlatformUserId;

            // 同步游戏库
            result.GamesSync = await SyncOwnedGamesAsync(userId, steamId, apiKey);

            // 同步成就
            result.AchievementsSync = await SyncAchievementsAsync(userId, steamId, apiKey);

            // 更新同步时间
            steamBinding.LastSyncTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            result.Success = true;
            result.Message = "同步成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步Steam数据失败: userId={UserId}", userId);
            result.Message = $"同步失败: {ex.Message}";
        }

        return result;
    }

    #region Steam API 调用方法

    /// <summary>
    /// 获取Steam API Key
    /// </summary>
    private async Task<string?> GetSteamApiKeyAsync(int userId)
    {
        var binding = await _context.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);

        if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
        {
            return null;
        }

        return _encryptionService.DecryptToken(binding.AccessToken);
    }

    /// <summary>
    /// 获取Steam用户统计（等级、徽章、好友）
    /// </summary>
    private async Task<(int Level, int Badges, int Friends)> GetSteamUserStatsAsync(string steamId, string apiKey)
    {
        var httpClient = _httpClientFactory.CreateClient();
        int level = 0, badges = 0, friends = 0;

        try
        {
            // 获取等级
            var levelUrl = $"{STEAM_API_BASE}/IPlayerService/GetSteamLevel/v1/?key={apiKey}&steamid={steamId}";
            var levelResponse = await httpClient.GetAsync(levelUrl);
            if (levelResponse.IsSuccessStatusCode)
            {
                var content = await levelResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("player_level", out var levelProp))
                {
                    level = levelProp.GetInt32();
                }
            }

            // 获取徽章
            var badgesUrl = $"{STEAM_API_BASE}/IPlayerService/GetBadges/v1/?key={apiKey}&steamid={steamId}";
            var badgesResponse = await httpClient.GetAsync(badgesUrl);
            if (badgesResponse.IsSuccessStatusCode)
            {
                var content = await badgesResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("response", out var response) &&
                    response.TryGetProperty("badges", out var badgesArray))
                {
                    badges = badgesArray.GetArrayLength();
                }
            }

            // 获取好友数
            var friendsUrl = $"{STEAM_API_BASE}/ISteamUser/GetFriendList/v1/?key={apiKey}&steamid={steamId}&relationship=friend";
            var friendsResponse = await httpClient.GetAsync(friendsUrl);
            if (friendsResponse.IsSuccessStatusCode)
            {
                var content = await friendsResponse.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("friendslist", out var friendsList) &&
                    friendsList.TryGetProperty("friends", out var friendsArray))
                {
                    friends = friendsArray.GetArrayLength();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取Steam用户统计失败: steamId={SteamId}", steamId);
        }

        return (level, badges, friends);
    }

    /// <summary>
    /// 获取最近2周游玩时长
    /// </summary>
    private async Task<int> GetRecentPlaytimeFromSteamAsync(string steamId, string apiKey)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{STEAM_API_BASE}/IPlayerService/GetRecentlyPlayedGames/v1/?key={apiKey}&steamid={steamId}";
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("response", out var responseData) &&
                    responseData.TryGetProperty("games", out var games))
                {
                    int totalMinutes = 0;
                    foreach (var game in games.EnumerateArray())
                    {
                        if (game.TryGetProperty("playtime_2weeks", out var playtime))
                        {
                            totalMinutes += playtime.GetInt32();
                        }
                    }
                    return totalMinutes;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取最近游玩时长失败: steamId={SteamId}", steamId);
        }

        return 0;
    }

    /// <summary>
    /// 从Steam获取最近游玩的游戏
    /// </summary>
    private async Task<List<RecentPlayedGameDto>> GetRecentPlayedFromSteamAsync(string steamId, string apiKey, int count)
    {
        var result = new List<RecentPlayedGameDto>();

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{STEAM_API_BASE}/IPlayerService/GetRecentlyPlayedGames/v1/?key={apiKey}&steamid={steamId}&count={count}";
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("response", out var responseData) &&
                    responseData.TryGetProperty("games", out var games))
                {
                    foreach (var game in games.EnumerateArray())
                    {
                        var appId = game.GetProperty("appid").GetInt32();
                        var name = game.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var playtimeForever = game.TryGetProperty("playtime_forever", out var ptf) ? ptf.GetInt32() : 0;
                        var playtime2Weeks = game.TryGetProperty("playtime_2weeks", out var pt2w) ? pt2w.GetInt32() : 0;
                        var imgIconUrl = game.TryGetProperty("img_icon_url", out var icon) ? icon.GetString() : "";

                        // 查找数据库中的游戏
                        var dbGame = await _context.GamePlatforms
                            .Include(gp => gp.Game)
                            .FirstOrDefaultAsync(gp => gp.PlatformGameId == appId.ToString() && gp.PlatformId == STEAM_PLATFORM_ID);

                        result.Add(new RecentPlayedGameDto
                        {
                            GameId = dbGame?.GameId ?? 0,
                            GameName = name,
                            HeaderImage = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/header.jpg",
                            PlaytimeMinutes = playtimeForever,
                            RecentPlaytimeMinutes = playtime2Weeks
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取最近游玩游戏失败: steamId={SteamId}", steamId);
        }

        return result;
    }

    /// <summary>
    /// 从Steam获取愿望单
    /// </summary>
    private async Task<List<UserReportWishlistItemDto>> GetWishlistFromSteamAsync(string steamId, string apiKey)
    {
        var result = new List<UserReportWishlistItemDto>();

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            // Steam愿望单API（公开愿望单）
            var url = $"https://store.steampowered.com/wishlist/profiles/{steamId}/wishlistdata/?p=0";
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out var appId))
                    {
                        var gameData = prop.Value;
                        var name = gameData.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var priority = gameData.TryGetProperty("priority", out var priorityProp) ? priorityProp.GetInt32() : 0;
                        var added = gameData.TryGetProperty("added", out var addedProp) ? addedProp.GetInt64() : 0;

                        // 获取价格信息
                        int? currentPrice = null;
                        int? originalPrice = null;
                        int? discountPercent = null;
                        bool isOnSale = false;

                        if (gameData.TryGetProperty("subs", out var subs) && subs.GetArrayLength() > 0)
                        {
                            var firstSub = subs[0];
                            if (firstSub.TryGetProperty("price", out var priceProp))
                            {
                                currentPrice = priceProp.GetInt32();
                            }
                            if (firstSub.TryGetProperty("discount_pct", out var discProp))
                            {
                                discountPercent = discProp.GetInt32();
                                isOnSale = discountPercent > 0;
                            }
                        }

                        result.Add(new UserReportWishlistItemDto
                        {
                            SteamAppId = appId,
                            GameName = name,
                            HeaderImage = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/header.jpg",
                            Priority = priority,
                            AddedTime = added > 0 ? DateTimeOffset.FromUnixTimeSeconds(added).ToString("yyyy-MM-dd") : null,
                            CurrentPrice = currentPrice,
                            DiscountPercent = discountPercent,
                            IsOnSale = isOnSale
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取愿望单失败: steamId={SteamId}", steamId);
        }

        return result.OrderBy(i => i.Priority).ToList();
    }

    #endregion

    #region 同步方法

    /// <summary>
    /// 同步拥有的游戏
    /// </summary>
    private async Task<int> SyncOwnedGamesAsync(int userId, string steamId, string apiKey)
    {
        int syncCount = 0;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{STEAM_API_BASE}/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={steamId}&include_appinfo=true&include_played_free_games=true";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return 0;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("response", out var responseData) ||
                !responseData.TryGetProperty("games", out var games))
            {
                return 0;
            }

            foreach (var game in games.EnumerateArray())
            {
                var appId = game.GetProperty("appid").GetInt32();
                var name = game.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                var playtimeForever = game.TryGetProperty("playtime_forever", out var ptf) ? ptf.GetInt32() : 0;
                var lastPlayed = game.TryGetProperty("rtime_last_played", out var rtp) ? rtp.GetInt64() : 0;

                // 查找或创建游戏
                var gamePlatform = await _context.GamePlatforms
                    .Include(gp => gp.Game)
                    .FirstOrDefaultAsync(gp => gp.PlatformGameId == appId.ToString() && gp.PlatformId == STEAM_PLATFORM_ID);

                long gameId;
                if (gamePlatform == null)
                {
                    // 创建新游戏
                    var newGame = new Game
                    {
                        Name = name,
                        HeaderImage = $"https://steamcdn-a.akamaihd.net/steam/apps/{appId}/header.jpg"
                    };
                    _context.Games.Add(newGame);
                    await _context.SaveChangesAsync();

                    _context.GamePlatforms.Add(new GamePlatform
                    {
                        GameId = newGame.GameId,
                        PlatformId = STEAM_PLATFORM_ID,
                        PlatformGameId = appId.ToString(),
                        GamePlatformUrl = $"https://store.steampowered.com/app/{appId}"
                    });
                    await _context.SaveChangesAsync();

                    gameId = newGame.GameId;
                }
                else
                {
                    gameId = gamePlatform.GameId;
                }

                // 更新用户游戏库
                var library = await _context.UserPlatformLibraries
                    .FirstOrDefaultAsync(l => l.PlatformUserId == steamId && l.PlatformId == STEAM_PLATFORM_ID && l.GameId == gameId);

                if (library == null)
                {
                    library = new UserPlatformLibrary
                    {
                        PlatformUserId = steamId,
                        PlatformId = STEAM_PLATFORM_ID,
                        GameId = gameId,
                        PlaytimeMinutes = playtimeForever,
                        LastPlayed = lastPlayed > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime : null
                    };
                    _context.UserPlatformLibraries.Add(library);
                }
                else
                {
                    library.PlaytimeMinutes = playtimeForever;
                    library.LastPlayed = lastPlayed > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime : library.LastPlayed;
                }

                syncCount++;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步游戏库失败: steamId={SteamId}", steamId);
        }

        return syncCount;
    }

    /// <summary>
    /// 同步成就（只同步最近玩过的10个游戏，避免API超时）
    /// </summary>
    private async Task<int> SyncAchievementsAsync(int userId, string steamId, string apiKey)
    {
        int syncCount = 0;

        try
        {
            // 只获取最近玩过的10个游戏，避免API调用过多
            var userGames = await _context.UserPlatformLibraries
                .Where(l => l.PlatformUserId == steamId && l.PlatformId == STEAM_PLATFORM_ID && l.PlaytimeMinutes > 0)
                .OrderByDescending(l => l.LastPlayed)
                .Select(l => new { l.GameId, l.Game.Name })
                .Take(10)
                .ToListAsync();

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // 设置超时

            foreach (var userGame in userGames)
            {
                var gamePlatform = await _context.GamePlatforms
                    .FirstOrDefaultAsync(gp => gp.GameId == userGame.GameId && gp.PlatformId == STEAM_PLATFORM_ID);

                if (gamePlatform == null || !int.TryParse(gamePlatform.PlatformGameId, out var appId))
                    continue;

                try
                {
                    // 获取玩家成就
                    var url = $"{STEAM_API_BASE}/ISteamUserStats/GetPlayerAchievements/v1/?key={apiKey}&steamid={steamId}&appid={appId}";
                    var response = await httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode) continue;

                    var content = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(content);

                    if (!doc.RootElement.TryGetProperty("playerstats", out var playerStats) ||
                        !playerStats.TryGetProperty("achievements", out var achievements))
                    {
                        continue;
                    }

                    int totalAchievements = 0;
                    int unlockedAchievements = 0;

                    foreach (var achievement in achievements.EnumerateArray())
                    {
                        totalAchievements++;
                        var achieved = achievement.TryGetProperty("achieved", out var achievedProp) && achievedProp.GetInt32() == 1;
                        if (achieved) unlockedAchievements++;
                    }

                    // 更新游戏库中的成就统计
                    var library = await _context.UserPlatformLibraries
                        .FirstOrDefaultAsync(l => l.PlatformUserId == steamId && l.PlatformId == STEAM_PLATFORM_ID && l.GameId == userGame.GameId);

                    if (library != null)
                    {
                        library.AchievementsTotal = totalAchievements;
                        library.AchievementsUnlocked = unlockedAchievements;
                        syncCount += unlockedAchievements;
                    }

                    // 短暂延迟避免API限制
                    await Task.Delay(100);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("同步游戏成就超时: gameId={GameId}", userGame.GameId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "同步游戏成就失败: gameId={GameId}", userGame.GameId);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步成就失败: steamId={SteamId}", steamId);
        }

        return syncCount;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 格式化游戏时长
    /// </summary>
    private string FormatPlaytime(long minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes}分钟";
        }
        else if (minutes < 1440) // 24小时
        {
            return $"{minutes / 60}小时{minutes % 60}分钟";
        }
        else
        {
            var hours = minutes / 60;
            return $"{hours}小时";
        }
    }

    #endregion
}
