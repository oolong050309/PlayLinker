using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 支持语言表
/// </summary>
[Table("languages")]
public partial class Language
{
    [Key]
    [Column("language_id")]
    public int LanguageId { get; set; }

    [Column("language_name")]
    [StringLength(50)]
    public string LanguageName { get; set; } = null!;

    [InverseProperty("Language")]
    public virtual ICollection<GameLanguage> GameLanguages { get; set; } = new List<GameLanguage>();
}
