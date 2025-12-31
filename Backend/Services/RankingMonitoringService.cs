using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.Entities;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// 排行榜监控服务
/// 每周更新一次游戏排行榜数据（仅限本地已存在的游戏）
/// </summary>
public class RankingMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RankingMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private const int STEAM_PLATFORM_ID = 1;

    public RankingMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<RankingMonitoringService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("游戏排行榜监控服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();

                // 1. 检查上次更新时间
                var lastUpdate = await context.GameRankings
                    .OrderByDescending(r => r.UpdatedAt)
                    .Select(r => r.UpdatedAt)
                    .FirstOrDefaultAsync(stoppingToken);

                // 如果从未更新过，或距离上次更新超过7天，则执行更新
                if (lastUpdate == default || (DateTime.UtcNow - lastUpdate).TotalDays >= 7)
                {
                    _logger.LogInformation("检测到排行榜数据过期（上次更新: {LastUpdate}），开始更新...", lastUpdate);
                    await UpdateRankingsAsync(context, stoppingToken);
                }
                else
                {
                    var nextRun = lastUpdate.AddDays(7);
                    _logger.LogInformation("排行榜数据依然新鲜，下次更新时间: {NextRun}", nextRun);
                }

                // 每天检查一次
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "排行榜更新任务执行失败");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task UpdateRankingsAsync(PlayLinkerDbContext context, CancellationToken stoppingToken)
    {
        var steamApiKey = _configuration["SteamAPI:Key"];
        if (string.IsNullOrEmpty(steamApiKey) || steamApiKey.Contains("YOUR_API_KEY"))
        {
            _logger.LogWarning("未配置 Steam API Key，跳过排行榜更新");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var requestUrl = $"https://api.steampowered.com/ISteamChartsService/GetMostPlayedGames/v1/?key={steamApiKey}&count=100";
        
        try 
        {
            var response = await client.GetAsync(requestUrl, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Steam API 请求失败: {Status}", response.StatusCode);
                return;
            }

            var jsonString = await response.Content.ReadAsStringAsync(stoppingToken);
            using var doc = JsonDocument.Parse(jsonString);

            if (!doc.RootElement.TryGetProperty("response", out var responseEl) || 
                !responseEl.TryGetProperty("ranks", out var ranksEl))
            {
                _logger.LogWarning("Steam API 返回数据格式不正确");
                return;
            }

            // 1. 获取 Steam 排名
            var steamRankings = new List<(int Rank, int SteamAppId, int Peak)>();
            foreach (var item in ranksEl.EnumerateArray())
            {
                var rank = item.GetProperty("rank").GetInt32();
                var appId = item.GetProperty("appid").GetInt32();
                var peak = item.GetProperty("peak_in_game").GetInt32();
                steamRankings.Add((rank, appId, peak));
            }

            // 2. [核心] 查询本地游戏映射 (不插入新游戏)
            var steamAppIds = steamRankings.Select(x => x.SteamAppId.ToString()).ToList();
            var matchedGames = await context.GamePlatforms
                .Where(gp => gp.PlatformId == STEAM_PLATFORM_ID && steamAppIds.Contains(gp.PlatformGameId))
                .Select(gp => new { gp.GameId, gp.PlatformGameId })
                .ToListAsync(stoppingToken);

            var appIdToGameIdMap = matchedGames.ToDictionary(k => k.PlatformGameId, v => v.GameId);

            // 3. 更新排行榜
            var now = DateTime.UtcNow;
            var existingRankings = await context.GameRankings.ToListAsync(stoppingToken);

            // 3.1 归档
            foreach (var r in existingRankings)
            {
                r.LastWeekRank = r.CurrentRank;
                r.CurrentRank = null; 
                r.UpdatedAt = now;
            }

            // 3.2 匹配更新
            int updateCount = 0;
            foreach (var item in steamRankings)
            {
                // 如果本地没有，跳过
                if (!appIdToGameIdMap.TryGetValue(item.SteamAppId.ToString(), out var localGameId))
                {
                    continue;
                }

                var existing = existingRankings.FirstOrDefault(r => r.GameId == localGameId);
                if (existing != null)
                {
                    existing.LastWeekRank = existing.CurrentRank > 0 ? existing.CurrentRank : null;
                    existing.CurrentRank = item.Rank;
                    existing.PeakPlayers = item.Peak;
                    existing.UpdatedAt = now;
                }
                else
                {
                    context.GameRankings.Add(new GameRanking
                    {
                        GameId = localGameId,
                        CurrentRank = item.Rank,
                        LastWeekRank = null,
                        PeakPlayers = item.Peak,
                        UpdatedAt = now
                    });
                }
                updateCount++;
            }

            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("排行榜更新完成，匹配并更新了 {Count} 个本地游戏。", updateCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新排行榜逻辑发生异常");
            throw;
        }
    }
}