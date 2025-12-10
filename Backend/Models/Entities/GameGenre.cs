using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与题材关联表
/// </summary>
[Table("game_genres")]
[Index("GenreId", Name = "genre_id")]
[Index("GameId", "GenreId", Name = "uk_game_genre", IsUnique = true)]
public partial class GameGenre
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("genre_id")]
    public int GenreId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GameGenres")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("GenreId")]
    [InverseProperty("GameGenres")]
    public virtual Genre Genre { get; set; } = null!;
}
