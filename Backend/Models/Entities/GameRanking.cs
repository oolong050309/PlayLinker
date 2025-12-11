using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏排行榜表
/// </summary>
[Table("game_ranking")]
[Index("GameId", Name = "game_id", IsUnique = true)]
[Index("CurrentRank", Name = "idx_current_rank")]
public partial class GameRanking
{
    [Key]
    [Column("rank_id")]
    public long RankId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    /// <summary>
    /// 峰值在线人数
    /// </summary>
    [Column("pack_in_game")]
    public int? PeakPlayers { get; set; }

    /// <summary>
    /// 上周排名
    /// </summary>
    [Column("last_week_rank")]
    public int? LastWeekRank { get; set; }

    /// <summary>
    /// 排名
    /// </summary>
    [Column("current_rank")]
    public int? CurrentRank { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GameRanking")]
    public virtual Game Game { get; set; } = null!;
}
