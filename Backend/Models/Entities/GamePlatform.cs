using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏在平台的映射
/// </summary>
[PrimaryKey("GameId", "PlatformId")]
[Table("game_platform")]
[Index("PlatformId", Name = "platform_id")]
public partial class GamePlatform
{
    [Key]
    [Column("game_id")]
    public long GameId { get; set; }

    [Key]
    [Column("platform_id")]
    public int PlatformId { get; set; }

    /// <summary>
    /// 平台内部标识
    /// </summary>
    [Column("platform_game_id")]
    [StringLength(128)]
    public string PlatformGameId { get; set; } = null!;

    [Column("game_platform_url")]
    [StringLength(2048)]
    public string? GamePlatformUrl { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GamePlatforms")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("PlatformId")]
    [InverseProperty("GamePlatforms")]
    public virtual Platform Platform { get; set; } = null!;
}
