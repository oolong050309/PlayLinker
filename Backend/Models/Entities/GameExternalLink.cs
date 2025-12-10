using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与外链关联表
/// </summary>
[Table("game_external_links")]
[Index("LinkId", Name = "link_id")]
[Index("GameId", "LinkId", Name = "uk_game_link", IsUnique = true)]
public partial class GameExternalLink
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GameExternalLinks")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("LinkId")]
    [InverseProperty("GameExternalLinks")]
    public virtual ExternalLink Link { get; set; } = null!;
}
