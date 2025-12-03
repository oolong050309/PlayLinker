namespace PlayLinker.Models.DTOs;

/// <summary>
/// 游戏成就列表响应DTO
/// </summary>
public class GameAchievementsDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public List<AchievementDto> Achievements { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// 成就DTO
/// </summary>
public class AchievementDto
{
    public long AchievementId { get; set; }
    public string AchievementName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Hidden { get; set; }
    public string IconUnlocked { get; set; } = string.Empty;
    public string IconLocked { get; set; } = string.Empty;
    public double GlobalUnlockRate { get; set; }
    public bool? Unlocked { get; set; }
    public string? UnlockTime { get; set; }
}

/// <summary>
/// 用户成就总览响应DTO
/// </summary>
public class UserAchievementsOverviewDto
{
    public int TotalAchievements { get; set; }
    public int UnlockedAchievements { get; set; }
    public double UnlockRate { get; set; }
    public int PerfectGames { get; set; }
    public List<RecentUnlockDto> RecentUnlocks { get; set; } = new();
    public List<RareAchievementDto> RareAchievements { get; set; } = new();
    public AchievementStatisticsDto Statistics { get; set; } = new();
}

/// <summary>
/// 最近解锁成就DTO
/// </summary>
public class RecentUnlockDto
{
    public long AchievementId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string AchievementName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UnlockTime { get; set; } = string.Empty;
    public string IconUnlocked { get; set; } = string.Empty;
}

/// <summary>
/// 稀有成就DTO
/// </summary>
public class RareAchievementDto
{
    public long AchievementId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string AchievementName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double GlobalUnlockRate { get; set; }
    public string UnlockTime { get; set; } = string.Empty;
}

/// <summary>
/// 成就统计DTO
/// </summary>
public class AchievementStatisticsDto
{
    public double AverageCompletionRate { get; set; }
    public int TotalPlaytime { get; set; }
    public double AchievementsPerHour { get; set; }
}

/// <summary>
/// 同步成就请求DTO
/// </summary>
public class SyncAchievementsRequestDto
{
    public int UserId { get; set; }
    public int? PlatformId { get; set; }
    public long? GameId { get; set; }
}

/// <summary>
/// 同步成就响应DTO
/// </summary>
public class SyncAchievementsResponseDto
{
    public int SyncedGames { get; set; }
    public int NewUnlocks { get; set; }
    public int TotalUnlocked { get; set; }
    public string SyncTime { get; set; } = string.Empty;
}

