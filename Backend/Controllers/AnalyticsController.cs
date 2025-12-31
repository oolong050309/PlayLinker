using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models; 
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // 确保只有登录用户能访问
public class AnalyticsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(PlayLinkerDbContext context, ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 获取当前登录用户 ID 的辅助方法
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return 0; 
    }

    /// <summary>
    /// 游玩时间分析 (基于真实历史快照)
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
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未登录"));

            // 1. 确定时间范围
            DateTime now = DateTime.UtcNow.Date;
            DateTime startDate;
            DateTime endDate = now;

            // 处理 period 参数
            if (period == "week" || period == "7days")
            {
                startDate = now.AddDays(-6); // 最近7天（含今天）
            }
            else if (period == "month" || (year.HasValue && month.HasValue))
            {
                int y = year ?? now.Year;
                int m = month ?? now.Month;
                startDate = new DateTime(y, m, 1);
                endDate = startDate.AddMonths(1).AddDays(-1);
                if (endDate > now) endDate = now; // 不展示未来的日期
            }
            else // 默认或 "year"
            {
                int y = year ?? now.Year;
                startDate = new DateTime(y, 1, 1);
                endDate = new DateTime(y, 12, 31);
                if (endDate > now) endDate = now;
            }

            // 为了计算 startDate 当天的增量，我们需要查询前一天的数据作为基准
            DateTime queryStart = startDate.AddDays(-1);

            // 2. 从数据库获取历史快照
            // 我们只需要每天的总时长之和 (Sum(PlaytimeForever))
            var historySnapshots = await _context.UserPlaytimeHistories
                .Where(h => h.UserId == userId && h.RecordDate >= queryStart && h.RecordDate <= endDate)
                .GroupBy(h => h.RecordDate)
                .Select(g => new 
                { 
                    Date = g.Key, 
                    TotalForeverMinutes = g.Sum(x => x.PlaytimeForever) 
                })
                .ToListAsync();

            // 转为字典方便查询： Date -> TotalMinutes
            var snapshotMap = historySnapshots.ToDictionary(x => x.Date, x => x.TotalForeverMinutes);

            // 3. 构建每日分布数据 (Daily Distribution)
            var distribution = new List<DailyPlaytime>();
            
            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                int dailyMinutes = 0;
                
                // 如果今天有快照记录
                if (snapshotMap.TryGetValue(date, out var todayTotal))
                {
                    // 尝试找昨天的记录来计算差值
                    if (snapshotMap.TryGetValue(date.AddDays(-1), out var yesterdayTotal))
                    {
                        dailyMinutes = todayTotal - yesterdayTotal;
                    }
                    else
                    {
                        // 如果没有昨天的记录（可能是刚开始统计的第一天），
                        // 这里我们保守处理为0，表示"相对于昨天的增量未知"。
                        // 这样图表第一天会是0，第二天开始有数据，符合逻辑。
                        dailyMinutes = 0; 
                    }
                }

                // 防止出现负数（例如用户退款游戏导致总时长减少，或数据同步异常）
                if (dailyMinutes < 0) dailyMinutes = 0;

                distribution.Add(new DailyPlaytime 
                { 
                    Date = date.ToString("yyyy-MM-dd"),
                    Minutes = dailyMinutes
                });
            }

            // 4. 获取当前总览数据 (Total & Breakdown)
            // 这部分依然查 UserPlatformLibrary，代表“当前最新状态”
            var libraryGames = await _context.UserPlatformLibraries
                .Include(r => r.Game)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .ToListAsync();

            var totalMinutes = libraryGames.Sum(r => r.PlaytimeMinutes);
            
            // 游戏分解 (Top 10)
            var gameBreakdown = libraryGames
                .OrderByDescending(r => r.PlaytimeMinutes)
                .Take(10)
                .Select(r => new GamePlaytimeBreakdown
                {
                    GameId = r.GameId,
                    Name = r.Game?.Name ?? "未知游戏",
                    Minutes = r.PlaytimeMinutes,
                    Percentage = totalMinutes > 0 ? Math.Round((decimal)r.PlaytimeMinutes / totalMinutes * 100, 1) : 0,
                    Sessions = 0 // 暂无会话数据，留空或可由前端估算
                })
                .ToList();

            // 计算日均 (简单算法：总时长 / 活跃天数)
            // 这里为了简单，分母使用“当前统计周期内的天数”或固定值
            var daysCount = Math.Max(1, (int)(now - startDate).TotalDays); 
            var dailyAverage = totalMinutes > 0 ? totalMinutes / 365 : 0; // 或者按注册时间算，这里暂且按年均估算

            var response = new PlaytimeAnalyticsResponse
            {
                Period = period ?? "custom",
                TotalMinutes = totalMinutes,
                DailyAverage = dailyAverage,
                Distribution = distribution,
                GameBreakdown = gameBreakdown,
                TimeSlotDistribution = new List<TimeSlotDistribution>(), // 暂不支持
                WeekdayDistribution = new List<WeekdayDistribution>()    // 暂不支持
            };

            return Ok(ApiResponse<PlaytimeAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游玩时间分析失败");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
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
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未登录"));

            var gameRecords = await _context.UserPlatformLibraries
                .Include(r => r.Game).ThenInclude(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .Include(r => r.PlayerPlatform).ThenInclude(pp => pp.UserPlatformBindings)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .ToListAsync();

            var genreStats = gameRecords
                .SelectMany(r => r.Game.GameGenres.Select(gg => new { Genre = gg.Genre, Record = r }))
                .Where(x => x.Genre != null)
                .GroupBy(x => new { x.Genre!.GenreId, GenreName = x.Genre.Name })
                .Select(g => new GenrePreference
                {
                    GenreId = g.Key.GenreId,
                    GenreName = g.Key.GenreName,
                    GamesOwned = g.Select(x => x.Record.GameId).Distinct().Count(),
                    GamesPlayed = g.Where(x => x.Record.PlaytimeMinutes > 0).Select(x => x.Record.GameId).Distinct().Count(),
                    TotalPlaytimeMinutes = g.Sum(x => x.Record.PlaytimeMinutes),
                    AveragePlaytime = g.Count() > 0 ? g.Sum(x => x.Record.PlaytimeMinutes) / g.Count() : 0,
                    PreferenceScore = gameRecords.Sum(r => r.PlaytimeMinutes) > 0 
                        ? Math.Round((decimal)g.Sum(x => x.Record.PlaytimeMinutes) / gameRecords.Sum(r => r.PlaytimeMinutes), 2) 
                        : 0
                })
                .OrderByDescending(g => g.TotalPlaytimeMinutes)
                .ToList();

            var response = new GenreAnalyticsResponse
            {
                GenrePreferences = genreStats,
                TopGenre = genreStats.FirstOrDefault()?.GenreName ?? "暂无数据",
                TotalGenres = genreStats.Count
            };

            return Ok(ApiResponse<GenreAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取题材偏好分析失败");
            return StatusCode(500, ApiResponse<GenreAnalyticsResponse>.ErrorResponse("ERR_INTERNAL", "Error"));
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
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未登录"));

            var platformStats = await _context.UserPlatformLibraries
                .Include(r => r.PlayerPlatform).ThenInclude(pp => pp.Platform)
                .Include(r => r.PlayerPlatform).ThenInclude(pp => pp.UserPlatformBindings)
                .Where(r => r.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .GroupBy(r => new { r.PlatformId, PlatformName = r.PlayerPlatform.Platform.PlatformName })
                .Select(g => new
                {
                    PlatformId = g.Key.PlatformId,
                    PlatformName = g.Key.PlatformName ?? "Unknown",
                    GamesCount = g.Count(),
                    PlaytimeMinutes = g.Sum(r => r.PlaytimeMinutes)
                })
                .ToListAsync();

            var totalPlaytime = platformStats.Sum(p => p.PlaytimeMinutes);

            var platformDistribution = platformStats
                .Select(p => new PlatformDistribution
                {
                    PlatformId = p.PlatformId,
                    PlatformName = p.PlatformName,
                    GamesCount = p.GamesCount,
                    PlaytimeMinutes = p.PlaytimeMinutes,
                    Percentage = totalPlaytime > 0 ? Math.Round((decimal)p.PlaytimeMinutes / totalPlaytime * 100, 2) : 0
                })
                .OrderByDescending(p => p.PlaytimeMinutes)
                .ToList();

            var response = new PlatformAnalyticsResponse
            {
                PlatformDistribution = platformDistribution,
                MostUsedPlatform = platformDistribution.FirstOrDefault()?.PlatformName ?? "暂无数据",
                TotalPlatforms = platformDistribution.Count
            };

            return Ok(ApiResponse<PlatformAnalyticsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取平台分布分析失败");
            return StatusCode(500, ApiResponse<PlatformAnalyticsResponse>.ErrorResponse("ERR_INTERNAL", "Error"));
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
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未登录"));

            var userAchievements = await _context.UserAchievements
                .Include(a => a.Achievement).ThenInclude(ach => ach.Game)
                .Where(a => a.UserId == userId)
                .ToListAsync();

            var totalAchievements = userAchievements.Count;
            var unlockedAchievements = userAchievements.Count(a => a.Unlocked);
            var unlockRate = totalAchievements > 0 ? Math.Round((decimal)unlockedAchievements / totalAchievements, 2) : 0;

            // 完美游戏统计
            var gameStatsRaw = await _context.UserAchievements
                .Include(ua => ua.Achievement).ThenInclude(a => a.Game)
                .Where(ua => ua.UserId == userId)
                .GroupBy(ua => new { ua.Achievement.GameId, ua.Achievement.Game.Name })
                .Select(g => new 
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.Name,
                    Total = g.Count(),
                    Unlocked = g.Count(ua => ua.Unlocked)
                })
                .ToListAsync();

            var perfectGames = gameStatsRaw.Count(g => g.Unlocked == g.Total && g.Total > 0);
            var averageCompletionRate = gameStatsRaw.Any() 
                ? Math.Round((decimal)gameStatsRaw.Average(g => (double)g.Unlocked / g.Total), 2) 
                : 0;

            // 最近趋势 (Last 7/30 days)
            var now = DateTime.UtcNow;
            var last7Days = userAchievements.Count(a => a.Unlocked && a.UnlockTime.HasValue && a.UnlockTime.Value >= now.AddDays(-7));
            var last30Days = userAchievements.Count(a => a.Unlocked && a.UnlockTime.HasValue && a.UnlockTime.Value >= now.AddDays(-30));
            var trend = last7Days > 0 ? "increasing" : "stable";

            // Top Games
            var topAchievementGames = gameStatsRaw
                .OrderByDescending(g => g.Unlocked)
                .Take(5)
                .Select(g => new TopAchievementGame
                {
                    GameId = g.GameId,
                    GameName = g.GameName,
                    TotalAchievements = g.Total,
                    Unlocked = g.Unlocked,
                    CompletionRate = g.Total > 0 ? Math.Round((decimal)g.Unlocked / g.Total, 2) : 0
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
            _logger.LogError(ex, "获取成就统计分析失败");
            return StatusCode(500, ApiResponse<AchievementAnalyticsResponse>.ErrorResponse("ERR_INTERNAL", "Error"));
        }
    }

    /// <summary>
    /// 消费分析 (暂未实现)
    /// </summary>
    [HttpGet("spending")]
    [ProducesResponseType(typeof(ApiResponse<SpendingAnalyticsResponse>), 200)]
    public Task<ActionResult<ApiResponse<SpendingAnalyticsResponse>>> GetSpendingAnalytics(
        [FromQuery] string? period = null,
        [FromQuery] int? year = null)
    {
        var analyzePeriod = period ?? year?.ToString() ?? DateTime.UtcNow.Year.ToString();
        var response = new SpendingAnalyticsResponse
        {
            Period = analyzePeriod,
            TotalSpending = 0,
            Currency = "CNY",
            GamesCount = 0,
            AverageGamePrice = 0,
            PlatformBreakdown = new List<PlatformSpending>()
        };
        return Task.FromResult<ActionResult<ApiResponse<SpendingAnalyticsResponse>>>(Ok(ApiResponse<SpendingAnalyticsResponse>.SuccessResponse(response)));
    }
}