using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;
using System.Text.Json;

namespace PlayLinker.Controllers;

/// <summary>
/// 新闻资讯API控制器
/// 提供新闻列表、游戏新闻、新闻详情查询
/// </summary>
[ApiController]
[Route("api/v1/news")]
public class NewsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<NewsController> _logger;
    private readonly ISteamService _steamService;

    public NewsController(PlayLinkerDbContext context, ILogger<NewsController> logger, ISteamService steamService)
    {
        _context = context;
        _logger = logger;
        _steamService = steamService;
    }

    /// <summary>
    /// 获取新闻列表
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="sortBy">排序字段</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NewsListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsListDto>>> GetNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null)
    {
        try
        {
            _logger.LogInformation("获取新闻列表: page={Page}, pageSize={PageSize}", page, pageSize);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.News
                .OrderByDescending(n => n.Date);

            var total = await query.CountAsync();
            var news = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(n => n.GameNews).ThenInclude(gn => gn.Game)
                .ToListAsync();

            var items = news.Select(n => new NewsItemDto
            {
                NewsId = n.NewsId,
                Title = n.NewsTitle,
                Author = n.Author,
                Date = n.Date,
                Contents = n.Contents,
                NewsUrl = n.NewsUrl,
                RelatedGames = n.GameNews.Select(gn => new RelatedGameDto
                {
                    GameId = gn.Game?.GameId ?? 0,
                    GameName = gn.Game?.Name ?? ""
                }).ToList()
            }).ToList();

            var result = new NewsListDto
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<NewsListDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取新闻列表时发生错误");
            return StatusCode(500, ApiResponse<NewsListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取新闻详情
    /// </summary>
    /// <param name="id">新闻ID</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<NewsDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<NewsDetailDto>>> GetNewsDetail(long id)
    {
        try
        {
            _logger.LogInformation("获取新闻详情: newsId={NewsId}", id);

            var news = await _context.News
                .Include(n => n.GameNews).ThenInclude(gn => gn.Game)
                .FirstOrDefaultAsync(n => n.NewsId == id);

            if (news == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NEWS_NOT_FOUND", "新闻不存在"));
            }

            var result = new NewsDetailDto
            {
                NewsId = news.NewsId,
                Title = news.NewsTitle,
                Author = news.Author,
                Date = news.Date,
                Contents = news.Contents,
                NewsUrl = news.NewsUrl,
                RelatedGames = news.GameNews.Select(gn => new RelatedGameDto
                {
                    GameId = gn.Game?.GameId ?? 0,
                    GameName = gn.Game?.Name ?? "",
                    HeaderImage = gn.Game?.HeaderImage
                }).ToList(),
                Tags = new List<string> { "update", "patch" },
                Views = 15000
            };

            return Ok(ApiResponse<NewsDetailDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取新闻详情时发生错误: newsId={NewsId}", id);
            return StatusCode(500, ApiResponse<NewsDetailDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 同步所有游戏的Steam新闻
    /// </summary>
    /// <param name="request">同步请求</param>
    [HttpPost("steam/sync-all")]
    [ProducesResponseType(typeof(ApiResponse<SteamNewsSyncResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SteamNewsSyncResponseDto>>> SyncAllSteamNews(
        [FromBody] SteamNewsSyncAllRequestDto? request = null)
    {
        try
        {
            _logger.LogInformation("开始同步所有游戏的Steam新闻");

            var count = request?.Count ?? 20;
            var processedGames = 0;
            var totalNews = 0;
            var errors = new List<string>();

            // 获取所有有Steam平台映射的游戏
            var gamesWithSteam = await _context.GamePlatforms
                .Where(gp => gp.PlatformId == 1) // Steam平台ID = 1
                .Include(gp => gp.Game)
                .ToListAsync();

            _logger.LogInformation("找到 {Count} 个有Steam映射的游戏", gamesWithSteam.Count);

            foreach (var gamePlatform in gamesWithSteam)
            {
                try
                {
                    if (gamePlatform.Game == null) continue;

                    // 解析Steam AppID
                    if (!int.TryParse(gamePlatform.PlatformGameId, out int appId))
                    {
                        _logger.LogWarning("无法解析Steam AppID: gameId={GameId}, platformGameId={PlatformGameId}", 
                            gamePlatform.GameId, gamePlatform.PlatformGameId);
                        continue;
                    }

                    // 调用Steam API获取新闻
                    var newsData = await _steamService.GetGameNews(appId, count);
                    if (newsData == null)
                    {
                        _logger.LogWarning("Steam API返回空数据: gameId={GameId}, appId={AppId}", 
                            gamePlatform.GameId, appId);
                        continue;
                    }

                    // 解析Steam API返回的JSON
                    var jsonString = newsData.ToString() ?? "{}";
                    var jsonDoc = JsonDocument.Parse(jsonString);

                    if (jsonDoc.RootElement.TryGetProperty("appnews", out var appNews) &&
                        appNews.TryGetProperty("newsitems", out var newsItems))
                    {
                        var newsCount = newsItems.GetArrayLength();
                        totalNews += newsCount;
                        _logger.LogInformation("游戏 {GameId} (AppID: {AppId}) 获取到 {Count} 条新闻", 
                            gamePlatform.GameId, appId, newsCount);
                    }

                    processedGames++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理游戏新闻时发生错误: gameId={GameId}", gamePlatform.GameId);
                    errors.Add($"游戏 {gamePlatform.GameId}: {ex.Message}");
                }
            }

            var result = new SteamNewsSyncResponseDto
            {
                ProcessedGames = processedGames,
                TotalGames = gamesWithSteam.Count,
                TotalNews = totalNews,
                Errors = errors
            };

            return Ok(ApiResponse<SteamNewsSyncResponseDto>.SuccessResponse(result, "Steam新闻同步完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步所有Steam新闻时发生错误");
            return StatusCode(500, ApiResponse<SteamNewsSyncResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 同步指定游戏的Steam新闻
    /// </summary>
    /// <param name="request">同步请求</param>
    [HttpPost("steam/sync")]
    [ProducesResponseType(typeof(ApiResponse<SteamGameNewsResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SteamGameNewsResponseDto>>> SyncSteamNews(
        [FromBody] SteamNewsSyncRequestDto request)
    {
        try
        {
            _logger.LogInformation("同步游戏Steam新闻: gameId={GameId}", request.GameId);

            // 验证游戏是否存在
            var game = await _context.Games.FindAsync(request.GameId);
            if (game == null)
            {
                return NotFound(ApiResponse<SteamGameNewsResponseDto>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            // 获取游戏的Steam AppID
            var gamePlatform = await _context.GamePlatforms
                .FirstOrDefaultAsync(gp => gp.GameId == request.GameId && gp.PlatformId == 1);

            if (gamePlatform == null || !int.TryParse(gamePlatform.PlatformGameId, out int appId))
            {
                return BadRequest(ApiResponse<SteamGameNewsResponseDto>.ErrorResponse("ERR_NO_STEAM_MAPPING", 
                    $"游戏 {request.GameId} 没有Steam平台映射"));
            }

            // 调用Steam API获取新闻
            var count = request.Count ?? 20;
            var newsData = await _steamService.GetGameNews(appId, count);

            if (newsData == null)
            {
                return Ok(ApiResponse<SteamGameNewsResponseDto>.ErrorResponse("ERR_STEAM_API_FAILED", 
                    "Steam API返回空数据"));
            }

            // 解析Steam API返回的JSON
            var jsonString = newsData.ToString() ?? "{}";
            var jsonDoc = JsonDocument.Parse(jsonString);

            var newsItems = new List<SteamNewsItemDto>();

            if (jsonDoc.RootElement.TryGetProperty("appnews", out var appNews) &&
                appNews.TryGetProperty("newsitems", out var newsItemsArray))
            {
                foreach (var item in newsItemsArray.EnumerateArray())
                {
                    var newsItem = new SteamNewsItemDto
                    {
                        Gid = item.TryGetProperty("gid", out var gid) ? gid.GetString() ?? "" : "",
                        Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Url = item.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
                        IsExternalUrl = item.TryGetProperty("is_external_url", out var isExternal) && isExternal.GetBoolean(),
                        Author = item.TryGetProperty("author", out var author) ? author.GetString() ?? "" : "",
                        Contents = item.TryGetProperty("contents", out var contents) ? contents.GetString() ?? "" : "",
                        FeedLabel = item.TryGetProperty("feedlabel", out var feedLabel) ? feedLabel.GetString() ?? "" : "",
                        Date = item.TryGetProperty("date", out var date) ? date.GetInt64() : 0,
                        FeedName = item.TryGetProperty("feedname", out var feedName) ? feedName.GetString() ?? "" : "",
                        FeedType = item.TryGetProperty("feed_type", out var feedType) ? feedType.GetInt32() : 0,
                        AppId = item.TryGetProperty("appid", out var appIdProp) ? appIdProp.GetInt32() : 0
                    };
                    newsItems.Add(newsItem);
                }
            }

            var result = new SteamGameNewsResponseDto
            {
                GameId = request.GameId,
                GameName = game.Name,
                AppId = appId,
                News = newsItems,
                Total = newsItems.Count
            };

            return Ok(ApiResponse<SteamGameNewsResponseDto>.SuccessResponse(result, "Steam新闻获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步Steam新闻时发生错误: gameId={GameId}", request.GameId);
            return StatusCode(500, ApiResponse<SteamGameNewsResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

/// <summary>
/// 游戏新闻API控制器扩展
/// </summary>
[ApiController]
[Route("api/v1/games")]
public class GameNewsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<GameNewsController> _logger;

    public GameNewsController(PlayLinkerDbContext context, ILogger<GameNewsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取游戏相关新闻
    /// </summary>
    /// <param name="id">游戏ID</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    [HttpGet("{id}/news")]
    [ProducesResponseType(typeof(ApiResponse<GameNewsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameNewsDto>>> GetGameNews(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("获取游戏新闻: gameId={GameId}", id);

            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(ApiResponse<GameNewsDto>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.GameNews
                .Where(gn => gn.GameId == id)
                .Include(gn => gn.News)
                .OrderByDescending(gn => gn.News!.Date);

            var total = await query.CountAsync();
            var gameNews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var newsItems = gameNews.Select(gn => new NewsItemDto
            {
                NewsId = gn.News?.NewsId ?? 0,
                Title = gn.News?.NewsTitle ?? "",
                Author = gn.News?.Author ?? "",
                Date = gn.News?.Date ?? 0,
                Contents = gn.News?.Contents ?? "",
                NewsUrl = gn.News?.NewsUrl
            }).ToList();

            var result = new GameNewsDto
            {
                GameId = id,
                GameName = game.Name,
                News = newsItems,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<GameNewsDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏新闻时发生错误: gameId={GameId}", id);
            return StatusCode(500, ApiResponse<GameNewsDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

