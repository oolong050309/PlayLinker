using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Text.Json;
using System.Collections.Concurrent; // [新增] 用于缓存游戏名

namespace PlayLinker.Controllers;

/// <summary>
/// 游戏数据API控制器
/// 提供游戏列表、详情、搜索、排行榜等功能
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

    // [新增] 静态缓存，避免重复请求 Steam Store API 导致限流
    private static readonly ConcurrentDictionary<int, string> _gameNameCache = new();

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
            _logger.LogInformation("获取游戏列表: page={Page}, pageSize={PageSize}", page, pageSize);

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
            _logger.LogInformation("获取游戏详情: gameId={GameId}", id);

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
    /// 搜索游戏
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<GameListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameListDto>>> SearchGames(
        [FromQuery] string q = "",
        [FromQuery] string? category = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("搜索游戏: q={Query}, category={Category}", q, category);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Games.AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(g => EF.Functions.Like(g.Name, $"%{q}%"));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(g => g.GameCategories.Any(gc => gc.Category!.Name == category));
            }

            query = sortBy?.ToLower() switch
            {
                "release_date" => query.OrderByDescending(g => g.ReleaseDate),
                "name" => query.OrderBy(g => g.Name),
                _ => query.OrderByDescending(g => g.ReviewScore)
            };

            var total = await query.CountAsync();
            var games = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .ToListAsync();

            var items = games.Select(g => new GameItemDto
            {
                GameId = g.GameId,
                Name = g.Name,
                IsFree = g.IsFree,
                ReleaseDate = g.ReleaseDate.ToString("yyyy-MM-dd"),
                HeaderImage = g.HeaderImage,
                Genres = g.GameGenres.Select(gg => gg.Genre?.Name ?? "").ToList(),
                Platforms = new PlatformSupportDto { Windows = g.Windows, Mac = g.Mac, Linux = g.Linux },
                ReviewScore = g.ReviewScore,
                TotalPositive = g.TotalPositive,
                CurrentPlayers = 0
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
            _logger.LogError(ex, "搜索游戏时发生错误");
            return StatusCode(500, ApiResponse<GameListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取游戏排行榜 (适配数据库存储Key，自动获取真实名称)
    /// </summary>
    [HttpGet("ranking")]
    [ProducesResponseType(typeof(ApiResponse<GameRankingListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameRankingListDto>>> GetGameRanking(
        [FromQuery] string type = "popular",
        [FromQuery] int limit = 100)
    {
        try
        {
            _logger.LogInformation("[GetGameRanking] Starting fetch... Limit={Limit}", limit);

            // 1. 获取 Steam API Key
            string steamApiKey = await GetValidSteamApiKeyAsync();

            // 2. 检查 Key
            if (string.IsNullOrEmpty(steamApiKey))
            {
                _logger.LogWarning("[GetGameRanking] No valid Steam API Key. Returning MOCK data.");
                return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
                { 
                    Items = GetMockRankings(limit), 
                    TotalCount = limit 
                }));
            }

            // 3. 构建请求
            var requestUrl = $"https://api.steampowered.com/ISteamChartsService/GetMostPlayedGames/v1/?key={steamApiKey}&count={limit}";
            _logger.LogInformation("[GetGameRanking] Requesting URL: {Url}", requestUrl.Replace(steamApiKey, "***"));

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15); // 稍微增加超时，因为后续可能要查 Store API

            var response = await client.GetAsync(requestUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[GetGameRanking] Steam API failed: {Status}", response.StatusCode);
                return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto { Items = GetMockRankings(limit), TotalCount = limit }));
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            if (!doc.RootElement.TryGetProperty("response", out var responseEl) || 
                !responseEl.TryGetProperty("ranks", out var ranksEl))
            {
                return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto { Items = GetMockRankings(limit), TotalCount = limit }));
            }

            var rankingList = new List<GameRankingItemDto>();
            int index = 0;

            foreach (var item in ranksEl.EnumerateArray())
            {
                if (index >= limit) break;

                var rank = item.GetProperty("rank").GetInt32();
                var appId = item.GetProperty("appid").GetInt32();
                var peakPlayers = item.GetProperty("peak_in_game").GetInt32();
                
                var headerImage = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";
                
                // [修改] 获取真实名称 (缓存 + Store API)
                var gameName = await GetSteamGameNameAsync(client, appId);

                rankingList.Add(new GameRankingItemDto
                {
                    RankId = rank,
                    GameId = appId,
                    GameName = gameName,
                    CurrentRank = rank,
                    LastWeekRank = null, // [用户需求] 上周峰值/排名不需要，置空
                    PeakPlayers = peakPlayers,
                    HeaderImage = headerImage
                });

                index++;
            }

            return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
            { 
                Items = rankingList, 
                TotalCount = rankingList.Count 
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetGameRanking] Internal Error");
            return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(new GameRankingListDto 
            { 
                Items = GetMockRankings(limit), 
                TotalCount = limit 
            }));
        }
    }

    /// <summary>
    /// 获取 Steam API Key
    /// </summary>
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
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 1 && b.BindingStatus == true);
                if (userBinding != null && !string.IsNullOrEmpty(userBinding.AccessToken))
                    return _tokenEncryptionService.DecryptToken(userBinding.AccessToken);
            }

            var anyBinding = await _context.UserPlatformBindings
                .Where(b => b.PlatformId == 1 && b.BindingStatus == true && !string.IsNullOrEmpty(b.AccessToken))
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

    // 辅助：获取当前用户ID
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdClaim, out var userId)) return 0;
        return userId;
    }

    /// <summary>
    /// [新增] 获取游戏名称：缓存 -> 字典 -> Steam Store API
    /// </summary>
    private async Task<string> GetSteamGameNameAsync(HttpClient client, int appId)
    {
        // 1. 查缓存
        if (_gameNameCache.TryGetValue(appId, out var cachedName)) return cachedName;

        // 2. 查本地字典 (快速)
        var commonGames = new Dictionary<int, string>
        {
            { 570, "Dota 2" }, { 730, "Counter-Strike 2" }, { 578080, "PUBG: BATTLEGROUNDS" },
            { 1172470, "Apex Legends" }, { 271590, "Grand Theft Auto V" }, { 1245620, "Elden Ring" },
            { 1091500, "Cyberpunk 2077" }, { 1086940, "Baldur's Gate 3" }, { 2358720, "Black Myth: Wukong" },
            { 1938090, "Call of Duty®" }, { 431960, "Wallpaper Engine" }, { 2195250, "EA SPORTS FC™ 25" },
            { 230410, "Warframe" }, { 440, "Team Fortress 2" }, { 252490, "Rust" }
        };

        if (commonGames.TryGetValue(appId, out var name))
        {
            _gameNameCache.TryAdd(appId, name);
            return name;
        }

        // 3. 查 Steam Store API (慢速，需注意频次)
        try
        {
            // 注意：Store API 有速率限制。如果在循环中大量调用，可能会变慢或 429。
            // 实际上线时应配合后台任务或 Redis 缓存。
            var storeUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=basic";
            var response = await client.GetAsync(storeUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                
                // 响应结构: { "appid": { "success": true, "data": { "name": "xxx" } } }
                if (doc.RootElement.TryGetProperty(appId.ToString(), out var appEl) &&
                    appEl.TryGetProperty("success", out var successEl) && successEl.GetBoolean() &&
                    appEl.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("name", out var apiName))
                {
                    var realName = apiName.GetString();
                    if (!string.IsNullOrEmpty(realName))
                    {
                        _gameNameCache.TryAdd(appId, realName);
                        return realName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fetch name failed for {AppId}: {Msg}", appId, ex.Message);
        }

        // 4. 兜底
        return $"Steam Game {appId}";
    }

    private List<GameRankingItemDto> GetMockRankings(int limit)
    {
        var mocks = new List<GameRankingItemDto>();
        var games = new[] 
        {
            new { Name = "Black Myth: Wukong (Mock)", Img = "https://cdn.akamai.steamstatic.com/steam/apps/2358720/header.jpg", Players = 2400000 },
            new { Name = "Counter-Strike 2", Img = "https://cdn.akamai.steamstatic.com/steam/apps/730/header.jpg", Players = 1500000 },
            new { Name = "Dota 2", Img = "https://cdn.akamai.steamstatic.com/steam/apps/570/header.jpg", Players = 800000 }
        };
        for (int i = 0; i < Math.Min(limit, 10); i++) {
             var g = games[i % games.Length];
             mocks.Add(new GameRankingItemDto { RankId = i+1, GameId = 10000+i, GameName = g.Name, PeakPlayers = g.Players, CurrentRank = i+1, HeaderImage = g.Img });
        }
        return mocks;
    }

    /// <summary>
    /// 添加游戏(管理员)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> AddGame([FromBody] AddGameRequestDto request)
    {
        try
        {
            _logger.LogInformation("添加游戏: name={Name}", request.Name);
            var game = new Game
            {
                Name = request.Name, IsFree = request.IsFree, ReleaseDate = request.ReleaseDate,
                ShortDescription = request.ShortDescription, DetailedDescription = request.DetailedDescription,
                HeaderImage = request.HeaderImage, CapsuleImage = request.CapsuleImage, Background = request.Background,
                RequireAge = request.RequireAge, Windows = request.Platforms.Windows, Mac = request.Platforms.Mac, Linux = request.Platforms.Linux
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetGame), new { id = game.GameId }, ApiResponse<object>.SuccessResponse(new { gameId = game.GameId }, "游戏添加成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加游戏错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "错误"));
        }
    }

    /// <summary>
    /// 更新游戏(管理员)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateGame(long id, [FromBody] UpdateGameRequestDto request)
    {
        try
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null) return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "不存在"));
            
            if (!string.IsNullOrEmpty(request.Name)) game.Name = request.Name;
            if (!string.IsNullOrEmpty(request.HeaderImage)) game.HeaderImage = request.HeaderImage;
            
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<object>.SuccessResponse(new { gameId = id }, "更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新游戏错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "错误"));
        }
    }

    /// <summary>
    /// 获取游戏Mod列表
    /// </summary>
    [HttpGet("{gameId}/mods")]
    public async Task<ActionResult<ApiResponse<GameModsResponse>>> GetGameMods(long gameId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return await Task.FromResult(Ok(ApiResponse<GameModsResponse>.SuccessResponse(new GameModsResponse 
        { 
            GameId = gameId, 
            Mods = new List<ModDetailDto>(), 
            Meta = new PaginationMeta() 
        })));
    }
}