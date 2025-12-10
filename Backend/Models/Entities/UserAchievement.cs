using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户成就解锁记录
/// </summary>
[Table("user_achievements")]
[Index("AchievementId", Name = "achievement_id")]
[Index("UserId", Name = "idx_user_id")]
[Index("PlatformId", Name = "platform_id")]
[Index("UserId", "AchievementId", "PlatformId", Name = "uk_user_achievement", IsUnique = true)]
public partial class UserAchievement
{
    [Key]
    [Column("user_achievement_id")]
    public long UserAchievementId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("achievement_id")]
    public long AchievementId { get; set; }

    /// <summary>
    /// 0未解锁/1已解锁
    /// </summary>
    [Column("unlocked")]
    public bool Unlocked { get; set; }

    /// <summary>
    /// null表示未解锁
    /// </summary>
    [Column("unlock_time", TypeName = "datetime")]
    public DateTime? UnlockTime { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("AchievementId")]
    [InverseProperty("UserAchievements")]
    public virtual Achievement Achievement { get; set; } = null!;

    [ForeignKey("PlatformId")]
    [InverseProperty("UserAchievements")]
    public virtual Platform Platform { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserAchievements")]
    public virtual User User { get; set; } = null!;
}
