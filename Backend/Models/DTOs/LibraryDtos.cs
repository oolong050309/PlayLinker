namespace PlayLinker.Models.DTOs;

/// <summary>
/// 游戏库概览响应DTO
/// </summary>
public class LibraryOverviewDto
{
    public int TotalGamesOwned { get; set; }
    public int GamesPlayed { get; set; }
    public int TotalPlaytimeMinutes { get; set; }
    public int TotalAchievements { get; set; }
    public int UnlockedAchievements { get; set; }
    public int RecentlyPlayedCount { get; set; }
    public int RecentPlaytimeMinutes { get; set; }
    public List<PlatformStatsDto> PlatformStats { get; set; } = new();
    public List<GenreDistributionDto> GenreDistribution { get; set; } = new();
}

/// <summary>
/// 平台统计DTO
/// </summary>
public class PlatformStatsDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public int GamesOwned { get; set; }
    public string LastSyncTime { get; set; } = string.Empty;
}

/// <summary>
/// 题材分布DTO
/// </summary>
public class GenreDistributionDto
{
    public string Genre { get; set; } = string.Empty;
    public int Count { get; set; }
    public int PlaytimeMinutes { get; set; }
}

/// <summary>
/// 用户游戏列表响应DTO
/// </summary>
public class UserGameListDto
{
    public List<UserGameItemDto> Items { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// 用户游戏项DTO
/// </summary>
public class UserGameItemDto
{
    public long GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HeaderImage { get; set; } = string.Empty;
    public List<int> Platforms { get; set; } = new();
    public int PlaytimeMinutes { get; set; }
    public string? LastPlayed { get; set; }
    public int AchievementsUnlocked { get; set; }
    public int AchievementsTotal { get; set; }
    public List<OwnedPlatformDto> OwnedPlatforms { get; set; } = new();
}

/// <summary>
/// 拥有的平台DTO
/// </summary>
public class OwnedPlatformDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public int PlaytimeMinutes { get; set; }
}

/// <summary>
/// 同步平台数据请求DTO
/// </summary>
public class SyncPlatformRequestDto
{
    public int PlatformId { get; set; }
    public bool FullSync { get; set; }
}

/// <summary>
/// 同步平台数据响应DTO
/// </summary>
public class SyncPlatformResponseDto
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public int EstimatedTime { get; set; }
    public int GamesDetected { get; set; }
}

/// <summary>
/// 游戏统计数据响应DTO
/// </summary>
public class GameStatsDto
{
    public int TotalPlaytime { get; set; }
    public int AveragePlaytime { get; set; }
    public MostPlayedGameDto? MostPlayedGame { get; set; }
    public List<GenreDistributionDto> GenreDistribution { get; set; } = new();
    public List<PlatformDistributionDto> PlatformDistribution { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
}

/// <summary>
/// 最常玩游戏DTO
/// </summary>
public class MostPlayedGameDto
{
    public long GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Playtime { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// 平台分布DTO
/// </summary>
public class PlatformDistributionDto
{
    public string Platform { get; set; } = string.Empty;
    public int GamesCount { get; set; }
    public int Playtime { get; set; }
}

/// <summary>
/// 最近活动DTO
/// </summary>
public class RecentActivityDto
{
    public string Date { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int PlaytimeMinutes { get; set; }
}

