using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 价格提醒日志表
/// </summary>
[Table("price_alert_log")]
[Index("AlertTime", Name = "idx_alert_time")]
[Index("NotificationId", Name = "notification_id")]
[Index("PriceId", Name = "price_id")]
[Index("SubscriptionId", Name = "subscription_id")]
public partial class PriceAlertLog
{
    [Key]
    [Column("alert_id")]
    public long AlertId { get; set; }

    [Column("subscription_id")]
    public long SubscriptionId { get; set; }

    [Column("price_id")]
    public long PriceId { get; set; }

    [Column("alert_type", TypeName = "enum('target_price','target_discount')")]
    public string AlertType { get; set; } = null!;

    [Column("alert_time", TypeName = "datetime")]
    public DateTime? AlertTime { get; set; }

    [Column("notification_id")]
    public long? NotificationId { get; set; }

    [ForeignKey("NotificationId")]
    [InverseProperty("PriceAlertLogs")]
    public virtual NotificationCenter? Notification { get; set; }

    [ForeignKey("PriceId")]
    [InverseProperty("PriceAlertLogs")]
    public virtual PriceHistory Price { get; set; } = null!;

    [ForeignKey("SubscriptionId")]
    [InverseProperty("PriceAlertLogs")]
    public virtual PriceAlertSubscription Subscription { get; set; } = null!;
}
