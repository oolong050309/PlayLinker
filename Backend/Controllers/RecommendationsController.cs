using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using Microsoft.Extensions.Logging; // [新增] 引用日志命名空间

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<RecommendationsController> _logger; // [新增] 日志对象

    // [修改] 构造函数注入 logger
    public RecommendationsController(PlayLinkerDbContext context, ILogger<RecommendationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        // [新增] 记录解析的用户ID
        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("[Auth] Failed to parse user_id from token claims.");
            return 0;
        }
        return userId;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetRecommendations([FromQuery] string? type, [FromQuery] int limit = 10)
    {
        var userId = GetCurrentUserId();
        // [新增] 日志
        _logger.LogInformation($"[GetRecommendations] Fetching for User {userId}, Type: {type}, Limit: {limit}");

        var query = _context.Recommendations
            .Include(r => r.Game)
            .Where(r => r.UserId == userId && r.ExpireTime > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(r => r.RecommendationType == type);
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync();
        
        _logger.LogInformation($"[GetRecommendations] Found {list.Count} items.");

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
            r.ExpireTime,
            tags = new[] { r.RecommendationType }, 
            reviewScore = 90 
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }

    // [修改] 探索逻辑 + 详细日志
    [HttpGet("explore")]
    public async Task<ActionResult<ApiResponse<object>>> ExploreGames()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation($"[ExploreGames] Start exploring for User {userId}...");

        try 
        {
            // 1. 获取用户玩过的游戏类型偏好
            var recentGenreIds = await _context.UserPlatformLibraries
                .Include(upl => upl.Game).ThenInclude(g => g.GameGenres)
                .Where(upl => upl.PlayerPlatform.UserPlatformBindings.Any(upb => upb.UserId == userId))
                .OrderByDescending(upl => upl.LastPlayed)
                .Take(20)
                .SelectMany(upl => upl.Game.GameGenres.Select(gg => gg.GenreId))
                .ToListAsync();

            _logger.LogInformation($"[ExploreGames] Found {recentGenreIds.Count} genre records from recent games.");

            // 统计最常玩的 Top 3 类型
            var topGenreIds = recentGenreIds
                .GroupBy(id => id)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            _logger.LogInformation($"[ExploreGames] Top Genre IDs: {string.Join(",", topGenreIds)}");
            
            // 2. 获取用户已拥有的游戏ID
            var ownedGameIds = await _context.UserPlatformLibraries
                 .Where(upl => upl.PlayerPlatform.UserPlatformBindings.Any(upb => upb.UserId == userId))
                 .Select(upl => upl.GameId)
                 .ToListAsync();

            _logger.LogInformation($"[ExploreGames] User owns {ownedGameIds.Count} games. These will be excluded.");

            // 3. 构建推荐查询
            var query = _context.Games
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .AsQueryable();

            if (topGenreIds.Any())
            {
                query = query.Where(g => g.GameGenres.Any(gg => topGenreIds.Contains(gg.GenreId)));
                _logger.LogInformation("[ExploreGames] Strategy: Content-Based (Matching Top Genres)");
            }
            else 
            {
                query = query.Where(g => g.ReviewScore > 85);
                _logger.LogInformation("[ExploreGames] Strategy: Popularity (No preference data found)");
            }

            // 4. 执行查询
            var rawList = await query
                .Where(g => !ownedGameIds.Contains(g.GameId))
                .Take(50) // 先取一部分再随机，避免全表扫
                .ToListAsync();
            
            _logger.LogInformation($"[ExploreGames] Query returned {rawList.Count} candidates before random selection.");

            var recommendations = rawList
                .OrderBy(r => Guid.NewGuid()) // 内存中随机排序
                .Take(3)
                .Select(g => new
                {
                    g.GameId,
                    GameName = g.Name,
                    g.HeaderImage,
                    g.ReviewScore,
                    g.ReleaseDate,
                    WhyExplore = topGenreIds.Any() ? "根据您的游戏库风格推荐" : "近期热门高分游戏",
                    UniqueFeatures = g.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList()
                })
                .ToList();

            _logger.LogInformation($"[ExploreGames] Returning {recommendations.Count} final recommendations.");

            return Ok(ApiResponse<object>.SuccessResponse(new { 
                exploreCategory = topGenreIds.Any() ? "AI 智能探索" : "热门推荐", 
                items = recommendations 
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExploreGames] Error occurred during exploration.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", ex.Message));
        }
    }

    [HttpPost("{id}/feedback")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitFeedback(int id, [FromBody] FeedbackRequestDto request)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation($"[SubmitFeedback] User {userId} feedback for Rec {id}: Result={request.FeedbackResult}");
        
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
        return Ok(ApiResponse<object>.SuccessResponse(new { sourceGameId = gameId, similarGames = new List<object>() }));
    }
}