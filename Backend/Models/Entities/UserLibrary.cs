using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户游戏库统计实体类
/// </summary>
[Table("user_game_library")]
public class UserGameLibrary
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("total_games_owned")]
    public int TotalGamesOwned { get; set; }

    [Column("games_played")]
    public int GamesPlayed { get; set; }

    [Column("total_playtime_minutes")]
    public int TotalPlaytimeMinutes { get; set; }

    [Column("total_achievements")]
    public int? TotalAchievements { get; set; }

    [Column("unlocked_achievements")]
    public int? UnlockedAchievements { get; set; }

    [Column("recently_played_count")]
    public int RecentlyPlayedCount { get; set; }

    [Column("recent_playtime_minutes")]
    public int RecentPlaytimeMinutes { get; set; }
}

/// <summary>
/// 平台实体类
/// </summary>
[Table("platforms")]
public class Platform
{
    [Key]
    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("platform_name")]
    public string PlatformName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(2048)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [Column("status")]
    public bool Status { get; set; } = true;
}

/// <summary>
/// 用户平台游戏记录实体类
/// </summary>
[Table("user_platform_library")]
public class UserPlatformLibrary
{
    [Column("platform_user_id")]
    [MaxLength(128)]
    public string PlatformUserId { get; set; } = string.Empty;

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("playtime_minutes")]
    public int PlaytimeMinutes { get; set; }

    [Column("last_played")]
    public DateTime? LastPlayed { get; set; }

    [Column("achievements_total")]
    public int? AchievementsTotal { get; set; }

    [Column("achievements_unlocked")]
    public int? AchievementsUnlocked { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }
}

/// <summary>
/// 玩家平台账号资料实体类
/// </summary>
[Table("player_platform")]
public class PlayerPlatform
{
    [Column("platform_user_id")]
    [MaxLength(128)]
    public string PlatformUserId { get; set; } = string.Empty;

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("profile_name")]
    public string ProfileName { get; set; } = string.Empty;

    [MaxLength(2048)]
    [Column("profile_url")]
    public string? ProfileUrl { get; set; }

    [Column("account_created")]
    public DateTime? AccountCreated { get; set; }

    [MaxLength(50)]
    [Column("country")]
    public string? Country { get; set; }
}

/// <summary>
/// 用户平台绑定实体类
/// </summary>
[Table("user_platform_binding")]
public class UserPlatformBinding
{
    [Key]
    [Column("binding_id")]
    public int BindingId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("platform_user_id")]
    public string PlatformUserId { get; set; } = string.Empty;

    [MaxLength(128)]
    [Column("access_token")]
    public string? AccessToken { get; set; }

    [MaxLength(128)]
    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    [Column("binding_status")]
    public bool BindingStatus { get; set; } = true;

    [Column("binding_time")]
    public DateTime BindingTime { get; set; } = DateTime.UtcNow;

    [Column("last_sync_time")]
    public DateTime? LastSyncTime { get; set; }

    [Column("expire_time")]
    public DateTime ExpireTime { get; set; }
}

