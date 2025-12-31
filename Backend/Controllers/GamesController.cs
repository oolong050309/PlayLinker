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
    private const int STEAM_PLATFORM_ID = 1; // 假设 Steam 平台 ID 为 1

    public GamesController(
        PlayLinkerDbContext context, 
        ILogger<GamesController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ITokenEncryptionService tokenEncryptionService)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _tokenEncryptionService = tokenEncryptionService;
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
        [FromQuery] bool? isFree = null)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Games.AsQueryable();

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
}