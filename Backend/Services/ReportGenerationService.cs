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

        // 按游戏分组计算月度增量
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

        // 查询成就
        var achievements = await _context.UserAchievements
            .Where(a => a.UserId == userId && a.Unlocked)
            .ToListAsync();

        // 计算统计数据
        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var totalAchievements = achievements.Count;

        if (totalGames == 0)
        {
            return GenerateEmptyReportHtml(startDate, endDate, "本月暂无游戏时长数据");
        }

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

        // 计算活跃天数
        var activeDays = historyRecords.Select(h => h.RecordDate.Date).Distinct().Count();
        
        _logger.LogInformation("月度报告 - 用户ID: {UserId}, 日期范围: {Start} - {End}", userId, startDate, endDate);
        _logger.LogInformation("月度报告 - 历史记录数: {Count}, 游戏数: {Games}, 活跃天数: {Days}", 
            historyRecords.Count, gamePlaytimeDict.Count, activeDays);

        // 生成洞察文本
        var insights = new List<string>();
        if (topGameEntry.Value.playtimeMinutes > 0)
        {
            var topGamePercent = totalMinutes > 0 ? Math.Round((decimal)topGameEntry.Value.playtimeMinutes / totalMinutes * 100, 1) : 0;
            insights.Add($"🎮 本月最爱是《{topGameName}》，投入了 {topGameHours} 小时，占总时长的 {topGamePercent}%");
        }
        insights.Add($"📅 本月活跃 {activeDays} 天，平均每天游玩 {(activeDays > 0 ? Math.Round(totalHours / activeDays, 1) : 0)} 小时");
        if (genreStats.Any())
        {
            insights.Add($"💜 最喜欢的游戏类型是「{genreStats.First().Genre}」，共游玩 {Math.Round(genreStats.First().Minutes / 60.0, 1)} 小时");
        }

        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>月度游戏报告 - {startDate:yyyy年MM月}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'Microsoft YaHei', Arial, sans-serif; 
            background: linear-gradient(135deg, #0f0c29 0%, #302b63 50%, #24243e 100%);
            color: #fff; 
            min-height: 100vh; 
            padding: 30px;
        }}
        .container {{ max-width: 1000px; margin: 0 auto; }}
        .hero {{ text-align: center; padding: 50px 0; position: relative; }}
        .hero .month {{ 
            font-size: 8em; 
            font-weight: bold; 
            background: linear-gradient(180deg, rgba(124, 58, 237, 0.3), transparent);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            line-height: 1;
        }}
        .hero h1 {{ 
            font-size: 2.5em; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed, #ff6b6b);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-top: -20px;
        }}
        .hero .subtitle {{ color: #888; margin-top: 15px; font-size: 1.1em; }}
        
        .stats-row {{ 
            display: flex; 
            justify-content: center; 
            gap: 25px; 
            margin: 40px 0;
            flex-wrap: wrap;
        }}
        .stat-box {{ 
            background: rgba(255,255,255,0.08);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 20px; 
            padding: 30px 40px; 
            text-align: center;
            min-width: 180px;
            transition: transform 0.3s;
        }}
        .stat-box:hover {{ transform: translateY(-5px); }}
        .stat-box .value {{ 
            font-size: 3em; 
            font-weight: bold; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }}
        .stat-box .label {{ color: #888; margin-top: 10px; }}
        
        .highlight-card {{ 
            background: linear-gradient(135deg, #7c3aed, #00d4ff); 
            border-radius: 20px; 
            padding: 35px; 
            margin: 30px 0;
            text-align: center;
            box-shadow: 0 10px 40px rgba(124, 58, 237, 0.3);
        }}
        .highlight-card h3 {{ font-size: 1.2em; opacity: 0.9; }}
        .highlight-card .game-name {{ font-size: 2.5em; font-weight: bold; margin: 15px 0; }}
        .highlight-card .hours {{ font-size: 1.3em; opacity: 0.9; }}
        
        .insights {{
            background: linear-gradient(135deg, rgba(124, 58, 237, 0.15), rgba(0, 212, 255, 0.15));
            border: 1px solid rgba(124, 58, 237, 0.3);
            border-radius: 20px;
            padding: 30px;
            margin: 30px 0;
        }}
        .insights h3 {{ color: #00d4ff; margin-bottom: 20px; font-size: 1.3em; }}
        .insights p {{ color: #ccc; margin: 12px 0; line-height: 1.8; font-size: 1.05em; }}
        
        .section {{ 
            background: rgba(255,255,255,0.05);
            border-radius: 20px;
            padding: 30px;
            margin: 30px 0;
        }}
        .section h2 {{ 
            color: #00d4ff; 
            margin-bottom: 25px;
            font-size: 1.4em;
        }}
        
        .genre-bar {{ display: flex; align-items: center; margin: 18px 0; }}
        .genre-bar .name {{ width: 100px; color: #ccc; }}
        .genre-bar .bar {{ 
            flex: 1; 
            height: 24px; 
            background: rgba(255,255,255,0.1); 
            border-radius: 12px; 
            overflow: hidden; 
            margin: 0 15px;
        }}
        .genre-bar .fill {{ 
            height: 100%; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed); 
            border-radius: 12px;
        }}
        .genre-bar .hours {{ width: 60px; text-align: right; color: #00d4ff; font-weight: 500; }}
        
        .game-rank {{ margin: 15px 0; }}
        .game-rank-item {{ 
            display: flex; 
            align-items: center; 
            margin: 12px 0;
            padding: 12px 15px;
            background: rgba(255,255,255,0.03);
            border-radius: 12px;
            transition: background 0.3s;
        }}
        .game-rank-item:hover {{ background: rgba(255,255,255,0.08); }}
        .game-rank-item .rank {{ 
            width: 40px; 
            height: 40px;
            background: linear-gradient(135deg, #7c3aed, #00d4ff);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            margin-right: 15px;
        }}
        .game-rank-item .rank.gold {{ background: linear-gradient(135deg, #ffd700, #ff8c00); }}
        .game-rank-item .rank.silver {{ background: linear-gradient(135deg, #c0c0c0, #a0a0a0); }}
        .game-rank-item .rank.bronze {{ background: linear-gradient(135deg, #cd7f32, #8b4513); }}
        .game-rank-item .info {{ flex: 1; }}
        .game-rank-item .name {{ font-weight: 500; color: #fff; }}
        .game-rank-item .platform {{ font-size: 0.85em; color: #888; margin-top: 3px; }}
        .game-rank-item .bar-container {{ 
            flex: 1;
            height: 8px;
            background: rgba(255,255,255,0.1);
            border-radius: 4px;
            margin: 0 20px;
            overflow: hidden;
        }}
        .game-rank-item .bar {{ 
            height: 100%;
            background: linear-gradient(90deg, #00d4ff, #7c3aed);
            border-radius: 4px;
        }}
        .game-rank-item .hours {{ 
            min-width: 80px;
            text-align: right;
            color: #00d4ff;
            font-weight: 600;
            font-size: 1.1em;
        }}
        
        .footer {{ 
            text-align: center; 
            margin-top: 50px; 
            padding-top: 25px;
            border-top: 1px solid rgba(255,255,255,0.1);
            color: #666;
        }}
        
        @media print {{
            body {{ background: #1a1a2e; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='hero'>
            <div class='month'>{startDate:MM}月</div>
            <h1>🎮 月度游戏报告</h1>
            <div class='subtitle'>{startDate:yyyy年MM月dd日} - {endDate:yyyy年MM月dd日}</div>
        </div>

        <div class='stats-row'>
            <div class='stat-box'>
                <div class='value'>{totalHours}</div>
                <div class='label'>总游戏时长（小时）</div>
            </div>
            <div class='stat-box'>
                <div class='value'>{totalGames}</div>
                <div class='label'>游玩游戏数</div>
            </div>
            <div class='stat-box'>
                <div class='value'>{activeDays}</div>
                <div class='label'>活跃天数</div>
            </div>
            <div class='stat-box'>
                <div class='value'>{totalAchievements}</div>
                <div class='label'>解锁成就</div>
            </div>
        </div>

        {(topGameEntry.Value.playtimeMinutes > 0 ? $@"
        <div class='highlight-card'>
            <h3>🏆 本月最爱游戏</h3>
            <div class='game-name'>{topGameName}</div>
            <div class='hours'>共游玩 {topGameHours} 小时</div>
        </div>" : "")}

        <div class='insights'>
            <h3>💡 本月洞察</h3>
            {string.Join("", insights.Select(i => $"<p>{i}</p>"))}
        </div>

        {(genreStats.Any() ? $@"
        <div class='section'>
            <h2>📊 游戏类型偏好</h2>
            {string.Join("", genreStats.Select(g => {{
                var maxMinutes = genreStats.Max(x => x.Minutes);
                var percent = maxMinutes > 0 ? Math.Round((double)g.Minutes / maxMinutes * 100) : 0;
                return $@"
            <div class='genre-bar'>
                <span class='name'>{g.Genre}</span>
                <div class='bar'><div class='fill' style='width: {percent}%'></div></div>
                <span class='hours'>{Math.Round(g.Minutes / 60.0)}h</span>
            </div>";
            }}))}
        </div>" : "")}

        <div class='section'>
            <h2>🎯 游戏排行榜 TOP 10</h2>
            <div class='game-rank'>
                {string.Join("", topGames.Select((r, i) => {{
                    var maxMinutes = topGames.Max(g => g.PlaytimeMinutes);
                    var barWidth = maxMinutes > 0 ? Math.Round((double)r.PlaytimeMinutes / maxMinutes * 100) : 0;
                    var rankClass = i == 0 ? "gold" : (i == 1 ? "silver" : (i == 2 ? "bronze" : ""));
                    return $@"
                <div class='game-rank-item'>
                    <div class='rank {rankClass}'>{i + 1}</div>
                    <div class='info'>
                        <div class='name'>{r.GameName}</div>
                        <div class='platform'>{r.PlatformName}</div>
                    </div>
                    <div class='bar-container'>
                        <div class='bar' style='width: {barWidth}%'></div>
                    </div>
                    <div class='hours'>{Math.Round(r.PlaytimeMinutes / 60.0, 1)}h</div>
                </div>";
                }}))}
            </div>
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

        // 按游戏分组计算月度增量
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
        var activeDays = historyRecords.Select(h => h.RecordDate.Date).Distinct().Count();

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

        // 生成洞察文本
        var insights = new List<string>();
        if (topGameEntry.Value.playtimeMinutes > 0)
        {
            var topGamePercent = totalMinutes > 0 ? Math.Round((decimal)topGameEntry.Value.playtimeMinutes / totalMinutes * 100, 1) : 0;
            insights.Add($"本月最爱是《{topGameName}》，投入了 {topGameHours} 小时，占总时长的 {topGamePercent}%");
        }
        insights.Add($"本月活跃 {activeDays} 天，平均每天游玩 {(activeDays > 0 ? Math.Round(totalHours / activeDays, 1) : 0)} 小时");
        if (genreStats.Any())
        {
            insights.Add($"最喜欢的游戏类型是「{genreStats.First().Genre}」，共游玩 {Math.Round(genreStats.First().Minutes / 60.0, 1)} 小时");
        }

        // 生成PDF - 暗色系风格
        var darkBg = Color.FromHex("#1a1a2e");
        var darkCard = Color.FromHex("#252542");
        var accentCyan = Color.FromHex("#00d4ff");
        var accentPurple = Color.FromHex("#7c3aed");
        var textLight = Color.FromHex("#e0e0e0");
        var textMuted = Color.FromHex("#888888");
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // 页面设置
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(darkBg);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(textLight));

                // 页眉
                page.Header()
                    .PaddingBottom(15)
                    .Column(col =>
                    {
                        col.Item().Text($"{startDate:MM}月").FontSize(48).Bold().FontColor(accentPurple);
                        col.Item().Text("月度游戏报告").FontSize(28).SemiBold().FontColor(accentCyan);
                        col.Item().Text($"{startDate:yyyy年MM月dd日} - {endDate:yyyy年MM月dd日}").FontSize(11).FontColor(textMuted);
                    });

                // 内容
                page.Content()
                    .PaddingVertical(0.5f, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(12);

                        // 总体统计卡片
                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            row.RelativeItem().Background(darkCard).Padding(12).Column(col =>
                            {
                                col.Item().Text(totalHours.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                                col.Item().Text("总时长(小时)").FontSize(9).FontColor(textMuted);
                            });

                            row.RelativeItem().Background(darkCard).Padding(12).Column(col =>
                            {
                                col.Item().Text(totalGames.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                                col.Item().Text("游戏数").FontSize(9).FontColor(textMuted);
                            });

                            row.RelativeItem().Background(darkCard).Padding(12).Column(col =>
                            {
                                col.Item().Text(activeDays.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                                col.Item().Text("活跃天数").FontSize(9).FontColor(textMuted);
                            });

                            row.RelativeItem().Background(darkCard).Padding(12).Column(col =>
                            {
                                col.Item().Text(totalAchievements.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                                col.Item().Text("成就数").FontSize(9).FontColor(textMuted);
                            });
                        });

                        // 本月最爱游戏高亮卡片
                        if (topGameEntry.Value.playtimeMinutes > 0)
                        {
                            column.Item().Background(accentPurple).Padding(18).Column(col =>
                            {
                                col.Item().Text("🏆 本月最爱游戏").FontSize(11).FontColor(Colors.White);
                                col.Item().Text(topGameName).FontSize(20).Bold().FontColor(Colors.White);
                                col.Item().Text($"共游玩 {topGameHours} 小时").FontSize(12).FontColor(Colors.White);
                            });
                        }

                        // 本月洞察
                        column.Item().Background(darkCard).Padding(15).Column(col =>
                        {
                            col.Item().Text("💡 本月洞察").FontSize(13).SemiBold().FontColor(accentCyan);
                            col.Item().PaddingTop(8);
                            foreach (var insight in insights)
                            {
                                col.Item().Text($"• {insight}").FontSize(10).FontColor(textLight);
                            }
                        });

                        // 游戏类型偏好
                        if (genreStats.Any())
                        {
                            column.Item().Text("📊 游戏类型偏好").FontSize(14).SemiBold().FontColor(accentCyan);

                            column.Item().Background(darkCard).Padding(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(4);
                                    columns.ConstantColumn(50);
                                });

                                var maxMinutes = genreStats.Max(x => x.Minutes);
                                foreach (var genre in genreStats)
                                {
                                    var percent = maxMinutes > 0 ? Math.Max(1, (int)Math.Round((double)genre.Minutes / maxMinutes * 100)) : 1;
                                    var remaining = Math.Max(1, 100 - percent);
                                    table.Cell().Padding(4).Text(genre.Genre).FontSize(9).FontColor(textLight);
                                    table.Cell().Padding(4).Column(col =>
                                    {
                                        col.Item().Height(12).Background(Color.FromHex("#333355")).Row(row =>
                                        {
                                            row.RelativeItem(percent).Background(accentCyan);
                                            row.RelativeItem(remaining);
                                        });
                                    });
                                    table.Cell().Padding(4).AlignRight().Text($"{Math.Round(genre.Minutes / 60.0)}h").FontSize(9).FontColor(accentCyan);
                                }
                            });
                        }

                        // 游戏排行榜
                        column.Item().Text("🎯 游戏排行榜 TOP 10").FontSize(14).SemiBold().FontColor(accentCyan);

                        column.Item().Background(darkCard).Padding(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(65);
                                columns.ConstantColumn(50);
                            });

                            // 表头
                            table.Header(header =>
                            {
                                header.Cell().Padding(6).Text("#").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(6).Text("游戏").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(6).Text("平台").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(6).Text("时长").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(6).Text("占比").FontSize(9).SemiBold().FontColor(accentCyan);
                            });

                            int rank = 1;
                            foreach (var game in topGames)
                            {
                                var rankColor = rank == 1 ? Color.FromHex("#ffd700") : (rank == 2 ? Color.FromHex("#c0c0c0") : (rank == 3 ? Color.FromHex("#cd7f32") : textLight));

                                table.Cell().Padding(5).Text($"{rank}").FontSize(9).Bold().FontColor(rankColor);
                                table.Cell().Padding(5).Text(game.GameName).FontSize(9).FontColor(textLight);
                                table.Cell().Padding(5).Text(game.PlatformName).FontSize(8).FontColor(textMuted);
                                table.Cell().Padding(5).Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h").FontSize(9).FontColor(accentCyan);
                                table.Cell().Padding(5).Text($"{(totalMinutes > 0 ? Math.Round((decimal)game.PlaytimeMinutes / totalMinutes * 100, 1) : 0)}%").FontSize(9).FontColor(textMuted);

                                rank++;
                            }
                        });
                    });

                // 页脚
                page.Footer()
                    .BorderTop(1)
                    .BorderColor(Color.FromHex("#333355"))
                    .PaddingTop(8)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"PlayLinker · {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(textMuted);
                        row.ConstantItem(80).AlignRight().Text(text =>
                        {
                            text.Span("第 ").FontSize(8).FontColor(textMuted);
                            text.CurrentPageNumber().FontSize(8).FontColor(textMuted);
                            text.Span(" / ").FontSize(8).FontColor(textMuted);
                            text.TotalPages().FontSize(8).FontColor(textMuted);
                            text.Span(" 页").FontSize(8).FontColor(textMuted);
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

        // 计算每月游玩数据 - 需要按游戏分组后再按月汇总
        var monthlyPlaytimeDict = new Dictionary<int, int>(); // month -> minutes
        foreach (var group in historyRecords.GroupBy(h => h.GameId))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            for (int i = 1; i < records.Count; i++)
            {
                var month = records[i].RecordDate.Month;
                var increase = records[i].PlaytimeForever - records[i - 1].PlaytimeForever;
                if (increase > 0)
                {
                    if (monthlyPlaytimeDict.ContainsKey(month))
                        monthlyPlaytimeDict[month] += increase;
                    else
                        monthlyPlaytimeDict[month] = increase;
                }
            }
        }

        _logger.LogInformation("年度报告 - 用户ID: {UserId}, 年份: {Year}", userId, year);
        _logger.LogInformation("年度报告 - 历史记录数: {Count}, 游戏数: {Games}, 月度数据点: {Monthly}", 
            historyRecords.Count, gamePlaytimeDict.Count, monthlyPlaytimeDict.Count);

        // 计算活跃天数
        var activeDays = historyRecords.Select(h => h.RecordDate).Distinct().Count();
        
        // 生成年度洞察
        var insights = new List<string>();
        if (topGameEntry.Value.playtimeMinutes > 0)
        {
            var topGamePercent = totalMinutes > 0 ? Math.Round((decimal)topGameEntry.Value.playtimeMinutes / totalMinutes * 100, 1) : 0;
            insights.Add($"🎮 你的年度最爱是《{topGameName}》，投入了 {topGameHours} 小时，占全年游戏时长的 {topGamePercent}%");
        }
        insights.Add($"📅 全年活跃 {activeDays} 天，平均每天游玩 {(activeDays > 0 ? Math.Round(totalHours / activeDays, 1) : 0)} 小时");
        if (genreStats.Any())
        {
            insights.Add($"💜 你最喜欢的游戏类型是「{genreStats.First().Genre}」，共游玩 {Math.Round(genreStats.First().Minutes / 60.0, 1)} 小时");
        }
        if (totalAchievements > 0)
        {
            insights.Add($"🏅 全年解锁 {totalAchievements} 个成就，平均每款游戏 {(playedGames > 0 ? Math.Round((double)totalAchievements / playedGames, 1) : 0)} 个");
        }

        // 生成月度数据用于图表
        var monthlyData = new StringBuilder();
        for (int m = 1; m <= 12; m++)
        {
            var minutes = monthlyPlaytimeDict.GetValueOrDefault(m, 0);
            var hours = Math.Round(minutes / 60.0, 1);
            if (m > 1) monthlyData.Append(",");
            monthlyData.Append(hours);
        }
        
        _logger.LogInformation("年度报告 - 月度数据: {Data}", monthlyData.ToString());

        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{year}年度游戏总结</title>
    <script src='https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js'></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'Microsoft YaHei', Arial, sans-serif; 
            background: linear-gradient(135deg, #0f0c29 0%, #302b63 50%, #24243e 100%);
            color: #fff; 
            min-height: 100vh; 
            padding: 30px;
        }}
        .container {{ max-width: 1000px; margin: 0 auto; }}
        .hero {{ text-align: center; padding: 50px 0; position: relative; }}
        .hero .year {{ 
            font-size: 8em; 
            font-weight: bold; 
            background: linear-gradient(180deg, rgba(124, 58, 237, 0.3), transparent);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            line-height: 1;
        }}
        .hero h1 {{ 
            font-size: 2.5em; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed, #ff6b6b);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-top: -20px;
        }}
        .hero .subtitle {{ color: #888; margin-top: 15px; font-size: 1.1em; }}
        
        .stats-row {{ 
            display: flex; 
            justify-content: center; 
            gap: 25px; 
            margin: 40px 0;
            flex-wrap: wrap;
        }}
        .stat-box {{ 
            background: rgba(255,255,255,0.08);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 20px; 
            padding: 30px 40px; 
            text-align: center;
            min-width: 180px;
            transition: transform 0.3s;
        }}
        .stat-box:hover {{ transform: translateY(-5px); }}
        .stat-box .value {{ 
            font-size: 3em; 
            font-weight: bold; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }}
        .stat-box .label {{ color: #888; margin-top: 10px; }}
        
        .highlight-card {{ 
            background: linear-gradient(135deg, #7c3aed, #00d4ff); 
            border-radius: 20px; 
            padding: 35px; 
            margin: 30px 0;
            text-align: center;
            box-shadow: 0 10px 40px rgba(124, 58, 237, 0.3);
        }}
        .highlight-card h3 {{ font-size: 1.2em; opacity: 0.9; }}
        .highlight-card .game-name {{ font-size: 2.5em; font-weight: bold; margin: 15px 0; }}
        .highlight-card .hours {{ font-size: 1.3em; opacity: 0.9; }}
        
        .insights {{
            background: linear-gradient(135deg, rgba(124, 58, 237, 0.15), rgba(0, 212, 255, 0.15));
            border: 1px solid rgba(124, 58, 237, 0.3);
            border-radius: 20px;
            padding: 30px;
            margin: 30px 0;
        }}
        .insights h3 {{ color: #00d4ff; margin-bottom: 20px; font-size: 1.3em; }}
        .insights p {{ color: #ccc; margin: 12px 0; line-height: 1.8; font-size: 1.05em; }}
        
        .section {{ 
            background: rgba(255,255,255,0.05);
            border-radius: 20px;
            padding: 30px;
            margin: 30px 0;
        }}
        .section h2 {{ 
            color: #00d4ff; 
            margin-bottom: 25px;
            font-size: 1.4em;
        }}
        
        .chart-container {{ height: 280px; }}
        
        .genre-bar {{ display: flex; align-items: center; margin: 18px 0; }}
        .genre-bar .name {{ width: 100px; color: #ccc; }}
        .genre-bar .bar {{ 
            flex: 1; 
            height: 24px; 
            background: rgba(255,255,255,0.1); 
            border-radius: 12px; 
            overflow: hidden; 
            margin: 0 15px;
        }}
        .genre-bar .fill {{ 
            height: 100%; 
            background: linear-gradient(90deg, #00d4ff, #7c3aed); 
            border-radius: 12px;
            transition: width 0.5s;
        }}
        .genre-bar .hours {{ width: 60px; text-align: right; color: #00d4ff; font-weight: 500; }}
        
        .game-rank {{ margin: 15px 0; }}
        .game-rank-item {{ 
            display: flex; 
            align-items: center; 
            margin: 12px 0;
            padding: 12px 15px;
            background: rgba(255,255,255,0.03);
            border-radius: 12px;
            transition: background 0.3s;
        }}
        .game-rank-item:hover {{ background: rgba(255,255,255,0.08); }}
        .game-rank-item .rank {{ 
            width: 40px; 
            height: 40px;
            background: linear-gradient(135deg, #7c3aed, #00d4ff);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            margin-right: 15px;
        }}
        .game-rank-item .rank.gold {{ background: linear-gradient(135deg, #ffd700, #ff8c00); }}
        .game-rank-item .rank.silver {{ background: linear-gradient(135deg, #c0c0c0, #a0a0a0); }}
        .game-rank-item .rank.bronze {{ background: linear-gradient(135deg, #cd7f32, #8b4513); }}
        .game-rank-item .info {{ flex: 1; }}
        .game-rank-item .name {{ font-weight: 500; color: #fff; }}
        .game-rank-item .platform {{ font-size: 0.85em; color: #888; margin-top: 3px; }}
        .game-rank-item .bar-container {{ 
            flex: 1;
            height: 8px;
            background: rgba(255,255,255,0.1);
            border-radius: 4px;
            margin: 0 20px;
            overflow: hidden;
        }}
        .game-rank-item .bar {{ 
            height: 100%;
            background: linear-gradient(90deg, #00d4ff, #7c3aed);
            border-radius: 4px;
        }}
        .game-rank-item .hours {{ 
            min-width: 80px;
            text-align: right;
            color: #00d4ff;
            font-weight: 600;
            font-size: 1.1em;
        }}
        
        .footer {{ 
            text-align: center; 
            margin-top: 50px; 
            padding-top: 25px;
            border-top: 1px solid rgba(255,255,255,0.1);
            color: #666;
        }}
        
        @media print {{
            body {{ background: #1a1a2e; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='hero'>
            <div class='year'>{year}</div>
            <h1>🎮 年度游戏总结</h1>
            <div class='subtitle'>这一年，你在游戏世界里留下了这些足迹</div>
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
                <div class='value'>{activeDays}</div>
                <div class='label'>活跃天数</div>
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
            <div class='hours'>共游玩 {topGameHours} 小时</div>
        </div>" : "")}

        <div class='insights'>
            <h3>💡 年度洞察</h3>
            {string.Join("", insights.Select(i => $"<p>{i}</p>"))}
        </div>

        <div class='section'>
            <h2>📈 月度游戏时长趋势</h2>
            <div id='monthlyChart' class='chart-container'></div>
        </div>

        <div class='section'>
            <h2>📊 游戏类型偏好</h2>
            {string.Join("", genreStats.Select(g => {{
                var maxMinutes = genreStats.Max(x => x.Minutes);
                var percent = maxMinutes > 0 ? Math.Round((double)g.Minutes / maxMinutes * 100) : 0;
                return $@"
            <div class='genre-bar'>
                <span class='name'>{g.Genre}</span>
                <div class='bar'><div class='fill' style='width: {percent}%'></div></div>
                <span class='hours'>{Math.Round(g.Minutes / 60.0)}h</span>
            </div>";
            }}))}
        </div>

        <div class='section'>
            <h2>🎯 游戏排行榜 TOP 10</h2>
            <div class='game-rank'>
                {string.Join("", topGames.Select((r, i) => {{
                    var maxMinutes = topGames.Max(g => g.PlaytimeMinutes);
                    var barWidth = maxMinutes > 0 ? Math.Round((double)r.PlaytimeMinutes / maxMinutes * 100) : 0;
                    var rankClass = i == 0 ? "gold" : (i == 1 ? "silver" : (i == 2 ? "bronze" : ""));
                    return $@"
                <div class='game-rank-item'>
                    <div class='rank {rankClass}'>{i + 1}</div>
                    <div class='info'>
                        <div class='name'>{r.GameName}</div>
                        <div class='platform'>{r.PlatformName}</div>
                    </div>
                    <div class='bar-container'>
                        <div class='bar' style='width: {barWidth}%'></div>
                    </div>
                    <div class='hours'>{Math.Round(r.PlaytimeMinutes / 60.0, 1)}h</div>
                </div>";
                }}))}
            </div>
        </div>

        <div class='footer'>
            <p>PlayLinker · {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        </div>
    </div>

    <script>
        // 调试信息
        console.log('ECharts loaded:', typeof echarts !== 'undefined');
        console.log('Monthly chart container:', document.getElementById('monthlyChart'));
        
        if (typeof echarts === 'undefined') {{
            document.getElementById('monthlyChart').innerHTML = '<p style=""color: #ff6b6b; text-align: center; padding: 20px;"">图表库加载失败，请检查网络连接</p>';
        }} else {{
            try {{
                var monthlyChart = echarts.init(document.getElementById('monthlyChart'));
                var monthlyData = [{monthlyData}];
                
                console.log('Monthly data:', monthlyData);
                
                var option = {{
                    tooltip: {{
                        trigger: 'axis',
                        formatter: '{{b}}月<br/>游戏时长: {{c}} 小时'
                    }},
                    xAxis: {{
                        type: 'category',
                        data: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
                        axisLine: {{ lineStyle: {{ color: '#444' }} }},
                        axisLabel: {{ color: '#888' }}
                    }},
                    yAxis: {{
                        type: 'value',
                        name: '小时',
                        axisLine: {{ lineStyle: {{ color: '#444' }} }},
                        axisLabel: {{ color: '#888' }},
                        splitLine: {{ lineStyle: {{ color: 'rgba(255,255,255,0.1)' }} }}
                    }},
                    series: [{{
                        data: monthlyData,
                        type: 'bar',
                        itemStyle: {{
                            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                                {{ offset: 0, color: '#7c3aed' }},
                                {{ offset: 1, color: '#00d4ff' }}
                            ]),
                            borderRadius: [8, 8, 0, 0]
                        }},
                        emphasis: {{
                            itemStyle: {{
                                color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                                    {{ offset: 0, color: '#9f67ff' }},
                                    {{ offset: 1, color: '#33e0ff' }}
                                ])
                            }}
                        }}
                    }}]
                }};
                
                monthlyChart.setOption(option);
                window.addEventListener('resize', function() {{ monthlyChart.resize(); }});
                console.log('Monthly chart initialized successfully');
            }} catch (e) {{
                console.error('Monthly chart error:', e);
                document.getElementById('monthlyChart').innerHTML = '<p style=""color: #ff6b6b; text-align: center; padding: 20px;"">图表渲染失败: ' + e.message + '</p>';
            }}
        }}
    </script>
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

        var totalMinutes = gamePlaytimeDict.Values.Sum(v => v.playtimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60.0, 1);
        var totalGames = gamePlaytimeDict.Count;
        var playedGames = gamePlaytimeDict.Count(g => g.Value.playtimeMinutes > 0);
        var totalAchievements = achievements.Count;
        var activeDays = historyRecords.Select(h => h.RecordDate.Date).Distinct().Count();

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

        // 计算每月游玩数据
        var monthlyPlaytimeDict = new Dictionary<int, int>();
        foreach (var group in historyRecords.GroupBy(h => h.GameId))
        {
            var records = group.OrderBy(h => h.RecordDate).ToList();
            for (int i = 1; i < records.Count; i++)
            {
                var month = records[i].RecordDate.Month;
                var increase = records[i].PlaytimeForever - records[i - 1].PlaytimeForever;
                if (increase > 0)
                {
                    if (monthlyPlaytimeDict.ContainsKey(month))
                        monthlyPlaytimeDict[month] += increase;
                    else
                        monthlyPlaytimeDict[month] = increase;
                }
            }
        }

        // 生成洞察文本
        var insights = new List<string>();
        if (topGameEntry.Value.playtimeMinutes > 0)
        {
            var topGamePercent = totalMinutes > 0 ? Math.Round((decimal)topGameEntry.Value.playtimeMinutes / totalMinutes * 100, 1) : 0;
            insights.Add($"年度最爱是《{topGameName}》，投入了 {topGameHours} 小时，占全年游戏时长的 {topGamePercent}%");
        }
        insights.Add($"全年活跃 {activeDays} 天，平均每天游玩 {(activeDays > 0 ? Math.Round(totalHours / activeDays, 1) : 0)} 小时");
        if (genreStats.Any())
        {
            insights.Add($"最喜欢的游戏类型是「{genreStats.First().Genre}」，共游玩 {Math.Round(genreStats.First().Minutes / 60.0, 1)} 小时");
        }
        if (totalAchievements > 0)
        {
            insights.Add($"全年解锁 {totalAchievements} 个成就，平均每款游戏 {(playedGames > 0 ? Math.Round((double)totalAchievements / playedGames, 1) : 0)} 个");
        }

        // 生成PDF - 暗色系风格
        var darkBg = Color.FromHex("#1a1a2e");
        var darkCard = Color.FromHex("#252542");
        var accentCyan = Color.FromHex("#00d4ff");
        var accentPurple = Color.FromHex("#7c3aed");
        var textLight = Color.FromHex("#e0e0e0");
        var textMuted = Color.FromHex("#888888");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(darkBg);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(textLight));

                page.Header().PaddingBottom(15).Column(col =>
                {
                    col.Item().Text($"{year}").FontSize(56).Bold().FontColor(accentPurple);
                    col.Item().Text("年度游戏报告").FontSize(28).SemiBold().FontColor(accentCyan);
                    col.Item().Text("PlayLinker 年度回顾").FontSize(11).FontColor(textMuted);
                });

                page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(12);

                    // 统计卡片
                    column.Item().Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(totalHours.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                            c.Item().Text("总时长(小时)").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(playedGames.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                            c.Item().Text("游戏数").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(activeDays.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                            c.Item().Text("活跃天数").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(totalAchievements.ToString()).FontSize(28).Bold().FontColor(accentCyan);
                            c.Item().Text("成就数").FontSize(9).FontColor(textMuted);
                        });
                    });

                    // 年度最爱游戏高亮卡片
                    if (topGameEntry.Value.playtimeMinutes > 0)
                    {
                        column.Item().Background(accentPurple).Padding(18).Column(col =>
                        {
                            col.Item().Text("🏆 年度最爱游戏").FontSize(11).FontColor(Colors.White);
                            col.Item().Text(topGameName).FontSize(20).Bold().FontColor(Colors.White);
                            col.Item().Text($"共游玩 {topGameHours} 小时").FontSize(12).FontColor(Colors.White);
                        });
                    }

                    // 年度洞察
                    column.Item().Background(darkCard).Padding(15).Column(col =>
                    {
                        col.Item().Text("💡 年度洞察").FontSize(13).SemiBold().FontColor(accentCyan);
                        col.Item().PaddingTop(8);
                        foreach (var insight in insights)
                        {
                            col.Item().Text($"• {insight}").FontSize(10).FontColor(textLight);
                        }
                    });

                    // 月度游戏时长
                    column.Item().Text("📈 月度游戏时长").FontSize(14).SemiBold().FontColor(accentCyan);
                    
                    column.Item().Background(darkCard).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < 12; i++)
                                columns.RelativeColumn();
                        });

                        // 月份标题
                        for (int m = 1; m <= 12; m++)
                        {
                            table.Cell().Padding(3).AlignCenter().Text($"{m}月").FontSize(8).FontColor(accentCyan);
                        }

                        // 时长数据
                        for (int m = 1; m <= 12; m++)
                        {
                            var hours = Math.Round(monthlyPlaytimeDict.GetValueOrDefault(m, 0) / 60.0, 1);
                            table.Cell().Padding(3).AlignCenter().Text($"{hours}h").FontSize(8).FontColor(textLight);
                        }
                    });

                    // 游戏类型偏好
                    if (genreStats.Any())
                    {
                        column.Item().Text("📊 游戏类型偏好").FontSize(14).SemiBold().FontColor(accentCyan);

                        column.Item().Background(darkCard).Padding(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(50);
                            });

                            var maxMinutes = genreStats.Max(x => x.Minutes);
                            foreach (var genre in genreStats)
                            {
                                var percent = maxMinutes > 0 ? Math.Max(1, (int)Math.Round((double)genre.Minutes / maxMinutes * 100)) : 1;
                                var remaining = Math.Max(1, 100 - percent);
                                table.Cell().Padding(4).Text(genre.Genre).FontSize(9).FontColor(textLight);
                                table.Cell().Padding(4).Column(col =>
                                {
                                    col.Item().Height(12).Background(Color.FromHex("#333355")).Row(row =>
                                    {
                                        row.RelativeItem(percent).Background(accentPurple);
                                        row.RelativeItem(remaining);
                                    });
                                });
                                table.Cell().Padding(4).AlignRight().Text($"{Math.Round(genre.Minutes / 60.0)}h").FontSize(9).FontColor(accentCyan);
                            }
                        });
                    }

                    // 游戏排行榜
                    column.Item().Text("🎯 游戏排行榜 TOP 10").FontSize(14).SemiBold().FontColor(accentCyan);

                    column.Item().Background(darkCard).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(35);
                            columns.RelativeColumn(4);
                            columns.ConstantColumn(65);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Padding(6).Text("#").FontSize(9).SemiBold().FontColor(accentCyan);
                            header.Cell().Padding(6).Text("游戏").FontSize(9).SemiBold().FontColor(accentCyan);
                            header.Cell().Padding(6).Text("时长").FontSize(9).SemiBold().FontColor(accentCyan);
                        });

                        int rank = 1;
                        foreach (var game in topGames)
                        {
                            var rankColor = rank == 1 ? Color.FromHex("#ffd700") : (rank == 2 ? Color.FromHex("#c0c0c0") : (rank == 3 ? Color.FromHex("#cd7f32") : textLight));
                            
                            table.Cell().Padding(5).Text($"{rank}").FontSize(9).Bold().FontColor(rankColor);
                            table.Cell().Padding(5).Text(game.GameName).FontSize(9).FontColor(textLight);
                            table.Cell().Padding(5).Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h").FontSize(9).FontColor(accentCyan);
                            rank++;
                        }
                    });
                });

                page.Footer()
                    .BorderTop(1)
                    .BorderColor(Color.FromHex("#333355"))
                    .PaddingTop(8)
                    .Text($"PlayLinker · {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(textMuted);
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

        // 按平台统计游戏数量
        var platformStats = gameRecords
            .GroupBy(r => r.PlayerPlatform?.Platform?.PlatformName ?? "未知")
            .Select(g => new { Platform = g.Key, Count = g.Count(), TotalHours = Math.Round(g.Sum(r => r.PlaytimeMinutes) / 60.0, 1) })
            .OrderByDescending(p => p.Count)
            .ToList();

        // 生成洞察
        var insights = new List<string>();
        if (totalGames > 0)
        {
            var playedPercent = installedGames > 0 ? Math.Round((double)installedGames / totalGames * 100, 1) : 0;
            insights.Add($"📦 你的游戏库共有 {totalGames} 款游戏，已安装 {installedGames} 款（{playedPercent}%）");
        }
        if (platformStats.Any())
        {
            insights.Add($"🎮 你在 {platformStats.First().Platform} 平台拥有最多游戏（{platformStats.First().Count} 款）");
        }
        if (totalSizeGB > 0)
        {
            insights.Add($"💾 本地游戏占用 {totalSizeGB:F1} GB 存储空间");
        }
        if (totalSaves > 0)
        {
            insights.Add($"📁 共有 {totalSaves} 个游戏存档");
        }

        var html = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>游戏库存报告</title>
    <script src='https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js'></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'Microsoft YaHei', Arial, sans-serif; 
            background: linear-gradient(135deg, #0d1b2a 0%, #1b263b 50%, #415a77 100%);
            color: #e0e0e0;
            min-height: 100vh;
            padding: 30px;
        }}
        .container {{ max-width: 1100px; margin: 0 auto; }}
        .header {{ 
            background: linear-gradient(135deg, rgba(65, 90, 119, 0.5), rgba(27, 38, 59, 0.5));
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255,255,255,0.1);
            padding: 40px; 
            border-radius: 20px; 
            margin-bottom: 30px;
            text-align: center;
        }}
        .header h1 {{ 
            font-size: 2.5em; 
            background: linear-gradient(90deg, #00d4ff, #48cae4);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-bottom: 10px;
        }}
        .header .subtitle {{ color: #888; }}
        
        .stats-grid {{ 
            display: grid; 
            grid-template-columns: repeat(4, 1fr); 
            gap: 20px; 
            margin-bottom: 30px;
        }}
        .stat-card {{ 
            background: rgba(255,255,255,0.08);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255,255,255,0.1);
            padding: 25px; 
            border-radius: 16px; 
            text-align: center;
            transition: transform 0.3s;
        }}
        .stat-card:hover {{ transform: translateY(-5px); }}
        .stat-card .value {{ 
            font-size: 2.5em; 
            font-weight: bold; 
            background: linear-gradient(90deg, #00d4ff, #48cae4);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }}
        .stat-card .label {{ color: #888; margin-top: 8px; }}
        
        .insights {{
            background: linear-gradient(135deg, rgba(0, 212, 255, 0.1), rgba(72, 202, 228, 0.1));
            border: 1px solid rgba(0, 212, 255, 0.2);
            border-radius: 16px;
            padding: 25px;
            margin-bottom: 30px;
        }}
        .insights h3 {{ color: #00d4ff; margin-bottom: 15px; }}
        .insights p {{ color: #ccc; margin: 10px 0; line-height: 1.6; }}
        
        .section {{ 
            background: rgba(255,255,255,0.05);
            border-radius: 16px;
            padding: 25px;
            margin-bottom: 25px;
        }}
        .section h2 {{ 
            color: #00d4ff; 
            margin-bottom: 20px;
            font-size: 1.3em;
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        .section h2 .count {{
            background: rgba(0, 212, 255, 0.2);
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 0.8em;
        }}
        
        .chart-container {{ height: 250px; }}
        
        .game-list {{ max-height: 400px; overflow-y: auto; }}
        .game-item {{ 
            display: flex; 
            align-items: center; 
            padding: 12px 15px;
            margin: 8px 0;
            background: rgba(255,255,255,0.03);
            border-radius: 10px;
            transition: background 0.3s;
        }}
        .game-item:hover {{ background: rgba(255,255,255,0.08); }}
        .game-item .icon {{ 
            width: 50px; 
            height: 50px; 
            background: linear-gradient(135deg, #00d4ff, #48cae4);
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin-right: 15px;
            font-size: 1.2em;
            flex-shrink: 0;
        }}
        .game-item .game-icon {{
            width: 50px;
            height: 50px;
            border-radius: 8px;
            object-fit: cover;
            margin-right: 15px;
            flex-shrink: 0;
        }}
        .game-item .info {{ flex: 1; }}
        .game-item .name {{ font-weight: 500; color: #fff; }}
        .game-item .meta {{ font-size: 0.85em; color: #888; margin-top: 3px; }}
        .game-item .badge {{ 
            padding: 4px 12px; 
            border-radius: 20px; 
            font-size: 0.8em;
            margin-left: 10px;
        }}
        .badge-installed {{ background: rgba(76, 175, 80, 0.2); color: #4caf50; }}
        .badge-platform {{ background: rgba(0, 212, 255, 0.2); color: #00d4ff; }}
        
        .footer {{ 
            text-align: center; 
            margin-top: 40px; 
            padding-top: 20px;
            border-top: 1px solid rgba(255,255,255,0.1);
            color: #666;
        }}
        
        @media (max-width: 768px) {{
            .stats-grid {{ grid-template-columns: repeat(2, 1fr); }}
        }}
        @media print {{
            body {{ background: #1b263b; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📦 游戏库存报告</h1>
            <p class='subtitle'>生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
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
                <div class='value'>{totalSizeGB:F1}</div>
                <div class='label'>占用空间 (GB)</div>
            </div>
        </div>

        <div class='insights'>
            <h3>💡 库存概览</h3>
            {string.Join("", insights.Select(i => $"<p>{i}</p>"))}
        </div>

        {(platformStats.Any() ? $@"
        <div class='section'>
            <h2>📊 平台分布</h2>
            <div id='platformChart' class='chart-container'></div>
        </div>" : "")}

        <div class='section'>
            <h2>🎮 游戏收藏 <span class='count'>{totalGames}</span></h2>
            <div class='game-list'>
                {string.Join("", gameRecords.Take(30).Select(r => {
                    var isInstalled = localGames.Any(l => l.GameId == r.GameId);
                    var headerImage = r.Game?.HeaderImage;
                    var hasImage = !string.IsNullOrEmpty(headerImage);
                    return $@"
                <div class='game-item'>
                    {(hasImage ? $"<img class='game-icon' src='{headerImage}' alt='' onerror=\"this.style.display='none';this.nextElementSibling.style.display='flex';\" /><div class='icon' style='display:none;'>🎮</div>" : "<div class='icon'>🎮</div>")}
                    <div class='info'>
                        <div class='name'>{r.Game?.Name ?? "Unknown"}</div>
                        <div class='meta'>{Math.Round(r.PlaytimeMinutes / 60.0, 1)} 小时</div>
                    </div>
                    <span class='badge badge-platform'>{r.PlayerPlatform?.Platform?.PlatformName ?? "Unknown"}</span>
                    {(isInstalled ? "<span class='badge badge-installed'>已安装</span>" : "")}
                </div>";
                }))}
            </div>
        </div>

        {(localGames.Any() ? $@"
        <div class='section'>
            <h2>💾 本地安装 <span class='count'>{installedGames}</span></h2>
            <div class='game-list'>
                {string.Join("", localGames.Select(l => {{
                    var headerImage = l.Game?.HeaderImage;
                    var hasImage = !string.IsNullOrEmpty(headerImage);
                    return $@"
                <div class='game-item'>
                    {(hasImage ? $"<img class='game-icon' src='{headerImage}' alt='' onerror=\"this.style.display='none';this.nextElementSibling.style.display='flex';\" /><div class='icon' style='display:none;'>💿</div>" : "<div class='icon'>💿</div>")}
                    <div class='info'>
                        <div class='name'>{l.Game?.Name ?? "Unknown"}</div>
                        <div class='meta'>{l.InstallPath}</div>
                    </div>
                    <span class='badge badge-platform'>{(l.SizeBytes / 1024.0 / 1024.0 / 1024.0):F1} GB</span>
                </div>";
                }}))}
            </div>
        </div>" : "")}

        {(saves.Any() ? $@"
        <div class='section'>
            <h2>📁 存档统计 <span class='count'>{totalSaves}</span></h2>
            <div class='game-list'>
                {string.Join("", saves.Take(20).Select(s => {{
                    var headerImage = s.Install?.Game?.HeaderImage;
                    var hasImage = !string.IsNullOrEmpty(headerImage);
                    return $@"
                <div class='game-item'>
                    {(hasImage ? $"<img class='game-icon' src='{headerImage}' alt='' onerror=\"this.style.display='none';this.nextElementSibling.style.display='flex';\" /><div class='icon' style='display:none;'>💾</div>" : "<div class='icon'>💾</div>")}
                    <div class='info'>
                        <div class='name'>{s.Install?.Game?.Name ?? "Unknown"}</div>
                        <div class='meta'>{s.FilePath}</div>
                    </div>
                    <span class='badge badge-platform'>{s.UpdatedAt:MM-dd HH:mm}</span>
                </div>";
                }}))}
            </div>
        </div>" : "")}

        <div class='footer'>
            <p>PlayLinker 游戏管理平台</p>
        </div>
    </div>

    {(platformStats.Any() ? $@"
    <script>
        // 调试信息
        console.log('ECharts loaded:', typeof echarts !== 'undefined');
        console.log('Platform chart container:', document.getElementById('platformChart'));
        
        if (typeof echarts === 'undefined') {{
            document.getElementById('platformChart').innerHTML = '<p style=""color: #ff6b6b; text-align: center; padding: 20px;"">图表库加载失败，请检查网络连接</p>';
        }} else {{
            try {{
                var platformChart = echarts.init(document.getElementById('platformChart'));
                var platformData = [{string.Join(",", platformStats.Select(p => $"{{value: {p.Count}, name: '{p.Platform}'}}"))}];
                
                console.log('Platform data:', platformData);
                
                var option = {{
                    tooltip: {{
                        trigger: 'item',
                        formatter: '{{b}}<br/>游戏数: {{c}} ({{d}}%)'
                    }},
                    legend: {{
                        orient: 'vertical',
                        right: 10,
                        top: 'center',
                        textStyle: {{ color: '#888' }}
                    }},
                    series: [{{
                        type: 'pie',
                        radius: ['40%', '70%'],
                        center: ['35%', '50%'],
                        avoidLabelOverlap: false,
                        itemStyle: {{
                            borderRadius: 10,
                            borderColor: '#1b263b',
                            borderWidth: 2
                        }},
                        label: {{
                            show: false
                        }},
                        emphasis: {{
                            label: {{
                                show: true,
                                fontSize: 16,
                                fontWeight: 'bold',
                                color: '#fff'
                            }}
                        }},
                        data: platformData,
                        color: ['#00d4ff', '#48cae4', '#90e0ef', '#caf0f8', '#7c3aed']
                    }}]
                }};
                
                platformChart.setOption(option);
                window.addEventListener('resize', function() {{ platformChart.resize(); }});
                console.log('Platform chart initialized successfully');
            }} catch (e) {{
                console.error('Platform chart error:', e);
                document.getElementById('platformChart').innerHTML = '<p style=""color: #ff6b6b; text-align: center; padding: 20px;"">图表渲染失败: ' + e.message + '</p>';
            }}
        }}
    </script>" : "")}
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

        var totalGames = gameRecords.Count;
        var installedGames = localGames.Count;
        var totalSaves = saves.Count;
        var totalSizeGB = localGames.Sum(l => l.SizeBytes) / 1024.0 / 1024.0 / 1024.0;

        // 按平台统计游戏数量
        var platformStats = gameRecords
            .GroupBy(r => r.PlayerPlatform?.Platform?.PlatformName ?? "未知")
            .Select(g => new { Platform = g.Key, Count = g.Count(), TotalHours = Math.Round(g.Sum(r => r.PlaytimeMinutes) / 60.0, 1) })
            .OrderByDescending(p => p.Count)
            .ToList();

        // 生成洞察
        var insights = new List<string>();
        if (totalGames > 0)
        {
            var playedPercent = installedGames > 0 ? Math.Round((double)installedGames / totalGames * 100, 1) : 0;
            insights.Add($"游戏库共有 {totalGames} 款游戏，已安装 {installedGames} 款（{playedPercent}%）");
        }
        if (platformStats.Any())
        {
            insights.Add($"在 {platformStats.First().Platform} 平台拥有最多游戏（{platformStats.First().Count} 款）");
        }
        if (totalSizeGB > 0)
        {
            insights.Add($"本地游戏占用 {totalSizeGB:F1} GB 存储空间");
        }
        if (totalSaves > 0)
        {
            insights.Add($"共有 {totalSaves} 个游戏存档");
        }

        // 暗色系风格
        var darkBg = Color.FromHex("#0d1b2a");
        var darkCard = Color.FromHex("#1b263b");
        var accentCyan = Color.FromHex("#00d4ff");
        var accentBlue = Color.FromHex("#48cae4");
        var textLight = Color.FromHex("#e0e0e0");
        var textMuted = Color.FromHex("#888888");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(darkBg);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(textLight));

                page.Header().PaddingBottom(15).Column(col =>
                {
                    col.Item().Text("📦 游戏库存报告").FontSize(26).SemiBold().FontColor(accentCyan);
                    col.Item().Text($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(10).FontColor(textMuted);
                });

                page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(12);

                    // 统计卡片
                    column.Item().Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(totalGames.ToString()).FontSize(26).Bold().FontColor(accentCyan);
                            c.Item().Text("游戏总数").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(installedGames.ToString()).FontSize(26).Bold().FontColor(accentCyan);
                            c.Item().Text("已安装").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text(totalSaves.ToString()).FontSize(26).Bold().FontColor(accentCyan);
                            c.Item().Text("存档数").FontSize(9).FontColor(textMuted);
                        });
                        row.RelativeItem().Background(darkCard).Padding(12).Column(c =>
                        {
                            c.Item().Text($"{totalSizeGB:F1}").FontSize(26).Bold().FontColor(accentCyan);
                            c.Item().Text("GB 占用").FontSize(9).FontColor(textMuted);
                        });
                    });

                    // 库存洞察
                    column.Item().Background(darkCard).Padding(15).Column(col =>
                    {
                        col.Item().Text("💡 库存概览").FontSize(13).SemiBold().FontColor(accentCyan);
                        col.Item().PaddingTop(8);
                        foreach (var insight in insights)
                        {
                            col.Item().Text($"• {insight}").FontSize(10).FontColor(textLight);
                        }
                    });

                    // 平台分布
                    if (platformStats.Any())
                    {
                        column.Item().Text("📊 平台分布").FontSize(14).SemiBold().FontColor(accentCyan);

                        column.Item().Background(darkCard).Padding(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(50);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Padding(5).Text("平台").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(5).Text("占比").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(5).Text("游戏数").FontSize(9).SemiBold().FontColor(accentCyan);
                                header.Cell().Padding(5).Text("时长").FontSize(9).SemiBold().FontColor(accentCyan);
                            });

                            var maxCount = platformStats.Max(x => x.Count);
                            foreach (var platform in platformStats)
                            {
                                var percent = maxCount > 0 ? Math.Max(1, (int)Math.Round((double)platform.Count / maxCount * 100)) : 1;
                                var remaining = Math.Max(1, 100 - percent);
                                table.Cell().Padding(4).Text(platform.Platform).FontSize(9).FontColor(textLight);
                                table.Cell().Padding(4).Column(col =>
                                {
                                    col.Item().Height(12).Background(Color.FromHex("#2a3f5f")).Row(row =>
                                    {
                                        row.RelativeItem(percent).Background(accentCyan);
                                        row.RelativeItem(remaining);
                                    });
                                });
                                table.Cell().Padding(4).AlignCenter().Text(platform.Count.ToString()).FontSize(9).FontColor(textLight);
                                table.Cell().Padding(4).AlignRight().Text($"{platform.TotalHours}h").FontSize(9).FontColor(accentCyan);
                            }
                        });
                    }

                    // 游戏收藏
                    column.Item().Text("🎮 游戏收藏").FontSize(14).SemiBold().FontColor(accentCyan);

                    column.Item().Background(darkCard).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(55);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Padding(5).Text("游戏").FontSize(9).SemiBold().FontColor(accentCyan);
                            header.Cell().Padding(5).Text("平台").FontSize(9).SemiBold().FontColor(accentCyan);
                            header.Cell().Padding(5).Text("时长").FontSize(9).SemiBold().FontColor(accentCyan);
                            header.Cell().Padding(5).Text("状态").FontSize(9).SemiBold().FontColor(accentCyan);
                        });

                        foreach (var game in gameRecords.Take(30))
                        {
                            var isInstalled = localGames.Any(l => l.GameId == game.GameId);
                            table.Cell().Padding(4).Text(game.Game?.Name ?? "未知").FontSize(8).FontColor(textLight);
                            table.Cell().Padding(4).Text(game.PlayerPlatform?.Platform?.PlatformName ?? "-").FontSize(8).FontColor(textMuted);
                            table.Cell().Padding(4).Text($"{Math.Round(game.PlaytimeMinutes / 60.0, 1)}h").FontSize(8).FontColor(accentCyan);
                            table.Cell().Padding(4).Text(isInstalled ? "已安装" : "-").FontSize(8).FontColor(isInstalled ? Color.FromHex("#4caf50") : textMuted);
                        }
                    });
                });

                page.Footer()
                    .BorderTop(1)
                    .BorderColor(Color.FromHex("#2a3f5f"))
                    .PaddingTop(8)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"PlayLinker").FontSize(8).FontColor(textMuted);
                        row.ConstantItem(80).AlignRight().Text(text =>
                        {
                            text.Span("第 ").FontSize(8).FontColor(textMuted);
                            text.CurrentPageNumber().FontSize(8).FontColor(textMuted);
                            text.Span(" / ").FontSize(8).FontColor(textMuted);
                            text.TotalPages().FontSize(8).FontColor(textMuted);
                            text.Span(" 页").FontSize(8).FontColor(textMuted);
                        });
                    });
            });
        });

        return document.GeneratePdf();
    }
}
