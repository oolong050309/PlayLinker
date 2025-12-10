using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏主表
/// </summary>
[Table("games")]
[Index("Name", Name = "idx_name", IsUnique = true)]
[Index("ReleaseDate", Name = "idx_release_date")]
public partial class Game
{
    [Key]
    [Column("game_id")]
    public long GameId { get; set; }

    [Column("name")]
    [StringLength(128)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// 1代表免费，0代表不免费
    /// </summary>
    [Column("is_free")]
    public bool IsFree { get; set; }

    [Column("require_age")]
    public byte? RequireAge { get; set; }

    [Column("short_description", TypeName = "text")]
    public string? ShortDescription { get; set; }

    [Column("detailed_description", TypeName = "text")]
    public string? DetailedDescription { get; set; }

    [Column("header_image")]
    [StringLength(2048)]
    public string HeaderImage { get; set; } = null!;

    [Column("capsule_image")]
    [StringLength(2048)]
    public string CapsuleImage { get; set; } = null!;

    [Column("background")]
    [StringLength(2048)]
    public string Background { get; set; } = null!;

    [Column("pc_recommended", TypeName = "text")]
    public string? PcRecommended { get; set; }

    [Column("pc_minimum", TypeName = "text")]
    public string? PcMinimum { get; set; }

    [Column("mac_recommended", TypeName = "text")]
    public string? MacRecommended { get; set; }

    [Column("mac_minimum", TypeName = "text")]
    public string? MacMinimum { get; set; }

    [Column("linux_recommended", TypeName = "text")]
    public string? LinuxRecommended { get; set; }

    [Column("linux_minimum", TypeName = "text")]
    public string? LinuxMinimum { get; set; }

    [Column("windows")]
    public bool Windows { get; set; }

    [Column("mac")]
    public bool Mac { get; set; }

    [Column("linux")]
    public bool Linux { get; set; }

    [Column("release_date", TypeName = "datetime")]
    public DateTime ReleaseDate { get; set; }

    [Column("review_score")]
    public int ReviewScore { get; set; }

    [Column("review_score_desc")]
    [StringLength(20)]
    public string ReviewScoreDesc { get; set; } = null!;

    [Column("num_reviews")]
    public int NumReviews { get; set; }

    [Column("total_positive")]
    public int TotalPositive { get; set; }

    [InverseProperty("Game")]
    public virtual ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();

    [InverseProperty("Game")]
    public virtual ICollection<CloudSaveBackup> CloudSaveBackups { get; set; } = new List<CloudSaveBackup>();

    [InverseProperty("Game")]
    public virtual ICollection<GameCategory> GameCategories { get; set; } = new List<GameCategory>();

    [InverseProperty("Game")]
    public virtual ICollection<GameDeveloper> GameDevelopers { get; set; } = new List<GameDeveloper>();

    [InverseProperty("Game")]
    public virtual ICollection<GameExternalLink> GameExternalLinks { get; set; } = new List<GameExternalLink>();

    [InverseProperty("Game")]
    public virtual ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();

    [InverseProperty("Game")]
    public virtual ICollection<GameLanguage> GameLanguages { get; set; } = new List<GameLanguage>();

    [InverseProperty("Game")]
    public virtual ICollection<GameNews> GameNews { get; set; } = new List<GameNews>();

    [InverseProperty("Game")]
    public virtual ICollection<GamePlatform> GamePlatforms { get; set; } = new List<GamePlatform>();

    [InverseProperty("Game")]
    public virtual ICollection<GamePublisher> GamePublishers { get; set; } = new List<GamePublisher>();

    [InverseProperty("Game")]
    public virtual GameRanking? GameRanking { get; set; }

    [InverseProperty("Game")]
    public virtual ICollection<LocalGameInstall> LocalGameInstalls { get; set; } = new List<LocalGameInstall>();

    [InverseProperty("Game")]
    public virtual ICollection<PriceAlertSubscription> PriceAlertSubscriptions { get; set; } = new List<PriceAlertSubscription>();

    [InverseProperty("Game")]
    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    [InverseProperty("Game")]
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

    [InverseProperty("Game")]
    public virtual ICollection<UserPlatformLibrary> UserPlatformLibraries { get; set; } = new List<UserPlatformLibrary>();
}
