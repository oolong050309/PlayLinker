using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏实体类 - 对应games表
/// </summary>
[Table("games")]
public class Game
{
    [Key]
    [Column("game_id")]
    public long GameId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("is_free")]
    public bool IsFree { get; set; }

    [Column("require_age")]
    public byte? RequireAge { get; set; }

    [Column("short_description")]
    public string? ShortDescription { get; set; }

    [Column("detailed_description")]
    public string? DetailedDescription { get; set; }

    [Required]
    [MaxLength(2048)]
    [Column("header_image")]
    public string HeaderImage { get; set; } = string.Empty;

    [Required]
    [MaxLength(2048)]
    [Column("capsile_image")]
    public string CapsuleImage { get; set; } = string.Empty;

    [Required]
    [MaxLength(2048)]
    [Column("background")]
    public string Background { get; set; } = string.Empty;

    [Column("pc_recommended")]
    public string? PcRecommended { get; set; }

    [Column("pc_minimum")]
    public string? PcMinimum { get; set; }

    [Column("mac_recommended")]
    public string? MacRecommended { get; set; }

    [Column("mac_minimum")]
    public string? MacMinimum { get; set; }

    [Column("linux_recommended")]
    public string? LinuxRecommended { get; set; }

    [Column("linux_minimum")]
    public string? LinuxMinimum { get; set; }

    [Column("windows")]
    public bool Windows { get; set; }

    [Column("mac")]
    public bool Mac { get; set; }

    [Column("linux")]
    public bool Linux { get; set; }

    [Required]
    [Column("release_date")]
    public DateTime ReleaseDate { get; set; }

    [Column("review_score")]
    public int ReviewScore { get; set; }

    [Column("review_score_desc")]
    [MaxLength(20)]
    public string ReviewScoreDesc { get; set; } = string.Empty;

    [Column("num_reviews")]
    public int NumReviews { get; set; }

    [Column("total_positive")]
    public int TotalPositive { get; set; }

    // 导航属性
    public virtual ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
    public virtual ICollection<GameDeveloper> GameDevelopers { get; set; } = new List<GameDeveloper>();
    public virtual ICollection<GamePublisher> GamePublishers { get; set; } = new List<GamePublisher>();
    public virtual ICollection<GameCategory> GameCategories { get; set; } = new List<GameCategory>();
    public virtual ICollection<GameLanguage> GameLanguages { get; set; } = new List<GameLanguage>();
    public virtual ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();
    public virtual ICollection<GameNews> GameNews { get; set; } = new List<GameNews>();
    public virtual GameRanking? GameRanking { get; set; }
}

/// <summary>
/// 游戏排行榜实体类
/// </summary>
[Table("game_ranking")]
public class GameRanking
{
    [Key]
    [Column("rank_id")]
    public long RankId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("pack_in_game")]
    public int? PeakPlayers { get; set; }

    [Column("last_week_rank")]
    public int? LastWeekRank { get; set; }

    [Column("current_rank")]
    public int? CurrentRank { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }
}

/// <summary>
/// 游戏题材实体类
/// </summary>
[Table("genres")]
public class Genre
{
    [Key]
    [Column("genre_id")]
    public int GenreId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
}

/// <summary>
/// 游戏与题材关联实体类
/// </summary>
[Table("game_genres")]
public class GameGenre
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("genre_id")]
    public int GenreId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("GenreId")]
    public virtual Genre? Genre { get; set; }
}

/// <summary>
/// 开发商实体类
/// </summary>
[Table("developers")]
public class Developer
{
    [Key]
    [Column("developers_id")]
    public int DeveloperId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GameDeveloper> GameDevelopers { get; set; } = new List<GameDeveloper>();
}

/// <summary>
/// 游戏与开发商关联实体类
/// </summary>
[Table("game_developers")]
public class GameDeveloper
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("developers_id")]
    public int DeveloperId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("DeveloperId")]
    public virtual Developer? Developer { get; set; }
}

/// <summary>
/// 发行商实体类
/// </summary>
[Table("publishers")]
public class Publisher
{
    [Key]
    [Column("publishers_id")]
    public int PublisherId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GamePublisher> GamePublishers { get; set; } = new List<GamePublisher>();
}

/// <summary>
/// 游戏与发行商关联实体类
/// </summary>
[Table("game_publishers")]
public class GamePublisher
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("publishers_id")]
    public int PublisherId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("PublisherId")]
    public virtual Publisher? Publisher { get; set; }
}

/// <summary>
/// 分类实体类
/// </summary>
[Table("categories")]
public class Category
{
    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public virtual ICollection<GameCategory> GameCategories { get; set; } = new List<GameCategory>();
}

/// <summary>
/// 游戏与分类关联实体类
/// </summary>
[Table("game_categories")]
public class GameCategory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }
}

/// <summary>
/// 语言实体类
/// </summary>
[Table("languages")]
public class Language
{
    [Key]
    [Column("language_id")]
    public int LanguageId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("language_name")]
    public string LanguageName { get; set; } = string.Empty;

    public virtual ICollection<GameLanguage> GameLanguages { get; set; } = new List<GameLanguage>();
}

/// <summary>
/// 游戏与语言关联实体类
/// </summary>
[Table("game_languages")]
public class GameLanguage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("language_id")]
    public int LanguageId { get; set; }

    [Column("notes")]
    [MaxLength(256)]
    public string? Notes { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }

    [ForeignKey("LanguageId")]
    public virtual Language? Language { get; set; }
}

