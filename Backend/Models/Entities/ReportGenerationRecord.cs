using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 报表生成历史
/// </summary>
[Table("report_generation_record")]
[Index("GeneratedAt", Name = "idx_generated_at")]
[Index("UserId", Name = "idx_user_id")]
[Index("TemplateId", Name = "template_id")]
public partial class ReportGenerationRecord
{
    [Key]
    [Column("report_id")]
    [StringLength(20)]
    public string ReportId { get; set; } = null!;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("template_id")]
    public int TemplateId { get; set; }

    [Column("generated_at", TypeName = "datetime")]
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// 0代表未生成，1代表生成
    /// </summary>
    [Column("status")]
    public bool Status { get; set; }

    [Column("output_path")]
    [StringLength(2048)]
    public string? OutputPath { get; set; }

    [ForeignKey("TemplateId")]
    [InverseProperty("ReportGenerationRecords")]
    public virtual ReportTemplate Template { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ReportGenerationRecords")]
    public virtual User User { get; set; } = null!;
}
