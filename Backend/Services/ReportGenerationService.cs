using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.Entities;
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
    /// 生成空报告 HTML（当没有数据时）
    /// </summary>
    private string GenerateEmptyReportHtml(DateTime startDate, DateTime endDate, string message)
    {
        return $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>游戏报告</title>
    <style>
        body {{ font-family: 'Microsoft YaHei', Arial, sans-serif; background: #f5f5f5; padding: 40px; }}
        .container {{ max-width: 800px; margin: 0 auto; background: white; padding: 60px; border-radius: 10px; text-align: center; }}
        h1 {{ color: #666; margin-bottom: 20px; }}
        p {{ color: #999; font-size: 1.2em; }}
        .period {{ color: #aaa; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>📊 游戏报告</h1>
        <p>{message}</p>
        <p class='period'>{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}</p>
        <p style='margin-top: 40px; color: #ccc;'>请先绑定 Steam 或其他游戏平台账号</p>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// 生成HTML格式的月度游戏报告
    /// </summary>
    public async Task<string> GenerateMonthlyReportHtml(int userId, DateTime startDate, DateTime endDate)
    {
        // 获取用户绑定的平台用户ID
        var platformUserIds = await _context.UserPlatformBindings
            .Where(b => b.UserId == userId && b.BindingStatus == true)
            .Select(b => b.PlatformUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToListAsync();

        if (!platformUserIds.Any())
        {
            return GenerateEmptyReportHtml(startDate, endDate, "未绑定任何游戏平台");
        }

        // 从 user_playtime_history 表计算月度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算月度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            // 计算增量：最后一天的总时长 - 第一天的总时长
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            
            // 如果只有一条记录，说明是本月新增的游戏，使用该记录的时长
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease);
                }
            }
        }

        // 查询月度成就（如果有 UnlockTime 字段）
        var achievements = await _context.UserAchievements
            .Include(a => a.Achievement)
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();
        
        // 尝试筛选月度成就（如果有时间字段）
        var monthlyAchievements = achievements; // 暂时使用全部成就，因为表结构可能没有解锁时间

        // 计算统计数据
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var totalAchievements = monthlyAchievements.Count;

        // 如果没有任何游戏数据
        if (totalGames == 0)
        {
            return GenerateEmptyReportHtml(startDate, endDate, "本月暂无游戏时长数据");
        }

        // 游戏排行
        var topGames = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .Take(10)
            .Select(kv => new
            {
                GameName = kv.Value.gameName,
                PlatformName = kv.Value.platformName,
                PlaytimeMinutes = kv.Value.playtimeMinutes
            })
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
                        <td>{r.GameName}</td>
                        <td>{r.PlatformName}</td>
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
        // 从 user_playtime_history 表计算月度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算月度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease);
                }
            }
        }

        var gameRecords = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .ToList();

        var csv = new StringBuilder();
        
        // 添加BOM以支持Excel正确显示中文
        csv.Append("\uFEFF");
        
        // 标题
        csv.AppendLine($"月度游戏报告,{startDate:yyyy年MM月dd日} - {endDate:yyyy年MM月dd日}");
        csv.AppendLine();
        
        // 总体统计
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        csv.AppendLine("总体统计");
        csv.AppendLine("指标,数值");
        csv.AppendLine($"总游玩时长（小时）,{Math.Round(totalMinutes / 60.0, 1)}");
        csv.AppendLine($"游戏数量,{gamePlaytimeDict.Count}");
        csv.AppendLine();
        
        // 游戏详情
        csv.AppendLine("游戏详情");
        csv.AppendLine("排名,游戏名称,平台,游玩时长（小时）,游玩时长（分钟）");
        
        int rank = 1;
        foreach (var record in gameRecords)
        {
            csv.AppendLine($"{rank},{record.Value.gameName},{record.Value.platformName},{Math.Round(record.Value.playtimeMinutes / 60.0, 1)},{record.Value.playtimeMinutes}");
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

        // 从 user_playtime_history 表计算月度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算月度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease);
                }
            }
        }

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var totalAchievements = achievements.Count;

        // 游戏排行
        var topGames = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .Take(10)
            .Select(kv => new
            {
                GameName = kv.Value.gameName,
                PlatformName = kv.Value.platformName,
                PlaytimeMinutes = kv.Value.playtimeMinutes
            })
            .ToList();

        // 生成PDF
        var document = Document.Create(container =>
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
                                    .Text(game.GameName);
                                table.Cell().Background(bgColor).Padding(8)
                                    .Text(game.PlatformName);
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
                            text.Span("Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return pdfBytes;
    }

    /// <summary>
    /// 生成年度总结报告 HTML
    /// </summary>
    public async Task<string> GenerateYearlyReportHtml(int userId, int year)
    {
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);

        // 从 user_playtime_history 表计算年度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
                .ThenInclude(g => g.GameGenres)
                    .ThenInclude(gg => gg.Genre)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        if (!historyRecords.Any())
        {
            return GenerateEmptyReportHtml(startDate, endDate, "本年度暂无游戏时长数据");
        }

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算年度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes, Game? game)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease, existing.game);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease, firstRecord.Game);
                }
            }
        }

        var achievements = await _context.UserAchievements
            .Include(a => a.Achievement)
                .ThenInclude(a => a.Game)
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var playedGames = gamePlaytimeDict.Count(g => g.Value.playtimeMinutes > 0);
        var totalAchievements = achievements.Count;

        // 最常玩的游戏
        var topGameEntry = gamePlaytimeDict.OrderByDescending(kv => kv.Value.playtimeMinutes).FirstOrDefault();
        var topGameName = topGameEntry.Value.gameName ?? "无";
        var topGameHours = topGameEntry.Value.playtimeMinutes > 0 ? Math.Round(topGameEntry.Value.playtimeMinutes / 60.0, 1) : 0;

        // 按类型统计
        var genreStats = gamePlaytimeDict.Values
            .Where(v => v.game is not null)
            .SelectMany(v => v.game!.GameGenres.Select(gg => new { Genre = gg.Genre?.Name ?? "未知", Minutes = v.playtimeMinutes }))
            .GroupBy(x => x.Genre)
            .Select(g => new { Genre = g.Key, Minutes = g.Sum(x => x.Minutes) })
            .OrderByDescending(x => x.Minutes)
            .Take(5)
            .ToList();

        // 游戏排行
        var topGames = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .Take(10)
            .Select(kv => new
            {
                GameName = kv.Value.gameName,
                PlatformName = kv.Value.platformName,
                PlaytimeMinutes = kv.Value.playtimeMinutes
            })
            .ToList();

        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>{year}年度游戏总结</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Microsoft YaHei', Arial, sans-serif; background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); color: #fff; min-height: 100vh; padding: 40px; }}
        .container {{ max-width: 1000px; margin: 0 auto; }}
        .hero {{ text-align: center; padding: 60px 0; }}
        .hero h1 {{ font-size: 3em; background: linear-gradient(90deg, #00d4ff, #7c3aed); -webkit-background-clip: text; -webkit-text-fill-color: transparent; margin-bottom: 10px; }}
        .hero .year {{ font-size: 6em; font-weight: bold; color: #7c3aed; opacity: 0.3; }}
        .stats-row {{ display: flex; justify-content: center; gap: 30px; margin: 40px 0; flex-wrap: wrap; }}
        .stat-box {{ background: rgba(255,255,255,0.1); border-radius: 20px; padding: 30px 40px; text-align: center; min-width: 180px; }}
        .stat-box .value {{ font-size: 3em; font-weight: bold; color: #00d4ff; }}
        .stat-box .label {{ color: #aaa; margin-top: 10px; }}
        .section {{ background: rgba(255,255,255,0.05); border-radius: 20px; padding: 30px; margin: 30px 0; }}
        .section h2 {{ color: #00d4ff; margin-bottom: 20px; font-size: 1.5em; }}
        .highlight-card {{ background: linear-gradient(135deg, #7c3aed, #00d4ff); border-radius: 15px; padding: 30px; margin: 20px 0; }}
        .highlight-card h3 {{ font-size: 1.2em; opacity: 0.8; }}
        .highlight-card .game-name {{ font-size: 2em; font-weight: bold; margin: 10px 0; }}
        .highlight-card .hours {{ font-size: 1.5em; }}
        .genre-bar {{ display: flex; align-items: center; margin: 15px 0; }}
        .genre-bar .name {{ width: 100px; }}
        .genre-bar .bar {{ flex: 1; height: 20px; background: rgba(255,255,255,0.1); border-radius: 10px; overflow: hidden; margin: 0 15px; }}
        .genre-bar .fill {{ height: 100%; background: linear-gradient(90deg, #00d4ff, #7c3aed); border-radius: 10px; }}
        .genre-bar .percent {{ width: 50px; text-align: right; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th {{ text-align: left; padding: 15px; color: #00d4ff; border-bottom: 1px solid rgba(255,255,255,0.1); }}
        td {{ padding: 15px; border-bottom: 1px solid rgba(255,255,255,0.05); }}
        .rank {{ color: #7c3aed; font-weight: bold; }}
        .footer {{ text-align: center; margin-top: 50px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='hero'>
            <div class='year'>{year}</div>
            <h1>🎮 年度游戏总结</h1>
        </div>

        <div class='stats-row'>
            <div class='stat-box'>
                <div class='value'>{totalHours}</div>
                <div class='label'>总游戏时长（小时）</div>
            </div>
            <div class='stat-box'>
                <div class='value'>{playedGames}</div>
                <div class='label'>游玩游戏数</div>
            </div>
            <div class='stat-box'>
                <div class='value'>{totalAchievements}</div>
                <div class='label'>解锁成就</div>
            </div>
        </div>

        {(topGameEntry.Value.playtimeMinutes > 0 ? $@"
        <div class='highlight-card'>
            <h3>🏆 年度最爱游戏</h3>
            <div class='game-name'>{topGameName}</div>
            <div class='hours'>游玩 {topGameHours} 小时</div>
        </div>" : "")}

        <div class='section'>
            <h2>📊 游戏类型偏好</h2>
            {string.Join("", genreStats.Select(g => {
                var maxMinutes = genreStats.Max(x => x.Minutes);
                var percent = maxMinutes > 0 ? Math.Round((double)g.Minutes / maxMinutes * 100) : 0;
                return $@"
            <div class='genre-bar'>
                <span class='name'>{g.Genre}</span>
                <div class='bar'><div class='fill' style='width: {percent}%'></div></div>
                <span class='percent'>{Math.Round(g.Minutes / 60.0)}h</span>
            </div>";
            }))}
        </div>

        <div class='section'>
            <h2>🎯 游戏排行榜</h2>
            <table>
                <thead>
                    <tr><th>排名</th><th>游戏</th><th>时长</th></tr>
                </thead>
                <tbody>
                    {string.Join("", topGames.Select((r, i) => $@"
                    <tr>
                        <td class='rank'>#{i + 1}</td>
                        <td>{r.GameName}</td>
                        <td>{Math.Round(r.PlaytimeMinutes / 60.0, 1)} 小时</td>
                    </tr>"))}
                </tbody>
            </table>
        </div>

        <div class='footer'>
            <p>PlayLinker · {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    /// <summary>
    /// 生成年度总结报告 PDF
    /// </summary>
    public async Task<byte[]> GenerateYearlyReportPdf(int userId, int year)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);

        // 从 user_playtime_history 表计算年度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算年度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease);
                }
            }
        }

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var playedGames = gamePlaytimeDict.Count(g => g.Value.playtimeMinutes > 0);
        var totalAchievements = achievements.Count;
        
        var topGames = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .Take(10)
            .Select(kv => new
            {
                GameName = kv.Value.gameName,
                PlatformName = kv.Value.platformName,
                PlaytimeMinutes = kv.Value.playtimeMinutes
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text($"{year} Annual Game Report")
                        .FontSize(28).SemiBold().FontColor(Colors.Purple.Medium);
                    col.Item().Text("PlayLinker Year in Review")
                        .FontSize(12).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(15);

                    column.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Background(Colors.Purple.Lighten4).Padding(20).Column(c =>
                        {
                            c.Item().Text(totalHours.ToString()).FontSize(36).SemiBold().FontColor(Colors.Purple.Darken2);
                            c.Item().Text("Total Hours").FontSize(10);
                        });
                        row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(20).Column(c =>
                        {
                            c.Item().Text(playedGames.ToString()).FontSize(36).SemiBold().FontColor(Colors.Blue.Darken2);
                            c.Item().Text("Games Played").FontSize(10);
                        });
                        row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(20).Column(c =>
                        {
                            c.Item().Text(totalAchievements.ToString()).FontSize(36).SemiBold().FontColor(Colors.Orange.Darken2);
                            c.Item().Text("Achievements").FontSize(10);
                        });
                    });

                    column.Item().PaddingTop(20).Text("Top 10 Games").FontSize(18).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(4);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Purple.Medium).Padding(8).Text("Rank").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Purple.Medium).Padding(8).Text("Game").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Purple.Medium).Padding(8).Text("Hours").FontColor(Colors.White).SemiBold();
                        });

                        int rank = 1;
                        foreach (var game in topGames)
                        {
                            var bgColor = rank % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Background(bgColor).Padding(8).Text($"#{rank}").FontColor(Colors.Purple.Medium).SemiBold();
                            table.Cell().Background(bgColor).Padding(8).Text(game.GameName);
                            table.Cell().Background(bgColor).Padding(8).Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h");
                            rank++;
                        }
                    });
                });

                page.Footer().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// 生成年度总结报告 CSV
    /// </summary>
    public async Task<byte[]> GenerateYearlyReportCsv(int userId, int year)
    {
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);

        // 从 user_playtime_history 表计算年度时长增量
        var historyRecords = await _context.UserPlaytimeHistories
            .Include(h => h.Game)
                .ThenInclude(g => g.GameGenres)
                    .ThenInclude(gg => gg.Genre)
            .Where(h => h.UserId == userId 
                && h.RecordDate >= startDate 
                && h.RecordDate <= endDate)
            .OrderBy(h => h.GameId)
            .ThenBy(h => h.RecordDate)
            .ToListAsync();

        // 获取所有相关的平台信息
        var platformIds = historyRecords.Select(h => h.PlatformId).Distinct().ToList();
        var platforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId))
            .ToDictionaryAsync(p => p.PlatformId, p => p.PlatformName);

        // 按游戏分组计算年度增量
        var gamePlaytimeDict = new Dictionary<int, (string gameName, string platformName, int playtimeMinutes, Game? game)>();
        
        foreach (var group in historyRecords.GroupBy(h => new { h.GameId, h.PlatformId }))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            if (records.Count == 0) continue;

            var firstRecord = records.First();
            var lastRecord = records.Last();
            
            var playtimeIncrease = lastRecord.PlaytimeForever - firstRecord.PlaytimeForever;
            if (records.Count == 1)
            {
                playtimeIncrease = firstRecord.PlaytimeForever;
            }
            
            if (playtimeIncrease > 0)
            {
                var gameId = (int)group.Key.GameId;
                var gameName = firstRecord.Game?.Name ?? "未知游戏";
                var platformName = platforms.GetValueOrDefault(group.Key.PlatformId, "未知平台");
                
                if (gamePlaytimeDict.ContainsKey(gameId))
                {
                    var existing = gamePlaytimeDict[gameId];
                    gamePlaytimeDict[gameId] = (existing.gameName, existing.platformName, existing.playtimeMinutes + playtimeIncrease, existing.game);
                }
                else
                {
                    gamePlaytimeDict[gameId] = (gameName, platformName, playtimeIncrease, firstRecord.Game);
                }
            }
        }

        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var totalAchievements = achievements.Count;

        // 按类型统计
        var genreStats = gamePlaytimeDict.Values
            .Where(v => v.game is not null)
            .SelectMany(v => v.game!.GameGenres.Select(gg => new { Genre = gg.Genre?.Name ?? "未知", Minutes = v.playtimeMinutes }))
            .GroupBy(x => x.Genre)
            .Select(g => new { Genre = g.Key, Minutes = g.Sum(x => x.Minutes) })
            .OrderByDescending(x => x.Minutes)
            .ToList();

        // 游戏排行
        var gameRecords = gamePlaytimeDict
            .OrderByDescending(kv => kv.Value.playtimeMinutes)
            .ToList();

        var csv = new StringBuilder();
        
        // 添加BOM以支持Excel正确显示中文
        csv.Append("\uFEFF");
        
        // 标题
        csv.AppendLine($"年度游戏报告,{year}年");
        csv.AppendLine();
        
        // 总体统计
        csv.AppendLine("总体统计");
        csv.AppendLine("指标,数值");
        csv.AppendLine($"总游玩时长（小时）,{totalHours}");
        csv.AppendLine($"游戏数量,{totalGames}");
        csv.AppendLine($"解锁成就,{totalAchievements}");
        csv.AppendLine();
        
        // 类型统计
        csv.AppendLine("游戏类型统计");
        csv.AppendLine("类型,游玩时长（小时）");
        foreach (var genre in genreStats)
        {
            csv.AppendLine($"{genre.Genre},{Math.Round(genre.Minutes / 60.0, 1)}");
        }
        csv.AppendLine();
        
        // 游戏详情
        csv.AppendLine("游戏详情");
        csv.AppendLine("排名,游戏名称,平台,游玩时长（小时）,游玩时长（分钟）");
        
        int rank = 1;
        foreach (var record in gameRecords)
        {
            csv.AppendLine($"{rank},{record.Value.gameName},{record.Value.platformName},{Math.Round(record.Value.playtimeMinutes / 60.0, 1)},{record.Value.playtimeMinutes}");
            rank++;
        }
        
        csv.AppendLine();
        csv.AppendLine($"报告生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// 生成游戏库存报告 HTML
    /// </summary>
    public async Task<string> GenerateInventoryReportHtml(int userId)
    {
        _logger.LogInformation("生成库存报告，用户ID: {UserId}", userId);
        
        // 获取用户绑定的平台用户ID
        var platformUserIds = await _context.UserPlatformBindings
            .Where(b => b.UserId == userId && b.BindingStatus == true)
            .Select(b => b.PlatformUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToListAsync();

        _logger.LogInformation("用户绑定的平台数: {Count}", platformUserIds.Count);

        var gameRecords = await _context.UserPlatformLibraries
            .Include(r => r.Game)
            .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.Platform)
            .Where(r => platformUserIds.Contains(r.PlatformUserId))
            .ToListAsync();

        _logger.LogInformation("游戏库记录数: {Count}", gameRecords.Count);

        var localGames = await _context.LocalGameInstalls
            .Include(l => l.Game)
            .Where(l => l.UserId == userId)
            .ToListAsync();

        _logger.LogInformation("本地安装游戏数: {Count}", localGames.Count);

        // 获取所有存档 - 通过 LocalGameInstall 关联
        var installIds = localGames.Select(l => l.InstallId).ToList();
        var saves = await _context.LocalSaveFiles
            .Include(s => s.Install)
                .ThenInclude(i => i.Game)
            .Where(s => installIds.Contains(s.InstallId))
            .ToListAsync();

        _logger.LogInformation("存档数: {Count}", saves.Count);

        var totalGames = gameRecords.Count;
        var installedGames = localGames.Count;
        var totalSaves = saves.Count;
        var totalSizeGB = localGames.Sum(l => l.SizeBytes) / 1024.0 / 1024.0 / 1024.0;

        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>游戏库存报告</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Microsoft YaHei', Arial, sans-serif; background: #f0f2f5; padding: 30px; }}
        .container {{ max-width: 1200px; margin: 0 auto; }}
        .header {{ background: linear-gradient(135deg, #2196F3, #1976D2); color: white; padding: 40px; border-radius: 15px; margin-bottom: 30px; }}
        .header h1 {{ font-size: 2em; margin-bottom: 10px; }}
        .stats-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 20px; margin-bottom: 30px; }}
        .stat-card {{ background: white; padding: 25px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); text-align: center; }}
        .stat-card .value {{ font-size: 2.5em; font-weight: bold; color: #2196F3; }}
        .stat-card .label {{ color: #666; margin-top: 5px; }}
        .section {{ background: white; border-radius: 12px; padding: 25px; margin-bottom: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .section h2 {{ color: #333; margin-bottom: 20px; padding-bottom: 10px; border-bottom: 2px solid #2196F3; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th {{ background: #f5f5f5; padding: 12px; text-align: left; font-weight: 600; }}
        td {{ padding: 12px; border-bottom: 1px solid #eee; }}
        .badge {{ display: inline-block; padding: 4px 10px; border-radius: 20px; font-size: 0.85em; }}
        .badge-installed {{ background: #e8f5e9; color: #2e7d32; }}
        .badge-cloud {{ background: #e3f2fd; color: #1565c0; }}
        .footer {{ text-align: center; color: #999; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📦 游戏库存报告</h1>
            <p>生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>

        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='value'>{totalGames}</div>
                <div class='label'>游戏总数</div>
            </div>
            <div class='stat-card'>
                <div class='value'>{installedGames}</div>
                <div class='label'>已安装</div>
            </div>
            <div class='stat-card'>
                <div class='value'>{totalSaves}</div>
                <div class='label'>存档数量</div>
            </div>
            <div class='stat-card'>
                <div class='value'>{totalSizeGB:F1} GB</div>
                <div class='label'>占用空间</div>
            </div>
        </div>

        <div class='section'>
            <h2>🎮 游戏收藏 ({totalGames})</h2>
            <table>
                <thead>
                    <tr><th>游戏名称</th><th>平台</th><th>游玩时长</th><th>状态</th></tr>
                </thead>
                <tbody>
                    {string.Join("", gameRecords.Take(20).Select(r => {
                        var isInstalled = localGames.Any(l => l.GameId == r.GameId);
                        return $@"
                    <tr>
                        <td>{r.Game?.Name ?? "Unknown"}</td>
                        <td>{r.PlayerPlatform?.Platform?.PlatformName ?? "Unknown"}</td>
                        <td>{Math.Round(r.PlaytimeMinutes / 60.0, 1)} 小时</td>
                        <td>{(isInstalled ? "<span class='badge badge-installed'>已安装</span>" : "")}</td>
                    </tr>";
                    }))}
                </tbody>
            </table>
        </div>

        <div class='section'>
            <h2>💾 本地安装 ({installedGames})</h2>
            <table>
                <thead>
                    <tr><th>游戏名称</th><th>安装路径</th><th>大小</th><th>版本</th></tr>
                </thead>
                <tbody>
                    {string.Join("", localGames.Select(l => $@"
                    <tr>
                        <td>{l.Game?.Name ?? "Unknown"}</td>
                        <td>{l.InstallPath}</td>
                        <td>{(l.SizeBytes / 1024.0 / 1024.0 / 1024.0):F1} GB</td>
                        <td>{l.Version ?? "-"}</td>
                    </tr>"))}
                </tbody>
            </table>
        </div>

        <div class='section'>
            <h2>📁 存档统计 ({totalSaves})</h2>
            <table>
                <thead>
                    <tr><th>游戏名称</th><th>存档路径</th><th>大小</th><th>更新时间</th></tr>
                </thead>
                <tbody>
                    {string.Join("", saves.Take(20).Select(s => $@"
                    <tr>
                        <td>{s.Install?.Game?.Name ?? "Unknown"}</td>
                        <td>{s.FilePath}</td>
                        <td>{(s.FileSize / 1024.0):F2} MB</td>
                        <td>{s.UpdatedAt:yyyy-MM-dd HH:mm}</td>
                    </tr>"))}
                </tbody>
            </table>
        </div>

        <div class='footer'>
            <p>PlayLinker 游戏管理平台</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    /// <summary>
    /// 生成游戏库存报告 CSV
    /// </summary>
    public async Task<byte[]> GenerateInventoryReportCsv(int userId)
    {
        // 获取用户绑定的平台用户ID
        var platformUserIds = await _context.UserPlatformBindings
            .Where(b => b.UserId == userId && b.BindingStatus == true)
            .Select(b => b.PlatformUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToListAsync();

        var gameRecords = await _context.UserPlatformLibraries
            .Include(r => r.Game)
            .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.Platform)
            .Where(r => platformUserIds.Contains(r.PlatformUserId))
            .OrderBy(r => r.Game.Name)
            .ToListAsync();

        var localGames = await _context.LocalGameInstalls
            .Include(l => l.Game)
            .Where(l => l.UserId == userId)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.Append("\uFEFF");
        
        csv.AppendLine("游戏库存报告");
        csv.AppendLine($"生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine();
        
        csv.AppendLine("游戏收藏");
        csv.AppendLine("游戏名称,平台,游玩时长（小时）,是否安装");
        
        foreach (var record in gameRecords)
        {
            var isInstalled = localGames.Any(l => l.GameId == record.GameId) ? "是" : "否";
            csv.AppendLine($"{record.Game?.Name ?? "Unknown"},{record.PlayerPlatform?.Platform?.PlatformName ?? "Unknown"},{Math.Round(record.PlaytimeMinutes / 60.0, 1)},{isInstalled}");
        }
        
        csv.AppendLine();
        csv.AppendLine("本地安装");
        csv.AppendLine("游戏名称,安装路径,大小（GB）,版本");
        
        foreach (var local in localGames)
        {
            csv.AppendLine($"{local.Game?.Name ?? "Unknown"},{local.InstallPath},{(local.SizeBytes / 1024.0 / 1024.0 / 1024.0):F1},{local.Version ?? "-"}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// 生成游戏库存报告 PDF
    /// </summary>
    public async Task<byte[]> GenerateInventoryReportPdf(int userId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // 获取用户绑定的平台用户ID
        var platformUserIds = await _context.UserPlatformBindings
            .Where(b => b.UserId == userId && b.BindingStatus == true)
            .Select(b => b.PlatformUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToListAsync();

        var gameRecords = await _context.UserPlatformLibraries
            .Include(r => r.Game)
            .Include(r => r.PlayerPlatform)
                .ThenInclude(pp => pp.Platform)
            .Where(r => platformUserIds.Contains(r.PlatformUserId))
            .ToListAsync();

        var localGames = await _context.LocalGameInstalls
            .Include(l => l.Game)
            .Where(l => l.UserId == userId)
            .ToListAsync();

        // 获取所有存档 - 通过 LocalGameInstall 关联
        var installIds = localGames.Select(l => l.InstallId).ToList();
        var saves = await _context.LocalSaveFiles
            .Include(s => s.Install)
            .Where(s => installIds.Contains(s.InstallId))
            .ToListAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Game Inventory Report").FontSize(24).SemiBold().FontColor(Colors.Blue.Medium);
                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(15);

                    column.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(15).Column(c =>
                        {
                            c.Item().Text(gameRecords.Count.ToString()).FontSize(28).SemiBold().FontColor(Colors.Blue.Darken2);
                            c.Item().Text("Total Games").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Green.Lighten4).Padding(15).Column(c =>
                        {
                            c.Item().Text(localGames.Count.ToString()).FontSize(28).SemiBold().FontColor(Colors.Green.Darken2);
                            c.Item().Text("Installed").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(15).Column(c =>
                        {
                            c.Item().Text(saves.Count.ToString()).FontSize(28).SemiBold().FontColor(Colors.Orange.Darken2);
                            c.Item().Text("Saves").FontSize(9);
                        });
                        row.RelativeItem().Background(Colors.Purple.Lighten4).Padding(15).Column(c =>
                        {
                            c.Item().Text($"{localGames.Sum(l => l.SizeBytes) / 1024.0 / 1024.0 / 1024.0:F1}").FontSize(28).SemiBold().FontColor(Colors.Purple.Darken2);
                            c.Item().Text("GB Used").FontSize(9);
                        });
                    });

                    column.Item().PaddingTop(15).Text("Game Collection").FontSize(16).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(60);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Medium).Padding(6).Text("Game").FontColor(Colors.White).FontSize(9);
                            header.Cell().Background(Colors.Blue.Medium).Padding(6).Text("Platform").FontColor(Colors.White).FontSize(9);
                            header.Cell().Background(Colors.Blue.Medium).Padding(6).Text("Hours").FontColor(Colors.White).FontSize(9);
                            header.Cell().Background(Colors.Blue.Medium).Padding(6).Text("Status").FontColor(Colors.White).FontSize(9);
                        });

                        foreach (var game in gameRecords.Take(30))
                        {
                            var isInstalled = localGames.Any(l => l.GameId == game.GameId);
                            table.Cell().Padding(5).Text(game.Game?.Name ?? "Unknown").FontSize(9);
                            table.Cell().Padding(5).Text(game.PlayerPlatform?.Platform?.PlatformName ?? "-").FontSize(9);
                            table.Cell().Padding(5).Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h").FontSize(9);
                            table.Cell().Padding(5).Text(isInstalled ? "Installed" : "-").FontSize(9).FontColor(isInstalled ? Colors.Green.Medium : Colors.Grey.Medium);
                        }
                    });
                });

                page.Footer().Text(text =>
                {
                    text.Span("Page ").FontSize(9);
                    text.CurrentPageNumber().FontSize(9);
                    text.Span(" / ").FontSize(9);
                    text.TotalPages().FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }
}
