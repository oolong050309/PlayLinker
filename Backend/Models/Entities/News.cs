using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 新闻实体类
/// </summary>
[Table("news")]
public class News
{
    [Key]
    [Column("news_id")]
    public long NewsId { get; set; }

    [Required]
    [MaxLength(512)]
    [Column("news_title")]
    public string NewsTitle { get; set; } = string.Empty;

    [MaxLength(2048)]
    [Column("news_url")]
    public string? NewsUrl { get; set; }

    [Column("date")]
    public long Date { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("author")]
    public string Author { get; set; } = string.Empty;

    [Required]
    [Column("contents")]
    public string Contents { get; set; } = string.Empty;

    public virtual ICollection<GameNews> GameNews { get; set; } = new List<GameNews>();
}

/// <summary>
/// 游戏与新闻关联实体类
/// </summary>
[Table("game_news")]
public class GameNews
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("news_id")]
    public long NewsId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("NewsId")]
    public virtual News? News { get; set; }
}

