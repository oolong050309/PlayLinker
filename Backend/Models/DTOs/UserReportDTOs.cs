namespace PlayLinker.Models.DTOs;

/// <summary>
/// 用户报表概览响应DTO
/// </summary>
public class UserReportOverviewDto
{
    /// <summary>
    /// 用户基本信息
    /// </summary>
    public UserProfileSummaryDto Profile { get; set; } = new();
    
    /// <summary>
    /// 游戏库统计
    /// </summary>
    public GameLibrarySummaryDto GameLibrary { get; set; } = new();
    
    /// <summary>
    /// 成就统计
    /// </summary>
    public AchievementSummaryDto Achievements { get; set; } = new();
    
    /// <summary>
    /// 最近游玩记录
    /// </summary>
    public List<RecentPlayedGameDto> RecentPlayed { get; set; } = new();
    
    /// <summary>
    /// 愿望单摘要
    /// </summary>
    public WishlistSummaryDto Wishlist { get; set; } = new();
}

/// <summary>
/// 用户资料摘要
/// </summary>
public class UserProfileSummaryDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? SteamId { get; set; }
    public string? SteamProfileName { get; set; }
    public int SteamLevel { get; set; }
    public int BadgeCount { get; set; }
    public int FriendCount { get; set; }
    public string? AccountCreated { get; set; }
    public string? Country { get; set; }
}

/// <summary>
/// 游戏库摘要
/// </summary>
public class GameLibrarySummaryDto
{
    /// <summary>
    /// 总游戏数
    /// </summary>
    public int TotalGames { get; set; }
    
    /// <summary>
    /// 总游玩时长（分钟）
    /// </summary>
    public long TotalPlaytimeMinutes { get; set; }
    
    /// <summary>
    /// 总游玩时长（小时，格式化）
    /// </summary>
    public string TotalPlaytimeFormatted { get; set; } = "0小时";
    
    /// <summary>
    /// 已玩过的游戏数
    /// </summary>
    public int PlayedGames { get; set; }
    
    /// <summary>
    /// 从未玩过的游戏数
    /// </summary>
    public int NeverPlayedGames { get; set; }
    
    /// <summary>
    /// 最近2周游玩时长（分钟）
    /// </summary>
    public int RecentPlaytimeMinutes { get; set; }
    
    /// <summary>
    /// 本周游玩时长（分钟）
    /// </summary>
    public int ThisWeekPlaytimeMinutes { get; set; }
    
    /// <summary>
    /// 本月游玩时长（分钟）
    /// </summary>
    public int ThisMonthPlaytimeMinutes { get; set; }
    
    /// <summary>
    /// 日均游玩时长（分钟，基于最近30天）
    /// </summary>
    public int DailyAverageMinutes { get; set; }
    
    /// <summary>
    /// 绑定的平台数量
    /// </summary>
    public int BoundPlatformCount { get; set; }
    
    /// <summary>
    /// 跨平台游戏数（在多个平台拥有的游戏）
    /// </summary>
    public int CrossPlatformGames { get; set; }
    
    /// <summary>
    /// 每日游戏时长趋势（最近14天）
    /// </summary>
    public List<DailyPlaytimeDto> DailyPlaytimeTrend { get; set; } = new();
    
    /// <summary>
    /// 游戏时长分布（按类型）
    /// </summary>
    public List<PlaytimeByGenreDto> PlaytimeByGenre { get; set; } = new();
    
    /// <summary>
    /// TOP 10 最常玩游戏
    /// </summary>
    public List<TopPlayedGameDto> TopPlayedGames { get; set; } = new();
    
    /// <summary>
    /// 各平台统计
    /// </summary>
    public List<PlatformStatsDto> PlatformStats { get; set; } = new();
}

/// <summary>
/// 每日游戏时长
/// </summary>
public class DailyPlaytimeDto
{
    /// <summary>
    /// 日期 (yyyy-MM-dd)
    /// </summary>
    public string Date { get; set; } = string.Empty;
    
    /// <summary>
    /// 当日游玩时长（分钟）
    /// </summary>
    public int PlaytimeMinutes { get; set; }
    
    /// <summary>
    /// 当日游玩的游戏数
    /// </summary>
    public int GamesPlayed { get; set; }
}

/// <summary>
/// 按类型的游戏时长
/// </summary>
public class PlaytimeByGenreDto
{
    public string Genre { get; set; } = string.Empty;
    public long PlaytimeMinutes { get; set; }
    public double Percentage { get; set; }
    public int GameCount { get; set; }
}

/// <summary>
/// 最常玩游戏
/// </summary>
public class TopPlayedGameDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? HeaderImage { get; set; }
    public int PlaytimeMinutes { get; set; }
    public string PlaytimeFormatted { get; set; } = string.Empty;
    public string? LastPlayed { get; set; }
    public int? AchievementsUnlocked { get; set; }
    public int? AchievementsTotal { get; set; }
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// 成就摘要
/// </summary>
public class AchievementSummaryDto
{
    /// <summary>
    /// 总成就数
    /// </summary>
    public int TotalAchievements { get; set; }
    
    /// <summary>
    /// 已解锁成就数
    /// </summary>
    public int UnlockedAchievements { get; set; }
    
    /// <summary>
    /// 完成率
    /// </summary>
    public double CompletionRate { get; set; }
    
    /// <summary>
    /// 完美游戏数（100%成就）
    /// </summary>
    public int PerfectGames { get; set; }
    
    /// <summary>
    /// 最近解锁的成就
    /// </summary>
    public List<RecentAchievementDto> RecentUnlocks { get; set; } = new();
    
    /// <summary>
    /// 稀有成就（全球解锁率低于5%）
    /// </summary>
    public List<RareAchievementSummaryDto> RareAchievements { get; set; } = new();
    
    /// <summary>
    /// 各游戏成就进度
    /// </summary>
    public List<GameAchievementProgressDto> GameProgress { get; set; } = new();
}

/// <summary>
/// 最近解锁成就
/// </summary>
public class RecentAchievementDto
{
    public long AchievementId { get; set; }
    public string AchievementName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IconUnlocked { get; set; } = string.Empty;
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? UnlockTime { get; set; }
}

/// <summary>
/// 稀有成就摘要
/// </summary>
public class RareAchievementSummaryDto
{
    public long AchievementId { get; set; }
    public string AchievementName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string IconUnlocked { get; set; } = string.Empty;
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public double GlobalUnlockRate { get; set; }
    public string? UnlockTime { get; set; }
}

/// <summary>
/// 游戏成就进度
/// </summary>
public class GameAchievementProgressDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? HeaderImage { get; set; }
    public int TotalAchievements { get; set; }
    public int UnlockedAchievements { get; set; }
    public double CompletionRate { get; set; }
    public string? LastUnlockTime { get; set; }
}

/// <summary>
/// 最近游玩游戏
/// </summary>
public class RecentPlayedGameDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? HeaderImage { get; set; }
    public int PlaytimeMinutes { get; set; }
    public int RecentPlaytimeMinutes { get; set; }
    public string? LastPlayed { get; set; }
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// 愿望单摘要
/// </summary>
public class WishlistSummaryDto
{
    /// <summary>
    /// 愿望单游戏数
    /// </summary>
    public int TotalItems { get; set; }
    
    /// <summary>
    /// 当前有折扣的游戏数
    /// </summary>
    public int OnSaleCount { get; set; }
    
    /// <summary>
    /// 愿望单游戏列表
    /// </summary>
    public List<UserReportWishlistItemDto> Items { get; set; } = new();
}

/// <summary>
/// 用户报表愿望单项目
/// </summary>
public class UserReportWishlistItemDto
{
    public long GameId { get; set; }
    public int? SteamAppId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? HeaderImage { get; set; }
    public int? Priority { get; set; }
    public string? AddedTime { get; set; }
    
    /// <summary>
    /// 当前价格（分）
    /// </summary>
    public int? CurrentPrice { get; set; }
    
    /// <summary>
    /// 原价（分）
    /// </summary>
    public int? OriginalPrice { get; set; }
    
    /// <summary>
    /// 折扣百分比
    /// </summary>
    public int? DiscountPercent { get; set; }
    
    /// <summary>
    /// 是否在打折
    /// </summary>
    public bool IsOnSale { get; set; }
}
