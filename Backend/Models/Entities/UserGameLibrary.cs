using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户统一游戏库统计
/// </summary>
[Table("user_game_library")]
public partial class UserGameLibrary
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

    [ForeignKey("UserId")]
    [InverseProperty("UserGameLibrary")]
    public virtual User User { get; set; } = null!;
}
