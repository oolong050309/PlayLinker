using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 第三方攻略/外链源
/// </summary>
[Table("external_links")]
public partial class ExternalLink
{
    [Key]
    [Column("link_id")]
    public int LinkId { get; set; }

    /// <summary>
    /// bilibili,youtube等
    /// </summary>
    [Column("source")]
    [StringLength(255)]
    public string Source { get; set; } = null!;

    [Column("link_title")]
    [StringLength(255)]
    public string LinkTitle { get; set; } = null!;

    [Column("link_url")]
    [StringLength(2048)]
    public string LinkUrl { get; set; } = null!;

    [Column("link_type", TypeName = "enum('guide','video','review','wiki','community')")]
    public string LinkType { get; set; } = null!;

    [Column("link_summary")]
    [StringLength(255)]
    public string? LinkSummary { get; set; }

    [InverseProperty("Link")]
    public virtual ICollection<GameExternalLink> GameExternalLinks { get; set; } = new List<GameExternalLink>();
}
