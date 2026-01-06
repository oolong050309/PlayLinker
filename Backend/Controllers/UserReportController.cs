using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

/// <summary>
/// 用户报表控制器
/// 提供用户个人数据报表功能
/// </summary>
[ApiController]
[Route("api/v1/user-report")]
[Authorize]
public class UserReportController : ControllerBase
{
    private readonly IUserReportService _userReportService;
    private readonly ReportGenerationService _reportGenerationService;
    private readonly ILogger<UserReportController> _logger;

    public UserReportController(
        IUserReportService userReportService, 
        ReportGenerationService reportGenerationService,
        ILogger<UserReportController> logger)
    {
        _userReportService = userReportService;
        _reportGenerationService = reportGenerationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前用户ID
    /// </summary>
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// 获取用户报表概览
    /// </summary>
    /// <remarks>
    /// 获取用户的完整报表数据，包括：
    /// - 用户资料摘要（Steam等级、徽章、好友数等）
    /// - 游戏库统计（总游戏数、总时长、时长分布等）
    /// - 成就统计（总成就、完成率、稀有成就等）
    /// - 最近游玩记录
    /// - 愿望单摘要
    /// </remarks>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<UserReportOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserReportOverviewDto>>> GetOverview()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            var result = await _userReportService.GetUserReportOverviewAsync(userId);
            return Ok(ApiResponse<UserReportOverviewDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户报表概览失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "获取报表失败"));
        }
    }

    /// <summary>
    /// 获取游戏库统计
    /// </summary>
    /// <remarks>
    /// 获取用户游戏库的详细统计数据：
    /// - 总游戏数、总时长
    /// - 已玩/未玩游戏数
    /// - 最近2周游玩时长
    /// - 按类型的时长分布（饼图数据）
    /// - TOP 10 最常玩游戏
    /// </remarks>
    [HttpGet("game-library")]
    [ProducesResponseType(typeof(ApiResponse<GameLibrarySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameLibrarySummaryDto>>> GetGameLibraryStats()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            var result = await _userReportService.GetGameLibraryStatsAsync(userId);
            return Ok(ApiResponse<GameLibrarySummaryDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏库统计失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "获取统计失败"));
        }
    }

    /// <summary>
    /// 获取成就统计
    /// </summary>
    /// <remarks>
    /// 获取用户成就的详细统计数据：
    /// - 总成就数、已解锁数、完成率
    /// - 完美游戏数（100%成就）
    /// - 最近解锁的成就
    /// - 稀有成就列表
    /// - 各游戏成就进度
    /// </remarks>
    [HttpGet("achievements")]
    [ProducesResponseType(typeof(ApiResponse<AchievementSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AchievementSummaryDto>>> GetAchievementStats()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            var result = await _userReportService.GetAchievementStatsAsync(userId);
            return Ok(ApiResponse<AchievementSummaryDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取成就统计失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "获取统计失败"));
        }
    }

    /// <summary>
    /// 获取最近游玩记录
    /// </summary>
    /// <param name="count">返回数量，默认10</param>
    [HttpGet("recent-played")]
    [ProducesResponseType(typeof(ApiResponse<List<RecentPlayedGameDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<RecentPlayedGameDto>>>> GetRecentPlayed([FromQuery] int count = 10)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            var result = await _userReportService.GetRecentPlayedGamesAsync(userId, count);
            return Ok(ApiResponse<List<RecentPlayedGameDto>>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近游玩记录失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "获取记录失败"));
        }
    }

    /// <summary>
    /// 获取愿望单
    /// </summary>
    /// <remarks>
    /// 获取用户的Steam愿望单数据：
    /// - 愿望单游戏列表
    /// - 当前价格和折扣信息
    /// - 打折游戏数量
    /// </remarks>
    [HttpGet("wishlist")]
    [ProducesResponseType(typeof(ApiResponse<WishlistSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WishlistSummaryDto>>> GetWishlist()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            var result = await _userReportService.GetWishlistAsync(userId);
            return Ok(ApiResponse<WishlistSummaryDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取愿望单失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "获取愿望单失败"));
        }
    }

    /// <summary>
    /// 同步Steam数据
    /// </summary>
    /// <remarks>
    /// 从Steam同步用户数据到本地数据库：
    /// - 同步游戏库（游戏列表、游玩时长）
    /// - 同步成就数据
    /// </remarks>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<SyncResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> SyncFromSteam()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("UNAUTHORIZED", "用户未登录"));
        }

        try
        {
            _logger.LogInformation("开始同步Steam数据: userId={UserId}", userId);
            var result = await _userReportService.SyncFromSteamAsync(userId);
            return Ok(ApiResponse<SyncResultDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步Steam数据失败: userId={UserId}", userId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", "同步失败"));
        }
    }

    #region 报告生成接口

    /// <summary>
    /// 生成月度报告 HTML
    /// </summary>
    [HttpGet("reports/monthly/html")]
    public async Task<IActionResult> GenerateMonthlyReportHtml([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var html = await _reportGenerationService.GenerateMonthlyReportHtml(userId, startDate, endDate);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成月度HTML报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成月度报告 CSV
    /// </summary>
    [HttpGet("reports/monthly/csv")]
    public async Task<IActionResult> GenerateMonthlyReportCsv([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var csv = await _reportGenerationService.GenerateMonthlyReportCsv(userId, startDate, endDate);
            return File(csv, "text/csv; charset=utf-8", $"monthly_report_{targetYear}_{targetMonth:D2}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成月度CSV报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成月度报告 PDF
    /// </summary>
    [HttpGet("reports/monthly/pdf")]
    public async Task<IActionResult> GenerateMonthlyReportPdf([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var pdf = await _reportGenerationService.GenerateMonthlyReportPdf(userId, startDate, endDate);
            return File(pdf, "application/pdf", $"monthly_report_{targetYear}_{targetMonth:D2}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成月度PDF报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成年度报告 CSV
    /// </summary>
    [HttpGet("reports/yearly/csv")]
    public async Task<IActionResult> GenerateYearlyReportCsv([FromQuery] int? year)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var csv = await _reportGenerationService.GenerateYearlyReportCsv(userId, targetYear);
            return File(csv, "text/csv; charset=utf-8", $"yearly_report_{targetYear}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成年度CSV报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成年度报告 HTML
    /// </summary>
    [HttpGet("reports/yearly/html")]
    public async Task<IActionResult> GenerateYearlyReportHtml([FromQuery] int? year)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var html = await _reportGenerationService.GenerateYearlyReportHtml(userId, targetYear);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成年度HTML报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成年度报告 PDF
    /// </summary>
    [HttpGet("reports/yearly/pdf")]
    public async Task<IActionResult> GenerateYearlyReportPdf([FromQuery] int? year)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var targetYear = year ?? DateTime.Now.Year;
            var pdf = await _reportGenerationService.GenerateYearlyReportPdf(userId, targetYear);
            return File(pdf, "application/pdf", $"yearly_report_{targetYear}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成年度PDF报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成库存报告 HTML
    /// </summary>
    [HttpGet("reports/inventory/html")]
    public async Task<IActionResult> GenerateInventoryReportHtml()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var html = await _reportGenerationService.GenerateInventoryReportHtml(userId);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成库存HTML报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成库存报告 CSV
    /// </summary>
    [HttpGet("reports/inventory/csv")]
    public async Task<IActionResult> GenerateInventoryReportCsv()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var csv = await _reportGenerationService.GenerateInventoryReportCsv(userId);
            return File(csv, "text/csv; charset=utf-8", $"inventory_report_{DateTime.Now:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成库存CSV报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    /// <summary>
    /// 生成库存报告 PDF
    /// </summary>
    [HttpGet("reports/inventory/pdf")]
    public async Task<IActionResult> GenerateInventoryReportPdf()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        try
        {
            var pdf = await _reportGenerationService.GenerateInventoryReportPdf(userId);
            return File(pdf, "application/pdf", $"inventory_report_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成库存PDF报告失败");
            return StatusCode(500, "生成报告失败");
        }
    }

    #endregion
}
