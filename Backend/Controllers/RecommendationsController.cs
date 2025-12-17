using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;

    public RecommendationsController(PlayLinkerDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetRecommendations([FromQuery] string? type, [FromQuery] int limit = 10)
    {
        var userId = GetCurrentUserId();
        var query = _context.Recommendations
            .Include(r => r.Game)
            .Where(r => r.UserId == userId && r.ExpireTime > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(r => r.RecommendationType == type);
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync();

        var result = list.Select(r => new
        {
            r.RecommendationId,
            r.GameId,
            GameName = r.Game.Name,
            HeaderImage = r.Game.HeaderImage,
            r.RecommendationType,
            r.RecommendationStrategy,
            Score = 0.95,
            r.Reason,
            r.CreatedAt,
            r.ExpireTime
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }

    [HttpGet("explore")]
    public async Task<ActionResult<ApiResponse<object>>> ExploreGames()
    {
        var games = await _context.Games
            .Where(g => g.ReviewScore > 80)
            .OrderBy(r => Guid.NewGuid())
            .Take(5)
            .Select(g => new
            {
                g.GameId,
                GameName = g.Name,
                g.HeaderImage,
                g.ReviewScore,
                g.ReleaseDate,
                WhyExplore = "高评分游戏推荐"
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { exploreCategory = "热门探索", items = games }));
    }

    [HttpPost("{id}/feedback")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitFeedback(int id, [FromBody] FeedbackRequestDto request)
    {
        var userId = GetCurrentUserId();
        var feedback = new RecommendationFeedback
        {
            RecommendationId = id,
            UserId = userId,
            FeedbackResult = request.FeedbackResult,
            Remark = request.Remark,
            FeedbackTime = DateTime.UtcNow
        };

        _context.RecommendationFeedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { feedback.FeedbackId }, "感谢您的反馈"));
    }

    [HttpGet("similar/{gameId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetSimilarGames(long gameId)
    {
        var sourceGameGenres = await _context.GameGenres
            .Where(gg => gg.GameId == gameId)
            .Select(gg => gg.GenreId)
            .ToListAsync();

        var similarGames = await _context.GameGenres
            .Where(gg => sourceGameGenres.Contains(gg.GenreId) && gg.GameId != gameId)
            .GroupBy(gg => gg.Game)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new
            {
                g.Key.GameId,
                GameName = g.Key.Name,
                g.Key.HeaderImage,
                SimilarityScore = 0.85
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { sourceGameId = gameId, similarGames }));
    }
}