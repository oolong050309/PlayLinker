using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 新闻/公告源
/// </summary>
[Table("news")]
[Index("Date", Name = "idx_date")]
public partial class News
{
    [Key]
    [Column("news_id")]
    public int NewsId { get; set; }

    [Column("news_title")]
    [StringLength(512)]
    public string NewsTitle { get; set; } = null!;

    [Column("news_url")]
    [StringLength(2048)]
    public string? NewsUrl { get; set; }

    /// <summary>
    /// Unix时间戳
    /// </summary>
    [Column("date")]
    public long Date { get; set; }

    [Column("author")]
    [StringLength(128)]
    public string Author { get; set; } = null!;

    [Column("contents", TypeName = "text")]
    public string Contents { get; set; } = null!;

    [InverseProperty("News")]
    public virtual ICollection<GameNews> GameNews { get; set; } = new List<GameNews>();
}
