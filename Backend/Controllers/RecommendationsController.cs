using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using PlayLinker.Models.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IAiService _aiService;

    public RecommendationsController(
        PlayLinkerDbContext context, 
        ILogger<RecommendationsController> logger,
        IAiService aiService)
    {
        _context = context;
        _logger = logger;
        _aiService = aiService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// 获取普通推荐列表
    /// </summary>
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
            r.Reason,
            r.CreatedAt,
            Tags = new[] { r.RecommendationType },
            r.Game.ReviewScore
        });

        return Ok(ApiResponse<object>.SuccessResponse(new { items = result }));
    }

    /// <summary>
    /// 获取探索推荐 (AI 推荐 + 规则推荐)
    /// </summary>
    [HttpGet("explore")]
    public async Task<ActionResult<ApiResponse<object>>> ExploreGames([FromQuery] bool refresh = false)
    {
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        const string TYPE_EXPLORE = "game";

        try 
        {
            List<Recommendation> aiRecs = new();
            List<Recommendation> ruleRecs = new();

            // 1. 尝试读取缓存 (如果未请求强制刷新)
            if (!refresh)
            {
                var allCached = await _context.Recommendations
                    .Include(r => r.Game).ThenInclude(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                    .Include(r => r.RecommendationFeedback)
                    .Where(r => r.UserId == userId 
                             && r.RecommendationType == TYPE_EXPLORE
                             && r.ExpireTime > now)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                aiRecs = allCached.Where(r => r.RecommendationStrategy == "hybrid").Take(3).ToList();
                ruleRecs = allCached.Where(r => r.RecommendationStrategy != "hybrid").Take(3).ToList();

                // 如果两部分都有数据，直接返回
                if (aiRecs.Count > 0 && ruleRecs.Count > 0)
                {
                    var hasAi = aiRecs.Any();
                    return Ok(ApiResponse<object>.SuccessResponse(new { 
                        aiCategory = "AI 深度探索 (缓存)", 
                        aiItems = MapToDto(aiRecs),
                        ruleCategory = "热门精选 (缓存)",
                        ruleItems = MapToDto(ruleRecs)
                    }));
                }
            }

            _logger.LogInformation($"[Explore] Generating NEW recommendations for User {userId}...");

            // 2. 准备基础数据 (排除已拥有和不喜欢的)
            var ownedIds = await _context.UserPlatformLibraries
                .Where(u => u.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                .Select(u => u.GameId)
                .ToListAsync();
            
            var dislikedIds = await _context.RecommendationFeedbacks
                .Where(f => f.UserId == userId && f.FeedbackResult == 2)
                .Select(f => f.Recommendation.GameId)
                .ToListAsync();

            var excludeIds = ownedIds.Concat(dislikedIds).Distinct().ToHashSet();

            // 3. 生成 AI 推荐
            try 
            {
                var pref = await _context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
                var recentGames = await _context.UserPlatformLibraries
                    .Where(u => u.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
                    .OrderByDescending(u => u.LastPlayed)
                    .Take(5)
                    .Select(u => u.Game.Name)
                    .ToListAsync();

                // [修复] 移除 ReviewScore >= 80 的限制，因为新入库游戏评分为0，但这不代表它们不好
                var candidates = await _context.Games
                    .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                    .Where(g => !excludeIds.Contains(g.GameId)) 
                    .OrderBy(x => EF.Functions.Random())
                    .Take(50)
                    .Select(g => new GameCandidateDto {
                        GameId = g.GameId,
                        Name = g.Name,
                        ReviewScore = g.ReviewScore,
                        Tags = g.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList()
                    })
                    .ToListAsync();

                if (candidates.Count == 0)
                {
                    _logger.LogWarning("[Explore] No candidates found for AI. Ensure DB has games.");
                }
                else 
                {
                    var aiContext = new AiRecommendationContextDto
                    {
                        PlaytimeRange = pref?.PlaytimeRange ?? "未知",
                        PriceSensitivity = pref?.PriceSensitivity ?? 2,
                        RecentGames = recentGames,
                        CandidateGames = candidates
                    };

                    // 调用 AI
                    var aiResults = await _aiService.GetRecommendationsAsync(aiContext);

                    if (aiResults != null && aiResults.Count > 0)
                    {
                        aiRecs = aiResults.Select(r => new Recommendation
                        {
                            UserId = userId,
                            GameId = r.GameId,
                            RecommendationType = TYPE_EXPLORE,
                            RecommendationStrategy = "hybrid", // 标识为 AI
                            Reason = r.Reason,
                            CreatedAt = DateTime.UtcNow,
                            ExpireTime = DateTime.UtcNow.AddHours(24)
                        }).ToList();
                        
                        // 将 AI 推荐的游戏 ID 加入排除列表，避免规则推荐重复
                        foreach (var r in aiResults) excludeIds.Add(r.GameId);
                    }
                }
            }
            catch (Exception aiEx)
            {
                _logger.LogError(aiEx, "[Explore] AI generation failed, falling back to rules.");
            }

            // 4. 生成规则推荐 (必定执行)
            ruleRecs = await GenerateRuleBasedRecommendations(userId, excludeIds);

            // 5. 保存所有新生成的记录
            var allNew = aiRecs.Concat(ruleRecs).ToList();
            if (allNew.Any())
            {
                _context.Recommendations.AddRange(allNew);
                await _context.SaveChangesAsync();
            }

            // 6. 重新加载完整数据以返回
            var newIds = allNew.Select(e => e.RecommendationId).ToList();
            var finalRecs = await _context.Recommendations
                .Include(r => r.Game).ThenInclude(g => g.GameGenres).ThenInclude(gg => gg.Genre)
                .Include(r => r.RecommendationFeedback)
                .Where(r => newIds.Contains(r.RecommendationId))
                .ToListAsync();

            var finalAiItems = finalRecs.Where(r => r.RecommendationStrategy == "hybrid").ToList();
            var finalRuleItems = finalRecs.Where(r => r.RecommendationStrategy != "hybrid").ToList();

            return Ok(ApiResponse<object>.SuccessResponse(new { 
                aiCategory = "AI 深度探索", 
                aiItems = MapToDto(finalAiItems),
                ruleCategory = "热门与偏好精选",
                ruleItems = MapToDto(finalRuleItems)
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Explore] Fatal error.");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "推荐生成失败"));
        }
    }

    private async Task<List<Recommendation>> GenerateRuleBasedRecommendations(int userId, HashSet<long> excludeIds)
    {
        // 1. 分析用户喜欢的类型 (Top Genres)
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

        var query = _context.Games
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .AsQueryable();

        string strategy = "popular";
        string reasonTemplate = "近期热门游戏";

        if (topGenreIds.Any())
        {
            // 基于内容推荐
            query = query.Where(g => g.GameGenres.Any(gg => topGenreIds.Contains(gg.GenreId)));
            strategy = "content_based";
            reasonTemplate = "根据您的游戏库风格推荐";
        }
        // [修复] 移除 ReviewScore 限制，因为部分游戏数据不全
        // else { query = query.Where(g => g.ReviewScore > 85); }

        var candidates = await query
            .Where(g => !excludeIds.Contains(g.GameId))
            .OrderBy(x => EF.Functions.Random())
            .Take(3)
            .ToListAsync();

        // 如果规则推荐没找到（例如类型太冷门），再次尝试全局随机热门
        if (candidates.Count == 0)
        {
            candidates = await _context.Games
                .Where(g => !excludeIds.Contains(g.GameId))
                .OrderBy(x => EF.Functions.Random())
                .Take(3)
                .ToListAsync();
            strategy = "popular";
            reasonTemplate = "为您精选的热门游戏";
        }

        return candidates.Select(g => new Recommendation
        {
            UserId = userId,
            GameId = g.GameId,
            RecommendationType = "game",
            RecommendationStrategy = strategy,
            Reason = reasonTemplate,
            CreatedAt = DateTime.UtcNow,
            ExpireTime = DateTime.UtcNow.AddHours(24)
        }).ToList();
    }

    [HttpPost("{id}/feedback")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitFeedback(int id, [FromBody] FeedbackRequestDto request)
    {
        var userId = GetCurrentUserId();
        try 
        {
            var existing = await _context.RecommendationFeedbacks
                .FirstOrDefaultAsync(f => f.RecommendationId == id && f.UserId == userId);

            if (existing != null)
            {
                existing.FeedbackResult = request.FeedbackResult;
                existing.Remark = request.Remark;
                existing.FeedbackTime = DateTime.UtcNow;
            }
            else
            {
                var feedback = new RecommendationFeedback
                {
                    RecommendationId = id,
                    UserId = userId,
                    FeedbackResult = request.FeedbackResult,
                    Remark = request.Remark,
                    FeedbackTime = DateTime.UtcNow
                };
                _context.RecommendationFeedbacks.Add(feedback);
            }
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<object>.SuccessResponse(null, "反馈已记录"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback error");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "反馈失败"));
        }
    }

    private object MapToDto(List<Recommendation> recs)
    {
        return recs.Select(r => new
        {
            r.RecommendationId,
            r.Game.GameId,
            GameName = r.Game.Name,
            r.Game.HeaderImage,
            r.Game.ReviewScore,
            r.Game.ReleaseDate,
            WhyExplore = r.Reason,
            UniqueFeatures = r.Game.GameGenres.Select(gg => gg.Genre.Name).Take(3).ToList(),
            UserFeedback = r.RecommendationFeedback?.FeedbackResult ?? 0 
        });
    }
}