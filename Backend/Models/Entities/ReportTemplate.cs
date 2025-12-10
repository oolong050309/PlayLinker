using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 报表模板
/// </summary>
[Table("report_template")]
public partial class ReportTemplate
{
    [Key]
    [Column("template_id")]
    public int TemplateId { get; set; }

    [Column("template_name")]
    [StringLength(128)]
    public string TemplateName { get; set; } = null!;

    [Column("description", TypeName = "text")]
    public string Description { get; set; } = null!;

    [Column("created_at", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Template")]
    public virtual ICollection<ReportGenerationRecord> ReportGenerationRecords { get; set; } = new List<ReportGenerationRecord>();
}
