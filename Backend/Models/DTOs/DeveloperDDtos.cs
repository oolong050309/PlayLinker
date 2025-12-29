using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

// --- 偏好模块 DTOs ---

public class UserPreferenceDto
{
    public int PreferenceId { get; set; }
    public int UserId { get; set; }
    public string? PlaytimeRange { get; set; }
    public int PriceSensitivity { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PreferenceGenreDto> FavoriteGenres { get; set; } = new();
}

public class PreferenceGenreDto
{
    public int GenreId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UpdatePreferenceDto
{
    public List<int> FavoriteGenres { get; set; } = new();
    public string? PlaytimeRange { get; set; }
    public int PriceSensitivity { get; set; }
}

public class AnalyzePreferenceRequestDto
{
    public bool AnalyzePlaytime { get; set; }
    public bool AnalyzePurchases { get; set; }
    public string TimeRange { get; set; } = "last_month";
}

public class AnalyzePreferenceResponseDto
{
    public int AnalyzedGames { get; set; }
    public string AnalyzedPeriod { get; set; } = string.Empty;
    public object DetectedPreferences { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

// --- 价格 & 愿望单模块 DTOs ---

public class PriceHistoryDto
{
    public DateTime Date { get; set; }
    public decimal CurrentPrice { get; set; }
    public bool IsDiscount { get; set; }
}

public class PricePredictionDto
{
    public double Probability { get; set; }
    public string EstimatedDate { get; set; } = string.Empty; // 保持 string 类型以匹配 AI 输出
    public string Reasoning { get; set; } = string.Empty;
}

public class WishlistItemDto
{
    public long SubscriptionId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string HeaderImage { get; set; } = string.Empty;
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
    public DateTime AddedAt { get; set; }
}

public class AddWishlistDto
{
    public long GameId { get; set; }
    public int PlatformId { get; set; }
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
    public bool? NotifyEmail { get; set; }
}

public class UpdateWishlistDto
{
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
    public bool? NotifyEmail { get; set; }
}

public class TrackPriceDto
{
    public long GameId { get; set; }
    public int PlatformId { get; set; }
    public bool NotifyOnDiscount { get; set; }
    public int? TargetDiscount { get; set; }
    public decimal? TargetPrice { get; set; }
}

// --- 推荐模块 DTOs ---

public class FeedbackRequestDto
{
    public int FeedbackResult { get; set; } // 1=喜欢, 2=不喜欢
    public string? Remark { get; set; }
}