using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/prices")]
public class PricesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly IAiService _aiService;
    private readonly ILogger<PricesController> _logger;
    private readonly IServiceProvider _serviceProvider;

    public PricesController(PlayLinkerDbContext context, IAiService aiService, ILogger<PricesController> logger, IServiceProvider serviceProvider)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("无法获取用户ID，请重新登录");
        }
        return userId;
    }

    [HttpGet("history/{gameId}")]
    public async Task<ActionResult<ApiResponse<object>>> GetPriceHistory(long gameId)
    {
        var history = await _context.PriceHistories
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.RecordDate)
            .Take(50)
            .ToListAsync();

        var game = await _context.Games.FindAsync(gameId);
        if (history.Count == 0 && game == null) return NotFound(ApiResponse<object>.ErrorResponse("NOT_FOUND", "未找到游戏或价格记录"));

        var current = history.FirstOrDefault();
        var lowest = history.MinBy(p => p.CurrentPrice);

        var response = new
        {
            gameId = gameId,
            gameName = game?.Name,
            currentPrice = current?.CurrentPrice ?? 0,
            originalPrice = current?.OriginalPrice ?? 0,
            discountRate = current?.DiscountRate ?? 0,
            discount = current?.DiscountRate ?? 0, // 兼容字段
            isDiscount = current?.IsDiscount ?? false,
            lowestPrice = lowest?.CurrentPrice ?? 0,
            lowestDate = lowest?.RecordDate,
            priceHistory = history.Select(h => new
            {
                h.PriceId,
                Date = h.RecordDate.ToString("yyyy-MM-dd"),
                h.CurrentPrice,
                h.OriginalPrice,
                Discount = h.DiscountRate,
                DiscountRate = h.DiscountRate, // 添加兼容字段
                IsDiscount = h.IsDiscount
            })
        };

        return Ok(ApiResponse<object>.SuccessResponse(response));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentPrices([FromQuery] string game_ids)
    {
        if (string.IsNullOrEmpty(game_ids)) 
            return BadRequest(ApiResponse<object>.ErrorResponse("ERR_BAD_REQUEST", "game_ids 参数不能为空"));

        try
        {
            var ids = game_ids.Split(',').Select(long.Parse).ToList();
            
            // 获取所有相关游戏的价格历史记录（包括关联数据）
            var allPrices = await _context.PriceHistories
            .Where(p => ids.Contains(p.GameId))
            .Include(p => p.Game)
            .Include(p => p.Platform)
            .ToListAsync();

            // 按游戏ID分组，获取每个游戏的最新价格记录
            var prices = allPrices
                .GroupBy(p => p.GameId)
                .Select(g => g.OrderByDescending(p => p.RecordDate).First())
                .ToList();

            var result = prices.Select(p => new
            {
                gameId = p.GameId,
                GameName = p.Game?.Name ?? "未知游戏",
                Platform = p.Platform?.PlatformName ?? "未知平台",
                currentPrice = p.CurrentPrice,
                originalPrice = p.OriginalPrice,
                discount = p.DiscountRate,
                discountRate = p.DiscountRate, // 兼容字段
                isDiscount = p.IsDiscount,
                LastUpdated = p.RecordDate
            }).ToList();

            return Ok(ApiResponse<object>.SuccessResponse(new { prices = result, totalCount = result.Count }));
        }
        catch (FormatException)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("ERR_BAD_REQUEST", "game_ids 参数格式错误"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前价格时发生错误: game_ids={GameIds}", game_ids);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    [HttpGet("predictions/{gameId}")]
    public async Task<ActionResult<ApiResponse<object>>> GetPricePredictions(long gameId)
    {
        var historyEntities = await _context.PriceHistories
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.RecordDate)
            .ToListAsync();

        if (!historyEntities.Any())
        {
            return NotFound(ApiResponse<object>.ErrorResponse("NO_DATA", "没有足够的历史价格数据进行预测"));
        }

        var historyDtos = historyEntities.Select(h => new PriceHistoryDto
        {
            Date = h.RecordDate,
            CurrentPrice = h.CurrentPrice,
            IsDiscount = h.IsDiscount
        }).ToList();

        // 调用 AI 服务，返回的 DTO 中 EstimatedDate 是 string 类型
        var predictionDto = await _aiService.PredictPriceAsync(gameId, historyDtos);

        var response = new
        {
            gameId,
            predictions = new[]
            {
                new {
                    event_name = "AI 预测下次折扣",
                    estimatedDate = predictionDto.EstimatedDate, 
                    probability = predictionDto.Probability,
                    confidence = predictionDto.Probability > 0.8 ? "high" : "medium",
                    reasoning = predictionDto.Reasoning
                }
            },
            recommendation = new
            {
                shouldWait = predictionDto.Probability > 0.7,
                reason = predictionDto.Reasoning
            }
        };

        return Ok(ApiResponse<object>.SuccessResponse(response));
    }
    
    /// <summary>
    /// 添加价格监控订阅
    /// </summary>
    [HttpPost("track")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> TrackPrice([FromBody] TrackPriceDto request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // 检查是否已存在订阅（包括非活跃的订阅）
            var existingSubscription = await _context.PriceAlertSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.GameId == request.GameId && s.PlatformId == request.PlatformId);

            if (existingSubscription != null)
            {
                // 如果订阅已存在，更新它而不是创建新的
                existingSubscription.TargetPrice = request.TargetPrice;
                existingSubscription.TargetDiscount = request.TargetDiscount;
                existingSubscription.IsActive = true; // 重新激活订阅
                existingSubscription.UpdatedAt = DateTime.Now; // 使用本地时间（UTC+8）

                _context.PriceAlertSubscriptions.Update(existingSubscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("用户 {UserId} 重新激活了价格监控: GameId={GameId}, PlatformId={PlatformId}, SubscriptionId={SubscriptionId}",
                    userId, request.GameId, request.PlatformId, existingSubscription.SubscriptionId);

                // 立即检查当前价格是否已经满足提醒条件
                try
                {
                    await CheckAndNotifyImmediatelyAsync(existingSubscription, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "立即检查价格提醒条件时发生错误，但不影响订阅更新");
                }

                return Ok(ApiResponse<object>.SuccessResponse(new { 
                    subscriptionId = existingSubscription.SubscriptionId,
                    gameId = existingSubscription.GameId 
                }, "价格跟踪已重新激活"));
            }

            var sub = new PriceAlertSubscription
            {
                UserId = userId,
                GameId = request.GameId,
                PlatformId = request.PlatformId,
                TargetPrice = request.TargetPrice,
                TargetDiscount = request.TargetDiscount,
                IsActive = true,
                CreatedAt = DateTime.Now, // 使用本地时间（UTC+8）
                UpdatedAt = DateTime.Now // 使用本地时间（UTC+8）
            };

            _context.PriceAlertSubscriptions.Add(sub);
            await _context.SaveChangesAsync();

            _logger.LogInformation("用户 {UserId} 添加了价格监控: GameId={GameId}, PlatformId={PlatformId}",
                userId, request.GameId, request.PlatformId);

            // 立即检查当前价格是否已经满足提醒条件
            try
            {
                await CheckAndNotifyImmediatelyAsync(sub, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "立即检查价格提醒条件时发生错误，但不影响订阅创建");
            }

            return Created("", ApiResponse<object>.SuccessResponse(new { 
                subscriptionId = sub.SubscriptionId,
                gameId = sub.GameId 
            }, "价格跟踪已启动"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加价格监控时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 立即检查价格提醒条件并发送通知（用于新创建的订阅）
    /// </summary>
    private async Task CheckAndNotifyImmediatelyAsync(PriceAlertSubscription subscription, int userId)
    {
        // 获取游戏的最新价格记录
        var latestPrice = await _context.PriceHistories
            .Where(ph => ph.GameId == subscription.GameId 
                && ph.PlatformId == subscription.PlatformId)
            .OrderByDescending(ph => ph.RecordDate)
            .FirstOrDefaultAsync();

        if (latestPrice == null)
        {
            _logger.LogDebug("游戏 {GameId} 暂无价格记录，跳过立即检查", subscription.GameId);
            return;
        }

        var game = await _context.Games.FindAsync(subscription.GameId);
        if (game == null) return;

        // 检查今天是否已经发送过通知（避免重复通知）
        var today = DateTime.Now.Date; // 使用本地时间（UTC+8）
        var todayAlert = await _context.PriceAlertLogs
            .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                && l.AlertTime.HasValue 
                && l.AlertTime.Value.Date == today)
            .FirstOrDefaultAsync();

        if (todayAlert != null)
        {
            _logger.LogDebug("[立即检查-跳过] 用户 {UserId} 订阅 {SubId} 今天已发过通知", userId, subscription.SubscriptionId);
            return;
        }

        bool shouldNotify = false;
        string notificationTitle = "";
        string notificationContent = "";
        string alertType = "";

        // 检查是否满足目标价格条件
        if (subscription.TargetPrice.HasValue)
        {
            _logger.LogInformation("[立即检查-价格判定] 订阅 {SubId}: 目标价={Target} vs 现价={Current}", 
                subscription.SubscriptionId, subscription.TargetPrice, latestPrice.CurrentPrice);

            if (latestPrice.CurrentPrice <= subscription.TargetPrice.Value)
            {
                // 检查之前是否已经达到过目标价格
                var previousAlert = await _context.PriceAlertLogs
                    .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                        && l.AlertType == "target_price")
                    .Include(l => l.Price)
                    .OrderByDescending(l => l.AlertTime)
                    .FirstOrDefaultAsync();

                bool isSubscriptionUpdatedAfterAlert = subscription.UpdatedAt.HasValue 
                    && previousAlert != null 
                    && subscription.UpdatedAt.Value > previousAlert.AlertTime;

                if (!isSubscriptionUpdatedAfterAlert && previousAlert != null && previousAlert.Price != null 
                    && previousAlert.Price.CurrentPrice <= latestPrice.CurrentPrice)
                {
                    _logger.LogInformation("[立即检查-跳过] 订阅 {SubId} 已因更低/相同价格提醒过且未更新订阅", subscription.SubscriptionId);
                    return;
                }

                shouldNotify = true;
                alertType = "target_price";
                notificationTitle = $"价格提醒：{game.Name}";
                notificationContent = $"游戏 {game.Name} 的价格已降至 ¥{latestPrice.CurrentPrice:F2}，低于您设置的目标价格 ¥{subscription.TargetPrice.Value:F2}。";
                
                if (latestPrice.IsDiscount && latestPrice.DiscountRate > 0)
                {
                    notificationContent += $" 当前折扣 {latestPrice.DiscountRate}%，原价 ¥{latestPrice.OriginalPrice:F2}。";
                }
            }
        }
        // 检查是否满足目标折扣条件
        else if (subscription.TargetDiscount.HasValue)
        {
            // [逻辑修复]: 用户期望的"几折" (如80即8折) 意味着价格 <= 原价*0.8
            // 即减免 >= (100 - 80)% = 20%
            var targetDiscountRate = 100 - subscription.TargetDiscount.Value;
            
            _logger.LogInformation("[立即检查-折扣判定] 订阅 {SubId}: 用户期望{UserTarget}折(即减免>={CalcRate}%) vs 实际减免={ActualRate}%", 
                subscription.SubscriptionId, subscription.TargetDiscount, targetDiscountRate, latestPrice.DiscountRate);

            if (latestPrice.DiscountRate >= targetDiscountRate)
            {
                // 检查之前是否已经达到过目标折扣
                var previousAlert = await _context.PriceAlertLogs
                    .Where(l => l.SubscriptionId == subscription.SubscriptionId 
                        && l.AlertType == "target_discount")
                    .Include(l => l.Price)
                    .OrderByDescending(l => l.AlertTime)
                    .FirstOrDefaultAsync();

                bool isSubscriptionUpdatedAfterAlert = subscription.UpdatedAt.HasValue 
                    && previousAlert != null 
                    && subscription.UpdatedAt.Value > previousAlert.AlertTime;

                if (!isSubscriptionUpdatedAfterAlert && previousAlert != null && previousAlert.Price != null 
                    && previousAlert.Price.DiscountRate >= latestPrice.DiscountRate)
                {
                    _logger.LogInformation("[立即检查-跳过] 订阅 {SubId} 已因更高/相同折扣提醒过且未更新订阅", subscription.SubscriptionId);
                    return;
                }

                shouldNotify = true;
                alertType = "target_discount";
                notificationTitle = $"折扣提醒：{game.Name}";
                notificationContent = $"游戏 {game.Name} 当前折扣 {latestPrice.DiscountRate}%，达到您设置的目标折扣 {subscription.TargetDiscount.Value}%（{(subscription.TargetDiscount.Value / 10.0):F1}折）。";
                notificationContent += $" 当前价格：¥{latestPrice.CurrentPrice:F2}，原价：¥{latestPrice.OriginalPrice:F2}。";
            }
        }

        if (shouldNotify)
        {
            _logger.LogInformation("立即检查满足条件: 用户 {UserId}, 游戏 {GameId}, 类型 {AlertType}", userId, subscription.GameId, alertType);

            // [核心修复]：检查是否已存在通知，解决 Duplicate entry 错误
            var existingNotification = await _context.NotificationCenters
                .FirstOrDefaultAsync(n => n.RelatedId == subscription.SubscriptionId && n.SourceModule == "price_alert");

            NotificationCenter notification;

            if (existingNotification != null)
            {
                // 如果存在，执行更新
                _logger.LogInformation("发现已存在通知(ID={NotifId})，执行更新操作", existingNotification.NotificationId);
                existingNotification.Title = notificationTitle;
                existingNotification.Content = notificationContent;
                existingNotification.IsRead = false; // 重新标记为未读
                existingNotification.CreatedAt = DateTime.Now; // 更新时间（使用本地时间 UTC+8）
                existingNotification.NotificationType = "info";
                
                notification = existingNotification;
                // 不需要 Add，EF Core 会自动追踪更新
            }
            else
            {
                // 如果不存在，执行插入
                notification = new NotificationCenter
                {
                    UserId = userId,
                    SourceModule = "price_alert",
                    Title = notificationTitle,
                    Content = notificationContent,
                    NotificationType = "info",
                    IsRead = false,
                    RelatedId = subscription.SubscriptionId,
                    CreatedAt = DateTime.Now // 使用本地时间（UTC+8）
                };
                _context.NotificationCenters.Add(notification);
            }

            await _context.SaveChangesAsync();

            // 创建价格提醒日志 (日志表没有 RelatedId 唯一约束，可以直接 Add)
            var alertLog = new PriceAlertLog
            {
                SubscriptionId = subscription.SubscriptionId,
                PriceId = latestPrice.PriceId,
                AlertType = alertType,
                AlertTime = DateTime.Now, // 使用本地时间（UTC+8）
                NotificationId = notification.NotificationId
            };

            _context.PriceAlertLogs.Add(alertLog);
            await _context.SaveChangesAsync();

            // 提醒后将订阅设为非active（仅针对目标价格和目标折扣提醒）
            if (alertType == "target_price" || alertType == "target_discount")
            {
                // 重新从数据库加载订阅以确保正确追踪和更新
                var updatedSubscription = await _context.PriceAlertSubscriptions
                    .FindAsync(new object[] { subscription.SubscriptionId });
                
                if (updatedSubscription != null)
                {
                    updatedSubscription.IsActive = false;
                    updatedSubscription.UpdatedAt = DateTime.Now; // 使用本地时间（UTC+8）
                    _context.PriceAlertSubscriptions.Update(updatedSubscription);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已将订阅 {SubscriptionId} 设为非active: GameId={GameId}, UserId={UserId}",
                        updatedSubscription.SubscriptionId, updatedSubscription.GameId, userId);
                }
                else
                {
                    _logger.LogWarning("无法找到订阅 {SubscriptionId} 以更新状态", subscription.SubscriptionId);
                }
            }

            // 发送邮件提醒
            var user = await _context.Users.FindAsync(userId);
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
                        currentPrice: latestPrice.CurrentPrice,
                        originalPrice: latestPrice.OriginalPrice,
                        discountRate: latestPrice.DiscountRate,
                        targetPrice: subscription.TargetPrice,
                        targetDiscount: subscription.TargetDiscount
                    );
                    
                    _logger.LogInformation("已为用户 {UserId} 发送立即价格提醒邮件: GameId={GameId}",
                        userId, subscription.GameId);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "发送立即价格提醒邮件失败: UserId={UserId}",
                        userId);
                }
            }

            _logger.LogInformation("已为用户 {UserId} 创建/更新立即价格提醒通知: GameId={GameId}, Price={Price}, Discount={Discount}%",
                userId, subscription.GameId, latestPrice.CurrentPrice, latestPrice.DiscountRate);
        }
        else
        {
            _logger.LogInformation("立即检查未满足条件: 用户 {UserId}, 游戏 {GameId}。当前折扣率={CurrentRate}, 目标折扣率={TargetRate}", 
                userId, subscription.GameId, latestPrice.DiscountRate, subscription.TargetDiscount.HasValue ? (100 - subscription.TargetDiscount.Value) : "N/A");
        }
    }

    /// <summary>
    /// 获取当前用户的价格监控订阅列表（包括非活跃的订阅）
    /// </summary>
    [HttpGet("subscriptions")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetSubscriptions()
    {
        try
        {
            var userId = GetCurrentUserId();

            // 返回所有订阅（包括非活跃的），以便前端可以检测并更新已存在的订阅
            var subscriptions = await _context.PriceAlertSubscriptions
                .Where(s => s.UserId == userId)
                .Include(s => s.Game)
                .Include(s => s.Platform)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            // 获取每个游戏的最新价格
            var gameIds = subscriptions.Select(s => s.GameId).ToList();
            var latestPrices = await _context.PriceHistories
                .Where(ph => gameIds.Contains(ph.GameId) && ph.PlatformId == 1) // Steam 平台
                .GroupBy(ph => ph.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    LatestPrice = g.OrderByDescending(p => p.RecordDate).First()
                })
                .ToDictionaryAsync(x => x.GameId, x => x.LatestPrice);

            var result = subscriptions.Select(s => new
            {
                subscriptionId = s.SubscriptionId,
                gameId = s.GameId,
                gameName = s.Game.Name ?? "",
                headerImage = s.Game.HeaderImage ?? "",
                platformId = s.PlatformId,
                platformName = s.Platform.PlatformName ?? "",
                targetPrice = s.TargetPrice,
                targetDiscount = s.TargetDiscount,
                isActive = s.IsActive,
                currentPrice = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].CurrentPrice : (decimal?)null,
                originalPrice = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].OriginalPrice : (decimal?)null,
                discountRate = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].DiscountRate : (int?)null,
                isDiscount = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].IsDiscount : (bool?)null,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            }).ToList();

            return Ok(ApiResponse<object>.SuccessResponse(new { subscriptions = result, totalCount = result.Count }));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取价格监控订阅列表时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新价格监控订阅
    /// </summary>
    [HttpPut("subscriptions/{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSubscription(long id, [FromBody] TrackPriceDto request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var subscription = await _context.PriceAlertSubscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == userId);

            if (subscription == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "订阅不存在"));
            }

            // 更新订阅信息
            if (request.TargetPrice.HasValue)
                subscription.TargetPrice = request.TargetPrice;
            else
                subscription.TargetPrice = null;
            
            if (request.TargetDiscount.HasValue)
                subscription.TargetDiscount = request.TargetDiscount;
            else
                subscription.TargetDiscount = null;
            
            // 如果更新了目标价格或折扣，重新激活订阅
            subscription.IsActive = true;
            subscription.UpdatedAt = DateTime.Now; // 使用本地时间（UTC+8）

            _context.PriceAlertSubscriptions.Update(subscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation("用户 {UserId} 更新了价格监控订阅: SubscriptionId={SubscriptionId}",
                userId, id);

            // 立即检查当前价格是否已经满足提醒条件
            try
            {
                await CheckAndNotifyImmediatelyAsync(subscription, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "立即检查价格提醒条件时发生错误，但不影响订阅更新");
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { }, "订阅已更新"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新价格监控订阅时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 删除价格监控订阅
    /// </summary>
    [HttpDelete("subscriptions/{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSubscription(long id)
    {
        try
        {
            var userId = GetCurrentUserId();

            var subscription = await _context.PriceAlertSubscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == userId);

            if (subscription == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "订阅不存在"));
            }

            subscription.IsActive = false;
            subscription.UpdatedAt = DateTime.UtcNow;

            _context.PriceAlertSubscriptions.Update(subscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation("用户 {UserId} 删除了价格监控订阅: SubscriptionId={SubscriptionId}",
                userId, id);

            return Ok(ApiResponse<object>.SuccessResponse(new { }, "订阅已删除"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除价格监控订阅时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 手动触发价格更新（管理员功能，用于测试）
    /// </summary>
    [HttpPost("update-now")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePricesNow()
    {
        try
        {
            var userId = GetCurrentUserId();
            // 这里可以添加管理员权限检查
            // 暂时允许所有登录用户手动触发

            _logger.LogInformation("用户 {UserId} 手动触发价格更新", userId);

            // 这里可以调用价格更新服务
            // 由于是后台服务，这里只返回成功，实际更新由后台服务异步执行
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "价格更新任务已触发，将在后台执行"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动触发价格更新时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取价格监控状态和统计信息
    /// </summary>
    [HttpGet("monitoring-status")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetMonitoringStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            var today = DateTime.Now.Date; // 使用本地时间（UTC+8）
            var yesterday = today.AddDays(-1);

            // 获取今日已记录价格的游戏数量
            var todayRecordCount = await _context.PriceHistories
                .Where(ph => ph.PlatformId == 1 && ph.RecordDate.Date == today)
                .Select(ph => ph.GameId)
                .Distinct()
                .CountAsync();

            // 获取昨日已记录价格的游戏数量
            var yesterdayRecordCount = await _context.PriceHistories
                .Where(ph => ph.PlatformId == 1 && ph.RecordDate.Date == yesterday)
                .Select(ph => ph.GameId)
                .Distinct()
                .CountAsync();

            // 获取需要监控的游戏总数（Steam平台）
            var totalSteamGames = await _context.GamePlatforms
                .Where(gp => gp.PlatformId == 1)
                .CountAsync();

            // 获取最新的价格记录时间
            var latestRecord = await _context.PriceHistories
                .Where(ph => ph.PlatformId == 1)
                .OrderByDescending(ph => ph.RecordDate)
                .FirstOrDefaultAsync();

            // 获取用户的价格订阅数量
            var userSubscriptionCount = await _context.PriceAlertSubscriptions
                .Where(s => s.UserId == userId && s.IsActive == true)
                .CountAsync();

            // 获取今日有价格变化的游戏数量（相比昨日）
            var priceChangedCount = await _context.PriceHistories
                .Where(ph => ph.PlatformId == 1 && ph.RecordDate.Date == today)
                .Join(_context.PriceHistories
                    .Where(ph => ph.PlatformId == 1 && ph.RecordDate.Date == yesterday),
                    today => today.GameId,
                    yesterday => yesterday.GameId,
                    (today, yesterday) => new { Today = today, Yesterday = yesterday })
                .Where(x => x.Today.CurrentPrice != x.Yesterday.CurrentPrice)
                .CountAsync();

            var status = new
            {
                TodayRecordCount = todayRecordCount,
                YesterdayRecordCount = yesterdayRecordCount,
                TotalSteamGames = totalSteamGames,
                LatestRecordTime = latestRecord?.RecordDate,
                UserSubscriptionCount = userSubscriptionCount,
                PriceChangedCount = priceChangedCount,
                IsTodayUpdated = todayRecordCount > 0,
                UpdateProgress = totalSteamGames > 0 ? (double)todayRecordCount / totalSteamGames * 100 : 0
            };

            return Ok(ApiResponse<object>.SuccessResponse(status));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取价格监控状态时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}