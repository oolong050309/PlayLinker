using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

// 用户偏好
[Table("user_preference", Schema = "business_features")]
public class UserPreference
{
    [Key]
    [Column("preference_id")]
    public int PreferenceId { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("playtime_range")]
    public string? PlaytimeRange { get; set; }
    [Column("price_sensitivity")]
    public int PriceSensitivity { get; set; } = 2;
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual List<PreferenceGenre> PreferenceGenres { get; set; } = new();
}

// 偏好题材关联
[Table("preference_genre", Schema = "business_features")]
public class PreferenceGenre
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("preference_id")]
    public int PreferenceId { get; set; }
    [Column("genre_id")]
    public int GenreId { get; set; }
    
    [ForeignKey("GenreId")]
    public virtual Genre? Genre { get; set; }
}

// AI推荐记录
[Table("recommendation", Schema = "business_features")]
public class Recommendation
{
    [Key]
    [Column("recommendation_id")]
    public int RecommendationId { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("game_id")]
    public long GameId { get; set; }
    [Column("recommendation_type")]
    public string RecommendationType { get; set; } = "game"; // game, discount, similar, trending
    [Column("recommendation_strategy")]
    public string RecommendationStrategy { get; set; } = "popular"; // collaborative, etc.
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("expire_time")]
    public DateTime ExpireTime { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }
}

// 推荐反馈
[Table("recommendation_feedback", Schema = "business_features")]
public class RecommendationFeedback
{
    [Key]
    [Column("feedback_id")]
    public int FeedbackId { get; set; }
    [Column("recommendation_id")]
    public int RecommendationId { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("feedback_result")]
    public int Result { get; set; } // 1=Like, 2=Dislike
    [Column("feedback_time")]
    public DateTime FeedbackTime { get; set; } = DateTime.UtcNow;
    [Column("remark")]
    public string? Remark { get; set; }
}

// 价格历史
[Table("price_history", Schema = "business_features")]
public class PriceHistory
{
    [Key]
    [Column("price_id")]
    public long PriceId { get; set; }
    [Column("game_id")]
    public long GameId { get; set; }
    [Column("platform_id")]
    public int PlatformId { get; set; }
    [Column("current_price")]
    public decimal CurrentPrice { get; set; }
    [Column("original_price")]
    public decimal OriginalPrice { get; set; }
    [Column("discount_rate")]
    public int DiscountRate { get; set; }
    [Column("is_discount")]
    public bool IsDiscount { get; set; }
    [Column("record_date")]
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;
}

// 愿望单/价格订阅
[Table("price_alert_subscription", Schema = "business_features")]
public class PriceAlertSubscription
{
    [Key]
    [Column("subscription_id")]
    public long SubscriptionId { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("game_id")]
    public long GameId { get; set; }
    [Column("platform_id")]
    public int PlatformId { get; set; }
    [Column("target_price")]
    public decimal? TargetPrice { get; set; }
    [Column("target_discount")]
    public int? TargetDiscount { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }
    [ForeignKey("PlatformId")]
    public virtual Platform? Platform { get; set; }
}

// 价格报警日志
[Table("price_alert_log", Schema = "business_features")]
public class PriceAlertLog
{
    [Key]
    [Column("alert_id")]
    public long AlertId { get; set; }
    [Column("subscription_id")]
    public long SubscriptionId { get; set; }
    [Column("price_id")]
    public long PriceId { get; set; }
    [Column("alert_type")]
    public string AlertType { get; set; } = "target_price";
    [Column("alert_time")]
    public DateTime AlertTime { get; set; } = DateTime.UtcNow;
    [Column("notification_id")]
    public long? NotificationId { get; set; }
}