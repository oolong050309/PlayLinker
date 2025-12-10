using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 价格提醒订阅表(愿望单)
/// </summary>
[Table("price_alert_subscription")]
[Index("GameId", Name = "game_id")]
[Index("IsActive", Name = "idx_is_active")]
[Index("PlatformId", Name = "platform_id")]
[Index("UserId", "GameId", "PlatformId", Name = "uk_user_game_platform", IsUnique = true)]
public partial class PriceAlertSubscription
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

    /// <summary>
    /// NULL表示不启用
    /// </summary>
    [Column("target_price")]
    [Precision(10, 2)]
    public decimal? TargetPrice { get; set; }

    /// <summary>
    /// NULL表示不启用
    /// </summary>
    [Column("target_discount")]
    public int? TargetDiscount { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("PriceAlertSubscriptions")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("PlatformId")]
    [InverseProperty("PriceAlertSubscriptions")]
    public virtual Platform Platform { get; set; } = null!;

    [InverseProperty("Subscription")]
    public virtual ICollection<PriceAlertLog> PriceAlertLogs { get; set; } = new List<PriceAlertLog>();

    [ForeignKey("UserId")]
    [InverseProperty("PriceAlertSubscriptions")]
    public virtual User User { get; set; } = null!;
}
