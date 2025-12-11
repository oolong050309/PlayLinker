namespace PlayLinker.Models.DTOs;

// 游玩时间分析响应
public class PlaytimeAnalyticsResponse
{
    public string Period { get; set; } = string.Empty;
    public int TotalMinutes { get; set; }
    public int DailyAverage { get; set; }
    public string PeakDay { get; set; } = string.Empty;
    public int PeakMinutes { get; set; }
    public List<DailyPlaytime> Distribution { get; set; } = new();
    public List<GamePlaytimeBreakdown> GameBreakdown { get; set; } = new();
    public List<TimeSlotDistribution> TimeSlotDistribution { get; set; } = new();
    public List<WeekdayDistribution> WeekdayDistribution { get; set; } = new();
}

// 每日游玩时间
public class DailyPlaytime
{
    public string Date { get; set; } = string.Empty;
    public int Minutes { get; set; }
}

// 游戏游玩时间分解
public class GamePlaytimeBreakdown
{
    public long GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public decimal Percentage { get; set; }
    public int Sessions { get; set; }
}

// 时间段分布
public class TimeSlotDistribution
{
    public string Slot { get; set; } = string.Empty;
    public int Minutes { get; set; }
}

// 星期分布
public class WeekdayDistribution
{
    public string Day { get; set; } = string.Empty;
    public int Minutes { get; set; }
}

// 题材偏好分析响应
public class GenreAnalyticsResponse
{
    public List<GenrePreference> GenrePreferences { get; set; } = new();
    public string TopGenre { get; set; } = string.Empty;
    public int TotalGenres { get; set; }
}

// 题材偏好
public class GenrePreference
{
    public int GenreId { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public int GamesOwned { get; set; }
    public int GamesPlayed { get; set; }
    public int TotalPlaytimeMinutes { get; set; }
    public int AveragePlaytime { get; set; }
    public decimal PreferenceScore { get; set; }
}

// 平台分布分析响应
public class PlatformAnalyticsResponse
{
    public List<PlatformDistribution> PlatformDistribution { get; set; } = new();
    public string MostUsedPlatform { get; set; } = string.Empty;
    public int TotalPlatforms { get; set; }
}

// 平台分布
public class PlatformDistribution
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public int GamesCount { get; set; }
    public int PlaytimeMinutes { get; set; }
    public decimal Percentage { get; set; }
}
