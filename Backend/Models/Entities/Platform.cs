using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏平台表
/// </summary>
[Table("platforms")]
[Index("PlatformName", Name = "idx_platform_name", IsUnique = true)]
public partial class Platform
{
    [Key]
    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("platform_name")]
    [StringLength(128)]
    public string PlatformName { get; set; } = null!;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    [Column("logo_url")]
    [StringLength(2048)]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// 0不启用,1启用
    /// </summary>
    [Required]
    [Column("status")]
    public bool? Status { get; set; }

    [InverseProperty("Platform")]
    public virtual ICollection<GamePlatform> GamePlatforms { get; set; } = new List<GamePlatform>();

    [InverseProperty("Platform")]
    public virtual ICollection<LocalGameInstall> LocalGameInstalls { get; set; } = new List<LocalGameInstall>();

    [InverseProperty("Platform")]
    public virtual ICollection<PlayerPlatform> PlayerPlatforms { get; set; } = new List<PlayerPlatform>();

    [InverseProperty("Platform")]
    public virtual ICollection<PriceAlertSubscription> PriceAlertSubscriptions { get; set; } = new List<PriceAlertSubscription>();

    [InverseProperty("Platform")]
    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    [InverseProperty("Platform")]
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    [InverseProperty("Platform")]
    public virtual ICollection<UserPlatformBinding> UserPlatformBindings { get; set; } = new List<UserPlatformBinding>();
}
