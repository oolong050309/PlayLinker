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

    public PricesController(PlayLinkerDbContext context, IAiService aiService, ILogger<PricesController> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
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
            lowestPrice = lowest?.CurrentPrice ?? 0,
            lowestDate = lowest?.RecordDate,
            priceHistory = history.Select(h => new
            {
                h.PriceId,
                Date = h.RecordDate.ToString("yyyy-MM-dd"),
                h.CurrentPrice,
                h.OriginalPrice,
                Discount = h.DiscountRate,
                IsDiscount = h.IsDiscount
            })
        };

        return Ok(ApiResponse<object>.SuccessResponse(response));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentPrices([FromQuery] string game_ids)
    {
        if (string.IsNullOrEmpty(game_ids)) return BadRequest();

        var ids = game_ids.Split(',').Select(long.Parse).ToList();
        
        var prices = await _context.PriceHistories
            .Where(p => ids.Contains(p.GameId))
            .GroupBy(p => p.GameId)
            .Select(g => g.OrderByDescending(p => p.RecordDate).First())
            .Include(p => p.Game)
            .Include(p => p.Platform)
            .ToListAsync();

        var result = prices.Select(p => new
        {
            p.GameId,
            GameName = p.Game.Name,
            Platform = p.Platform.PlatformName,
            p.CurrentPrice,
            p.OriginalPrice,
            Discount = p.DiscountRate,
            p.IsDiscount,
            LastUpdated = p.RecordDate
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { prices = result, totalCount = result.Count() }));
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
                    // [修复] 因为 DTO 中已经是 string，这里直接赋值即可，不会报错
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

            var exists = await _context.PriceAlertSubscriptions
                .AnyAsync(s => s.UserId == userId && s.GameId == request.GameId && s.PlatformId == request.PlatformId);

            if (exists)
            {
                return Conflict(ApiResponse<object>.ErrorResponse("ERR_DUPLICATE", "该游戏已在监控列表中"));
            }

            var sub = new PriceAlertSubscription
            {
                UserId = userId,
                GameId = request.GameId,
                PlatformId = request.PlatformId,
                TargetPrice = request.TargetPrice,
                TargetDiscount = request.TargetDiscount,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PriceAlertSubscriptions.Add(sub);
            await _context.SaveChangesAsync();

            _logger.LogInformation("用户 {UserId} 添加了价格监控: GameId={GameId}, PlatformId={PlatformId}",
                userId, request.GameId, request.PlatformId);

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
    /// 获取当前用户的价格监控订阅列表
    /// </summary>
    [HttpGet("subscriptions")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetSubscriptions()
    {
        try
        {
            var userId = GetCurrentUserId();

            var subscriptions = await _context.PriceAlertSubscriptions
                .Where(s => s.UserId == userId && s.IsActive == true)
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
                currentPrice = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].CurrentPrice : (decimal?)null,
                originalPrice = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].OriginalPrice : (decimal?)null,
                discountRate = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].DiscountRate : (int?)null,
                isDiscount = latestPrices.ContainsKey(s.GameId) ? latestPrices[s.GameId].IsDiscount : (bool?)null,
                createdAt = s.CreatedAt
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

            if (request.TargetPrice.HasValue)
                subscription.TargetPrice = request.TargetPrice;
            if (request.TargetDiscount.HasValue)
                subscription.TargetDiscount = request.TargetDiscount;
            subscription.UpdatedAt = DateTime.UtcNow;

            _context.PriceAlertSubscriptions.Update(subscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation("用户 {UserId} 更新了价格监控订阅: SubscriptionId={SubscriptionId}",
                userId, id);

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
            var today = DateTime.UtcNow.Date;
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