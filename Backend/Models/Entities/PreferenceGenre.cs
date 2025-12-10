using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户偏好与游戏题材关联表
/// </summary>
[Table("preference_genre")]
[Index("GenreId", Name = "genre_id")]
[Index("PreferenceId", "GenreId", Name = "uk_preference_genre", IsUnique = true)]
public partial class PreferenceGenre
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("preference_id")]
    public int PreferenceId { get; set; }

    [Column("genre_id")]
    public int GenreId { get; set; }

    [ForeignKey("GenreId")]
    [InverseProperty("PreferenceGenres")]
    public virtual Genre Genre { get; set; } = null!;

    [ForeignKey("PreferenceId")]
    [InverseProperty("PreferenceGenres")]
    public virtual UserPreference Preference { get; set; } = null!;
}
