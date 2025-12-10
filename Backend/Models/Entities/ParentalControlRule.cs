using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 家长监管规则表
/// </summary>
[Table("parental_control_rule")]
[Index("ChildUserId", Name = "idx_child_user")]
public partial class ParentalControlRule
{
    [Key]
    [Column("rule_id")]
    public long RuleId { get; set; }

    [Column("child_user_id")]
    public int ChildUserId { get; set; }

    [Column("rule_type", TypeName = "enum('playtime_daily_limit','playtime_curfew','spending_limit','game_restriction','age_restriction')")]
    public string RuleType { get; set; } = null!;

    [Column("rule_value", TypeName = "json")]
    public string RuleValue { get; set; } = null!;

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ChildUserId")]
    [InverseProperty("ParentalControlRules")]
    public virtual User ChildUser { get; set; } = null!;

    [InverseProperty("Rule")]
    public virtual ICollection<ParentalAlertLog> ParentalAlertLogs { get; set; } = new List<ParentalAlertLog>();
}
