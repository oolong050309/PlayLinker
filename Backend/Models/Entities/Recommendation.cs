using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// AI推荐结果表-个性化推荐记录
/// </summary>
[Table("recommendation")]
[Index("GameId", Name = "game_id")]
[Index("ExpireTime", Name = "idx_expire_time")]
[Index("UserId", Name = "idx_user_id")]
public partial class Recommendation
{
    [Key]
    [Column("recommendation_id")]
    public int RecommendationId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    /// <summary>
    /// 推荐类型
    /// </summary>
    [Column("recommendation_type", TypeName = "enum('game','discount','similar','trending')")]
    public string RecommendationType { get; set; } = null!;

    [Column("recommendation_strategy", TypeName = "enum('collaborative','content_based','hybrid','popular')")]
    public string RecommendationStrategy { get; set; } = null!;

    /// <summary>
    /// AI生成解释短文
    /// </summary>
    [Column("reason", TypeName = "text")]
    public string Reason { get; set; } = null!;

    [Column("created_at", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 默认7天
    /// </summary>
    [Column("expire_time", TypeName = "datetime")]
    public DateTime ExpireTime { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("Recommendations")]
    public virtual Game Game { get; set; } = null!;

    [InverseProperty("Recommendation")]
    public virtual RecommendationFeedback? RecommendationFeedback { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Recommendations")]
    public virtual User User { get; set; } = null!;
}
