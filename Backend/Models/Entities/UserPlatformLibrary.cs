using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户在某平台的单款游戏记录
/// </summary>
[PrimaryKey("PlatformUserId", "PlatformId", "GameId")]
[Table("user_platform_library")]
[Index("GameId", Name = "game_id")]
[Index("LastPlayed", Name = "idx_last_played")]
public partial class UserPlatformLibrary
{
    /// <summary>
    /// 平台侧用户标识（平台唯一）
    /// </summary>
    [Key]
    [Column("platform_user_id")]
    [StringLength(128)]
    public string PlatformUserId { get; set; } = null!;

    [Key]
    [Column("platform_id")]
    public int PlatformId { get; set; }

    /// <summary>
    /// 若为单款记录
    /// </summary>
    [Key]
    [Column("game_id")]
    public long GameId { get; set; }

    /// <summary>
    /// 累计游玩分钟数（该平台/该游戏）
    /// </summary>
    [Column("playtime_minutes")]
    public int PlaytimeMinutes { get; set; }

    [Column("last_played", TypeName = "datetime")]
    public DateTime? LastPlayed { get; set; }

    /// <summary>
    /// 成就总数（平台/该游戏）
    /// </summary>
    [Column("achievements_total")]
    public int? AchievementsTotal { get; set; }

    /// <summary>
    /// 已解锁成就数（平台/该游戏）
    /// </summary>
    [Column("achievements_unlocked")]
    public int? AchievementsUnlocked { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("UserPlatformLibraries")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("PlatformUserId, PlatformId")]
    [InverseProperty("UserPlatformLibraries")]
    public virtual PlayerPlatform PlayerPlatform { get; set; } = null!;
}
