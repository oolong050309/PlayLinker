using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;
using System.Text.Json;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<ReportsController> _logger;
    private readonly ReportGenerationService _reportService;

    public ReportsController(PlayLinkerDbContext context, ILogger<ReportsController> logger, ReportGenerationService reportService)
    {
        _context = context;
        _logger = logger;
        _reportService = reportService;
    }

    /// <summary>
    /// 获取报表模板列表
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplatesResponse>), 200)]
    public ActionResult<ApiResponse<ReportTemplatesResponse>> GetTemplates()
    {
        try
        {
            // 模拟报表模板数据（实际项目中应从数据库读取）
            var templates = new List<ReportTemplateDto>
            {
                new ReportTemplateDto
                {
                    TemplateId = 1,
                    TemplateName = "月度游戏报告",
                    Description = "包含游戏时长、成就、消费等统计",
                    Category = "gaming",
                    SupportedFormats = new List<string> { "pdf", "excel", "html" },
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter { Name = "month", Type = "string", Required = true },
                        new TemplateParameter { Name = "includePlatforms", Type = "array", Required = false }
                    }
                },
                new ReportTemplateDto
                {
                    TemplateId = 2,
                    TemplateName = "年度总结报告",
                    Description = "年度游戏数据全面分析",
                    Category = "gaming",
                    SupportedFormats = new List<string> { "pdf", "html" },
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter { Name = "year", Type = "int", Required = true }
                    }
                },
                new ReportTemplateDto
                {
                    TemplateId = 3,
                    TemplateName = "游戏库存报告",
                    Description = "游戏收藏、安装、存档统计",
                    Category = "library",
                    SupportedFormats = new List<string> { "pdf", "excel" },
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter { Name = "includeUninstalled", Type = "boolean", Required = false }
                    }
                }
            };

            var response = new ReportTemplatesResponse
            {
                Templates = templates,
                TotalCount = templates.Count
            };

            return Ok(ApiResponse<ReportTemplatesResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report templates");
            return StatusCode(500, ApiResponse<ReportTemplatesResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取报表模板失败"));
        }
    }

    /// <summary>
    /// 生成报表
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<GenerateReportResponse>), 202)]
    public async Task<ActionResult<ApiResponse<GenerateReportResponse>>> GenerateReport([FromBody] GenerateReportRequest request)
    {
        try
        {
            // 生成报表ID
            var reportId = $"rpt_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 模拟报表生成（实际项目中应创建后台任务）
            var response = new GenerateReportResponse
            {
                ReportId = reportId,
                TemplateId = request.TemplateId,
                Status = "generating",
                EstimatedTime = 10, // 预计10秒
                QueuePosition = new Random().Next(1, 5),
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Report generation started: {ReportId}, Template: {TemplateId}", reportId, request.TemplateId);

            return StatusCode(202, ApiResponse<GenerateReportResponse>.SuccessResponse(response, "报表生成任务已创建"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            return StatusCode(500, ApiResponse<GenerateReportResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "生成报表失败"));
        }
    }

    /// <summary>
    /// 获取报表历史
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ReportListDto>>), 200)]
    public ActionResult<ApiResponse<PaginatedResponse<ReportListDto>>> GetReports(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);
            page = Math.Max(page, 1);

            // 模拟报表历史数据
            var allReports = new List<ReportListDto>
            {
                new ReportListDto
                {
                    ReportId = "rpt_20241127_100000",
                    TemplateId = 1,
                    TemplateName = "月度游戏报告",
                    Status = "completed",
                    Format = "pdf",
                    GeneratedAt = DateTime.UtcNow.AddDays(-1),
                    FileSizeMB = 2.5m,
                    DownloadUrl = "/api/v1/reports/rpt_20241127_100000/download"
                },
                new ReportListDto
                {
                    ReportId = "rpt_20241126_150000",
                    TemplateId = 2,
                    TemplateName = "年度总结报告",
                    Status = "completed",
                    Format = "html",
                    GeneratedAt = DateTime.UtcNow.AddDays(-2),
                    FileSizeMB = 1.8m,
                    DownloadUrl = "/api/v1/reports/rpt_20241126_150000/download"
                }
            };

            // 按状态过滤
            if (!string.IsNullOrEmpty(status))
            {
                allReports = allReports.Where(r => r.Status == status).ToList();
            }

            var total = allReports.Count;
            var items = allReports
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new PaginatedResponse<ReportListDto>
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<PaginatedResponse<ReportListDto>>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reports");
            return StatusCode(500, ApiResponse<PaginatedResponse<ReportListDto>>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取报表历史失败"));
        }
    }

    /// <summary>
    /// 获取报表详情
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ReportDetailDto>), 200)]
    public ActionResult<ApiResponse<ReportDetailDto>> GetReportDetail(string id)
    {
        try
        {
            // 模拟报表详情
            var report = new ReportDetailDto
            {
                ReportId = id,
                TemplateId = 1,
                TemplateName = "月度游戏报告",
                UserId = 1001,
                Status = "completed",
                Format = "pdf",
                Parameters = new Dictionary<string, object>
                {
                    { "startDate", "2024-11-01" },
                    { "endDate", "2024-11-30" }
                },
                GeneratedAt = DateTime.UtcNow.AddHours(-2),
                FileSizeMB = 2.5m,
                OutputPath = $"/reports/{id}.pdf",
                DownloadUrl = $"/api/v1/reports/{id}/download",
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            return Ok(ApiResponse<ReportDetailDto>.SuccessResponse(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report detail for {ReportId}", id);
            return StatusCode(500, ApiResponse<ReportDetailDto>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "获取报表详情失败"));
        }
    }

    /// <summary>
    /// 下载报表
    /// </summary>
    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileResult), 200)]
    public async Task<ActionResult> DownloadReport(string id, [FromQuery] string format = "html")
    {
        try
        {
            // 假设当前用户ID为1001
            int userId = 1001;
            
            // 解析报表ID中的日期信息（格式：rpt_yyyyMMdd_HHmmss）
            var startDate = DateTime.UtcNow.AddMonths(-1).Date;
            var endDate = DateTime.UtcNow.Date;

            format = format.ToLower();

            switch (format)
            {
                case "html":
                    var htmlContent = await _reportService.GenerateMonthlyReportHtml(userId, startDate, endDate);
                    var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);
                    var htmlFileName = $"monthly_report_{DateTime.UtcNow:yyyyMM}.html";
                    _logger.LogInformation("HTML report downloaded: {ReportId}", id);
                    return File(htmlBytes, "text/html", htmlFileName);

                case "excel":
                case "csv":
                    var csvContent = await _reportService.GenerateMonthlyReportCsv(userId, startDate, endDate);
                    var csvFileName = $"monthly_report_{DateTime.UtcNow:yyyyMM}.csv";
                    _logger.LogInformation("CSV report downloaded: {ReportId}", id);
                    return File(csvContent, "text/csv", csvFileName);

                case "pdf":
                    var pdfContent = await _reportService.GenerateMonthlyReportPdf(userId, startDate, endDate);
                    var pdfFileName = $"monthly_report_{DateTime.UtcNow:yyyyMM}.pdf";
                    _logger.LogInformation("PDF report downloaded: {ReportId}", id);
                    return File(pdfContent, "application/pdf", pdfFileName);

                default:
                    return BadRequest("Unsupported format. Use: html, csv, or pdf");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading report {ReportId}", id);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// 删除报表
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteReportResponse>), 200)]
    public ActionResult<ApiResponse<DeleteReportResponse>> DeleteReport(string id)
    {
        try
        {
            var response = new DeleteReportResponse
            {
                ReportId = id,
                DeletedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Report deleted: {ReportId}", id);

            return Ok(ApiResponse<DeleteReportResponse>.SuccessResponse(response, "报表已删除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report {ReportId}", id);
            return StatusCode(500, ApiResponse<DeleteReportResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "删除报表失败"));
        }
    }
}
