using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(PlayLinkerDbContext context, ILogger<RecommendationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("[Auth] Failed to parse user_id from token claims.");
            return 0;
        }
        return userId;
    }

    /// <summary>
    /// 获取推荐列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetRecommendations([FromQuery] string? type, [FromQuery] int limit = 10)
    {
        var userId = GetCurrentUserId();
        
        // 构造基础查询
        var query = _context.Recommendations
            .Include(r => r.Game)
            .Where(r => r.UserId == userId && r.ExpireTime > DateTime.UtcNow);

        // 过滤类型
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(r => r.RecommendationType == type);
        }
        else
        {
            // 如果不需要特别过滤 Explore 的记录，此处可以移除之前的 RecommendationType != "Explore" 判断
            // 因为现在统一使用符合数据库定义的类型（如 'game'）
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync();

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
            Tags = new[] { r.RecommendationType }, 
            r.Game.ReviewScore 
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }

    /// <summary>
    /// 探索新游戏 (带缓存机制)
    /// </summary>
    [HttpGet("explore")]
    public async Task<ActionResult<ApiResponse<object>>> ExploreGames([FromQuery] bool refresh = false)
    {
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        // [修复]：数据库 Enum 定义不支持 "Explore"，必须使用 'game', 'discount', 'similar', 'trending' 之一
        // 这里使用 'game' 作为探索功能的类型
        const string EXPLORE_TYPE_DB_VALUE = "game"; 

        try 
        {
            // 1. 尝试从数据库获取缓存的推荐 (如果不是强制刷新)
            if (!refresh)
            {
                var cachedRecs = await _context.Recommendations
                    .Include(r => r.Game)
                    .ThenInclude(g => g.GameGenres)
                    .ThenInclude(gg => gg.Genre)
                    .Where(r => r.UserId == userId 
                             && r.RecommendationType == EXPLORE_TYPE_DB_VALUE
                             && r.ExpireTime > now)
                    .OrderByDescending(r => r.CreatedAt) // 获取最新的
                    .Take(3) // 限制数量，防止取到历史旧数据
                    .ToListAsync();

                if (cachedRecs.Count > 0)
                {
                    _logger.LogInformation($"[ExploreGames] Returning {cachedRecs.Count} cached items for User {userId}.");
                    
                    // [修复]：策略值必须匹配数据库 Enum ('content_based', 'popular', etc.)
                    var cachedStrategy = cachedRecs.FirstOrDefault()?.RecommendationStrategy;
                    var cachedTitle = cachedStrategy == "content_based" ? "AI 智能探索" : "热门推荐";

                    var cachedItems = cachedRecs.Select(r => new
                    {
                        r.Game.GameId,
                        GameName = r.Game.Name,
                        r.Game.HeaderImage,
                        Genres = r.Game.GameGenres.Select(gg => gg.Genre.Name).ToList(),
                        r.Game.ReleaseDate,
                        r.Game.ReviewScore,
                        CurrentPrice = 0, 
                        WhyExplore = r.Reason,
                        UniqueFeatures = r.Game.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList()
                    });

                    return Ok(ApiResponse<object>.SuccessResponse(new { 
                        exploreCategory = cachedTitle, 
                        items = cachedItems 
                    }));
                }
            }

            // 2. 如果没有缓存或强制刷新，执行生成逻辑
            _logger.LogInformation($"[ExploreGames] Generating NEW recommendations for User {userId} (Refresh: {refresh})...");

            // 2.1 获取用户偏好 (Top 3 Genres)
            var recentGenreIds = await _context.UserPlatformLibraries
                .Include(upl => upl.Game).ThenInclude(g => g.GameGenres)
                .Where(upl => upl.PlayerPlatform.UserPlatformBindings.Any(upb => upb.UserId == userId))
                .OrderByDescending(upl => upl.LastPlayed)
                .Take(20)
                .SelectMany(upl => upl.Game.GameGenres.Select(gg => gg.GenreId))
                .ToListAsync();

            var topGenreIds = recentGenreIds
                .GroupBy(id => id)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            // 2.2 获取已拥有游戏ID (排除用)
            var ownedGameIds = await _context.UserPlatformLibraries
                 .Where(upl => upl.PlayerPlatform.UserPlatformBindings.Any(upb => upb.UserId == userId))
                 .Select(upl => upl.GameId)
                 .ToListAsync();

            // 2.3 构建查询
            var query = _context.Games
                .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .AsQueryable();

            // [修复]：策略字符串必须匹配数据库 Enum 定义 (全小写下划线)
            string strategyName = "popular"; // 对应 'popular'
            string title = "热门推荐";
            string defaultReason = "近期热门高分游戏";

            if (topGenreIds.Any())
            {
                query = query.Where(g => g.GameGenres.Any(gg => topGenreIds.Contains(gg.GenreId)));
                strategyName = "content_based"; // 对应 'content_based'
                title = "AI 智能探索";
                defaultReason = "根据您的游戏库风格推荐";
            }
            else 
            {
                query = query.Where(g => g.ReviewScore > 85);
            }

            // 2.4 随机获取 3 个候选
            var candidates = await query
                .Where(g => !ownedGameIds.Contains(g.GameId))
                .OrderBy(x => EF.Functions.Random())
                .Take(3)
                .ToListAsync();

            // 2.5 写入数据库
            // 注意：由于我们将类型改为了 "game"，不能简单地删除所有 "game" 类型的记录，因为可能误删其他推荐
            // 这里的策略改为：只添加新记录，利用 ExpireTime 来控制有效性
            
            var newRecEntities = candidates.Select(g => new Recommendation
            {
                UserId = userId,
                GameId = g.GameId,
                RecommendationType = EXPLORE_TYPE_DB_VALUE, // 使用 'game'
                RecommendationStrategy = strategyName,      // 使用 'popular' 或 'content_based'
                Reason = defaultReason,
                CreatedAt = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddHours(24) // 24小时有效期
            }).ToList();

            if (newRecEntities.Any())
            {
                _context.Recommendations.AddRange(newRecEntities);
                await _context.SaveChangesAsync();
            }

            // 2.6 返回结果
            var items = candidates.Select(g => new
            {
                g.GameId,
                GameName = g.Name,
                g.HeaderImage,
                Genres = g.GameGenres.Select(gg => gg.Genre.Name).ToList(),
                g.ReleaseDate,
                g.ReviewScore,
                CurrentPrice = 0, 
                WhyExplore = defaultReason,
                UniqueFeatures = g.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList()
            });

            return Ok(ApiResponse<object>.SuccessResponse(new { 
                exploreCategory = title, 
                items = items 
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExploreGames] Error occurred.");
            // 返回具体的错误信息以便调试，生产环境可隐藏
            return StatusCode(500, ApiResponse<object>.ErrorResponse("INTERNAL_ERROR", ex.InnerException?.Message ?? ex.Message));
        }
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

        return Ok(ApiResponse<object>.SuccessResponse(new { 
            feedbackId = feedback.FeedbackId,
            recommendationId = id,
            feedbackTime = feedback.FeedbackTime,
            impact = "您的反馈将帮助我们改进推荐算法"
        }, "感谢您的反馈"));
    }

    [HttpGet("similar/{gameId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetSimilarGames(long gameId)
    {
        var sourceGame = await _context.Games
            .Include(g => g.GameGenres)
            .FirstOrDefaultAsync(g => g.GameId == gameId);

        if (sourceGame == null) 
            return NotFound(ApiResponse<object>.ErrorResponse("NOT_FOUND", "游戏不存在"));

        var genreIds = sourceGame.GameGenres.Select(gg => gg.GenreId).ToList();

        var similarGames = await _context.Games
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Where(g => g.GameId != gameId && g.GameGenres.Any(gg => genreIds.Contains(gg.GenreId)))
            .OrderByDescending(g => g.ReviewScore)
            .Take(5)
            .Select(g => new 
            {
                g.GameId,
                GameName = g.Name,
                HeaderImage = g.HeaderImage,
                SimilarityScore = 0.85, 
                CommonTags = g.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList(),
                CurrentPrice = 0,
                g.ReviewScore
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { 
            sourceGameId = gameId, 
            sourceGameName = sourceGame.Name,
            similarGames = similarGames,
            totalCount = similarGames.Count
        }));
    }
}