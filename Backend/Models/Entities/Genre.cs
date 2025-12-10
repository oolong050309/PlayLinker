using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏题材/风格词表
/// </summary>
[Table("genres")]
[Index("Name", Name = "name", IsUnique = true)]
public partial class Genre
{
    [Key]
    [Column("genre_id")]
    public int GenreId { get; set; }

    [Column("name")]
    [StringLength(20)]
    public string Name { get; set; } = null!;

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("Genre")]
    public virtual ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();

    [InverseProperty("Genre")]
    public virtual ICollection<PreferenceGenre> PreferenceGenres { get; set; } = new List<PreferenceGenre>();
}
