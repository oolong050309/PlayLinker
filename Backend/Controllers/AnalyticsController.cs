using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(PlayLinkerDbContext context, ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 游玩时间分析
    /// </summary>
    [HttpGet("playtime")]
    [ProducesResponseType(typeof(ApiResponse<PlaytimeAnalyticsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PlaytimeAnalyticsResponse>>> GetPlaytimeAnalytics(
        [FromQuery] string? period = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        try
        {
            // 假设当前用户ID为1001（实际项目中应从JWT Token获取）
            int userId = 1001;

            // 确定分析周期
            var analyzePeriod = period ?? $"{year ?? DateTime.UtcNow.Year}-{month ?? DateTime.UtcNow.Month:D2}";

            // 从数据库查询用户的游戏记录
            var gameRecords = await _context.UserPlatformGameRecords
                .Include(r => r.Game)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            // 计算总游玩时间
            var totalMinutes = gameRecords.Sum(r => r.PlaytimeMinutes);
            var totalGames = gameRecords.Count;
            var dailyAverage = totalGames > 0 ? totalMinutes / 30 : 0; // 假设30天

            // 游戏分解统计（按游玩时间排序）
            var gameBreakdown = gameRecords
                .OrderByDescending(r => r.PlaytimeMinutes)
                .Take(10)
                .Select(r => new GamePlaytimeBreakdown
                {
                    GameId = r.GameId,
                    Name = r.Game?.Name ?? "Unknown",
                    Minutes = r.PlaytimeMinutes,
                    Percentage = totalMinutes > 0 ? Math.Round((decimal)r.PlaytimeMinutes / totalMinutes * 100, 1) : 0,
                    Sessions = new Random().Next(10, 50) // 模拟会话数（无法从现有数据获取）
                })
                .ToList();

            var response = new PlaytimeAnalyticsResponse
            {
                Period = analyzePeriod,
                TotalMinutes = totalMinutes,
                DailyAverage = dailyAverage,
                PeakDay = null, // 无法从累计数据获取
                PeakMinutes = 0,
                Distribution = new List<DailyPlaytime>(), // 无详细会话记录
                GameBreakdown = gameBreakdown,
                TimeSlotDistribution = new List<TimeSlotDistribution>(), // 无详细会话记录
                WeekdayDistribution = new List<WeekdayDistribution>() // 无详细会话记录
            };

            return Ok(ApiResponse<PlaytimeAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playtime analytics");
            return StatusCode(500, ApiResponse<PlaytimeAnalyticsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取游玩时间分析失败"));
        }
    }

    /// <summary>
    /// 题材偏好分析
    /// </summary>
    [HttpGet("genres")]
    [ProducesResponseType(typeof(ApiResponse<GenreAnalyticsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<GenreAnalyticsResponse>>> GetGenreAnalytics()
    {
        try
        {
            // 假设当前用户ID为1001
            int userId = 1001;

            // 从数据库查询用户的游戏记录，关联题材信息
            var gameRecords = await _context.UserPlatformGameRecords
                .Include(r => r.Game)
                .ThenInclude(g => g.GameGenres)
                .ThenInclude(gg => gg.Genre)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            // 按题材分组统计
            var genreStats = gameRecords
                .SelectMany(r => r.Game.GameGenres.Select(gg => new { Genre = gg.Genre, Record = r }))
                .GroupBy(x => new { x.Genre.GenreId, x.Genre.GenreName })
                .Select(g => new GenrePreference
                {
                    GenreId = g.Key.GenreId,
                    GenreName = g.Key.GenreName,
                    GamesOwned = g.Count(),
                    GamesPlayed = g.Count(x => x.Record.PlaytimeMinutes > 0),
                    TotalPlaytimeMinutes = g.Sum(x => x.Record.PlaytimeMinutes),
                    AveragePlaytime = g.Count() > 0 ? g.Sum(x => x.Record.PlaytimeMinutes) / g.Count() : 0,
                    PreferenceScore = g.Sum(x => x.Record.PlaytimeMinutes) > 0 
                        ? Math.Round((decimal)g.Sum(x => x.Record.PlaytimeMinutes) / gameRecords.Sum(r => r.PlaytimeMinutes), 2) 
                        : 0
                })
                .OrderByDescending(g => g.TotalPlaytimeMinutes)
                .ToList();

            // 如果没有题材数据，使用模拟数据
            var response = new GenreAnalyticsResponse
            {
                GenrePreferences = genreStats.Any() ? genreStats : new List<GenrePreference>
                {
                    new GenrePreference
                    {
                        GenreId = 1,
                        GenreName = "暂无数据",
                        GamesOwned = 0,
                        GamesPlayed = 0,
                        TotalPlaytimeMinutes = 0,
                        AveragePlaytime = 0,
                        PreferenceScore = 0
                    }
                },
                TopGenre = genreStats.Any() ? genreStats.First().GenreName : "暂无数据",
                TotalGenres = genreStats.Count
            };

            return Ok(ApiResponse<GenreAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting genre analytics");
            return StatusCode(500, ApiResponse<GenreAnalyticsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取题材偏好分析失败"));
        }
    }

    /// <summary>
    /// 平台分布分析
    /// </summary>
    [HttpGet("platforms")]
    [ProducesResponseType(typeof(ApiResponse<PlatformAnalyticsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<PlatformAnalyticsResponse>>> GetPlatformAnalytics()
    {
        try
        {
            // 假设当前用户ID为1001
            int userId = 1001;

            // 从数据库查询用户的游戏记录，按平台分组
            var platformStats = await _context.UserPlatformGameRecords
                .Include(r => r.Platform)
                .Where(r => r.UserId == userId)
                .GroupBy(r => new { r.PlatformId, r.Platform.PlatformName })
                .Select(g => new
                {
                    PlatformId = g.Key.PlatformId,
                    PlatformName = g.Key.PlatformName,
                    GamesCount = g.Count(),
                    PlaytimeMinutes = g.Sum(r => r.PlaytimeMinutes)
                })
                .ToListAsync();

            var totalPlaytime = platformStats.Sum(p => p.PlaytimeMinutes);

            var platformDistribution = platformStats
                .Select(p => new PlatformDistribution
                {
                    PlatformId = p.PlatformId,
                    PlatformName = p.PlatformName ?? "Unknown",
                    GamesCount = p.GamesCount,
                    PlaytimeMinutes = p.PlaytimeMinutes,
                    Percentage = totalPlaytime > 0 ? Math.Round((decimal)p.PlaytimeMinutes / totalPlaytime * 100, 2) : 0
                })
                .OrderByDescending(p => p.PlaytimeMinutes)
                .ToList();

            // 如果没有数据，使用模拟数据
            var response = new PlatformAnalyticsResponse
            {
                PlatformDistribution = platformDistribution.Any() ? platformDistribution : new List<PlatformDistribution>
                {
                    new PlatformDistribution
                    {
                        PlatformId = 0,
                        PlatformName = "暂无数据",
                        GamesCount = 0,
                        PlaytimeMinutes = 0,
                        Percentage = 0
                    }
                },
                MostUsedPlatform = platformDistribution.Any() ? platformDistribution.First().PlatformName : "暂无数据",
                TotalPlatforms = platformDistribution.Count
            };

            return Ok(ApiResponse<PlatformAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting platform analytics");
            return StatusCode(500, ApiResponse<PlatformAnalyticsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取平台分布分析失败"));
        }
    }

}
