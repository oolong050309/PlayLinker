using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏语言关联表
/// </summary>
[Table("game_languages")]
[Index("LanguageId", Name = "language_id")]
[Index("GameId", "LanguageId", Name = "uk_game_language", IsUnique = true)]
public partial class GameLanguage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("language_id")]
    public int LanguageId { get; set; }

    /// <summary>
    /// 比如 是否有简体中文
    /// </summary>
    [Column("notes")]
    [StringLength(256)]
    public string? Notes { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GameLanguages")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("LanguageId")]
    [InverseProperty("GameLanguages")]
    public virtual Language Language { get; set; } = null!;
}
