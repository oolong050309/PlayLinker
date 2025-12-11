using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

// 报表模板信息
public class ReportTemplateDto
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> SupportedFormats { get; set; } = new();
    public List<TemplateParameter> Parameters { get; set; } = new();
}

// 模板参数
public class TemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
}

// 报表模板列表响应
public class ReportTemplatesResponse
{
    public List<ReportTemplateDto> Templates { get; set; } = new();
    public int TotalCount { get; set; }
}

// 生成报表请求
public class GenerateReportRequest
{
    [Required]
    public int TemplateId { get; set; }
    
    [Required]
    public string ReportType { get; set; } = string.Empty;
    
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [Required]
    public string Format { get; set; } = "pdf"; // pdf, excel, html
}

// 生成报表响应
public class GenerateReportResponse
{
    public string ReportId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string Status { get; set; } = string.Empty; // generating, completed, failed
    public int EstimatedTime { get; set; }
    public int QueuePosition { get; set; }
    public DateTime CreatedAt { get; set; }
}

// 报表列表项
public class ReportListDto
{
    public string ReportId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public decimal FileSizeMB { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

// 报表详情
public class ReportDetailDto
{
    public string ReportId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public decimal FileSizeMB { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

// 删除报表响应
public class DeleteReportResponse
{
    public string ReportId { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
}
