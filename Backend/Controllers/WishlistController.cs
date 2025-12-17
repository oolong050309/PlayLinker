using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO
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

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetWishlist([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var query = _context.PriceAlertSubscriptions
            .Where(w => w.UserId == userId && (w.IsActive ?? true))
            .Include(s => s.Game)
            .Include(s => s.Platform);

        var total = await query.CountAsync();
        var list = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = new List<WishlistItemDto>();
        foreach (var sub in list)
        {
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
                AddedAt = sub.CreatedAt ?? DateTime.UtcNow
            });
        }

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            items,
            meta = new PaginationMeta { Page = page, PageSize = pageSize, Total = total }
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> AddToWishlist([FromBody] AddWishlistDto request)
    {
        var userId = GetCurrentUserId();
        var exists = await _context.PriceAlertSubscriptions
            .AnyAsync(s => s.UserId == userId && s.GameId == request.GameId && s.PlatformId == request.PlatformId);

        if (exists) return Conflict(ApiResponse<object>.ErrorResponse("ERR_DUPLICATE", "该游戏已在愿望单中"));

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

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFromWishlist(long id)
    {
        var userId = GetCurrentUserId();
        var sub = await _context.PriceAlertSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == userId);

        if (sub == null) return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "愿望单记录不存在"));

        sub.IsActive = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { }, "已从愿望单移除"));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateWishlist(long id, [FromBody] UpdateWishlistDto request)
    {
        var userId = GetCurrentUserId();
        var sub = await _context.PriceAlertSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == userId);

        if (sub == null) return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "愿望单记录不存在"));

        if (request.TargetPrice.HasValue) sub.TargetPrice = request.TargetPrice.Value;
        if (request.TargetDiscount.HasValue) sub.TargetDiscount = request.TargetDiscount.Value;
        
        sub.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { sub.SubscriptionId, sub.UpdatedAt }, "愿望单设置已更新"));
    }
}