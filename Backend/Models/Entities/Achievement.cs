using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 成就表
/// </summary>
[Table("achievements")]
[Index("GameId", Name = "idx_game_id")]
public partial class Achievement
{
    [Key]
    [Column("achievement_id")]
    public long AchievementId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("achievement_name")]
    [StringLength(128)]
    public string AchievementName { get; set; } = null!;

    [Column("displayName")]
    [StringLength(128)]
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// 0=不隐藏，1=隐藏
    /// </summary>
    [Column("hidden")]
    public bool Hidden { get; set; }

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    /// <summary>
    /// 解锁状态
    /// </summary>
    [Column("icon_unlocked")]
    [StringLength(2048)]
    public string IconUnlocked { get; set; } = null!;

    /// <summary>
    /// 未解锁状态/灰色
    /// </summary>
    [Column("icon_locked")]
    [StringLength(2048)]
    public string IconLocked { get; set; } = null!;

    [ForeignKey("GameId")]
    [InverseProperty("Achievements")]
    public virtual Game Game { get; set; } = null!;

    [InverseProperty("Achievement")]
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
