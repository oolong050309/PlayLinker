using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/sales")]
public class SalesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;

    public SalesController(PlayLinkerDbContext context)
    {
        _context = context;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentSales([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var discountedGamesQuery = _context.PriceHistories
            .Where(p => p.IsDiscount == true)
            .GroupBy(p => p.GameId)
            .Select(g => g.OrderByDescending(p => p.RecordDate).First())
            .Include(p => p.Game);

        var total = await discountedGamesQuery.CountAsync();
        var games = await discountedGamesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var activeSale = new
        {
            saleId = 1,
            saleName = "每日特惠",
            platformName = "All Platforms",
            description = "实时抓取的折扣游戏",
            totalGames = total,
            featuredGames = games.Select(g => new
            {
                g.GameId,
                GameName = g.Game.Name,
                g.CurrentPrice,
                g.OriginalPrice,
                Discount = g.DiscountRate,
                g.Game.HeaderImage
            })
        };

        return Ok(ApiResponse<object>.SuccessResponse(new { activeSales = new[] { activeSale } }));
    }

    [HttpGet("upcoming")]
    public ActionResult<ApiResponse<object>> GetUpcomingSales()
    {
        var upcoming = new[]
        {
            new
            {
                saleId = 2,
                saleName = "Steam 冬季特卖 (预测)",
                platformName = "Steam",
                startDate = "2024-12-20T00:00:00Z",
                daysUntilStart = (new DateTime(2024, 12, 20) - DateTime.UtcNow).Days,
                description = "年度最大促销活动"
            }
        };

        return Ok(ApiResponse<object>.SuccessResponse(new { upcomingSales = upcoming }));
    }
}