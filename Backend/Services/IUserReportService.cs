using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// 用户报表服务接口
/// 提供用户个人数据报表功能
/// </summary>
public interface IUserReportService
{
    /// <summary>
    /// 获取用户报表概览
    /// </summary>
    Task<UserReportOverviewDto> GetUserReportOverviewAsync(int userId);
    
    /// <summary>
    /// 获取游戏库详细统计
    /// </summary>
    Task<GameLibrarySummaryDto> GetGameLibraryStatsAsync(int userId);
    
    /// <summary>
    /// 获取成就详细统计
    /// </summary>
    Task<AchievementSummaryDto> GetAchievementStatsAsync(int userId);
    
    /// <summary>
    /// 获取最近游玩记录
    /// </summary>
    Task<List<RecentPlayedGameDto>> GetRecentPlayedGamesAsync(int userId, int count = 10);
    
    /// <summary>
    /// 获取愿望单
    /// </summary>
    Task<WishlistSummaryDto> GetWishlistAsync(int userId);
    
    /// <summary>
    /// 从Steam同步用户数据（游戏库、成就、愿望单等）
    /// </summary>
    Task<SyncResultDto> SyncFromSteamAsync(int userId);
}

/// <summary>
/// 同步结果DTO
/// </summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int GamesSync { get; set; }
    public int AchievementsSync { get; set; }
    public int WishlistSync { get; set; }
    public string SyncTime { get; set; } = string.Empty;
}
