using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 核心登录与基础信息表
/// </summary>
[Table("user")]
[Index("Email", Name = "email", IsUnique = true)]
[Index("Status", Name = "idx_status")]
[Index("Username", Name = "idx_username", IsUnique = true)]
[Index("Phone", Name = "phone", IsUnique = true)]
[Index("RoleId", Name = "role_id")]
public partial class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("username")]
    [StringLength(128)]
    public string Username { get; set; } = null!;

    /// <summary>
    /// AES-256加密
    /// </summary>
    [Column("hashed_password")]
    [StringLength(128)]
    public string HashedPassword { get; set; } = null!;

    [Column("email")]
    [StringLength(100)]
    public string? Email { get; set; }

    /// <summary>
    /// 1男/2女/0未知
    /// </summary>
    [Column("gender")]
    public int? Gender { get; set; }

    [Column("phone")]
    [StringLength(100)]
    public string? Phone { get; set; }

    [Column("avatar_url")]
    [StringLength(2048)]
    public string? AvatarUrl { get; set; }

    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("status", TypeName = "enum('active','disabled','inactive')")]
    public string? Status { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("last_login_time", TypeName = "datetime")]
    public DateTime? LastLoginTime { get; set; }

    [Column("login_ip")]
    [StringLength(45)]
    public string? LoginIp { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<CloudSaveBackup> CloudSaveBackups { get; set; } = new List<CloudSaveBackup>();

    [InverseProperty("User")]
    public virtual ICollection<LocalGameInstall> LocalGameInstalls { get; set; } = new List<LocalGameInstall>();

    [InverseProperty("User")]
    public virtual ICollection<NotificationCenter> NotificationCenters { get; set; } = new List<NotificationCenter>();

    [InverseProperty("ChildUser")]
    public virtual ICollection<ParentalAlertLog> ParentalAlertLogs { get; set; } = new List<ParentalAlertLog>();

    [InverseProperty("ChildUser")]
    public virtual ParentalControlRelationship? ParentalControlRelationshipChildUser { get; set; }

    [InverseProperty("ParentUser")]
    public virtual ICollection<ParentalControlRelationship> ParentalControlRelationshipParentUsers { get; set; } = new List<ParentalControlRelationship>();

    [InverseProperty("ChildUser")]
    public virtual ICollection<ParentalControlRule> ParentalControlRules { get; set; } = new List<ParentalControlRule>();

    [InverseProperty("User")]
    public virtual ICollection<PriceAlertSubscription> PriceAlertSubscriptions { get; set; } = new List<PriceAlertSubscription>();

    [InverseProperty("User")]
    public virtual ICollection<RecommendationFeedback> RecommendationFeedbacks { get; set; } = new List<RecommendationFeedback>();

    [InverseProperty("User")]
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

    [InverseProperty("User")]
    public virtual ICollection<ReportGenerationRecord> ReportGenerationRecords { get; set; } = new List<ReportGenerationRecord>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("User")]
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    [InverseProperty("User")]
    public virtual UserGameLibrary? UserGameLibrary { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<UserPlatformBinding> UserPlatformBindings { get; set; } = new List<UserPlatformBinding>();

    [InverseProperty("User")]
    public virtual UserPreference? UserPreference { get; set; }
}
