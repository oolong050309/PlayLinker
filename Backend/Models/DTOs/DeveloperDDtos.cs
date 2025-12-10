using System.Text.Json.Serialization;

namespace PlayLinker.Models.DTOs;

// 用户偏好 DTO
public class UserPreferenceDto
{
    public int PreferenceId { get; set; }
    public int UserId { get; set; }
    public List<PreferenceGenreDto> FavoriteGenres { get; set; } = new();
    public string? PlaytimeRange { get; set; }
    public int PriceSensitivity { get; set; }
    public DateTime UpdatedAt { get; set; }
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

// 愿望单 DTO
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
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
    public bool IsOnSale { get; set; }
    public DateTime AddedAt { get; set; }
}

public class AddWishlistDto
{
    public long GameId { get; set; }
    public int PlatformId { get; set; }
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
}

// 价格历史 DTO
public class PriceHistoryDto
{
    public long PriceId { get; set; }
    public string Date { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public int Discount { get; set; }
    public bool IsDiscount { get; set; }
}

public class PriceHistoryResponseDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal LowestPrice { get; set; }
    public List<PriceHistoryDto> PriceHistory { get; set; } = new();
}

// --- AI 分析相关 DTO ---

public class AnalyzePreferenceRequestDto
{
    public bool AnalyzePlaytime { get; set; }
    public bool AnalyzePurchases { get; set; }
    public string TimeRange { get; set; } = "last_6_months";
}

public class AnalyzePreferenceResponseDto
{
    public int AnalyzedGames { get; set; }
    public string AnalyzedPeriod { get; set; } = string.Empty;
    public object DetectedPreferences { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

// --- 价格预测相关 DTO ---

public class PricePredictionDto
{
    public double Probability { get; set; }
    public string EstimatedDate { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public class TrackPriceRequestDto
{
    public long GameId { get; set; }
    public int PlatformId { get; set; }
    public decimal? TargetPrice { get; set; }
    public int? TargetDiscount { get; set; }
}