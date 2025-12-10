using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与开发商关联表
/// </summary>
[Table("game_developers")]
[Index("DeveloperId", Name = "game_developers_ibfk_2")]
[Index("GameId", "DeveloperId", Name = "uk_game_developer", IsUnique = true)]
public partial class GameDeveloper
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("developer_id")]
    public int DeveloperId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("DeveloperId")]
    [InverseProperty("GameDevelopers")]
    public virtual Developer Developer { get; set; } = null!;

    [ForeignKey("GameId")]
    [InverseProperty("GameDevelopers")]
    public virtual Game Game { get; set; } = null!;
}
