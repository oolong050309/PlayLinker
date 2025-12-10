using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 推荐反馈表-AI算法优化反馈记录
/// </summary>
[Table("recommendation_feedback")]
[Index("FeedbackTime", Name = "idx_feedback_time")]
[Index("RecommendationId", Name = "recommendation_id", IsUnique = true)]
[Index("UserId", Name = "user_id")]
public partial class RecommendationFeedback
{
    [Key]
    [Column("feedback_id")]
    public int FeedbackId { get; set; }

    [Column("recommendation_id")]
    public int RecommendationId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// 1喜欢/2不喜欢
    /// </summary>
    [Column("feedback_result")]
    public int FeedbackResult { get; set; }

    [Column("feedback_time", TypeName = "datetime")]
    public DateTime FeedbackTime { get; set; }

    [Column("remark", TypeName = "text")]
    public string? Remark { get; set; }

    [ForeignKey("RecommendationId")]
    [InverseProperty("RecommendationFeedback")]
    public virtual Recommendation Recommendation { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("RecommendationFeedbacks")]
    public virtual User User { get; set; } = null!;
}
