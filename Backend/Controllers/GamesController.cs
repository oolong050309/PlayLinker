using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net;

namespace PlayLinker.Controllers;

/// <summary>
/// 游戏数据API控制器
/// </summary>
[ApiController]
[Route("api/v1/games")]
public class GamesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<GamesController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ITokenEncryptionService _tokenEncryptionService;
    private readonly ISteamService _steamService;
    private const int STEAM_PLATFORM_ID = 1; // 假设 Steam 平台 ID 为 1

    public GamesController(
        PlayLinkerDbContext context, 
        ILogger<GamesController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ITokenEncryptionService tokenEncryptionService,
        ISteamService steamService)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _tokenEncryptionService = tokenEncryptionService;
        _steamService = steamService;
    }

    /// <summary>
    /// 获取游戏列表
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GameListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameListDto>>> GetGames(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? platform = null,
        [FromQuery] string? genre = null,
        [FromQuery] bool? isFree = null,
        [FromQuery] string? q = null) // [修改] 新增搜索参数
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Games.AsQueryable();

            // [修改] 增加搜索逻辑
            if (!string.IsNullOrWhiteSpace(q))
            {
                // 使用 Contains 进行模糊匹配
                query = query.Where(g => g.Name.Contains(q));
            }

            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(g => g.GameGenres.Any(gg => gg.Genre!.Name == genre));
            }

            if (isFree.HasValue)
            {
                query = query.Where(g => g.IsFree == isFree.Value);
            }

            query = sortBy?.ToLower() switch
            {
                "release_date" => query.OrderByDescending(g => g.ReleaseDate),
                "name" => query.OrderBy(g => g.Name),
                "popularity" => query.OrderByDescending(g => g.TotalPositive),
                _ => query.OrderByDescending(g => g.GameId)
            };

            var total = await query.CountAsync();
            var games = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .Include(g => g.GameRanking)
                .ToListAsync();

            var items = games.Select(g => new GameItemDto
            {
                GameId = g.GameId,
                Name = g.Name,
                IsFree = g.IsFree,
                ReleaseDate = g.ReleaseDate.ToString("yyyy-MM-dd"),
                HeaderImage = g.HeaderImage,
                Genres = g.GameGenres.Select(gg => gg.Genre?.Name ?? "").ToList(),
                Platforms = new PlatformSupportDto
                {
                    Windows = g.Windows,
                    Mac = g.Mac,
                    Linux = g.Linux
                },
                ReviewScore = g.ReviewScore,
                TotalPositive = g.TotalPositive,
                CurrentPlayers = g.GameRanking?.PeakPlayers ?? 0
            }).ToList();

            var result = new GameListDto
            {
                Items = items,
                Meta = new PaginationMeta { Page = page, PageSize = pageSize, Total = total }
            };

            return Ok(ApiResponse<GameListDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏列表时发生错误");
            return StatusCode(500, ApiResponse<GameListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 搜索游戏
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<GameListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameListDto>>> SearchGames(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(ApiResponse<GameListDto>.SuccessResponse(new GameListDto
                {
                    Items = new List<GameItemDto>(),
                    Meta = new PaginationMeta { Page = page, PageSize = page_size, Total = 0 }
                }));
            }

            page = Math.Max(1, page);
            page_size = Math.Clamp(page_size, 1, 100);

            _logger.LogInformation("搜索游戏: query={Query}, page={Page}, pageSize={PageSize}", query, page, page_size);

            // 模糊搜索游戏名称
            var searchQuery = _context.Games
                .Where(g => EF.Functions.Like(g.Name, $"%{query}%"))
                .OrderBy(g => g.Name);

            var total = await searchQuery.CountAsync();
            var games = await searchQuery
                .Skip((page - 1) * page_size)
                .Take(page_size)
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .Include(g => g.GameDevelopers).ThenInclude(gd => gd.Developer)
                .ToListAsync();

            _logger.LogInformation("搜索到 {Count} 个游戏", total);

            var items = games.Select(g => new GameItemDto
            {
                GameId = g.GameId,
                Name = g.Name,
                IsFree = g.IsFree,
                ReleaseDate = g.ReleaseDate.ToString("yyyy-MM-dd"),
                HeaderImage = g.HeaderImage,
                Genres = g.GameGenres.Select(gg => gg.Genre?.Name ?? "").ToList(),
                Platforms = new PlatformSupportDto
                {
                    Windows = g.Windows,
                    Mac = g.Mac,
                    Linux = g.Linux
                }
            }).ToList();

            var result = new GameListDto
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = page_size,
                    Total = total
                }
            };

            return Ok(ApiResponse<GameListDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索游戏时发生错误: query={Query}", query);
            return StatusCode(500, ApiResponse<GameListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取游戏详情
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<GameDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GameDetailDto>>> GetGame(long id)
    {
        try
        {
            var game = await _context.Games
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .Include(g => g.GameDevelopers).ThenInclude(gd => gd.Developer)
                .Include(g => g.GamePublishers).ThenInclude(gp => gp.Publisher)
                .Include(g => g.GameCategories).ThenInclude(gc => gc.Category)
                .Include(g => g.GameLanguages).ThenInclude(gl => gl.Language)
                .FirstOrDefaultAsync(g => g.GameId == id);

            if (game == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            var detail = new GameDetailDto
            {
                GameId = game.GameId,
                Name = game.Name,
                IsFree = game.IsFree,
                RequireAge = game.RequireAge,
                ShortDescription = game.ShortDescription,
                DetailedDescription = game.DetailedDescription,
                Media = new GameMediaDto
                {
                    HeaderImage = game.HeaderImage,
                    CapsuleImage = game.CapsuleImage,
                    Background = game.Background,
                    Screenshots = new List<string>(),
                    Videos = new List<string>()
                },
                Requirements = new GameRequirementsDto
                {
                    PcMinimum = game.PcMinimum,
                    PcRecommended = game.PcRecommended,
                    MacMinimum = game.MacMinimum,
                    MacRecommended = game.MacRecommended,
                    LinuxMinimum = game.LinuxMinimum,
                    LinuxRecommended = game.LinuxRecommended
                },
                Genres = game.GameGenres.Select(gg => new GenreDto { GenreId = gg.Genre?.GenreId ?? 0, Name = gg.Genre?.Name ?? "" }).ToList(),
                Developers = game.GameDevelopers.Select(gd => new DeveloperDto { DeveloperId = gd.Developer?.DeveloperId ?? 0, Name = gd.Developer?.Name ?? "" }).ToList(),
                Publishers = game.GamePublishers.Select(gp => new PublisherDto { PublisherId = gp.Publisher?.PublisherId ?? 0, Name = gp.Publisher?.Name ?? "" }).ToList(),
                Categories = game.GameCategories.Select(gc => new CategoryDto { CategoryId = gc.Category?.CategoryId ?? 0, Name = gc.Category?.Name ?? "" }).ToList(),
                Languages = game.GameLanguages.Select(gl => new LanguageDto { LanguageId = gl.Language?.LanguageId ?? 0, Name = gl.Language?.LanguageName ?? "" }).ToList(),
                Platforms = new PlatformSupportDto { Windows = game.Windows, Mac = game.Mac, Linux = game.Linux },
                ReleaseDate = game.ReleaseDate.ToString("yyyy-MM-dd"),
                Reviews = new GameReviewsDto { Score = game.ReviewScore, ScoreDesc = game.ReviewScoreDesc, TotalReviews = game.NumReviews, TotalPositive = game.TotalPositive }
            };

            return Ok(ApiResponse<GameDetailDto>.SuccessResponse(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏详情时发生错误: gameId={GameId}", id);
            return StatusCode(500, ApiResponse<GameDetailDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取游戏排行榜
    /// </summary>
    [HttpGet("ranking")]
    [ProducesResponseType(typeof(ApiResponse<GameRankingListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameRankingListDto>>> GetGameRanking(
        [FromQuery] string type = "popular",
        [FromQuery] int limit = 100)
    {
        try
        {
            // 1. 尝试从数据库查询
            var rankings = await _context.GameRankings
                .Where(r => r.CurrentRank.HasValue && r.CurrentRank > 0)
                .OrderBy(r => r.CurrentRank)
                .Take(limit)
                .Include(r => r.Game)
                .Select(r => new GameRankingItemDto
                {
                    RankId = (int)r.RankId,
                    GameId = (int)r.GameId,
                    GameName = r.Game.Name,
                    HeaderImage = r.Game.HeaderImage,
                    CurrentRank = r.CurrentRank ?? 0,
                    LastWeekRank = r.LastWeekRank,
                    PeakPlayers = r.PeakPlayers ?? 0
                })
                .ToListAsync();

            // 2. 如果数据库有数据，直接返回
            if (rankings.Count > 0)
            {
                return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
                { 
                    Items = rankings, 
                    TotalCount = rankings.Count 
                }));
            }

            // 3. 数据库为空，触发实时拉取并入库 (冷启动)
            _logger.LogInformation("数据库暂无排行榜数据，正在从 Steam 实时拉取...");
            
            var success = await FetchAndSaveRankingsAsync(limit);
            
            if (success)
            {
                // 拉取成功后，再次查库
                rankings = await _context.GameRankings
                    .Where(r => r.CurrentRank.HasValue && r.CurrentRank > 0)
                    .OrderBy(r => r.CurrentRank)
                    .Take(limit)
                    .Include(r => r.Game)
                    .Select(r => new GameRankingItemDto
                    {
                        RankId = (int)r.RankId,
                        GameId = (int)r.GameId,
                        GameName = r.Game.Name,
                        HeaderImage = r.Game.HeaderImage,
                        CurrentRank = r.CurrentRank ?? 0,
                        LastWeekRank = r.LastWeekRank,
                        PeakPlayers = r.PeakPlayers ?? 0
                    })
                    .ToListAsync();

                 return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
                { 
                    Items = rankings, 
                    TotalCount = rankings.Count 
                }));
            }

            // 4. 真的拉取失败了，返回空列表（因为不让用 Mock 数据误导用户，或者返回空更符合逻辑）
            return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
            { 
                Items = new List<GameRankingItemDto>(), 
                TotalCount = 0 
            }, "暂无排行榜数据，且无法连接到Steam更新"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取排行榜失败");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器错误"));
        }
    }

    /// <summary>
    /// 从 Steam 拉取排行榜并保存到数据库 (冷启动逻辑)
    /// </summary>
    private async Task<bool> FetchAndSaveRankingsAsync(int limit)
    {
        try
        {
            var steamApiKey = await GetValidSteamApiKeyAsync();
            if (string.IsNullOrEmpty(steamApiKey)) return false;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var requestUrl = $"https://api.steampowered.com/ISteamChartsService/GetMostPlayedGames/v1/?key={steamApiKey}&count={limit}";
            
            var response = await client.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode) return false;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            if (!doc.RootElement.TryGetProperty("response", out var responseEl) || 
                !responseEl.TryGetProperty("ranks", out var ranksEl))
            {
                return false;
            }

            // 1. 获取 Steam 排行榜中的 AppID 列表
            var steamRankings = new List<(int Rank, int SteamAppId, int Peak)>();
            foreach (var item in ranksEl.EnumerateArray())
            {
                var rank = item.GetProperty("rank").GetInt32();
                var appId = item.GetProperty("appid").GetInt32();
                var peak = item.GetProperty("peak_in_game").GetInt32();
                steamRankings.Add((rank, appId, peak));
            }

            // 2. [核心修正] 在本地数据库中查找这些 AppID 对应的 GameId
            // 使用 GamePlatforms 表来匹配：PlatformId=1 (Steam) 且 PlatformGameId 匹配 SteamAppId
            var steamAppIds = steamRankings.Select(x => x.SteamAppId.ToString()).ToList();
            
            var matchedGames = await _context.GamePlatforms
                .Where(gp => gp.PlatformId == STEAM_PLATFORM_ID && steamAppIds.Contains(gp.PlatformGameId))
                .Select(gp => new { gp.GameId, gp.PlatformGameId })
                .ToListAsync();

            // 建立映射字典: SteamAppId (string) -> Local GameId (long)
            var appIdToGameIdMap = matchedGames.ToDictionary(k => k.PlatformGameId, v => v.GameId);

            // 3. 更新 Rankings 表
            var now = DateTime.UtcNow;
            var existingRankings = await _context.GameRankings.ToListAsync();

            // 3.1 归档现有排名
            foreach (var r in existingRankings)
            {
                r.LastWeekRank = r.CurrentRank; 
                r.CurrentRank = null;           
                r.UpdatedAt = now;
            }

            // 3.2 插入/更新新排名
            // 只有在本地 GamePlatforms 表中找到的游戏才会被加入排行榜
            foreach (var item in steamRankings)
            {
                var appIdStr = item.SteamAppId.ToString();
                
                // 如果本地没有这个游戏，直接跳过，不插入任何 Games 表数据！
                if (!appIdToGameIdMap.TryGetValue(appIdStr, out var localGameId))
                {
                    continue; 
                }

                var existing = existingRankings.FirstOrDefault(r => r.GameId == localGameId);
                if (existing != null)
                {
                    existing.CurrentRank = item.Rank;
                    existing.PeakPlayers = item.Peak;
                    existing.UpdatedAt = now;
                }
                else
                {
                    _context.GameRankings.Add(new GameRanking
                    {
                        GameId = localGameId,
                        CurrentRank = item.Rank,
                        LastWeekRank = null, // 新上榜
                        PeakPlayers = item.Peak,
                        UpdatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("排行榜更新完成，仅包含本地已有游戏。");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FetchAndSaveRankingsAsync 执行出错");
            return false;
        }
    }

    private async Task<string> GetValidSteamApiKeyAsync()
    {
        try 
        {
            var configKey = _configuration["SteamAPI:Key"];
            if (!string.IsNullOrEmpty(configKey) && !configKey.Contains("YOUR_API_KEY")) return configKey;

            var userId = GetCurrentUserId();
            if (userId > 0)
            {
                var userBinding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);
                if (userBinding != null && !string.IsNullOrEmpty(userBinding.AccessToken))
                    return _tokenEncryptionService.DecryptToken(userBinding.AccessToken);
            }

            var anyBinding = await _context.UserPlatformBindings
                .Where(b => b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true && !string.IsNullOrEmpty(b.AccessToken))
                .FirstOrDefaultAsync();

            if (anyBinding != null)
                return _tokenEncryptionService.DecryptToken(anyBinding.AccessToken ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KeyLookup] Failed.");
        }
        return string.Empty;
    }
    
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdClaim, out var userId)) return 0;
        return userId;
    }

    /// <summary>
    /// 从 Steam Store API 响应中提取 Metacritic 评分信息
    /// </summary>
    private (int score, string? url) ExtractMetacriticInfo(int appId, string jsonContent)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonContent);
            if (jsonDoc.RootElement.TryGetProperty(appId.ToString(), out var appData))
            {
                if (appData.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (appData.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("metacritic", out var metacritic))
                        {
                            var score = metacritic.TryGetProperty("score", out var scoreProp) 
                                ? scoreProp.GetInt32() 
                                : 0;
                            var url = metacritic.TryGetProperty("url", out var urlProp) 
                                ? urlProp.GetString() 
                                : null;
                            return (score, url);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 Metacritic 信息失败: appId={AppId}", appId);
        }
        return (0, null);
    }

    /// <summary>
    /// 从第三方 API 获取游戏的评论数据
    /// </summary>
    private async Task<(int totalReviews, int totalPositive)> GetGameReviewsFromThirdPartyApiAsync(int appId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"https://games-popularity.com/swagger/api/game/latest/{appId}";
            _logger.LogInformation("调用第三方API获取游戏评论数据: {Url}", url);
            
            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                
                int totalReviews = 0;
                int totalPositive = 0;
                
                if (jsonDoc.RootElement.TryGetProperty("reviews", out var reviewsObj))
                {
                    if (reviewsObj.TryGetProperty("reviewsAll", out var reviewsAllProp))
                    {
                        totalReviews = reviewsAllProp.GetInt32();
                    }
                    
                    if (reviewsObj.TryGetProperty("reviewsPositive", out var reviewsPositiveProp))
                    {
                        totalPositive = reviewsPositiveProp.GetInt32();
                    }
                }
                
                _logger.LogInformation("从第三方API获取到评论数据: appId={AppId}, totalReviews={TotalReviews}, totalPositive={TotalPositive}", 
                    appId, totalReviews, totalPositive);
                
                return (totalReviews, totalPositive);
            }
            else
            {
                _logger.LogWarning("第三方API请求失败: appId={AppId}, StatusCode={StatusCode}", appId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从第三方API获取游戏评论数据失败: appId={AppId}", appId);
        }
        
        return (0, 0);
    }

    /// <summary>
    /// 从数据库获取Steam API Key
    /// </summary>
    private async Task<string?> GetSteamApiKeyAsync(int userId)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);
            
            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                _logger.LogWarning("用户{UserId}未绑定Steam平台或API Key不存在", userId);
                return null;
            }
            
            return _tokenEncryptionService.DecryptToken(binding.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库获取Steam API Key失败");
            return null;
        }
    }

    /// <summary>
    /// 更新游戏信息
    /// 根据 game_id 更新 games 表中的游戏信息，支持更新 Steam 平台游戏
    /// </summary>
    /// <param name="request">更新请求</param>
    [HttpPost("update")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UpdateGameInfoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UpdateGameInfoResponseDto>>> UpdateGame(
        [FromBody] UpdateGameInfoRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始更新游戏信息: gameId={GameId}, updateAchievement={UpdateAchievement}", 
                request.GameId, request.UpdateAchievement);

            // 查找游戏
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == request.GameId);

            if (game == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            // 查找游戏的 Steam 平台映射
            var gamePlatform = await _context.GamePlatforms
                .FirstOrDefaultAsync(gp => gp.GameId == request.GameId && gp.PlatformId == STEAM_PLATFORM_ID);

            if (gamePlatform == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_NOT_STEAM_GAME", 
                    "该游戏不是 Steam 平台游戏，目前只支持更新 Steam 平台游戏"));
            }

            // 获取 Steam AppID
            if (!int.TryParse(gamePlatform.PlatformGameId, out var appId))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_APP_ID", 
                    "无法解析 Steam AppID"));
            }

            // 获取 Steam API Key
            var userId = GetCurrentUserId();
            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", 
                    "未找到Steam API Key，请先绑定Steam平台"));
            }

            // 获取游戏详细信息
            var gameUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
            var httpClient = _httpClientFactory.CreateClient();
            var gameResponse = await httpClient.GetAsync(gameUrl);
            
            if (!gameResponse.IsSuccessStatusCode)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_STEAM_API_FAILED", 
                    $"Steam API 请求失败: {gameResponse.StatusCode}"));
            }

            var gameContent = await gameResponse.Content.ReadAsStringAsync();
            var (metacriticScore, metacriticUrl) = ExtractMetacriticInfo(appId, gameContent);

            // 使用 SteamService 解析游戏数据
            var steamGame = await _steamService.GetSteamGame(appId, apiKey);
            if (steamGame == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_STEAM_GAME_NOT_FOUND", 
                    "无法从 Steam API 获取游戏信息"));
            }

            // 从第三方 API 获取评论数据
            var (totalReviews, totalPositive) = await GetGameReviewsFromThirdPartyApiAsync(appId);

            // 更新游戏信息
            game.Name = steamGame.Name;
            game.IsFree = steamGame.IsFree;
            game.RequireAge = (byte?)steamGame.RequiredAge;
            game.ShortDescription = steamGame.ShortDescription;
            game.DetailedDescription = steamGame.DetailedDescription;
            game.HeaderImage = steamGame.HeaderImage;
            game.CapsuleImage = steamGame.HeaderImage;
            game.Background = steamGame.HeaderImage;
            game.Windows = steamGame.Platforms.Windows;
            game.Mac = steamGame.Platforms.Mac;
            game.Linux = steamGame.Platforms.Linux;
            if (DateTime.TryParse(steamGame.ReleaseDate, out var releaseDate))
            {
                game.ReleaseDate = releaseDate;
            }
            game.ReviewScore = metacriticScore;
            game.ReviewScoreDesc = metacriticUrl ?? "";
            game.NumReviews = totalReviews;
            game.TotalPositive = totalPositive;

            await _context.SaveChangesAsync();
            _logger.LogInformation("成功更新游戏信息: gameId={GameId}, name={Name}", game.GameId, game.Name);

            bool achievementUpdated = false;

            // 如果需要更新成就
            if (request.UpdateAchievement)
            {
                try
                {
                    // 获取游戏成就架构信息
                    var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={apiKey}&appid={appId}&l=schinese";
                    var schemaResponse = await httpClient.GetAsync(schemaUrl);

                    if (schemaResponse.IsSuccessStatusCode)
                    {
                        var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                        var schemaDoc = JsonDocument.Parse(schemaContent);

                        if (schemaDoc.RootElement.TryGetProperty("game", out var gameData))
                        {
                            if (gameData.TryGetProperty("availableGameStats", out var stats))
                            {
                                if (stats.TryGetProperty("achievements", out var achievementsObj))
                                {
                                    // 处理成就数据（数组格式）
                                    if (achievementsObj.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var achElement in achievementsObj.EnumerateArray())
                                        {
                                            string? achievementName = null;
                                            if (achElement.TryGetProperty("name", out var nameProp))
                                            {
                                                achievementName = nameProp.GetString();
                                            }
                                            else if (achElement.TryGetProperty("apiname", out var apiNameProp))
                                            {
                                                achievementName = apiNameProp.GetString();
                                            }
                                            
                                            if (string.IsNullOrEmpty(achievementName)) continue;

                                            if (!achElement.TryGetProperty("displayName", out var displayNameProp)) continue;
                                            var displayName = displayNameProp.GetString();
                                            if (string.IsNullOrEmpty(displayName)) continue;

                                            var description = achElement.TryGetProperty("description", out var descProp) 
                                                ? descProp.GetString() 
                                                : null;
                                            var hidden = achElement.TryGetProperty("hidden", out var hiddenProp) 
                                                ? hiddenProp.GetInt32() == 1 
                                                : false;
                                            var icon = achElement.TryGetProperty("icon", out var iconProp) 
                                                ? iconProp.GetString() ?? "" 
                                                : "";
                                            var iconGray = achElement.TryGetProperty("icongray", out var iconGrayProp) 
                                                ? iconGrayProp.GetString() ?? "" 
                                                : "";

                                            var existingAchievement = await _context.Achievements
                                                .FirstOrDefaultAsync(a => a.GameId == game.GameId && a.AchievementName == achievementName);

                                            if (existingAchievement == null)
                                            {
                                                existingAchievement = new Achievement
                                                {
                                                    GameId = game.GameId,
                                                    PlatformId = STEAM_PLATFORM_ID,
                                                    AchievementName = achievementName,
                                                    DisplayName = displayName,
                                                    Description = description,
                                                    Hidden = hidden,
                                                    IconUnlocked = icon,
                                                    IconLocked = iconGray
                                                };
                                                _context.Achievements.Add(existingAchievement);
                                            }
                                            else
                                            {
                                                // 更新已有成就的缺失字段
                                                if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) 
                                                    existingAchievement.DisplayName = displayName;
                                                if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) 
                                                    existingAchievement.Description = description;
                                                if (existingAchievement.Hidden != hidden) 
                                                    existingAchievement.Hidden = hidden;
                                                if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) 
                                                    existingAchievement.IconUnlocked = icon;
                                                if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) 
                                                    existingAchievement.IconLocked = iconGray;
                                            }
                                        }
                                    }
                                    // 处理成就数据（对象格式）
                                    else if (achievementsObj.ValueKind == JsonValueKind.Object)
                                    {
                                        foreach (var achProp in achievementsObj.EnumerateObject())
                                        {
                                            var achKey = achProp.Name;
                                            var achValue = achProp.Value;

                                            if (!achValue.TryGetProperty("displayName", out var displayNameProp)) continue;
                                            var displayName = displayNameProp.GetString();
                                            if (string.IsNullOrEmpty(displayName)) continue;

                                            var achievementName = achKey;
                                            var description = achValue.TryGetProperty("description", out var descProp) 
                                                ? descProp.GetString() 
                                                : null;
                                            var hidden = achValue.TryGetProperty("hidden", out var hiddenProp) 
                                                ? hiddenProp.GetInt32() == 1 
                                                : false;
                                            var icon = achValue.TryGetProperty("icon", out var iconProp) 
                                                ? iconProp.GetString() ?? "" 
                                                : "";
                                            var iconGray = achValue.TryGetProperty("icongray", out var iconGrayProp) 
                                                ? iconGrayProp.GetString() ?? "" 
                                                : "";

                                            var existingAchievement = await _context.Achievements
                                                .FirstOrDefaultAsync(a => a.GameId == game.GameId && a.AchievementName == achievementName);

                                            if (existingAchievement == null)
                                            {
                                                existingAchievement = new Achievement
                                                {
                                                    GameId = game.GameId,
                                                    PlatformId = STEAM_PLATFORM_ID,
                                                    AchievementName = achievementName,
                                                    DisplayName = displayName,
                                                    Description = description,
                                                    Hidden = hidden,
                                                    IconUnlocked = icon,
                                                    IconLocked = iconGray
                                                };
                                                _context.Achievements.Add(existingAchievement);
                                            }
                                            else
                                            {
                                                // 更新已有成就的缺失字段
                                                if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) 
                                                    existingAchievement.DisplayName = displayName;
                                                if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) 
                                                    existingAchievement.Description = description;
                                                if (existingAchievement.Hidden != hidden) 
                                                    existingAchievement.Hidden = hidden;
                                                if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) 
                                                    existingAchievement.IconUnlocked = icon;
                                                if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) 
                                                    existingAchievement.IconLocked = iconGray;
                                            }
                                        }
                                    }

                                    await _context.SaveChangesAsync();
                                    achievementUpdated = true;
                                    _logger.LogInformation("成功更新游戏成就: gameId={GameId}", game.GameId);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "更新游戏成就失败: gameId={GameId}", game.GameId);
                }
            }

            var result = new UpdateGameInfoResponseDto
            {
                GameId = game.GameId,
                GameName = game.Name,
                Success = true,
                Message = "游戏信息更新成功",
                AchievementUpdated = achievementUpdated
            };

            return Ok(ApiResponse<UpdateGameInfoResponseDto>.SuccessResponse(result, "游戏信息更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新游戏信息时发生错误: gameId={GameId}", request.GameId);
            return StatusCode(500, ApiResponse<UpdateGameInfoResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}