using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO 命名空间
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;

    public AlertsController(PlayLinkerDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<ApiResponse<object>>> GetSubscriptions()
    {
        var userId = GetCurrentUserId();
        
        var subs = await _context.PriceAlertSubscriptions
            .Include(s => s.Game)
            .Include(s => s.Platform)
            .Where(s => s.UserId == userId && (s.IsActive ?? true))
            .ToListAsync();

        var subIds = subs.Select(s => s.SubscriptionId).ToList();
        
        // [修复] 将 l.PriceHistory 改为 l.Price
        var logs = await _context.PriceAlertLogs
            .Where(l => subIds.Contains(l.SubscriptionId))
            .Include(l => l.Price) 
            .OrderByDescending(l => l.AlertTime)
            .Take(20)
            .ToListAsync();

        var items = subs.Select(s => new
        {
            s.SubscriptionId,
            s.GameId,
            GameName = s.Game?.Name ?? "Unknown",
            PlatformName = s.Platform?.PlatformName ?? "Unknown",
            s.TargetPrice,
            s.TargetDiscount,
            s.IsActive,
            s.CreatedAt,
            AlertHistory = logs.Where(l => l.SubscriptionId == s.SubscriptionId).Select(l => new 
            {
                l.AlertTime,
                l.AlertType
            })
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { items }));
    }

    [HttpPost("subscribe")]
    // [修复] 使用全局 AddWishlistDto，不再依赖 WishlistController.AddWishlistDto
    public async Task<ActionResult<ApiResponse<object>>> SubscribeAlert([FromBody] AddWishlistDto request)
    {
        var userId = GetCurrentUserId();
        
        var exists = await _context.PriceAlertSubscriptions
            .AnyAsync(s => s.UserId == userId && s.GameId == request.GameId && s.PlatformId == request.PlatformId);

        if (exists) return Conflict(ApiResponse<object>.ErrorResponse("ERR_DUPLICATE", "已订阅"));

        var sub = new PriceAlertSubscription
        {
            UserId = userId,
            GameId = request.GameId,
            PlatformId = request.PlatformId,
            TargetPrice = request.TargetPrice,
            TargetDiscount = request.TargetDiscount,
            IsActive = true
        };

        _context.PriceAlertSubscriptions.Add(sub);
        await _context.SaveChangesAsync();

        return Created("", ApiResponse<object>.SuccessResponse(new { sub.SubscriptionId }, "价格提醒已设置"));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<object>>> GetAlertHistory()
    {
        var userId = GetCurrentUserId();
        
        // [修复] 将 l.PriceHistory 改为 l.Price
        var logs = await _context.PriceAlertLogs
            .Include(l => l.Subscription).ThenInclude(s => s.Game)
            .Include(l => l.Price) 
            .Where(l => l.Subscription.UserId == userId)
            .OrderByDescending(l => l.AlertTime)
            .Select(l => new
            {
                l.AlertId,
                l.SubscriptionId,
                GameName = l.Subscription.Game.Name,
                l.AlertType,
                l.AlertTime,
                PriceSnapshot = new 
                {
                    l.Price.CurrentPrice, // [修复]
                    l.Price.DiscountRate  // [修复]
                }
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { items = logs }));
    }
}