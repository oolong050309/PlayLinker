using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Data.Common;
using System.Text.Json;

namespace PlayLinker.Controllers;

/// <summary>
/// Steam API集成控制器
/// 提供Steam数据导入、用户信息查询、游戏信息查询等功能
/// </summary>
[ApiController]
[Route("api/v1/steam")]
[Authorize]
public class SteamController : ControllerBase
{
    private readonly ISteamService _steamService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<SteamController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private const int STEAM_PLATFORM_ID = 1; // Steam平台ID

    public SteamController(ISteamService steamService, PlayLinkerDbContext context, ILogger<SteamController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _steamService = steamService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    // 获取当前用户ID
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    /// <summary>
    /// 初始化平台数据，确保所有平台都已存在
    /// 根据API文档中的平台列表初始化platforms表
    /// </summary>
    private async Task InitializePlatformsAsync()
    {
        var platforms = new[]
        {
            new { Id = 1, Name = "Steam", Description = "Valve旗下游戏平台" },
            new { Id = 2, Name = "Epic Games", Description = "Epic Games商店" },
            new { Id = 3, Name = "Origin", Description = "EA游戏平台" },
            new { Id = 4, Name = "Uplay", Description = "Ubisoft游戏平台" },
            new { Id = 5, Name = "GOG", Description = "GOG游戏平台" },
            new { Id = 6, Name = "PSN", Description = "PlayStation Network" },
            new { Id = 7, Name = "Xbox", Description = "Xbox游戏平台" },
            new { Id = 8, Name = "Nintendo Switch", Description = "任天堂Switch平台" }
        };

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        foreach (var platformInfo in platforms)
        {
            // 检查平台是否已存在
            var exists = await _context.Platforms
                .AnyAsync(p => p.PlatformId == platformInfo.Id || p.PlatformName == platformInfo.Name);

            if (!exists)
            {
                // 使用原始SQL插入，确保platform_id正确设置
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO platforms (platform_id, platform_name, description, status) 
                    VALUES (@id, @name, @desc, 1)
                    ON DUPLICATE KEY UPDATE platform_name = VALUES(platform_name), description = VALUES(description)";
                
                var idParam = command.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = platformInfo.Id;
                command.Parameters.Add(idParam);

                var nameParam = command.CreateParameter();
                nameParam.ParameterName = "@name";
                nameParam.Value = platformInfo.Name;
                command.Parameters.Add(nameParam);

                var descParam = command.CreateParameter();
                descParam.ParameterName = "@desc";
                descParam.Value = platformInfo.Description ?? "";
                command.Parameters.Add(descParam);

                try
                {
                    await command.ExecuteNonQueryAsync();
                    _logger.LogInformation("创建平台: {PlatformName} (ID: {PlatformId})", platformInfo.Name, platformInfo.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "创建平台失败: {PlatformName} (ID: {PlatformId})", platformInfo.Name, platformInfo.Id);
                }
            }
        }
    }

    /// <summary>
    /// 导入Steam数据
    /// 将Steam用户数据导入到数据库中
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<SteamImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SteamImportResponseDto>>> ImportSteamData(
        [FromBody] SteamImportRequestDto request)
    {
        try
        {
            // 验证 userId 是否提供
            if (request.UserId <= 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", "userId 参数无效，必须提供有效的用户ID"));
            }

            // 验证用户是否存在
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var checkUserCommand = connection.CreateCommand();
            checkUserCommand.CommandText = "SELECT COUNT(*) FROM `user` WHERE `user_id` = @userId";
            var userIdParam = checkUserCommand.CreateParameter();
            userIdParam.ParameterName = "@userId";
            userIdParam.Value = request.UserId;
            checkUserCommand.Parameters.Add(userIdParam);

            var userExists = Convert.ToInt32(await checkUserCommand.ExecuteScalarAsync()) > 0;

            if (!userExists)
            {
                _logger.LogWarning("用户不存在: userId={UserId}", request.UserId);
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", $"用户ID {request.UserId} 不存在，请先创建用户"));
            }

            var userId = request.UserId;
            _logger.LogInformation("导入Steam数据: userId={UserId}, steamId={SteamId}", userId, request.SteamId);

            // 初始化平台数据（确保Steam平台存在）
            await InitializePlatformsAsync();

            // 生成任务ID
            var taskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            int gamesCount = 0;
            int achievementsCount = 0;
            int friendsCount = 0;

            // 1. 获取并存储Steam用户信息
            var steamUser = await _steamService.GetSteamUser(request.SteamId);
            if (steamUser == null)
            {
                return BadRequest(ApiResponse<SteamImportResponseDto>.ErrorResponse("ERR_STEAM_USER_NOT_FOUND", "Steam用户不存在或资料为私密状态"));
            }

            // 存储或更新PlayerPlatform信息
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.SteamId && pp.PlatformId == STEAM_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = request.SteamId,
                    PlatformId = STEAM_PLATFORM_ID,
                    ProfileName = steamUser.ProfileName,
                    ProfileUrl = steamUser.ProfileUrl,
                    AccountCreated = DateTime.TryParse(steamUser.AccountCreated, out var created) ? created : null,
                    Country = steamUser.Country
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = steamUser.ProfileName;
                playerPlatform.ProfileUrl = steamUser.ProfileUrl;
                playerPlatform.Country = steamUser.Country;
            }
            await _context.SaveChangesAsync();

            // 1.5. 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == STEAM_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = userId,
                    PlatformId = STEAM_PLATFORM_ID,
                    PlatformUserId = request.SteamId,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1) // 默认1年过期，可根据实际需求调整
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                // 更新绑定信息
                userPlatformBinding.PlatformUserId = request.SteamId;
                userPlatformBinding.BindingStatus = true;
                userPlatformBinding.LastSyncTime = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            // 2. 导入游戏库数据
            if (request.ImportGames)
            {
                try
                {
                    var apiKey = _configuration["SteamAPI:ApiKey"] ?? "";
                    var gamesUrl = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={request.SteamId}&include_appinfo=true&include_played_free_games=true";
                    var httpClient = _httpClientFactory.CreateClient();
                    var gamesResponse = await httpClient.GetAsync(gamesUrl);

                    if (gamesResponse.IsSuccessStatusCode)
                    {
                        var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                        var gamesDoc = JsonDocument.Parse(gamesContent);

                        if (gamesDoc.RootElement.TryGetProperty("response", out var gamesResponseData))
                        {
                            if (gamesResponseData.TryGetProperty("games", out var gamesArray))
                            {
                                foreach (var gameElement in gamesArray.EnumerateArray())
                                {
                                    if (!gameElement.TryGetProperty("appid", out var appIdElement)) continue;
                                    var appId = appIdElement.GetInt32();

                                    // 获取游戏详细信息
                                    var steamGame = await _steamService.GetSteamGame(appId);
                                    if (steamGame == null) continue;

                                    // 查找或创建游戏
                                    var game = await _context.Games
                                        .FirstOrDefaultAsync(g => g.Name == steamGame.Name);

                                    if (game == null)
                                    {
                                        // 创建新游戏
                                        game = new Game
                                        {
                                            Name = steamGame.Name,
                                            IsFree = steamGame.IsFree,
                                            RequireAge = (byte?)steamGame.RequiredAge,
                                            ShortDescription = steamGame.ShortDescription,
                                            DetailedDescription = steamGame.DetailedDescription,
                                            HeaderImage = steamGame.HeaderImage,
                                            CapsuleImage = steamGame.HeaderImage, // 使用headerImage作为capsule
                                            Background = steamGame.HeaderImage, // 使用headerImage作为background
                                            Windows = steamGame.Platforms.Windows,
                                            Mac = steamGame.Platforms.Mac,
                                            Linux = steamGame.Platforms.Linux,
                                            ReleaseDate = DateTime.TryParse(steamGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                            ReviewScore = 0,
                                            ReviewScoreDesc = "",
                                            NumReviews = 0,
                                            TotalPositive = 0
                                        };
                                        _context.Games.Add(game);
                                        await _context.SaveChangesAsync();

                                        // 添加开发商
                                        foreach (var devName in steamGame.Developers)
                                        {
                                            if (string.IsNullOrEmpty(devName)) continue;
                                            // 截断名称到20个字符（数据库限制）
                                            var truncatedName = devName.Length > 20 ? devName.Substring(0, 20) : devName;
                                            var developer = await _context.Developers.FirstOrDefaultAsync(d => d.Name == truncatedName);
                                            if (developer == null)
                                            {
                                                developer = new Developer { Name = truncatedName };
                                                _context.Developers.Add(developer);
                                                await _context.SaveChangesAsync();
                                            }
                                            if (!await _context.GameDevelopers.AnyAsync(gd => gd.GameId == game.GameId && gd.DeveloperId == developer.DeveloperId))
                                            {
                                                _context.GameDevelopers.Add(new GameDeveloper { GameId = game.GameId, DeveloperId = developer.DeveloperId });
                                            }
                                        }

                                        // 添加发行商
                                        foreach (var pubName in steamGame.Publishers)
                                        {
                                            if (string.IsNullOrEmpty(pubName)) continue;
                                            // 截断名称到20个字符（数据库限制）
                                            var truncatedName = pubName.Length > 20 ? pubName.Substring(0, 20) : pubName;
                                            var publisher = await _context.Publishers.FirstOrDefaultAsync(p => p.Name == truncatedName);
                                            if (publisher == null)
                                            {
                                                publisher = new Publisher { Name = truncatedName };
                                                _context.Publishers.Add(publisher);
                                                await _context.SaveChangesAsync();
                                            }
                                            if (!await _context.GamePublishers.AnyAsync(gp => gp.GameId == game.GameId && gp.PublisherId == publisher.PublisherId))
                                            {
                                                _context.GamePublishers.Add(new GamePublisher { GameId = game.GameId, PublisherId = publisher.PublisherId });
                                            }
                                        }
                                    }

                                    // 创建或更新游戏平台映射
                                    if (!await _context.GamePlatforms.AnyAsync(gp => gp.GameId == game.GameId && gp.PlatformId == STEAM_PLATFORM_ID))
                                    {
                                        _context.GamePlatforms.Add(new GamePlatform
                                        {
                                            GameId = game.GameId,
                                            PlatformId = STEAM_PLATFORM_ID,
                                            PlatformGameId = appId.ToString(),
                                            GamePlatformUrl = $"https://store.steampowered.com/app/{appId}"
                                        });
                                    }

                                    // 创建或更新用户平台游戏库记录
                                    var playtimeMinutes = gameElement.TryGetProperty("playtime_forever", out var playtime) ? playtime.GetInt32() : 0;
                                    var lastPlayed = gameElement.TryGetProperty("rtime_last_played", out var rtime) && rtime.GetInt64() > 0
                                        ? DateTimeOffset.FromUnixTimeSeconds(rtime.GetInt64()).DateTime
                                        : (DateTime?)null;

                                    var userGame = await _context.UserPlatformLibraries
                                        .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.SteamId 
                                            && upl.PlatformId == STEAM_PLATFORM_ID 
                                            && upl.GameId == game.GameId);

                                    if (userGame == null)
                                    {
                                        _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                        {
                                            PlatformUserId = request.SteamId,
                                            PlatformId = STEAM_PLATFORM_ID,
                                            GameId = game.GameId,
                                            PlaytimeMinutes = playtimeMinutes,
                                            LastPlayed = lastPlayed
                                        });
                                    }
                                    else
                                    {
                                        userGame.PlaytimeMinutes = playtimeMinutes;
                                        userGame.LastPlayed = lastPlayed;
                                    }

                                    gamesCount++;
                                }

                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "导入游戏库数据失败");
                }
            }

            // 3. 导入成就数据
            if (request.ImportAchievements)
            {
                try
                {
                    // 获取用户游戏列表
                    var userGames = await _context.UserPlatformLibraries
                        .Where(upl => upl.PlatformUserId == request.SteamId && upl.PlatformId == STEAM_PLATFORM_ID)
                        .ToListAsync();

                    var apiKey = _configuration["SteamAPI:ApiKey"] ?? "";
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogWarning("Steam API Key 未配置");
                    }
                    else
                    {
                        var httpClient = _httpClientFactory.CreateClient();

                        foreach (var userGame in userGames)
                        {
                            // 获取游戏的Steam AppID
                            var gamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.GameId == userGame.GameId && gp.PlatformId == STEAM_PLATFORM_ID);

                            int appId;
                            if (gamePlatform == null)
                            {
                                // 尝试直接使用 gameId 作为 AppID（如果 gameId 本身就是 Steam AppID）
                                if (!int.TryParse(userGame.GameId.ToString(), out appId))
                                {
                                    _logger.LogWarning("无法获取游戏的 Steam AppID: gameId={GameId}", userGame.GameId);
                                    continue;
                                }
                                _logger.LogInformation("使用 gameId 作为 AppID: gameId={GameId}, appId={AppId}", userGame.GameId, appId);
                            }
                            else
                            {
                                if (!int.TryParse(gamePlatform.PlatformGameId, out appId))
                                {
                                    _logger.LogWarning("无法解析 PlatformGameId: PlatformGameId={PlatformGameId}", gamePlatform.PlatformGameId);
                                    continue;
                                }
                            }

                            // 获取用户成就数据
                            var achievementsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={apiKey}&steamid={request.SteamId}&appid={appId}&l=schinese";
                            var achievementsResponse = await httpClient.GetAsync(achievementsUrl);

                            if (achievementsResponse.IsSuccessStatusCode)
                            {
                                var achievementsContent = await achievementsResponse.Content.ReadAsStringAsync();
                                var achievementsDoc = JsonDocument.Parse(achievementsContent);

                                if (achievementsDoc.RootElement.TryGetProperty("playerstats", out var playerStats))
                                {
                                    if (playerStats.TryGetProperty("achievements", out var achievementsArray))
                                    {
                                        var achievementsCountInGame = achievementsArray.GetArrayLength();
                                        _logger.LogInformation("找到 {Count} 个成就: gameId={GameId}, appId={AppId}", 
                                            achievementsCountInGame, userGame.GameId, appId);

                                        // 批量加载该游戏的所有成就，避免N+1查询问题
                                        var gameAchievements = await _context.Achievements
                                            .Where(a => a.GameId == userGame.GameId)
                                            .ToDictionaryAsync(a => a.AchievementName, a => a);

                                        // 第一遍：收集需要创建的新成就
                                        var newAchievementsToCreate = new List<Achievement>();
                                        foreach (var achElement in achievementsArray.EnumerateArray())
                                        {
                                            if (!achElement.TryGetProperty("apiname", out var apiName)) continue;
                                            var achievementName = apiName.GetString();
                                            if (string.IsNullOrEmpty(achievementName)) continue;

                                            // 如果数据库中不存在该成就，准备创建
                                            if (!gameAchievements.ContainsKey(achievementName))
                                            {
                                                var displayName = achElement.TryGetProperty("name", out var nameProp) 
                                                    ? nameProp.GetString() ?? achievementName 
                                                    : achievementName;
                                                var description = achElement.TryGetProperty("description", out var descProp) 
                                                    ? descProp.GetString() 
                                                    : null;

                                                var newAchievement = new Achievement
                                                {
                                                    GameId = userGame.GameId,
                                                    AchievementName = achievementName,
                                                    DisplayName = displayName ?? achievementName,
                                                    Description = description,
                                                    Hidden = false, // 默认不隐藏
                                                    IconUnlocked = "", // 默认空，后续可以通过 GetSchemaForGame API 获取
                                                    IconLocked = "" // 默认空，后续可以通过 GetSchemaForGame API 获取
                                                };
                                                newAchievementsToCreate.Add(newAchievement);
                                                gameAchievements[achievementName] = newAchievement; // 预先添加到字典
                                            }
                                        }

                                        // 批量创建新成就
                                        if (newAchievementsToCreate.Count > 0)
                                        {
                                            _context.Achievements.AddRange(newAchievementsToCreate);
                                            await _context.SaveChangesAsync(); // 保存以获取 AchievementId
                                            _logger.LogInformation("批量创建 {Count} 个新成就: gameId={GameId}", 
                                                newAchievementsToCreate.Count, userGame.GameId);
                                        }

                                        // 批量加载该用户已有的成就记录（包括新创建的成就）
                                        var allAchievementIds = gameAchievements.Values.Select(a => a.AchievementId).ToList();
                                        var existingUserAchievements = await _context.UserAchievements
                                            .Where(ua => ua.UserId == userId 
                                                && ua.PlatformId == STEAM_PLATFORM_ID
                                                && allAchievementIds.Contains(ua.AchievementId))
                                            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua);

                                        // 第二遍：处理用户成就数据
                                        foreach (var achElement in achievementsArray.EnumerateArray())
                                        {
                                            // 提取成就名称（apiname）
                                            if (!achElement.TryGetProperty("apiname", out var apiName)) continue;
                                            var achievementName = apiName.GetString();
                                            if (string.IsNullOrEmpty(achievementName)) continue;

                                            // 提取成就状态（achieved，注意：Steam API 返回的是 achieved 而不是 unlocked）
                                            var achieved = achElement.TryGetProperty("achieved", out var achievedProp) && achievedProp.GetInt32() == 1;
                                            
                                            // 提取解锁时间（unlocktime）
                                            var unlockTime = achElement.TryGetProperty("unlocktime", out var unlockTimeProp) && unlockTimeProp.GetInt64() > 0
                                                ? DateTimeOffset.FromUnixTimeSeconds(unlockTimeProp.GetInt64()).DateTime
                                                : (DateTime?)null;

                                            // 从内存字典中查找成就（应该已经存在）
                                            if (!gameAchievements.TryGetValue(achievementName, out var achievement))
                                            {
                                                _logger.LogWarning("成就未找到: gameId={GameId}, achievementName={AchievementName}", 
                                                    userGame.GameId, achievementName);
                                                continue;
                                            }

                                            // 从内存字典中查找用户成就记录
                                            if (existingUserAchievements.TryGetValue(achievement.AchievementId, out var userAchievement))
                                            {
                                                // 更新现有记录
                                                userAchievement.Unlocked = achieved;
                                                userAchievement.UnlockTime = unlockTime;
                                            }
                                            else
                                            {
                                                // 创建新用户成就记录
                                                _context.UserAchievements.Add(new UserAchievement
                                                {
                                                    UserId = userId,
                                                    AchievementId = achievement.AchievementId,
                                                    PlatformId = STEAM_PLATFORM_ID,
                                                    Unlocked = achieved,
                                                    UnlockTime = unlockTime
                                                });
                                                achievementsCount++;
                                            }
                                        }

                                        await _context.SaveChangesAsync();
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Steam API 返回数据中没有 achievements 数组: gameId={GameId}, appId={AppId}", 
                                            userGame.GameId, appId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Steam API 返回数据中没有 playerstats 对象: gameId={GameId}, appId={AppId}", 
                                        userGame.GameId, appId);
                                }
                            }
                            else
                            {
                                var errorContent = await achievementsResponse.Content.ReadAsStringAsync();
                                _logger.LogWarning("Steam API 调用失败: gameId={GameId}, appId={AppId}, StatusCode={StatusCode}, Content={Content}", 
                                    userGame.GameId, appId, achievementsResponse.StatusCode, errorContent);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "导入成就数据失败");
                }
            }

            // 4. 更新用户游戏库统计（统计该用户在所有平台的数据，而不仅仅是Steam）
            // 获取该用户绑定的所有平台账号的 platform_user_id 列表
            var userPlatformUserIds = await _context.UserPlatformBindings
                .Where(upb => upb.UserId == userId && upb.BindingStatus)
                .Select(upb => upb.PlatformUserId)
                .ToListAsync();

            if (userPlatformUserIds.Count == 0)
            {
                _logger.LogWarning("用户 {UserId} 没有绑定的平台账号", userId);
            }

            // 统计所有平台的游戏数据（使用 platform_user_id 列表）
            var allPlatformGames = await _context.UserPlatformLibraries
                .Where(upl => userPlatformUserIds.Contains(upl.PlatformUserId))
                .Select(upl => new { upl.GameId, upl.PlaytimeMinutes, upl.LastPlayed })
                .ToListAsync();

            // 去重统计游戏数量（不同平台可能有相同游戏）
            var uniqueGameIds = allPlatformGames.Select(g => g.GameId).Distinct().ToList();
            var totalGamesOwned = allPlatformGames.Count; // 所有平台的游戏记录总数
            var totalPlaytimeAllPlatforms = allPlatformGames.Sum(g => g.PlaytimeMinutes);

            // 统计所有平台的解锁成就数
            var unlockedAchievementsAllPlatforms = await _context.UserAchievements
                .CountAsync(ua => ua.UserId == userId && ua.Unlocked);

            // 统计所有平台的成就总数
            var totalAchievementsAllPlatforms = await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .Distinct()
                .CountAsync();

            // 计算最近游玩的游戏数量（最近30天）
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentlyPlayedCount = allPlatformGames
                .Where(g => g.LastPlayed.HasValue && g.LastPlayed.Value >= thirtyDaysAgo)
                .Select(g => g.GameId)
                .Distinct()
                .Count();

            // 计算最近30天的游戏时长
            var recentPlaytimeMinutes = allPlatformGames
                .Where(g => g.LastPlayed.HasValue && g.LastPlayed.Value >= thirtyDaysAgo)
                .Sum(g => g.PlaytimeMinutes);

            var userLibrary = await _context.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            if (userLibrary == null)
            {
                userLibrary = new UserGameLibrary
                {
                    UserId = userId,
                    TotalGamesOwned = totalGamesOwned,
                    GamesPlayed = uniqueGameIds.Count, // 去重后的游戏数量
                    TotalPlaytimeMinutes = totalPlaytimeAllPlatforms,
                    TotalAchievements = totalAchievementsAllPlatforms,
                    UnlockedAchievements = unlockedAchievementsAllPlatforms,
                    RecentlyPlayedCount = recentlyPlayedCount,
                    RecentPlaytimeMinutes = recentPlaytimeMinutes
                };
                _context.UserGameLibraries.Add(userLibrary);
            }
            else
            {
                userLibrary.TotalGamesOwned = totalGamesOwned;
                userLibrary.GamesPlayed = uniqueGameIds.Count; // 去重后的游戏数量
                userLibrary.TotalPlaytimeMinutes = totalPlaytimeAllPlatforms;
                userLibrary.TotalAchievements = totalAchievementsAllPlatforms;
                userLibrary.UnlockedAchievements = unlockedAchievementsAllPlatforms;
                userLibrary.RecentlyPlayedCount = recentlyPlayedCount;
                userLibrary.RecentPlaytimeMinutes = recentPlaytimeMinutes;
            }
            await _context.SaveChangesAsync();

            var result = new SteamImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                EstimatedTime = 0,
                Items = new SteamImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount,
                    Friends = friendsCount
                }
            };

            return Ok(ApiResponse<SteamImportResponseDto>.SuccessResponse(result, "Steam数据导入成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Steam数据时发生错误");
            return StatusCode(500, ApiResponse<SteamImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Steam用户信息
    /// </summary>
    /// <param name="steamId">Steam用户ID</param>
    [HttpGet("user/{steamId}")]
    [ProducesResponseType(typeof(ApiResponse<SteamUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SteamUserDto>>> GetSteamUser(string steamId)
    {
        try
        {
            _logger.LogInformation("获取Steam用户信息: steamId={SteamId}", steamId);

            var result = await _steamService.GetSteamUser(steamId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_USER_NOT_FOUND", "Steam用户不存在或资料为私密状态"));
            }

            return Ok(ApiResponse<SteamUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam用户信息时发生错误");
            return StatusCode(500, ApiResponse<SteamUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Steam游戏信息
    /// </summary>
    /// <param name="appId">Steam AppID</param>
    [HttpGet("games/{appId}")]
    [ProducesResponseType(typeof(ApiResponse<SteamGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SteamGameDto>>> GetSteamGame(int appId)
    {
        try
        {
            _logger.LogInformation("获取Steam游戏信息: appId={AppId}", appId);

            var result = await _steamService.GetSteamGame(appId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_GAME_NOT_FOUND", "Steam游戏不存在"));
            }

            return Ok(ApiResponse<SteamGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam游戏信息时发生错误");
            return StatusCode(500, ApiResponse<SteamGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

