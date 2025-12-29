using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayLinker.Data;
using PlayLinker.Models.Entities;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// 价格监控后台服务
/// 每天定时获取 Steam 游戏价格并记录到数据库
/// </summary>
public class PriceMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PriceMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private const int STEAM_PLATFORM_ID = 1; // Steam 平台 ID

    public PriceMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<PriceMonitoringService> logger,
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
        _logger.LogInformation("价格监控服务已启动");

        // 获取配置的执行时间（默认凌晨2点）
        var scheduledHour = _configuration.GetValue<int>("PriceMonitoring:ScheduledHour", 2);
        var scheduledMinute = _configuration.GetValue<int>("PriceMonitoring:ScheduledMinute", 0);
        
        // 启动时如果还没到执行时间，先等待；如果已经过了执行时间，立即执行一次
        var now = DateTime.Now;
        var todayRunTime = now.Date.AddHours(scheduledHour).AddMinutes(scheduledMinute);
        var nextRunTime = now < todayRunTime ? todayRunTime : todayRunTime.AddDays(1);
        
        _logger.LogInformation("价格监控服务配置: 每天 {Hour}:{Minute:D2} 执行", scheduledHour, scheduledMinute);
        _logger.LogInformation("下次价格更新将在 {NextRunTime} 执行", nextRunTime);

        // 如果启动时已经过了今天的执行时间，立即执行一次（确保今天有记录）
        if (now >= todayRunTime)
        {
            _logger.LogInformation("启动时检测到已过今日执行时间，立即执行一次价格更新");
            try
            {
                await UpdateSteamPricesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动时执行价格更新失败");
            }
            // 计算到明天执行时间的延迟
            nextRunTime = todayRunTime.AddDays(1);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = nextRunTime - DateTime.Now;
                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogInformation("等待 {Delay} 后执行价格更新", delay);
                    await Task.Delay(delay, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested)
                    break;

                // 执行价格更新
                await UpdateSteamPricesAsync(stoppingToken);

                // 计算下次执行时间
                nextRunTime = DateTime.Now.Date.AddDays(1).AddHours(scheduledHour).AddMinutes(scheduledMinute);
                _logger.LogInformation("下次价格更新将在 {NextRunTime} 执行", nextRunTime);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("价格监控服务已停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "价格监控服务执行时发生错误");
                // 发生错误后等待 1 小时再重试，但不超过下次正常执行时间
                var retryDelay = TimeSpan.FromHours(1);
                var timeUntilNextScheduled = nextRunTime - DateTime.Now;
                var actualDelay = retryDelay < timeUntilNextScheduled ? retryDelay : timeUntilNextScheduled;
                await Task.Delay(actualDelay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// 更新 Steam 游戏价格
    /// </summary>
    private async Task UpdateSteamPricesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始执行 Steam 价格更新任务");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            // 获取所有 Steam 平台的游戏映射
            var steamGames = await context.GamePlatforms
                .Where(gp => gp.PlatformId == STEAM_PLATFORM_ID)
                .Include(gp => gp.Game)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("找到 {Count} 个 Steam 游戏需要更新价格", steamGames.Count);

            int successCount = 0;
            int failCount = 0;
            int skipCount = 0;

            // 批量处理，每次处理 10 个游戏（避免 API 限流）
            const int batchSize = 10;
            for (int i = 0; i < steamGames.Count; i += batchSize)
            {
                var batch = steamGames.Skip(i).Take(batchSize).ToList();

                foreach (var gamePlatform in batch)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        if (!int.TryParse(gamePlatform.PlatformGameId, out int appId))
                        {
                            _logger.LogWarning("无法解析 Steam AppID: {PlatformGameId}, GameId={GameId}",
                                gamePlatform.PlatformGameId, gamePlatform.GameId);
                            skipCount++;
                            continue;
                        }

                        // 检查今天是否已经记录过价格（使用UTC日期确保一致性）
                        var todayUtc = DateTime.UtcNow.Date;
                        var existingRecord = await context.PriceHistories
                            .Where(ph => ph.GameId == gamePlatform.GameId 
                                && ph.PlatformId == STEAM_PLATFORM_ID 
                                && ph.RecordDate.Date == todayUtc)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (existingRecord != null)
                        {
                            _logger.LogDebug("游戏 {GameId} (AppID={AppId}) 今天已记录价格（记录时间: {RecordTime}），跳过", 
                                gamePlatform.GameId, appId, existingRecord.RecordDate);
                            skipCount++;
                            continue;
                        }

                        // 调用 Steam API 获取价格
                        var priceData = await GetSteamPriceAsync(httpClient, appId, cancellationToken);

                        if (priceData == null)
                        {
                            _logger.LogWarning("无法获取游戏 {GameId} (AppID={AppId}) 的价格信息", 
                                gamePlatform.GameId, appId);
                            failCount++;
                            continue;
                        }

                        // 保存价格记录
                        var priceHistory = new PriceHistory
                        {
                            GameId = gamePlatform.GameId,
                            PlatformId = STEAM_PLATFORM_ID,
                            CurrentPrice = priceData.Final / 100.0m, // Steam API 返回的是分为单位
                            OriginalPrice = priceData.Initial / 100.0m,
                            DiscountRate = priceData.DiscountPercent,
                            IsDiscount = priceData.DiscountPercent > 0,
                            RecordDate = DateTime.UtcNow
                        };

                        context.PriceHistories.Add(priceHistory);
                        await context.SaveChangesAsync(cancellationToken);

                        _logger.LogDebug("已记录游戏 {GameId} (AppID={AppId}) 的价格: {CurrentPrice} CNY (折扣: {Discount}%)",
                            gamePlatform.GameId, appId, priceHistory.CurrentPrice, priceData.DiscountPercent);

                        successCount++;

                        // 检查价格变化并创建通知
                        await CheckPriceChangeAndNotifyAsync(context, gamePlatform.GameId, priceHistory, cancellationToken);

                        // 避免请求过快，每个请求之间延迟 200ms
                        await Task.Delay(200, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理游戏 {GameId} (AppID={AppId}) 时发生错误",
                            gamePlatform.GameId, gamePlatform.PlatformGameId);
                        failCount++;
                    }
                }

                // 批次之间延迟 1 秒
                if (i + batchSize < steamGames.Count)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }

            var updateSummary = new
            {
                SuccessCount = successCount,
                FailCount = failCount,
                SkipCount = skipCount,
                TotalProcessed = steamGames.Count,
                UpdateTime = DateTime.UtcNow
            };

            _logger.LogInformation("Steam 价格更新完成: 成功={SuccessCount}, 失败={FailCount}, 跳过={SkipCount}, 总计={Total}",
                successCount, failCount, skipCount, steamGames.Count);

            // 记录更新统计到数据库（可选，用于前端显示）
            try
            {
                // 这里可以添加一个价格更新日志表来记录每次更新的统计信息
                // 目前先记录到日志中
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "记录价格更新统计时发生错误");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 Steam 价格时发生错误");
        }
    }

    /// <summary>
    /// 从 Steam API 获取游戏价格
    /// </summary>
    private async Task<SteamPriceInfo?> GetSteamPriceAsync(HttpClient httpClient, int appId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Steam API 请求失败: StatusCode={StatusCode}, AppID={AppId}",
                    response.StatusCode, appId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty(appId.ToString(), out var appData))
            {
                if (appData.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (appData.TryGetProperty("data", out var data))
                    {
                        // 检查是否为免费游戏
                        if (data.TryGetProperty("is_free", out var isFree) && isFree.GetBoolean())
                        {
                            return new SteamPriceInfo
                            {
                                Initial = 0,
                                Final = 0,
                                DiscountPercent = 0
                            };
                        }

                        // 解析价格信息
                        if (data.TryGetProperty("price_overview", out var priceData))
                        {
                            var initial = priceData.TryGetProperty("initial", out var init) 
                                ? (init.ValueKind == JsonValueKind.Number ? init.GetInt32() : 
                                   int.TryParse(init.GetString(), out var i) ? i : 0) 
                                : 0;
                            var final = priceData.TryGetProperty("final", out var fin) 
                                ? (fin.ValueKind == JsonValueKind.Number ? fin.GetInt32() : 
                                   int.TryParse(fin.GetString(), out var f) ? f : 0) 
                                : 0;
                            var discount = priceData.TryGetProperty("discount_percent", out var disc) 
                                ? (disc.ValueKind == JsonValueKind.Number ? disc.GetInt32() : 
                                   int.TryParse(disc.GetString(), out var d) ? d : 0) 
                                : 0;

                            return new SteamPriceInfo
                            {
                                Initial = initial,
                                Final = final,
                                DiscountPercent = discount
                            };
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Steam 价格时发生错误: AppID={AppId}", appId);
            return null;
        }
    }

    /// <summary>
    /// 检查价格变化并创建通知
    /// </summary>
    private async Task CheckPriceChangeAndNotifyAsync(
        PlayLinkerDbContext context,
        long gameId,
        PriceHistory newPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            // 获取昨天的价格记录（用于价格下降提醒）
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var yesterdayPrice = await context.PriceHistories
                .Where(ph => ph.GameId == gameId 
                    && ph.PlatformId == STEAM_PLATFORM_ID 
                    && ph.RecordDate.Date == yesterday)
                .OrderByDescending(ph => ph.RecordDate)
                .FirstOrDefaultAsync(cancellationToken);

            // 获取游戏信息
            var game = await context.Games.FindAsync(new object[] { gameId }, cancellationToken);
            if (game == null) return;

            // 获取所有订阅了该游戏价格提醒的用户
            var subscriptions = await context.PriceAlertSubscriptions
                .Where(s => s.GameId == gameId 
                    && s.PlatformId == STEAM_PLATFORM_ID 
                    && s.IsActive == true)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                bool shouldNotify = false;
                string notificationTitle = "";
                string notificationContent = "";
                string alertType = "";

                // 检查今天是否已经发送过通知（避免重复通知）
                var today = DateTime.UtcNow.Date;
                var todayAlert = await context.PriceAlertLogs
                    .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                        && l.AlertTime.HasValue 
                        && l.AlertTime.Value.Date == today)
                    .FirstOrDefaultAsync(cancellationToken);

                if (todayAlert != null)
                {
                    _logger.LogDebug("用户 {UserId} 的订阅 {SubscriptionId} 今天已发送过通知，跳过",
                        subscription.UserId, subscription.SubscriptionId);
                    continue;
                }

                // 检查是否满足目标价格条件（使用price_history中的currentPrice）
                if (subscription.TargetPrice.HasValue && newPrice.CurrentPrice <= subscription.TargetPrice.Value)
                {
                    // 检查之前是否已经达到过目标价格（避免重复通知）
                    var previousAlert = await context.PriceAlertLogs
                        .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                            && l.AlertType == "target_price")
                        .Include(l => l.Price)
                        .OrderByDescending(l => l.AlertTime)
                        .FirstOrDefaultAsync(cancellationToken);

                    // 如果之前已经通知过，且价格没有进一步下降，则不通知
                    if (previousAlert != null && previousAlert.Price != null 
                        && previousAlert.Price.CurrentPrice <= newPrice.CurrentPrice)
                    {
                        continue;
                    }

                    shouldNotify = true;
                    alertType = "target_price";
                    notificationTitle = $"价格提醒：{game.Name}";
                    notificationContent = $"游戏 {game.Name} 的价格已降至 ¥{newPrice.CurrentPrice:F2}，低于您设置的目标价格 ¥{subscription.TargetPrice.Value:F2}。";
                    
                    if (newPrice.IsDiscount && newPrice.DiscountRate > 0)
                    {
                        notificationContent += $" 当前折扣 {newPrice.DiscountRate}%，原价 ¥{newPrice.OriginalPrice:F2}。";
                    }
                }
                // 检查是否满足目标折扣条件（使用price_history中的discountRate）
                else if (subscription.TargetDiscount.HasValue && newPrice.DiscountRate >= subscription.TargetDiscount.Value)
                {
                    // 检查之前是否已经达到过目标折扣
                    var previousAlert = await context.PriceAlertLogs
                        .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                            && l.AlertType == "target_discount")
                        .Include(l => l.Price)
                        .OrderByDescending(l => l.AlertTime)
                        .FirstOrDefaultAsync(cancellationToken);

                    // 如果之前已经通知过，且折扣没有进一步增加，则不通知
                    if (previousAlert != null && previousAlert.Price != null 
                        && previousAlert.Price.DiscountRate >= newPrice.DiscountRate)
                    {
                        continue;
                    }

                    shouldNotify = true;
                    alertType = "target_discount";
                    notificationTitle = $"折扣提醒：{game.Name}";
                    notificationContent = $"游戏 {game.Name} 当前折扣 {newPrice.DiscountRate}%，达到您设置的目标折扣 {subscription.TargetDiscount.Value}%。";
                    notificationContent += $" 当前价格：¥{newPrice.CurrentPrice:F2}，原价：¥{newPrice.OriginalPrice:F2}。";
                }
                // 检查价格是否下降（即使没有设置目标）- 使用price_history中的价格数据
                // 注意：只有在有昨天的价格记录时才检查价格下降
                else if (yesterdayPrice != null && newPrice.CurrentPrice < yesterdayPrice.CurrentPrice && newPrice.IsDiscount)
                {
                    var priceDrop = yesterdayPrice.CurrentPrice - newPrice.CurrentPrice;
                    var dropPercent = (priceDrop / yesterdayPrice.CurrentPrice) * 100;
                    
                    // 价格下降超过 5% 时通知
                    if (dropPercent >= 5)
                    {
                        shouldNotify = true;
                        alertType = "price_drop";
                        notificationTitle = $"价格下降：{game.Name}";
                        notificationContent = $"游戏 {game.Name} 价格下降 {dropPercent:F1}%（¥{yesterdayPrice.CurrentPrice:F2} → ¥{newPrice.CurrentPrice:F2}），";
                        notificationContent += $"当前折扣 {newPrice.DiscountRate}%，原价 ¥{newPrice.OriginalPrice:F2}。";
                    }
                }

                if (shouldNotify)
                {
                    // 获取用户信息（用于发送邮件）
                    var user = await context.Users.FindAsync(new object[] { subscription.UserId }, cancellationToken);
                    
                    // 创建通知到消息中心
                    var notification = new NotificationCenter
                    {
                        UserId = subscription.UserId,
                        SourceModule = "price_alert",
                        Title = notificationTitle,
                        Content = notificationContent,
                        NotificationType = "info",
                        IsRead = false,
                        RelatedId = subscription.SubscriptionId,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.NotificationCenters.Add(notification);
                    await context.SaveChangesAsync(cancellationToken);

                    // 创建价格提醒日志
                    var alertLog = new PriceAlertLog
                    {
                        SubscriptionId = subscription.SubscriptionId,
                        PriceId = newPrice.PriceId,
                        AlertType = alertType,
                        AlertTime = DateTime.UtcNow,
                        NotificationId = notification.NotificationId
                    };

                    context.PriceAlertLogs.Add(alertLog);
                    await context.SaveChangesAsync(cancellationToken);

                    // 发送邮件提醒（如果用户有邮箱）
                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            
                            await emailService.SendPriceAlertAsync(
                                to: user.Email,
                                username: user.Username ?? "用户",
                                gameName: game.Name ?? "游戏",
                                alertType: alertType,
                                currentPrice: newPrice.CurrentPrice,
                                originalPrice: newPrice.OriginalPrice,
                                discountRate: newPrice.DiscountRate,
                                targetPrice: subscription.TargetPrice,
                                targetDiscount: subscription.TargetDiscount
                            );
                            
                            _logger.LogInformation("已为用户 {UserId} 发送价格提醒邮件: Email={Email}, GameId={GameId}",
                                subscription.UserId, user.Email, gameId);
                        }
                        catch (Exception emailEx)
                        {
                            // 邮件发送失败不影响通知创建
                            _logger.LogWarning(emailEx, "为用户 {UserId} 发送价格提醒邮件失败，但通知已创建",
                                subscription.UserId);
                        }
                    }

                    // 提醒后将订阅设为非active（仅针对目标价格和目标折扣提醒）
                    if (alertType == "target_price" || alertType == "target_discount")
                    {
                        subscription.IsActive = false;
                        context.PriceAlertSubscriptions.Update(subscription);
                        await context.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("已将订阅 {SubscriptionId} 设为非active: GameId={GameId}, UserId={UserId}",
                            subscription.SubscriptionId, gameId, subscription.UserId);
                    }

                    _logger.LogInformation("已为用户 {UserId} 创建价格提醒通知: GameId={GameId}, Price={Price}, Discount={Discount}%, AlertType={AlertType}",
                        subscription.UserId, gameId, newPrice.CurrentPrice, newPrice.DiscountRate, alertType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查价格变化并创建通知时发生错误: GameId={GameId}", gameId);
        }
    }

    /// <summary>
    /// Steam 价格信息
    /// </summary>
    private class SteamPriceInfo
    {
        public int Initial { get; set; } // 原价（分）
        public int Final { get; set; } // 现价（分）
        public int DiscountPercent { get; set; } // 折扣百分比
    }
}

