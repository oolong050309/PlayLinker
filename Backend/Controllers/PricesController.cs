using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO
using PlayLinker.Models.Entities;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/prices")]
public class PricesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly IAiService _aiService;

    public PricesController(PlayLinkerDbContext context, IAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }
    
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
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
    
    [HttpPost("track")]
    public async Task<ActionResult<ApiResponse<object>>> TrackPrice([FromBody] TrackPriceDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

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
            TargetDiscount = request.TargetDiscount ?? 50,
            IsActive = true
        };

        _context.PriceAlertSubscriptions.Add(sub);
        await _context.SaveChangesAsync();

        return Created("", ApiResponse<object>.SuccessResponse(new { 
            trackId = "track_" + sub.SubscriptionId,
            gameId = sub.GameId 
        }, "价格跟踪已启动"));
    }
}