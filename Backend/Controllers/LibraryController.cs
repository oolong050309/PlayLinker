using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.Linq;

namespace PlayLinker.Controllers;

/// <summary>
/// 游戏库管理API控制器
/// 提供用户游戏库概览、游戏列表、同步等功能(需要认证)
/// </summary>
[ApiController]
[Route("api/v1/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<LibraryController> _logger;

    public LibraryController(PlayLinkerDbContext context, ILogger<LibraryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 获取当前用户ID(从JWT Token中)
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("无法从JWT token中获取用户ID，token claims: {Claims}", 
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            throw new UnauthorizedAccessException("无法获取用户ID，请重新登录");
        }
        return userId;
    }

    /// <summary>
    /// 获取游戏库概览
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<LibraryOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LibraryOverviewDto>>> GetLibraryOverview()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取游戏库概览: userId={UserId}", userId);

            // 获取用户绑定的所有平台账号
            var userPlatformBindings = await _context.UserPlatformBindings
                .Where(upb => upb.UserId == userId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync();

            if (!userPlatformBindings.Any())
            {
                // 返回空数据
                var emptyResult = new LibraryOverviewDto
                {
                    TotalGamesOwned = 0,
                    GamesPlayed = 0,
                    TotalPlaytimeMinutes = 0,
                    TotalAchievements = 0,
                    UnlockedAchievements = 0,
                    RecentlyPlayedCount = 0,
                    RecentPlaytimeMinutes = 0,
                    PlatformStats = new List<PlatformStatsDto>(),
                    GenreDistribution = new List<GenreDistributionDto>()
                };
                return Ok(ApiResponse<LibraryOverviewDto>.SuccessResponse(emptyResult));
            }

            // 定义五个平台的ID：Steam(1), Epic Games(2), GOG(5), PSN(6), Xbox(7)
            var platformIds = new[] { 1, 2, 5, 6, 7 };

            // 获取用户绑定的平台用户ID和平台ID的组合（转换为列表以便在客户端使用）
            var bindingKeys = userPlatformBindings
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToList();

            // 获取用户在所有五个平台上的游戏库记录
            // 先查询所有符合条件的游戏，然后在客户端过滤
            var allPlatformGamesQuery = _context.UserPlatformLibraries
                .Where(upl => platformIds.Contains(upl.PlatformId));
            
            var allPlatformGamesTemp = await allPlatformGamesQuery.ToListAsync();
            
            // 在客户端过滤，匹配用户绑定的平台账号
            var allPlatformGames = allPlatformGamesTemp
                .Where(upl => bindingKeys.Any(bk => bk.PlatformUserId == upl.PlatformUserId && bk.PlatformId == upl.PlatformId))
                .ToList();

            // 统计所有平台的游戏数量（去重，因为同一游戏可能在不同平台）
            var uniqueGameIds = allPlatformGames.Select(g => g.GameId).Distinct().ToList();
            var totalGamesOwned = allPlatformGames.Count(); // 所有平台的游戏记录总数
            var gamesPlayed = uniqueGameIds.Count(); // 去重后的游戏数量
            var totalPlaytimeMinutes = allPlatformGames.Sum(g => g.PlaytimeMinutes);

            // 统计所有五个平台的成就数量
            var unlockedAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked && platformIds.Contains(ua.PlatformId))
                .CountAsync();

            var totalAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && platformIds.Contains(ua.PlatformId))
                .Select(ua => ua.AchievementId)
                .Distinct()
                .CountAsync();

            // 获取最近游玩的游戏（所有平台）
            var recentlyPlayedGames = allPlatformGames
                .Where(g => g.LastPlayed.HasValue)
                .OrderByDescending(g => g.LastPlayed)
                .Take(10)
                .ToList();
            var recentlyPlayedCount = recentlyPlayedGames.Count;
            var recentPlaytimeMinutes = recentlyPlayedGames.Sum(g => g.PlaytimeMinutes);

            // 获取平台统计(从数据库查询实际数据)
            var platformStatsList = new List<PlatformStatsDto>();
            foreach (var platformId in platformIds)
            {
                var platform = await _context.Platforms.FindAsync(platformId);
                if (platform == null) continue;

                var platformBindingUserIds = bindingKeys
                    .Where(bk => bk.PlatformId == platformId)
                    .Select(bk => bk.PlatformUserId)
                    .ToList();

                if (platformBindingUserIds.Count == 0)
                {
                    continue; // 该平台没有绑定账号，跳过
                }

                // 先查询该平台的所有游戏，然后在客户端过滤
                var platformGamesQuery = _context.UserPlatformLibraries
                    .Where(upl => upl.PlatformId == platformId);
                
                var platformGamesTemp = await platformGamesQuery.ToListAsync();
                var gamesOwned = platformGamesTemp
                    .Count(upl => platformBindingUserIds.Contains(upl.PlatformUserId));

                if (gamesOwned > 0)
                {
                    var binding = await _context.UserPlatformBindings
                        .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == platformId && upb.BindingStatus == true);
                    
                    var lastSyncTime = binding?.LastSyncTime?.ToString("yyyy-MM-ddTHH:mm:ssZ") 
                        ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

                    platformStatsList.Add(new PlatformStatsDto
                    {
                        PlatformId = platform.PlatformId,
                        PlatformName = platform.PlatformName,
                        GamesOwned = gamesOwned,
                        LastSyncTime = lastSyncTime
                    });
                }
            }
            var platformStats = platformStatsList;

            // 获取题材分布(从数据库查询实际数据)
            var gameGenres = await _context.GameGenres
                .Where(gg => uniqueGameIds.Contains(gg.GameId))
                .Include(gg => gg.Genre)
                .ToListAsync();

            var genreDistribution = gameGenres
                .GroupBy(gg => gg.Genre?.Name ?? "")
                .Select(g => new GenreDistributionDto
                {
                    Genre = g.Key,
                    Count = g.Count(),
                    PlaytimeMinutes = allPlatformGames
                        .Where(upl => g.Any(gg => gg.GameId == upl.GameId))
                        .Sum(upl => upl.PlaytimeMinutes)
                })
                .OrderByDescending(gd => gd.PlaytimeMinutes)
                .Take(10)
                .ToList();

            var result = new LibraryOverviewDto
            {
                TotalGamesOwned = totalGamesOwned,
                GamesPlayed = gamesPlayed,
                TotalPlaytimeMinutes = totalPlaytimeMinutes,
                TotalAchievements = totalAchievements,
                UnlockedAchievements = unlockedAchievements,
                RecentlyPlayedCount = recentlyPlayedCount,
                RecentPlaytimeMinutes = recentPlaytimeMinutes,
                PlatformStats = platformStats,
                GenreDistribution = genreDistribution
            };

            return Ok(ApiResponse<LibraryOverviewDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏库概览时发生错误");
            return StatusCode(500, ApiResponse<LibraryOverviewDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取用户游戏列表
    /// </summary>
    /// <param name="platform">平台筛选</param>
    /// <param name="sortBy">排序字段</param>
    /// <param name="gameId">可选：指定游戏ID，仅返回该游戏</param>
    /// <param name="search">搜索关键词（游戏名称）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    [HttpGet("games")]
    [ProducesResponseType(typeof(ApiResponse<UserGameListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserGameListDto>>> GetUserGames(
        [FromQuery] string? platform = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? gameId = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取用户游戏列表: userId={UserId}, platform={Platform}, sortBy={SortBy}, page={Page}, pageSize={PageSize}", 
                userId, platform, sortBy, page, pageSize);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // 获取用户绑定的所有平台账号的 platform_user_id 和 platform_id 组合
            var userPlatformBindings = await _context.UserPlatformBindings
                .Where(upb => upb.UserId == userId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync();

            _logger.LogInformation("用户 {UserId} 绑定的平台账号数量: {Count}", userId, userPlatformBindings.Count);
            foreach (var binding in userPlatformBindings)
            {
                _logger.LogInformation("  - PlatformId: {PlatformId}, PlatformUserId: {PlatformUserId}", 
                    binding.PlatformId, binding.PlatformUserId);
            }

            if (userPlatformBindings.Count == 0)
            {
                _logger.LogInformation("用户 {UserId} 没有绑定的平台账号", userId);
                var emptyResult = new UserGameListDto
                {
                    Items = new List<UserGameItemDto>(),
                    Meta = new PaginationMeta
                    {
                        Page = page,
                        PageSize = pageSize,
                        Total = 0
                    }
                };
                return Ok(ApiResponse<UserGameListDto>.SuccessResponse(emptyResult));
            }

            // 构建查询条件：必须同时精确匹配 PlatformUserId 和 PlatformId 的组合
            // 这样可以确保只查询当前用户绑定的平台账号的游戏
            _logger.LogInformation("用户绑定组合数量: {Count}", userPlatformBindings.Count);
            foreach (var binding in userPlatformBindings)
            {
                _logger.LogInformation("  - PlatformId: {PlatformId}, PlatformUserId: {PlatformUserId}", 
                    binding.PlatformId, binding.PlatformUserId);
            }

            // 直接通过绑定组合查询，确保只返回当前用户的游戏
            // 为每个绑定组合单独查询，确保精确匹配
            var userGames = new List<UserPlatformLibrary>();
            
            foreach (var binding in userPlatformBindings)
            {
                _logger.LogInformation("查询绑定: PlatformId={PlatformId}, PlatformUserId={PlatformUserId}", 
                    binding.PlatformId, binding.PlatformUserId);
                
                var gamesForBinding = await _context.UserPlatformLibraries
                    .Where(upl => upl.PlatformUserId == binding.PlatformUserId && upl.PlatformId == binding.PlatformId)
                    .Include(upl => upl.Game)
                    .Include(upl => upl.PlayerPlatform)
                        .ThenInclude(pp => pp.Platform)
                    .ToListAsync();
                
                _logger.LogInformation("绑定 (PlatformId={PlatformId}, PlatformUserId={PlatformUserId}) 查询到 {Count} 条游戏库记录", 
                    binding.PlatformId, binding.PlatformUserId, gamesForBinding.Count);
                
                userGames.AddRange(gamesForBinding);
            }

            _logger.LogInformation("总共查询到 {Count} 条用户游戏库记录", userGames.Count);

            // 平台筛选
            if (!string.IsNullOrEmpty(platform) && int.TryParse(platform, out int platformId))
            {
                userGames = userGames.Where(upl => upl.PlatformId == platformId).ToList();
                _logger.LogInformation("平台筛选后剩余 {Count} 条记录", userGames.Count);
            }

            // 按游戏ID筛选（用于游戏详情页只取当前游戏）
            if (gameId.HasValue)
            {
                userGames = userGames.Where(upl => upl.GameId == gameId.Value).ToList();
                _logger.LogInformation("按 gameId={GameId} 筛选后剩余 {Count} 条记录", gameId.Value, userGames.Count);
            }

            // 去重（同一游戏可能在不同平台）- 在内存中处理
            var distinctGamesList = userGames
                .GroupBy(upl => upl.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Game = g.First().Game,
                    TotalPlaytime = g.Sum(upl => upl.PlaytimeMinutes),
                    LastPlayed = g.Max(upl => upl.LastPlayed),
                    Platforms = g.Select(upl => upl.PlayerPlatform?.Platform).Where(p => p != null).Distinct().ToList(),
                    PlatformLibraries = g.ToList()
                })
                .ToList();

            // 搜索筛选（按游戏名称）
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                distinctGamesList = distinctGamesList
                    .Where(g => g.Game != null && !string.IsNullOrEmpty(g.Game.Name) && 
                           g.Game.Name.ToLower().Contains(searchLower))
                    .ToList();
                _logger.LogInformation("搜索关键词 '{Search}' 筛选后剩余 {Count} 条记录", search, distinctGamesList.Count);
            }

            // 排序（在内存中）
            switch (sortBy?.ToLower())
            {
                case "playtime":
                    distinctGamesList = distinctGamesList.OrderByDescending(g => g.TotalPlaytime).ToList();
                    break;
                case "lastplayed":
                    distinctGamesList = distinctGamesList.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue).ToList();
                    break;
                case "name":
                default:
                    distinctGamesList = distinctGamesList.OrderBy(g => g.Game.Name).ToList();
                    break;
            }

            // 获取总数（在内存中）
            var total = distinctGamesList.Count;

            // 分页（在内存中）
            var games = distinctGamesList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            _logger.LogInformation("查询到 {Count} 个游戏（分页后）", games.Count);

            // 获取每个游戏的成就进度
            var gameIds = games.Select(g => g.GameId).Distinct().ToList();
            
            if (gameIds.Count == 0)
            {
                // 没有游戏，返回空结果
                var emptyResult = new UserGameListDto
                {
                    Items = new List<UserGameItemDto>(),
                    Meta = new PaginationMeta
                    {
                        Page = page,
                        PageSize = pageSize,
                        Total = 0
                    }
                };
                return Ok(ApiResponse<UserGameListDto>.SuccessResponse(emptyResult));
            }
            
            // 获取所有相关成就
            var allAchievements = await _context.Achievements
                .Where(a => gameIds.Contains(a.GameId))
                .Select(a => new { a.AchievementId, a.GameId })
                .ToListAsync();
            
            var achievementIds = allAchievements.Select(a => a.AchievementId).ToList();
            
            // 获取用户已解锁的成就
            var userUnlockedAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked && achievementIds.Contains(ua.AchievementId))
                .Select(ua => ua.AchievementId)
                .ToListAsync();
            
            // 按游戏分组统计
            var achievementDict = allAchievements
                .GroupBy(a => a.GameId)
                .ToDictionary(
                    g => g.Key,
                    g => {
                        var total = g.Count();
                        var unlocked = g.Count(a => userUnlockedAchievements.Contains(a.AchievementId));
                        return total > 0 ? (double)unlocked / total * 100 : 0;
                    }
                );

            // 获取所有游戏的成就总数
            var gameAchievementCounts = await _context.Achievements
                .Where(a => gameIds.Contains(a.GameId))
                .GroupBy(a => a.GameId)
                .Select(g => new { GameId = g.Key, TotalCount = g.Count() })
                .ToDictionaryAsync(x => (long)x.GameId, x => x.TotalCount);
            
            // 构建返回数据
            var items = games.Select(g => {
                var totalCount = gameAchievementCounts.GetValueOrDefault(g.GameId, 0);
                var progress = achievementDict.GetValueOrDefault(g.GameId, 0);
                var unlockedCount = totalCount > 0 ? (int)(progress / 100.0 * totalCount) : 0;
                
                return new UserGameItemDto
                {
                    GameId = g.GameId,
                    Name = g.Game.Name ?? "",
                    HeaderImage = g.Game.HeaderImage ?? "",
                    Platforms = g.Platforms.Select(p => p.PlatformId).ToList(),
                    PlaytimeMinutes = g.TotalPlaytime,
                    LastPlayed = g.LastPlayed?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    AchievementsUnlocked = unlockedCount,
                    AchievementsTotal = totalCount,
                    OwnedPlatforms = g.PlatformLibraries.Select(upl => new OwnedPlatformDto
                    {
                        PlatformId = upl.PlatformId,
                        PlatformName = upl.PlayerPlatform?.Platform?.PlatformName ?? "",
                        PlaytimeMinutes = upl.PlaytimeMinutes
                    }).ToList()
                };
            }).ToList();

            var result = new UserGameListDto
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            _logger.LogInformation("返回游戏列表: 总数={Total}, 当前页={Page}, 返回项数={Count}", 
                total, page, items.Count);

            return Ok(ApiResponse<UserGameListDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户游戏列表时发生错误");
            return StatusCode(500, ApiResponse<UserGameListDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 同步平台数据
    /// </summary>
    /// <param name="request">同步请求</param>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<SyncPlatformResponseDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<SyncPlatformResponseDto>>> SyncPlatformData(
        [FromBody] SyncPlatformRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("同步平台数据: userId={UserId}, platformId={PlatformId}", userId, request.PlatformId);

            // 生成任务ID
            var taskId = $"sync_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            var result = new SyncPlatformResponseDto
            {
                TaskId = taskId,
                Status = "processing",
                EstimatedTime = 30,
                GamesDetected = 0
            };

            return Task.FromResult<ActionResult<ApiResponse<SyncPlatformResponseDto>>>(
                Ok(ApiResponse<SyncPlatformResponseDto>.SuccessResponse(result, "同步任务已启动")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步平台数据时发生错误");
            return Task.FromResult<ActionResult<ApiResponse<SyncPlatformResponseDto>>>(
                StatusCode(500, ApiResponse<SyncPlatformResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误")));
        }
    }

    /// <summary>
    /// 获取游戏统计数据
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<GameStatsDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<GameStatsDto>>> GetGameStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取游戏统计数据: userId={UserId}", userId);

            var result = new GameStatsDto
            {
                TotalPlaytime = 0,
                AveragePlaytime = 0,
                MostPlayedGame = null,
                GenreDistribution = new List<GenreDistributionDto>(),
                PlatformDistribution = new List<PlatformDistributionDto>(),
                RecentActivity = new List<RecentActivityDto>()
            };

            return Task.FromResult<ActionResult<ApiResponse<GameStatsDto>>>(
                Ok(ApiResponse<GameStatsDto>.SuccessResponse(result)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏统计数据时发生错误");
            return Task.FromResult<ActionResult<ApiResponse<GameStatsDto>>>(
                StatusCode(500, ApiResponse<GameStatsDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误")));
        }
    }
}

