using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

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

    public GamesController(PlayLinkerDbContext context, ILogger<GamesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取游戏列表
    /// </summary>
    /// <param name="page">页码,从1开始</param>
    /// <param name="pageSize">每页数量,默认20,最大100</param>
    /// <param name="sortBy">排序字段</param>
    /// <param name="platform">平台筛选</param>
    /// <param name="genre">题材筛选</param>
    /// <param name="isFree">是否免费</param>
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

            // 参数验证
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Games.AsQueryable();

            // 题材筛选
            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(g => g.GameGenres.Any(gg => gg.Genre!.Name == genre));
            }

            // 免费筛选
            if (isFree.HasValue)
            {
                query = query.Where(g => g.IsFree == isFree.Value);
            }

            // 排序
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
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
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
    /// <param name="id">游戏ID</param>
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
                Genres = game.GameGenres.Select(gg => new GenreDto
                {
                    GenreId = gg.Genre?.GenreId ?? 0,
                    Name = gg.Genre?.Name ?? ""
                }).ToList(),
                Developers = game.GameDevelopers.Select(gd => new DeveloperDto
                {
                    DeveloperId = gd.Developer?.DeveloperId ?? 0,
                    Name = gd.Developer?.Name ?? ""
                }).ToList(),
                Publishers = game.GamePublishers.Select(gp => new PublisherDto
                {
                    PublisherId = gp.Publisher?.PublisherId ?? 0,
                    Name = gp.Publisher?.Name ?? ""
                }).ToList(),
                Categories = game.GameCategories.Select(gc => new CategoryDto
                {
                    CategoryId = gc.Category?.CategoryId ?? 0,
                    Name = gc.Category?.Name ?? ""
                }).ToList(),
                Languages = game.GameLanguages.Select(gl => new LanguageDto
                {
                    LanguageId = gl.Language?.LanguageId ?? 0,
                    Name = gl.Language?.LanguageName ?? ""
                }).ToList(),
                Platforms = new PlatformSupportDto
                {
                    Windows = game.Windows,
                    Mac = game.Mac,
                    Linux = game.Linux
                },
                ReleaseDate = game.ReleaseDate.ToString("yyyy-MM-dd"),
                Reviews = new GameReviewsDto
                {
                    Score = game.ReviewScore,
                    ScoreDesc = game.ReviewScoreDesc,
                    TotalReviews = game.NumReviews,
                    TotalPositive = game.TotalPositive
                }
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
    /// <param name="q">搜索关键词</param>
    /// <param name="category">分类筛选</param>
    /// <param name="sortBy">排序字段</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
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

            // 关键词搜索
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(g => EF.Functions.Like(g.Name, $"%{q}%"));
            }

            // 分类筛选
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(g => g.GameCategories.Any(gc => gc.Category!.Name == category));
            }

            // 排序
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
                Platforms = new PlatformSupportDto
                {
                    Windows = g.Windows,
                    Mac = g.Mac,
                    Linux = g.Linux
                },
                ReviewScore = g.ReviewScore,
                TotalPositive = g.TotalPositive,
                CurrentPlayers = 0
            }).ToList();

            var result = new GameListDto
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
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
    /// 获取游戏排行榜
    /// </summary>
    /// <param name="type">排行榜类型</param>
    /// <param name="limit">返回数量限制</param>
    [HttpGet("ranking")]
    [ProducesResponseType(typeof(ApiResponse<GameRankingListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameRankingListDto>>> GetGameRanking(
        [FromQuery] string type = "popular",
        [FromQuery] int limit = 100)
    {
        try
        {
            _logger.LogInformation("获取游戏排行榜: type={Type}, limit={Limit}", type, limit);

            limit = Math.Clamp(limit, 1, 100);

            var rankings = await _context.GameRankings
                .Include(gr => gr.Game)
                .OrderBy(gr => gr.CurrentRank)
                .Take(limit)
                .ToListAsync();

            var items = rankings.Select(gr => new GameRankingItemDto
            {
                RankId = gr.RankId,
                GameId = gr.GameId,
                GameName = gr.Game?.Name ?? "",
                CurrentRank = gr.CurrentRank,
                LastWeekRank = gr.LastWeekRank,
                PeakPlayers = gr.PeakPlayers,
                HeaderImage = gr.Game?.HeaderImage ?? ""
            }).ToList();

            var result = new GameRankingListDto
            {
                Items = items,
                TotalCount = items.Count
            };

            return Ok(ApiResponse<GameRankingListDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏排行榜时发生错误");
            return StatusCode(500, ApiResponse<GameRankingListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 添加游戏(管理员)
    /// </summary>
    /// <param name="request">添加游戏请求</param>
    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> AddGame([FromBody] AddGameRequestDto request)
    {
        try
        {
            _logger.LogInformation("添加游戏: name={Name}", request.Name);

            var game = new Game
            {
                Name = request.Name,
                IsFree = request.IsFree,
                ReleaseDate = request.ReleaseDate,
                ShortDescription = request.ShortDescription,
                DetailedDescription = request.DetailedDescription,
                HeaderImage = request.HeaderImage,
                CapsuleImage = request.CapsuleImage,
                Background = request.Background,
                RequireAge = request.RequireAge,
                Windows = request.Platforms.Windows,
                Mac = request.Platforms.Mac,
                Linux = request.Platforms.Linux
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            var result = new
            {
                gameId = game.GameId,
                name = game.Name,
                createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return CreatedAtAction(nameof(GetGame), new { id = game.GameId }, 
                ApiResponse<object>.SuccessResponse(result, "游戏添加成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加游戏时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新游戏(管理员)
    /// </summary>
    /// <param name="id">游戏ID</param>
    /// <param name="request">更新游戏请求</param>
    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateGame(long id, [FromBody] UpdateGameRequestDto request)
    {
        try
        {
            _logger.LogInformation("更新游戏: gameId={GameId}", id);

            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            if (!string.IsNullOrEmpty(request.Name))
                game.Name = request.Name;
            if (!string.IsNullOrEmpty(request.ShortDescription))
                game.ShortDescription = request.ShortDescription;
            if (!string.IsNullOrEmpty(request.HeaderImage))
                game.HeaderImage = request.HeaderImage;

            await _context.SaveChangesAsync();

            var result = new
            {
                gameId = game.GameId,
                updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "游戏更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新游戏时发生错误: gameId={GameId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

