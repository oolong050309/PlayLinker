using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;

    public WishlistController(PlayLinkerDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    // GET /api/v1/wishlist
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetWishlist(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var query = _context.PriceAlertSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .Include(s => s.Game)
            .Include(s => s.Platform);

        var total = await query.CountAsync();
        var list = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 模拟获取当前价格 (实际应从 PriceHistory 获取最新一条)
        var items = new List<WishlistItemDto>();
        foreach (var sub in list)
        {
            // 获取最新价格记录
            var latestPrice = await _context.PriceHistories
                .Where(ph => ph.GameId == sub.GameId && ph.PlatformId == sub.PlatformId)
                .OrderByDescending(ph => ph.RecordDate)
                .FirstOrDefaultAsync();

            items.Add(new WishlistItemDto
            {
                SubscriptionId = sub.SubscriptionId,
                GameId = sub.GameId,
                GameName = sub.Game?.Name ?? "",
                HeaderImage = sub.Game?.HeaderImage ?? "",
                PlatformId = sub.PlatformId,
                PlatformName = sub.Platform?.PlatformName ?? "",
                CurrentPrice = latestPrice?.CurrentPrice ?? 0,
                OriginalPrice = latestPrice?.OriginalPrice ?? 0,
                IsOnSale = latestPrice?.IsDiscount ?? false,
                TargetPrice = sub.TargetPrice,
                TargetDiscount = sub.TargetDiscount,
                AddedAt = sub.CreatedAt
            });
        }

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            items,
            meta = new PaginationMeta { Page = page, PageSize = pageSize, Total = total }
        }));
    }

    // POST /api/v1/wishlist
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> AddToWishlist([FromBody] AddWishlistDto request)
    {
        var userId = GetCurrentUserId();

        // 检查是否已存在
        var exists = await _context.PriceAlertSubscriptions
            .AnyAsync(s => s.UserId == userId && s.GameId == request.GameId && s.PlatformId == request.PlatformId);

        if (exists)
        {
            return Conflict(ApiResponse<object>.ErrorResponse("ERR_DUPLICATE", "该游戏已在愿望单中"));
        }

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

        return Created("", ApiResponse<object>.SuccessResponse(new { sub.SubscriptionId }, "已添加到愿望单"));
    }

    // DELETE /api/v1/wishlist/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFromWishlist(long id)
    {
        var userId = GetCurrentUserId();
        var sub = await _context.PriceAlertSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == userId);

        if (sub == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "愿望单记录不存在"));
        }

        // 软删除，标记为非活动
        sub.IsActive = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null, "已从愿望单移除"));
    }
}