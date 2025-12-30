using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayLinker.Data;
using PlayLinker.Models.Entities;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// 家长监管监控后台服务
/// 定期检查监管规则违规情况并发送提醒
/// </summary>
public class ParentalMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ParentalMonitoringService> _logger;
    private readonly IConfiguration _configuration;

    public ParentalMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<ParentalMonitoringService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("家长监管监控服务已启动");

        // 获取配置的执行间隔（默认每30分钟检查一次）
        var checkIntervalMinutes = _configuration.GetValue<int>("ParentalMonitoring:CheckIntervalMinutes", 30);
        var checkInterval = TimeSpan.FromMinutes(checkIntervalMinutes);

        _logger.LogInformation("家长监管监控服务配置: 每 {IntervalMinutes} 分钟检查一次", checkIntervalMinutes);

        // 启动时立即执行一次检查
        try
        {
            await CheckParentalRulesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动时执行家长监管检查失败");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                await CheckParentalRulesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("家长监管监控服务已停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "家长监管监控服务执行时发生错误");
                // 发生错误后等待一段时间再重试
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// 检查所有活跃的家长监管规则
    /// </summary>
    private async Task CheckParentalRulesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始执行家长监管规则检查任务");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();

        try
        {
            // 获取所有活跃的监管规则
            var activeRules = await context.ParentalControlRules
                .Where(r => r.IsActive == true)
                .Include(r => r.ChildUser)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("找到 {Count} 个活跃的监管规则需要检查", activeRules.Count);

            int checkedCount = 0;
            int violationCount = 0;

            foreach (var rule in activeRules)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    checkedCount++;
                    var hasViolation = await CheckRuleViolationAsync(context, rule, cancellationToken);
                    if (hasViolation)
                    {
                        violationCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "检查规则 {RuleId} (类型: {RuleType}, 子账户: {ChildUserId}) 时发生错误",
                        rule.RuleId, rule.RuleType, rule.ChildUserId);
                }
            }

            _logger.LogInformation("家长监管规则检查完成: 检查={CheckedCount}, 违规={ViolationCount}, 总计={Total}",
                checkedCount, violationCount, activeRules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查家长监管规则时发生错误");
        }
    }

    /// <summary>
    /// 检查单个规则是否违规（公共方法，允许从外部调用）
    /// </summary>
    public async Task<bool> CheckSingleRuleAsync(long ruleId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();

        try
        {
            var rule = await context.ParentalControlRules
                .Include(r => r.ChildUser)
                .FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);

            if (rule == null)
            {
                _logger.LogWarning("规则 {RuleId} 不存在", ruleId);
                return false;
            }

            if (rule.IsActive != true)
            {
                _logger.LogDebug("规则 {RuleId} 未激活，跳过检查", ruleId);
                return false;
            }

            return await CheckRuleViolationAsync(context, rule, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查规则 {RuleId} 时发生错误", ruleId);
            return false;
        }
    }

    /// <summary>
    /// 检查单个规则是否违规
    /// </summary>
    private async Task<bool> CheckRuleViolationAsync(
        PlayLinkerDbContext context,
        ParentalControlRule rule,
        CancellationToken cancellationToken)
    {
        try
        {
            // 解析规则值
            var ruleValue = JsonSerializer.Deserialize<JsonElement>(rule.RuleValue);
            var now = DateTime.UtcNow;
            var today = now.Date;

            _logger.LogDebug("检查规则 {RuleId} (类型: {RuleType}, 子账户: {ChildUserId}), 规则值: {RuleValue}", 
                rule.RuleId, rule.RuleType, rule.ChildUserId, rule.RuleValue);

            // 检查今天是否已经发送过该规则的提醒（避免重复通知）
            // 只查询需要的字段，避免查询不存在的 severity 列
            var todayAlert = await context.ParentalAlertLogs
                .Where(l => l.RuleId == rule.RuleId
                    && l.AlertTime.HasValue
                    && l.AlertTime.Value.Date == today)
                .Select(l => new { l.AlertId, l.RuleId, l.AlertTime })
                .FirstOrDefaultAsync(cancellationToken);

            if (todayAlert != null)
            {
                _logger.LogDebug("规则 {RuleId} 今天已发送过提醒，跳过", rule.RuleId);
                return false;
            }

            bool hasViolation = false;
            string violationType = "";
            Dictionary<string, object> violationDetails = new();

            // 根据规则类型进行检查
            switch (rule.RuleType)
            {
                case "playtime_daily_limit":
                    hasViolation = await CheckPlaytimeDailyLimitAsync(context, rule, ruleValue, violationDetails, cancellationToken);
                    violationType = "playtime_daily_limit";
                    break;

                case "playtime_curfew":
                    hasViolation = await CheckPlaytimeCurfewAsync(context, rule, ruleValue, violationDetails, cancellationToken);
                    violationType = "playtime_curfew";
                    break;

                case "game_restriction":
                    hasViolation = await CheckGameRestrictionAsync(context, rule, ruleValue, violationDetails, cancellationToken);
                    violationType = "game_restriction";
                    break;

                case "age_restriction":
                    hasViolation = await CheckAgeRestrictionAsync(context, rule, ruleValue, violationDetails, cancellationToken);
                    violationType = "age_restriction";
                    break;

                default:
                    _logger.LogWarning("未知的规则类型: {RuleType}, 规则ID: {RuleId}", rule.RuleType, rule.RuleId);
                    return false;
            }

            if (hasViolation)
            {
                // 获取家长用户信息
                var relationship = await context.ParentalControlRelationships
                    .Include(r => r.ParentUser)
                    .FirstOrDefaultAsync(r => r.ChildUserId == rule.ChildUserId, cancellationToken);

                if (relationship == null)
                {
                    _logger.LogWarning("未找到子账户 {ChildUserId} 的监管关系", rule.ChildUserId);
                    return false;
                }

                var parentUser = relationship.ParentUser;
                var childUser = rule.ChildUser;

                // 创建通知
                string notificationTitle = "";
                string notificationContent = "";

                if (violationType == "playtime_daily_limit")
                {
                    notificationTitle = $"游戏时长提醒：{childUser.Username}";
                    var limitMinutes = ruleValue.TryGetProperty("limitMinutes", out var limit) ? limit.GetInt32() : 0;
                    var currentMinutes = violationDetails.ContainsKey("currentMinutes") 
                        ? (int)violationDetails["currentMinutes"] 
                        : 0;
                    notificationContent = $"您的孩子 {childUser.Username} 今日游戏时长已达到 {currentMinutes} 分钟，超过设定的限制 {limitMinutes} 分钟。";
                }
                else if (violationType == "playtime_curfew")
                {
                    notificationTitle = $"宵禁提醒：{childUser.Username}";
                    var startTime = ruleValue.TryGetProperty("startTime", out var start) ? start.GetString() : "";
                    var endTime = ruleValue.TryGetProperty("endTime", out var end) ? end.GetString() : "";
                    notificationContent = $"您的孩子 {childUser.Username} 在宵禁时间段（{startTime} - {endTime}）内仍在游戏。";
                }
                else if (violationType == "game_restriction")
                {
                    notificationTitle = $"游戏限制提醒：{childUser.Username}";
                    var blockedGameNames = violationDetails.ContainsKey("blockedGameNames") 
                        ? (List<string>)violationDetails["blockedGameNames"] 
                        : new List<string>();
                    notificationContent = $"您的孩子 {childUser.Username} 的游戏库中包含被限制的游戏：{string.Join("、", blockedGameNames)}。";
                }
                else if (violationType == "age_restriction")
                {
                    notificationTitle = $"年龄限制提醒：{childUser.Username}";
                    var maxAgeRating = ruleValue.TryGetProperty("maxAgeRating", out var maxAge) ? maxAge.GetInt32() : 0;
                    var violatingGameNames = violationDetails.ContainsKey("violatingGameNames") 
                        ? (List<string>)violationDetails["violatingGameNames"] 
                        : new List<string>();
                    notificationContent = $"您的孩子 {childUser.Username} 的游戏库中包含超出年龄分级（{maxAgeRating}+）的游戏：{string.Join("、", violatingGameNames)}。";
                }

                var notification = new NotificationCenter
                {
                    UserId = parentUser.UserId,
                    SourceModule = "parental_control",
                    Title = notificationTitle,
                    Content = notificationContent,
                    NotificationType = "warning",
                    IsRead = false,
                    RelatedId = rule.RuleId,
                    CreatedAt = DateTime.UtcNow
                };

                context.NotificationCenters.Add(notification);
                await context.SaveChangesAsync(cancellationToken);

                // 创建违规日志
                var alertLog = new ParentalAlertLog
                {
                    RuleId = rule.RuleId,
                    ChildUserId = rule.ChildUserId,
                    ViolationDetails = JsonSerializer.Serialize(violationDetails),
                    AlertTime = DateTime.UtcNow,
                    NotificationId = notification.NotificationId
                    // 注意：Severity 字段在数据库表中不存在，已标记为 NotMapped
                    // Severity = "warning"
                };

                context.ParentalAlertLogs.Add(alertLog);
                await context.SaveChangesAsync(cancellationToken);

                // 发送邮件提醒
                if (!string.IsNullOrWhiteSpace(parentUser.Email))
                {
                    try
                    {
                        using var emailScope = _serviceProvider.CreateScope();
                        var emailService = emailScope.ServiceProvider.GetRequiredService<IEmailService>();

                        await emailService.SendParentalAlertAsync(
                            to: parentUser.Email,
                            username: parentUser.Username ?? "家长",
                            childUsername: childUser.Username ?? "孩子",
                            ruleType: rule.RuleType,
                            violationDetails: violationDetails
                        );

                        _logger.LogInformation("已为家长 {ParentUserId} 发送家长监管提醒邮件: Email={Email}, 规则ID={RuleId}",
                            parentUser.UserId, parentUser.Email, rule.RuleId);
                    }
                    catch (Exception emailEx)
                    {
                        // 邮件发送失败不影响通知创建
                        _logger.LogWarning(emailEx, "为家长 {ParentUserId} 发送家长监管提醒邮件失败，但通知已创建",
                            parentUser.UserId);
                    }
                }

                _logger.LogInformation("已为家长 {ParentUserId} 创建家长监管提醒: 规则ID={RuleId}, 子账户={ChildUserId}, 违规类型={ViolationType}",
                    parentUser.UserId, rule.RuleId, rule.ChildUserId, violationType);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查规则违规时发生错误: 规则ID={RuleId}", rule.RuleId);
            return false;
        }
    }

    /// <summary>
    /// 检查每日游戏时长限制
    /// </summary>
    private async Task<bool> CheckPlaytimeDailyLimitAsync(
        PlayLinkerDbContext context,
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ruleValue.TryGetProperty("limitMinutes", out var limitProp))
            {
                _logger.LogWarning("规则 {RuleId} 缺少 limitMinutes 属性", rule.RuleId);
                return false;
            }

            var limitMinutes = limitProp.GetInt32();
            if (limitMinutes <= 0)
            {
                return false;
            }

            // 获取今天的游戏时长（从UserGameLibrary的recent_playtime_minutes或total_playtime_minutes）
            // 注意：这里假设recent_playtime_minutes是最近24小时的游戏时长
            // 如果需要更精确的每日统计，可能需要单独的游戏时长记录表
            var userLibrary = await context.UserGameLibraries
                .FirstOrDefaultAsync(ul => ul.UserId == rule.ChildUserId, cancellationToken);

            int currentMinutes = 0;
            if (userLibrary != null)
            {
                // 优先使用recent_playtime_minutes（最近游戏时长）
                // 如果系统没有更新这个字段，可以考虑使用total_playtime_minutes的增量
                currentMinutes = userLibrary.RecentPlaytimeMinutes;
            }

            // 如果recent_playtime_minutes为0，尝试从UserPlatformLibrary获取
            if (currentMinutes == 0)
            {
                var platformLibraries = await context.UserPlatformLibraries
                    .Include(upl => upl.PlayerPlatform)
                        .ThenInclude(pp => pp.UserPlatformBindings)
                    .Where(upl => upl.PlayerPlatform.UserPlatformBindings
                        .Any(upb => upb.UserId == rule.ChildUserId))
                    .ToListAsync(cancellationToken);

                currentMinutes = platformLibraries.Sum(upl => upl.PlaytimeMinutes);
            }

            violationDetails["currentMinutes"] = currentMinutes;
            violationDetails["limitMinutes"] = limitMinutes;
            violationDetails["exceededMinutes"] = Math.Max(0, currentMinutes - limitMinutes);

            // 检查是否超过限制
            if (currentMinutes >= limitMinutes)
            {
                // 检查是否有警告阈值
                if (ruleValue.TryGetProperty("warningMinutes", out var warningProp))
                {
                    var warningMinutes = warningProp.GetInt32();
                    // 如果设置了警告阈值且当前时长在警告和限制之间，发送警告
                    if (currentMinutes >= warningMinutes && currentMinutes < limitMinutes)
                    {
                        violationDetails["isWarning"] = true;
                        return true;
                    }
                }

                // 超过限制
                violationDetails["isWarning"] = false;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查每日游戏时长限制时发生错误: 规则ID={RuleId}", rule.RuleId);
            return false;
        }
    }

    /// <summary>
    /// 检查宵禁时间
    /// </summary>
    private async Task<bool> CheckPlaytimeCurfewAsync(
        PlayLinkerDbContext context,
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ruleValue.TryGetProperty("startTime", out var startProp) ||
                !ruleValue.TryGetProperty("endTime", out var endProp))
            {
                _logger.LogWarning("规则 {RuleId} 缺少 startTime 或 endTime 属性", rule.RuleId);
                return false;
            }

            var startTimeStr = startProp.GetString() ?? "22:00";
            var endTimeStr = endProp.GetString() ?? "07:00";

            // 解析时间（格式：HH:mm）
            if (!TimeSpan.TryParse(startTimeStr, out var startTime) ||
                !TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                _logger.LogWarning("规则 {RuleId} 的时间格式无效: startTime={StartTime}, endTime={EndTime}",
                    rule.RuleId, startTimeStr, endTimeStr);
                return false;
            }

            var now = DateTime.UtcNow;
            var nowTime = now.TimeOfDay;

            // 检查是否在宵禁时间段内
            bool isInCurfew = false;

            if (startTime < endTime)
            {
                // 正常情况：开始时间 < 结束时间（例如：22:00 - 07:00 跨天）
                // 这种情况需要特殊处理，因为实际上是从22:00到次日07:00
                // 我们简化为：如果当前时间在22:00-23:59或00:00-07:00之间
                if (nowTime >= startTime || nowTime < endTime)
                {
                    isInCurfew = true;
                }
            }
            else
            {
                // 跨天情况：开始时间 > 结束时间（例如：22:00 - 07:00）
                if (nowTime >= startTime || nowTime < endTime)
                {
                    isInCurfew = true;
                }
            }

            violationDetails["startTime"] = startTimeStr;
            violationDetails["endTime"] = endTimeStr;
            violationDetails["currentTime"] = nowTime.ToString(@"hh\:mm");
            violationDetails["isInCurfew"] = isInCurfew;

            // 如果在宵禁时间内，检查是否有游戏活动
            if (isInCurfew)
            {
                // 检查最近是否有游戏活动（例如：最近1小时内有游戏记录）
                // 这里简化处理：如果UserGameLibrary的recent_playtime_minutes > 0，认为可能在游戏
                var userLibrary = await context.UserGameLibraries
                    .FirstOrDefaultAsync(ul => ul.UserId == rule.ChildUserId, cancellationToken);

                bool hasRecentActivity = false;
                if (userLibrary != null && userLibrary.RecentPlaytimeMinutes > 0)
                {
                    // 这里可以进一步检查最近游戏时间，但当前简化处理
                    hasRecentActivity = true;
                }

                violationDetails["hasRecentActivity"] = hasRecentActivity;

                if (hasRecentActivity)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查宵禁时间时发生错误: 规则ID={RuleId}", rule.RuleId);
            return false;
        }
    }

    /// <summary>
    /// 检查游戏限制
    /// </summary>
    private async Task<bool> CheckGameRestrictionAsync(
        PlayLinkerDbContext context,
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            // 优先使用 blockedGameNames（新格式），兼容 blockedGameIds（旧格式）
            List<string> blockedGameNames = new List<string>();
            List<long> blockedGameIds = new List<long>();
            bool useGameNames = false;

            if (ruleValue.TryGetProperty("blockedGameNames", out var blockedGameNamesProp))
            {
                // 使用游戏名称（新格式）
                useGameNames = true;
                if (blockedGameNamesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in blockedGameNamesProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var gameName = item.GetString();
                            if (!string.IsNullOrWhiteSpace(gameName))
                            {
                                blockedGameNames.Add(gameName.Trim());
                            }
                        }
                    }
                }
                _logger.LogInformation("规则 {RuleId} 使用游戏名称限制，共 {Count} 个被限制的游戏: {Games}", 
                    rule.RuleId, blockedGameNames.Count, string.Join(", ", blockedGameNames));
            }
            else if (ruleValue.TryGetProperty("blockedGameIds", out var blockedGameIdsProp))
            {
                // 兼容旧格式：使用游戏ID
                _logger.LogWarning("规则 {RuleId} 使用已废弃的 blockedGameIds 格式，建议使用 blockedGameNames", rule.RuleId);
                if (blockedGameIdsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in blockedGameIdsProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var gameId))
                        {
                            blockedGameIds.Add(gameId);
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("规则 {RuleId} 缺少 blockedGameNames 或 blockedGameIds 属性", rule.RuleId);
                return false;
            }

            if (useGameNames && blockedGameNames.Count == 0)
            {
                _logger.LogDebug("规则 {RuleId} 没有限制的游戏名称", rule.RuleId);
                return false; // 没有限制的游戏
            }
            if (!useGameNames && blockedGameIds.Count == 0)
            {
                _logger.LogDebug("规则 {RuleId} 没有限制的游戏ID", rule.RuleId);
                return false; // 没有限制的游戏
            }

            // 获取孩子的所有平台绑定
            var platformBindings = await context.UserPlatformBindings
                .Where(upb => upb.UserId == rule.ChildUserId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync(cancellationToken);

            _logger.LogDebug("规则 {RuleId} 检查子账户 {ChildUserId}，找到 {Count} 个平台绑定", 
                rule.RuleId, rule.ChildUserId, platformBindings.Count);

            if (platformBindings.Count == 0)
            {
                _logger.LogDebug("子账户 {ChildUserId} 没有绑定的平台账户", rule.ChildUserId);
                return false;
            }

            // 获取孩子游戏库中的所有游戏ID
            var childGameIds = new List<long>();

            foreach (var binding in platformBindings)
            {
                var games = await context.UserPlatformLibraries
                    .Where(upl => upl.PlatformUserId == binding.PlatformUserId 
                        && upl.PlatformId == binding.PlatformId)
                    .Select(upl => upl.GameId)
                    .ToListAsync(cancellationToken);

                childGameIds.AddRange(games);
            }

            // 去重
            childGameIds = childGameIds.Distinct().ToList();

            _logger.LogDebug("规则 {RuleId} 子账户 {ChildUserId} 共有 {Count} 个游戏", 
                rule.RuleId, rule.ChildUserId, childGameIds.Count);

            if (childGameIds.Count == 0)
            {
                _logger.LogDebug("子账户 {ChildUserId} 没有游戏", rule.ChildUserId);
                return false; // 孩子没有游戏
            }

            // 获取孩子游戏库中的所有游戏名称
            var childGames = await context.Games
                .Where(g => childGameIds.Contains(g.GameId))
                .Select(g => new { g.GameId, g.Name })
                .ToListAsync(cancellationToken);

            var childGameNames = childGames.Select(g => g.Name).ToList();

            _logger.LogDebug("规则 {RuleId} 子账户游戏名称列表（前10个）: {Games}", 
                rule.RuleId, string.Join(", ", childGameNames.Take(10)));

            if (useGameNames)
            {
                // 使用游戏名称匹配（不区分大小写）
                var violatingGameNames = childGameNames
                    .Where(name => blockedGameNames.Any(blocked => 
                        string.Equals(name, blocked, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                _logger.LogInformation("规则 {RuleId} 游戏名称匹配结果: 被限制游戏={BlockedCount}, 子账户游戏={ChildCount}, 违规游戏={ViolatingCount}", 
                    rule.RuleId, blockedGameNames.Count, childGameNames.Count, violatingGameNames.Count);

                if (violatingGameNames.Count > 0)
                {
                    var violatingGameIds = childGames
                        .Where(g => violatingGameNames.Contains(g.Name))
                        .Select(g => g.GameId)
                        .ToList();

                    violationDetails["blockedGameNames"] = blockedGameNames;
                    violationDetails["violatingGameNames"] = violatingGameNames;
                    violationDetails["violatingGameIds"] = violatingGameIds;
                    violationDetails["totalBlockedGames"] = blockedGameNames.Count;
                    violationDetails["violatingGamesCount"] = violatingGameNames.Count;

                    _logger.LogInformation("规则 {RuleId} 检测到违规: 违规游戏={Games}", 
                        rule.RuleId, string.Join(", ", violatingGameNames));

                    return true;
                }
                else
                {
                    _logger.LogDebug("规则 {RuleId} 未检测到违规: 被限制游戏={BlockedGames}, 子账户游戏={ChildGames}", 
                        rule.RuleId, string.Join(", ", blockedGameNames), string.Join(", ", childGameNames.Take(5)));
                }
            }
            else
            {
                // 使用游戏ID匹配（兼容旧格式）
                var violatingGameIds = childGameIds.Intersect(blockedGameIds).ToList();

                if (violatingGameIds.Count > 0)
                {
                    var violatingGames = childGames
                        .Where(g => violatingGameIds.Contains(g.GameId))
                        .ToList();

                    var violatingGameNames = violatingGames.Select(g => g.Name).ToList();

                    violationDetails["blockedGameIds"] = blockedGameIds;
                    violationDetails["violatingGameIds"] = violatingGameIds;
                    violationDetails["blockedGameNames"] = violatingGameNames;
                    violationDetails["totalBlockedGames"] = blockedGameIds.Count;
                    violationDetails["violatingGamesCount"] = violatingGameIds.Count;

                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查游戏限制时发生错误: 规则ID={RuleId}", rule.RuleId);
            return false;
        }
    }

    /// <summary>
    /// 检查年龄限制
    /// </summary>
    private async Task<bool> CheckAgeRestrictionAsync(
        PlayLinkerDbContext context,
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ruleValue.TryGetProperty("maxAgeRating", out var maxAgeRatingProp))
            {
                _logger.LogWarning("规则 {RuleId} 缺少 maxAgeRating 属性", rule.RuleId);
                return false;
            }

            var maxAgeRating = maxAgeRatingProp.GetByte();
            if (maxAgeRating == 0)
            {
                return false; // 没有年龄限制
            }

            // 获取孩子的所有平台绑定
            var platformBindings = await context.UserPlatformBindings
                .Where(upb => upb.UserId == rule.ChildUserId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync(cancellationToken);

            if (platformBindings.Count == 0)
            {
                _logger.LogDebug("子账户 {ChildUserId} 没有绑定的平台账户", rule.ChildUserId);
                return false;
            }

            // 获取孩子游戏库中的所有游戏ID
            var childGameIds = new List<long>();

            foreach (var binding in platformBindings)
            {
                var games = await context.UserPlatformLibraries
                    .Where(upl => upl.PlatformUserId == binding.PlatformUserId 
                        && upl.PlatformId == binding.PlatformId)
                    .Select(upl => upl.GameId)
                    .ToListAsync(cancellationToken);

                childGameIds.AddRange(games);
            }

            // 去重
            childGameIds = childGameIds.Distinct().ToList();

            if (childGameIds.Count == 0)
            {
                return false; // 没有游戏
            }

            // 检查是否有超出年龄分级的游戏
            // 注意：RequireAge 为 null 的游戏不进行限制检查
            var violatingGames = await context.Games
                .Where(g => childGameIds.Contains(g.GameId)
                    && g.RequireAge.HasValue
                    && g.RequireAge.Value > maxAgeRating)
                .Select(g => new { g.GameId, g.Name, g.RequireAge })
                .ToListAsync(cancellationToken);

            if (violatingGames.Count > 0)
            {
                var violatingGameNames = violatingGames.Select(g => g.Name).ToList();
                var violatingGameAges = violatingGames
                    .Where(g => g.RequireAge.HasValue)
                    .Select(g => g.RequireAge!.Value)
                    .ToList();

                violationDetails["maxAgeRating"] = maxAgeRating;
                violationDetails["violatingGameIds"] = violatingGames.Select(g => g.GameId).ToList();
                violationDetails["violatingGameNames"] = violatingGameNames;
                violationDetails["violatingGameAges"] = violatingGameAges;
                violationDetails["violatingGamesCount"] = violatingGames.Count;

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查年龄限制时发生错误: 规则ID={RuleId}", rule.RuleId);
            return false;
        }
    }
}

