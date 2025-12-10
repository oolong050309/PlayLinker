using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与新闻关联表
/// </summary>
[Table("game_news")]
[Index("NewsId", Name = "game_news_ibfk_2")]
[Index("GameId", "NewsId", Name = "uk_game_news", IsUnique = true)]
public partial class GameNews
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("news_id")]
    public int NewsId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GameNews")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("NewsId")]
    [InverseProperty("GameNews")]
    public virtual News News { get; set; } = null!;
}
