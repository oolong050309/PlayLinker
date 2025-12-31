using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏Mod平台映射表
/// </summary>
[Table("game_mod_source")]
public class GameModSource
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    /// <summary>
    /// Mod来源: NexusMods, 3DM, GameBanana, Steam
    /// </summary>
    [Column("source")]
    [StringLength(50)]
    public string Source { get; set; } = null!;

    /// <summary>
    /// 第三方平台的游戏ID
    /// </summary>
    [Column("external_game_id")]
    [StringLength(100)]
    public string ExternalGameId { get; set; } = null!;

    /// <summary>
    /// 如 NexusMods 的 domain_name
    /// </summary>
    [Column("external_domain")]
    [StringLength(100)]
    public string? ExternalDomain { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game Game { get; set; } = null!;
}
