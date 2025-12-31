using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.Entities;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// 游戏时长历史记录服务
/// 功能：定期抓取 Steam 用户的游戏时长快照，用于生成趋势图
/// </summary>
public class PlaytimeHistoryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlaytimeHistoryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    
    private const int STEAM_PLATFORM_ID = 1;

    public PlaytimeHistoryService(
        IServiceProvider serviceProvider,
        ILogger<PlaytimeHistoryService> logger,
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
        // [调试模式] 启动服务后立即等待 10秒 运行一次
        _logger.LogInformation("⏳ 游戏时长历史记录服务已启动，将在 10秒 后执行首次数据同步...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        try 
        {
            await RecordPlaytimeSnapshotAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 首次执行时长快照失败");
        }

        // [常规逻辑] 每天凌晨 2:00 执行
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            var delay = nextRun - now;
            
            _logger.LogInformation("📅 下次时长快照任务将在 {NextRun} 执行，等待时长: {Delay}", nextRun, delay);

            try 
            {
                await Task.Delay(delay, stoppingToken);
                await RecordPlaytimeSnapshotAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 执行定时任务时发生异常");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task RecordPlaytimeSnapshotAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 开始执行游戏时长快照任务...");
        
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenEncryptionService>();
        var client = _httpClientFactory.CreateClient();
        
        var today = DateTime.UtcNow.Date;

        var bindings = await context.UserPlatformBindings
            .Where(b => b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true)
            .ToListAsync(stoppingToken);

        if (!bindings.Any())
        {
            _logger.LogWarning("⚠️ 没有找到任何绑定 Steam 的用户，跳过本次任务。");
            return;
        }

        int processedUsers = 0;
        int totalRecordsSaved = 0;

        foreach (var binding in bindings)
        {
            if (string.IsNullOrEmpty(binding.PlatformUserId)) continue;

            string steamKey = "";
            
            // 1. 解密 Key
            if (!string.IsNullOrEmpty(binding.AccessToken))
            {
                try 
                {
                    steamKey = tokenService.DecryptToken(binding.AccessToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("❌ 用户 {UserId} Token 解密失败: {Msg}", binding.UserId, ex.Message);
                }
            }

            // 2. [关键修复] 清理 Key 和 ID 的格式（去空格、换行）
            steamKey = steamKey?.Trim() ?? "";
            string steamId = binding.PlatformUserId?.Trim() ?? "";

            // 3. 最后的后备方案：使用全局配置 Key
            if (string.IsNullOrEmpty(steamKey))
            {
                steamKey = _configuration["SteamAPI:Key"]?.Trim() ?? "";
            }

            if (string.IsNullOrEmpty(steamKey) || steamKey.Contains("YOUR_API_KEY"))
            {
                _logger.LogError("❌ 用户 {UserId} 未配置有效的 Steam API Key，跳过。", binding.UserId);
                continue;
            }

            _logger.LogInformation("正在处理用户 {UserId} (SteamID: {SteamId})...", binding.UserId, steamId);

            try
            {
                // 4. [关键修复] 使用 Uri.EscapeDataString 安全构建 URL
                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(steamKey)}&steamid={Uri.EscapeDataString(steamId)}&include_appinfo=false&include_played_free_games=true";
                
                var response = await client.GetAsync(url, stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("❌ Steam API 请求失败: {Code}", response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(stoppingToken);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("response", out var responseEl) || 
                    !responseEl.TryGetProperty("games", out var gamesEl))
                {
                    _logger.LogInformation("用户 {UserId} 的 Steam 库为空或设为私密。", binding.UserId);
                    continue; 
                }

                var knownGamesMap = await context.GamePlatforms
                    .Where(gp => gp.PlatformId == STEAM_PLATFORM_ID)
                    .ToDictionaryAsync(gp => gp.PlatformGameId, gp => gp.GameId, stoppingToken);

                int gamesMatched = 0;

                foreach (var gameItem in gamesEl.EnumerateArray())
                {
                    var appId = gameItem.GetProperty("appid").GetInt32();
                    var playtimeForever = gameItem.GetProperty("playtime_forever").GetInt32();
                    
                    var playtime2Weeks = 0;
                    if (gameItem.TryGetProperty("playtime_2weeks", out var p2wEl))
                    {
                        playtime2Weeks = p2wEl.GetInt32();
                    }

                    if (playtimeForever > 0)
                    {
                        string appIdStr = appId.ToString();
                        if (knownGamesMap.TryGetValue(appIdStr, out var localGameId))
                        {
                            gamesMatched++;
                            var existingHistory = await context.UserPlaytimeHistories
                                .FirstOrDefaultAsync(h => 
                                    h.UserId == binding.UserId && 
                                    h.GameId == localGameId && 
                                    h.PlatformId == STEAM_PLATFORM_ID && 
                                    h.RecordDate == today, stoppingToken);

                            if (existingHistory == null)
                            {
                                context.UserPlaytimeHistories.Add(new UserPlaytimeHistory
                                {
                                    UserId = binding.UserId,
                                    GameId = localGameId,
                                    PlatformId = STEAM_PLATFORM_ID,
                                    PlaytimeForever = playtimeForever,
                                    Playtime2Weeks = playtime2Weeks,
                                    RecordDate = today,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                existingHistory.PlaytimeForever = playtimeForever;
                                existingHistory.Playtime2Weeks = playtime2Weeks;
                                existingHistory.CreatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
                processedUsers++;
                totalRecordsSaved += gamesMatched;
                
                _logger.LogInformation("✅ 用户 {UserId} 处理完成，入库 {Matched} 款游戏。", binding.UserId, gamesMatched);
                await Task.Delay(1000, stoppingToken); // 简单限流
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 处理用户 {UserId} 时发生错误", binding.UserId);
            }
        }

        _logger.LogInformation("🎉 每日时长快照任务结束。共更新 {Users} 个用户，{Records} 条记录。", processedUsers, totalRecordsSaved);
    }
}