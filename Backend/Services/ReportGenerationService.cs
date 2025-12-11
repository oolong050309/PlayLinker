using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PlayLinker.Services;

public class ReportGenerationService
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<ReportGenerationService> _logger;

    public ReportGenerationService(PlayLinkerDbContext context, ILogger<ReportGenerationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 生成HTML格式的月度游戏报告
    /// </summary>
    public async Task<string> GenerateMonthlyReportHtml(int userId, DateTime startDate, DateTime endDate)
    {
        // 查询数据
        var gameRecords = await _context.UserPlatformGameRecords
            .Include(r => r.Game)
            .Include(r => r.Platform)
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var achievements = await _context.UserAchievements
            .Include(a => a.Achievement)
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gameRecords.Sum(r => r.PlaytimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gameRecords.Count;
        var totalAchievements = achievements.Count;

        // 游戏排行
        var topGames = gameRecords
            .OrderByDescending(r => r.PlaytimeMinutes)
            .Take(10)
            .ToList();

        // 生成HTML
        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>月度游戏报告 - {startDate:yyyy年MM月}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Microsoft YaHei', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            background: #f5f5f5;
            padding: 20px;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 40px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            padding-bottom: 30px;
            border-bottom: 3px solid #4CAF50;
            margin-bottom: 30px;
        }}
        .header h1 {{
            color: #4CAF50;
            font-size: 2.5em;
            margin-bottom: 10px;
        }}
        .header .period {{
            color: #666;
            font-size: 1.2em;
        }}
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }}
        .stat-card {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px;
            border-radius: 10px;
            text-align: center;
        }}
        .stat-card.green {{
            background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%);
        }}
        .stat-card.blue {{
            background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%);
        }}
        .stat-card.orange {{
            background: linear-gradient(135deg, #FF9800 0%, #F57C00 100%);
        }}
        .stat-card .value {{
            font-size: 2.5em;
            font-weight: bold;
            margin-bottom: 5px;
        }}
        .stat-card .label {{
            font-size: 1em;
            opacity: 0.9;
        }}
        .section {{
            margin-bottom: 40px;
        }}
        .section h2 {{
            color: #4CAF50;
            font-size: 1.8em;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #e0e0e0;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
        }}
        th {{
            background: #4CAF50;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
        }}
        td {{
            padding: 12px 15px;
            border-bottom: 1px solid #e0e0e0;
        }}
        tr:hover {{
            background: #f5f5f5;
        }}
        .rank {{
            font-weight: bold;
            color: #4CAF50;
            font-size: 1.2em;
        }}
        .footer {{
            text-align: center;
            margin-top: 50px;
            padding-top: 20px;
            border-top: 2px solid #e0e0e0;
            color: #666;
        }}
        @media print {{
            body {{
                background: white;
                padding: 0;
            }}
            .container {{
                box-shadow: none;
                padding: 20px;
            }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎮 月度游戏报告</h1>
            <div class='period'>{startDate:yyyy年MM月dd日} - {endDate:yyyy年MM月dd日}</div>
        </div>

        <div class='stats-grid'>
            <div class='stat-card green'>
                <div class='value'>{totalHours}</div>
                <div class='label'>总游玩时长（小时）</div>
            </div>
            <div class='stat-card blue'>
                <div class='value'>{totalGames}</div>
                <div class='label'>游戏数量</div>
            </div>
            <div class='stat-card orange'>
                <div class='value'>{totalAchievements}</div>
                <div class='label'>获得成就</div>
            </div>
            <div class='stat-card'>
                <div class='value'>{(totalGames > 0 ? Math.Round(totalHours / totalGames, 1) : 0)}</div>
                <div class='label'>平均时长/游戏（小时）</div>
            </div>
        </div>

        <div class='section'>
            <h2>🏆 游戏排行榜 TOP 10</h2>
            <table>
                <thead>
                    <tr>
                        <th style='width: 60px;'>排名</th>
                        <th>游戏名称</th>
                        <th>平台</th>
                        <th style='width: 150px;'>游玩时长</th>
                        <th style='width: 100px;'>占比</th>
                    </tr>
                </thead>
                <tbody>
                    {string.Join("", topGames.Select((r, i) => $@"
                    <tr>
                        <td class='rank'>#{i + 1}</td>
                        <td>{r.Game?.Name ?? "Unknown"}</td>
                        <td>{r.Platform?.PlatformName ?? "Unknown"}</td>
                        <td>{Math.Round(r.PlaytimeMinutes / 60.0, 1)} 小时</td>
                        <td>{(totalMinutes > 0 ? Math.Round((decimal)r.PlaytimeMinutes / totalMinutes * 100, 1) : 0)}%</td>
                    </tr>"))}
                </tbody>
            </table>
        </div>

        <div class='section'>
            <h2>🎯 成就统计</h2>
            <p style='font-size: 1.1em; color: #666;'>
                本月共解锁 <strong style='color: #4CAF50; font-size: 1.3em;'>{totalAchievements}</strong> 个成就
            </p>
        </div>

        <div class='footer'>
            <p>报告生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
            <p style='margin-top: 10px;'>PlayLinker 游戏管理平台</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    /// <summary>
    /// 生成CSV格式的月度游戏报告（可用Excel打开）
    /// </summary>
    public async Task<byte[]> GenerateMonthlyReportCsv(int userId, DateTime startDate, DateTime endDate)
    {
        // 查询数据
        var gameRecords = await _context.UserPlatformGameRecords
            .Include(r => r.Game)
            .Include(r => r.Platform)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PlaytimeMinutes)
            .ToListAsync();

        var csv = new StringBuilder();
        
        // 添加BOM以支持Excel正确显示中文
        csv.Append("\uFEFF");
        
        // 标题
        csv.AppendLine($"月度游戏报告,{startDate:yyyy年MM月dd日} - {endDate:yyyy年MM月dd日}");
        csv.AppendLine();
        
        // 总体统计
        csv.AppendLine("总体统计");
        csv.AppendLine("指标,数值");
        csv.AppendLine($"总游玩时长（小时）,{Math.Round(gameRecords.Sum(r => r.PlaytimeMinutes) / 60.0, 1)}");
        csv.AppendLine($"游戏数量,{gameRecords.Count}");
        csv.AppendLine();
        
        // 游戏详情
        csv.AppendLine("游戏详情");
        csv.AppendLine("排名,游戏名称,平台,游玩时长（小时）,游玩时长（分钟）");
        
        int rank = 1;
        foreach (var record in gameRecords)
        {
            csv.AppendLine($"{rank},{record.Game?.Name ?? "Unknown"},{record.Platform?.PlatformName ?? "Unknown"},{Math.Round(record.PlaytimeMinutes / 60.0, 1)},{record.PlaytimeMinutes}");
            rank++;
        }
        
        csv.AppendLine();
        csv.AppendLine($"报告生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// 生成PDF格式的月度游戏报告
    /// </summary>
    public async Task<byte[]> GenerateMonthlyReportPdf(int userId, DateTime startDate, DateTime endDate)
    {
        // 设置QuestPDF许可证（社区版免费）
        QuestPDF.Settings.License = LicenseType.Community;

        // 查询数据
        var gameRecords = await _context.UserPlatformGameRecords
            .Include(r => r.Game)
            .Include(r => r.Platform)
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gameRecords.Sum(r => r.PlaytimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gameRecords.Count;
        var totalAchievements = achievements.Count;

        // 游戏排行
        var topGames = gameRecords
            .OrderByDescending(r => r.PlaytimeMinutes)
            .Take(10)
            .ToList();

        // 生成PDF
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                // 页面设置
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                // 页眉
                page.Header()
                    .BorderBottom(1)
                    .BorderColor(Colors.Green.Medium)
                    .PaddingBottom(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Monthly Game Report")
                                .FontSize(24).SemiBold().FontColor(Colors.Green.Medium);
                            col.Item().Text($"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}")
                                .FontSize(12).FontColor(Colors.Grey.Darken1);
                        });
                    });

                // 内容
                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(15);

                        // 总体统计卡片
                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            // 总游玩时长
                            row.RelativeItem().Background(Colors.Green.Lighten3)
                                .Padding(15).Column(col =>
                                {
                                    col.Item().Text(totalHours.ToString())
                                        .FontSize(32).SemiBold().FontColor(Colors.Green.Darken2);
                                    col.Item().Text("Total Hours")
                                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                                });

                            // 游戏数量
                            row.RelativeItem().Background(Colors.Blue.Lighten3)
                                .Padding(15).Column(col =>
                                {
                                    col.Item().Text(totalGames.ToString())
                                        .FontSize(32).SemiBold().FontColor(Colors.Blue.Darken2);
                                    col.Item().Text("Games")
                                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                                });

                            // 获得成就
                            row.RelativeItem().Background(Colors.Orange.Lighten3)
                                .Padding(15).Column(col =>
                                {
                                    col.Item().Text(totalAchievements.ToString())
                                        .FontSize(32).SemiBold().FontColor(Colors.Orange.Darken2);
                                    col.Item().Text("Achievements")
                                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                                });
                        });

                        // 游戏排行榜标题
                        column.Item().PaddingTop(20).Text("Top 10 Games")
                            .FontSize(18).SemiBold().FontColor(Colors.Green.Medium);

                        // 游戏排行榜表格
                        column.Item().Table(table =>
                        {
                            // 定义列宽
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);   // 排名
                                columns.RelativeColumn(3);    // 游戏名称
                                columns.RelativeColumn(2);    // 平台
                                columns.ConstantColumn(80);   // 时长
                                columns.ConstantColumn(60);   // 占比
                            });

                            // 表头
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Green.Medium)
                                    .Padding(8).Text("Rank").FontColor(Colors.White).SemiBold();
                                header.Cell().Background(Colors.Green.Medium)
                                    .Padding(8).Text("Game").FontColor(Colors.White).SemiBold();
                                header.Cell().Background(Colors.Green.Medium)
                                    .Padding(8).Text("Platform").FontColor(Colors.White).SemiBold();
                                header.Cell().Background(Colors.Green.Medium)
                                    .Padding(8).Text("Hours").FontColor(Colors.White).SemiBold();
                                header.Cell().Background(Colors.Green.Medium)
                                    .Padding(8).Text("Percent").FontColor(Colors.White).SemiBold();
                            });

                            // 数据行
                            int rank = 1;
                            foreach (var game in topGames)
                            {
                                var bgColor = rank % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bgColor).Padding(8)
                                    .Text($"#{rank}").FontColor(Colors.Green.Medium).SemiBold();
                                table.Cell().Background(bgColor).Padding(8)
                                    .Text(game.Game?.Name ?? "Unknown");
                                table.Cell().Background(bgColor).Padding(8)
                                    .Text(game.Platform?.PlatformName ?? "Unknown");
                                table.Cell().Background(bgColor).Padding(8)
                                    .Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h");
                                table.Cell().Background(bgColor).Padding(8)
                                    .Text($"{(totalMinutes > 0 ? Math.Round((decimal)game.PlaytimeMinutes / totalMinutes * 100, 1) : 0)}%");

                                rank++;
                            }
                        });

                        // 成就统计
                        column.Item().PaddingTop(20).Background(Colors.Grey.Lighten4)
                            .Padding(15).Row(row =>
                            {
                                row.RelativeItem().Text("Achievement Stats").FontSize(14).SemiBold();
                                row.ConstantItem(150).Text($"Total: {totalAchievements}")
                                    .FontSize(14).FontColor(Colors.Green.Medium).SemiBold();
                            });
                    });

                // 页脚
                page.Footer()
                    .BorderTop(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingTop(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                        row.ConstantItem(100).AlignRight().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        }).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
            });
        }).GeneratePdf();

        return pdfBytes;
    }
}
