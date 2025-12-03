using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏平台映射实体类
/// </summary>
[Table("game_platform")]
public class GamePlatform
{
    [Column("game_id")]
    public long GameId { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("platform_game_id")]
    public string PlatformGameId { get; set; } = string.Empty;

    [MaxLength(2048)]
    [Column("game_platform_url")]
    public string? GamePlatformUrl { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("PlatformId")]
    public virtual Platform? Platform { get; set; }
}

