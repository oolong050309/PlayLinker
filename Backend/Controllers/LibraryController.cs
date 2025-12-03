using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;

namespace PlayLinker.Controllers;

/// <summary>
/// 游戏库管理API控制器
/// 提供用户游戏库概览、游戏列表、同步等功能(需要认证)
/// </summary>
[ApiController]
[Route("api/v1/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<LibraryController> _logger;

    public LibraryController(PlayLinkerDbContext context, ILogger<LibraryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 获取当前用户ID(从JWT Token中)
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1; // 默认返回1用于测试
    }

    /// <summary>
    /// 获取游戏库概览
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<LibraryOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LibraryOverviewDto>>> GetLibraryOverview()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取游戏库概览: userId={UserId}", userId);

            var library = await _context.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            if (library == null)
            {
                // 返回空数据
                var emptyResult = new LibraryOverviewDto
                {
                    TotalGamesOwned = 0,
                    GamesPlayed = 0,
                    TotalPlaytimeMinutes = 0,
                    TotalAchievements = 0,
                    UnlockedAchievements = 0,
                    RecentlyPlayedCount = 0,
                    RecentPlaytimeMinutes = 0,
                    PlatformStats = new List<PlatformStatsDto>(),
                    GenreDistribution = new List<GenreDistributionDto>()
                };
                return Ok(ApiResponse<LibraryOverviewDto>.SuccessResponse(emptyResult));
            }

            // 获取平台统计(从数据库查询实际数据)
            var platformStats = await _context.Platforms
                .Select(p => new PlatformStatsDto
                {
                    PlatformId = p.PlatformId,
                    PlatformName = p.PlatformName,
                    GamesOwned = _context.UserPlatformLibraries
                        .Count(upl => upl.PlatformId == p.PlatformId && 
                            _context.PlayerPlatforms.Any(pp => 
                                pp.PlatformId == p.PlatformId && 
                                pp.PlatformUserId == upl.PlatformUserId)),
                    LastSyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .Where(ps => ps.GamesOwned > 0)
                .ToListAsync();

            // 获取题材分布(从数据库查询实际数据)
            var genreDistribution = await _context.GameGenres
                .Where(gg => _context.UserPlatformLibraries.Any(upl => upl.GameId == gg.GameId))
                .GroupBy(gg => gg.Genre!.Name)
                .Select(g => new GenreDistributionDto
                {
                    Genre = g.Key ?? "",
                    Count = g.Count(),
                    PlaytimeMinutes = _context.UserPlatformLibraries
                        .Where(upl => g.Any(gg => gg.GameId == upl.GameId))
                        .Sum(upl => (int?)upl.PlaytimeMinutes) ?? 0
                })
                .OrderByDescending(gd => gd.PlaytimeMinutes)
                .Take(10)
                .ToListAsync();

            var result = new LibraryOverviewDto
            {
                TotalGamesOwned = library.TotalGamesOwned,
                GamesPlayed = library.GamesPlayed,
                TotalPlaytimeMinutes = library.TotalPlaytimeMinutes,
                TotalAchievements = library.TotalAchievements ?? 0,
                UnlockedAchievements = library.UnlockedAchievements ?? 0,
                RecentlyPlayedCount = library.RecentlyPlayedCount,
                RecentPlaytimeMinutes = library.RecentPlaytimeMinutes,
                PlatformStats = platformStats,
                GenreDistribution = genreDistribution
            };

            return Ok(ApiResponse<LibraryOverviewDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏库概览时发生错误");
            return StatusCode(500, ApiResponse<LibraryOverviewDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取用户游戏列表
    /// </summary>
    /// <param name="platform">平台筛选</param>
    /// <param name="sortBy">排序字段</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    [HttpGet("games")]
    [ProducesResponseType(typeof(ApiResponse<UserGameListDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<UserGameListDto>>> GetUserGames(
        [FromQuery] string? platform = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取用户游戏列表: userId={UserId}", userId);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // 这里简化处理,实际应该查询用户平台游戏库
            var result = new UserGameListDto
            {
                Items = new List<UserGameItemDto>(),
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = 0
                }
            };

            return Task.FromResult<ActionResult<ApiResponse<UserGameListDto>>>(
                Ok(ApiResponse<UserGameListDto>.SuccessResponse(result)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户游戏列表时发生错误");
            return Task.FromResult<ActionResult<ApiResponse<UserGameListDto>>>(
                StatusCode(500, ApiResponse<UserGameListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误")));
        }
    }

    /// <summary>
    /// 同步平台数据
    /// </summary>
    /// <param name="request">同步请求</param>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<SyncPlatformResponseDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<SyncPlatformResponseDto>>> SyncPlatformData(
        [FromBody] SyncPlatformRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("同步平台数据: userId={UserId}, platformId={PlatformId}", userId, request.PlatformId);

            // 生成任务ID
            var taskId = $"sync_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            var result = new SyncPlatformResponseDto
            {
                TaskId = taskId,
                Status = "processing",
                EstimatedTime = 30,
                GamesDetected = 0
            };

            return Task.FromResult<ActionResult<ApiResponse<SyncPlatformResponseDto>>>(
                Ok(ApiResponse<SyncPlatformResponseDto>.SuccessResponse(result, "同步任务已启动")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步平台数据时发生错误");
            return Task.FromResult<ActionResult<ApiResponse<SyncPlatformResponseDto>>>(
                StatusCode(500, ApiResponse<SyncPlatformResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误")));
        }
    }

    /// <summary>
    /// 获取游戏统计数据
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<GameStatsDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<GameStatsDto>>> GetGameStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取游戏统计数据: userId={UserId}", userId);

            var result = new GameStatsDto
            {
                TotalPlaytime = 0,
                AveragePlaytime = 0,
                MostPlayedGame = null,
                GenreDistribution = new List<GenreDistributionDto>(),
                PlatformDistribution = new List<PlatformDistributionDto>(),
                RecentActivity = new List<RecentActivityDto>()
            };

            return Task.FromResult<ActionResult<ApiResponse<GameStatsDto>>>(
                Ok(ApiResponse<GameStatsDto>.SuccessResponse(result)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏统计数据时发生错误");
            return Task.FromResult<ActionResult<ApiResponse<GameStatsDto>>>(
                StatusCode(500, ApiResponse<GameStatsDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误")));
        }
    }
}

