using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 通知中心表
/// </summary>
[Table("notification_center")]
[Index("IsRead", Name = "idx_is_read")]
[Index("UserId", Name = "idx_user_id")]
[Index("RelatedId", Name = "related_id", IsUnique = true)]
public partial class NotificationCenter
{
    [Key]
    [Column("notification_id")]
    public long NotificationId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("source_module", TypeName = "enum('price_alert','parental_control','system','recommendation','game_update')")]
    public string SourceModule { get; set; } = null!;

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("content", TypeName = "text")]
    public string Content { get; set; } = null!;

    [Column("notification_type", TypeName = "enum('info','warning','alert')")]
    public string? NotificationType { get; set; }

    [Column("is_read")]
    public bool? IsRead { get; set; }

    [Column("related_id")]
    public long RelatedId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("Notification")]
    public virtual ICollection<ParentalAlertLog> ParentalAlertLogs { get; set; } = new List<ParentalAlertLog>();

    [InverseProperty("Notification")]
    public virtual ICollection<PriceAlertLog> PriceAlertLogs { get; set; } = new List<PriceAlertLog>();

    [ForeignKey("UserId")]
    [InverseProperty("NotificationCenters")]
    public virtual User User { get; set; } = null!;
}
