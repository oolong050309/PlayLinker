using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 成就实体类
/// </summary>
[Table("achievements")]
public class Achievement
{
    [Key]
    [Column("achievement_id")]
    public long AchievementId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("achievement_name")]
    public string AchievementName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("hidden")]
    public bool Hidden { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [MaxLength(2048)]
    [Column("icon_unlocked")]
    public string IconUnlocked { get; set; } = string.Empty;

    [Required]
    [MaxLength(2048)]
    [Column("icon_locked")]
    public string IconLocked { get; set; } = string.Empty;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}

/// <summary>
/// 用户成就解锁记录实体类
/// </summary>
[Table("user_achievements")]
public class UserAchievement
{
    [Key]
    [Column("user_achievement_id")]
    public long UserAchievementId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("achievement_id")]
    public long AchievementId { get; set; }

    [Column("unlocked")]
    public bool Unlocked { get; set; }

    [Column("unlock_time")]
    public DateTime? UnlockTime { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("AchievementId")]
    public virtual Achievement? Achievement { get; set; }
}

