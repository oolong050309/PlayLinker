using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 家长监管报警日志表
/// </summary>
[Table("parental_alert_log")]
[Index("ChildUserId", Name = "child_user_id")]
[Index("AlertTime", Name = "idx_alert_time")]
[Index("NotificationId", Name = "notification_id")]
[Index("RuleId", Name = "rule_id")]
public partial class ParentalAlertLog
{
    [Key]
    [Column("alert_id")]
    public long AlertId { get; set; }

    [Column("rule_id")]
    public long RuleId { get; set; }

    [Column("child_user_id")]
    public int ChildUserId { get; set; }

    [Column("violation_details", TypeName = "json")]
    public string ViolationDetails { get; set; } = null!;

    [Column("alert_time", TypeName = "datetime")]
    public DateTime? AlertTime { get; set; }

    [Column("notification_id")]
    public long? NotificationId { get; set; }

    [ForeignKey("ChildUserId")]
    [InverseProperty("ParentalAlertLogs")]
    public virtual User ChildUser { get; set; } = null!;

    [ForeignKey("NotificationId")]
    [InverseProperty("ParentalAlertLogs")]
    public virtual NotificationCenter? Notification { get; set; }

    [ForeignKey("RuleId")]
    [InverseProperty("ParentalAlertLogs")]
    public virtual ParentalControlRule Rule { get; set; } = null!;
}
