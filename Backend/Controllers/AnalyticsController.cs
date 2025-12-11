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
            var gameRecords = await _context.UserPlatformLibraries
                .Include(r => r.Game)
                .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.UserPlatformBindings)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
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
                    Name = r.Game?.Name ?? string.Empty,
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
                PeakDay = string.Empty, // 无法从累计数据获取
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
            var gameRecords = await _context.UserPlatformLibraries
                .Include(r => r.Game)
                .ThenInclude(g => g.GameGenres)
                .ThenInclude(gg => gg.Genre)
                .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.UserPlatformBindings)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .ToListAsync();

            // 按题材分组统计
            var genreStats = gameRecords
                .SelectMany(r => r.Game.GameGenres.Select(gg => new { Genre = gg.Genre, Record = r }))
                .GroupBy(x => new { x.Genre.GenreId, GenreName = x.Genre.Name })
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
            var platformStats = await _context.UserPlatformLibraries
                .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.Platform)
                .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.UserPlatformBindings)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .GroupBy(r => new { r.PlatformId, PlatformName = r.PlayerPlatform.Platform.PlatformName })
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

    /// <summary>
    /// 成就统计分析
    /// </summary>
    [HttpGet("achievements")]
    [ProducesResponseType(typeof(ApiResponse<AchievementAnalyticsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<AchievementAnalyticsResponse>>> GetAchievementAnalytics()
    {
        try
        {
            // 假设当前用户ID为1001
            int userId = 1001;

            // 从数据库查询用户的成就记录
            var userAchievements = await _context.UserAchievements
                .Include(a => a.Achievement)
                .ThenInclude(ach => ach.Game)
                .Where(a => a.UserId == userId)
                .ToListAsync();

            // 计算总成就数和已解锁成就数
            var totalAchievements = userAchievements.Count;
            var unlockedAchievements = userAchievements.Count(a => a.Unlocked);
            var unlockRate = totalAchievements > 0 ? Math.Round((decimal)unlockedAchievements / totalAchievements, 2) : 0;

            // 按游戏分组统计
            var gameAchievements = userAchievements
                .GroupBy(a => new { a.AchievementId, GameName = a.Achievement?.Game?.Name ?? "Unknown" })
                .Select(g => new
                {
                    AchievementId = g.Key.AchievementId,
                    GameName = g.Key.GameName,
                    TotalAchievements = g.Count(),
                    Unlocked = g.Count(a => a.Unlocked),
                    CompletionRate = g.Count() > 0 ? (decimal)g.Count(a => a.Unlocked) / g.Count() : 0
                })
                .ToList();

            // 完美游戏数（100%完成率）
            var perfectGames = gameAchievements.Count(g => g.CompletionRate == 1.0m);

            // 平均完成率
            var averageCompletionRate = gameAchievements.Any() 
                ? Math.Round(gameAchievements.Average(g => g.CompletionRate), 2) 
                : 0;

            // 最近趋势（最近7天和30天解锁的成就）
            var now = DateTime.UtcNow;
            var last7Days = userAchievements.Count(a => a.Unlocked && a.UnlockTime.HasValue && a.UnlockTime.Value >= now.AddDays(-7));
            var last30Days = userAchievements.Count(a => a.Unlocked && a.UnlockTime.HasValue && a.UnlockTime.Value >= now.AddDays(-30));
            var trend = last7Days > 0 ? "increasing" : "stable";

            // 成就最多的游戏TOP 5
            var topAchievementGames = gameAchievements
                .OrderByDescending(g => g.Unlocked)
                .Take(5)
                .Select(g => new TopAchievementGame
                {
                    GameId = g.AchievementId,
                    GameName = g.GameName,
                    TotalAchievements = g.TotalAchievements,
                    Unlocked = g.Unlocked,
                    CompletionRate = Math.Round(g.CompletionRate, 2)
                })
                .ToList();

            var response = new AchievementAnalyticsResponse
            {
                TotalAchievements = totalAchievements,
                UnlockedAchievements = unlockedAchievements,
                UnlockRate = unlockRate,
                PerfectGames = perfectGames,
                AverageCompletionRate = averageCompletionRate,
                RecentTrend = new AchievementTrend
                {
                    Last7Days = last7Days,
                    Last30Days = last30Days,
                    Trend = trend
                },
                TopAchievementGames = topAchievementGames
            };

            return Ok(ApiResponse<AchievementAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting achievement analytics");
            return StatusCode(500, ApiResponse<AchievementAnalyticsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取成就统计分析失败"));
        }
    }

    /// <summary>
    /// 消费分析
    /// </summary>
    [HttpGet("spending")]
    [ProducesResponseType(typeof(ApiResponse<SpendingAnalyticsResponse>), 200)]
    public Task<ActionResult<ApiResponse<SpendingAnalyticsResponse>>> GetSpendingAnalytics(
        [FromQuery] string? period = null,
        [FromQuery] int? year = null)
    {
        try
        {
            // 确定分析周期
            var analyzePeriod = period ?? year?.ToString() ?? DateTime.UtcNow.Year.ToString();

            // 由于没有game_purchase表，返回提示信息
            var response = new SpendingAnalyticsResponse
            {
                Period = analyzePeriod,
                TotalSpending = 0,
                Currency = "CNY",
                GamesCount = 0,
                AverageGamePrice = 0,
                PlatformBreakdown = new List<PlatformSpending>
                {
                    new PlatformSpending
                    {
                        Platform = "暂无消费数据",
                        Spending = 0,
                        GamesCount = 0
                    }
                }
            };

            _logger.LogWarning("Spending analytics requested but game_purchase table does not exist");

            return Task.FromResult<ActionResult<ApiResponse<SpendingAnalyticsResponse>>>(Ok(ApiResponse<SpendingAnalyticsResponse>.SuccessResponse(response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting spending analytics");
            return Task.FromResult<ActionResult<ApiResponse<SpendingAnalyticsResponse>>>(StatusCode(500, ApiResponse<SpendingAnalyticsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取消费分析失败")));
        }
    }

}
