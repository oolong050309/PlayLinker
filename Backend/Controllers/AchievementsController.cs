using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Text.Json;
using System.Linq;

namespace PlayLinker.Controllers;

/// <summary>
/// 成就系统API控制器
/// 提供游戏成就列表、用户成就查询、成就同步等功能
/// </summary>
[ApiController]
[Route("api/v1")]
public class AchievementsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<AchievementsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenEncryptionService _encryptionService;
    private const int STEAM_PLATFORM_ID = 1; // Steam平台ID

    public AchievementsController(PlayLinkerDbContext context, ILogger<AchievementsController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ITokenEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
    }

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
    /// 从数据库获取Steam API Key
    /// </summary>
    private async Task<string?> GetSteamApiKeyAsync(int userId)
    {
        try
        {
            // 首先尝试从用户绑定中获取
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);
            
            if (binding != null && !string.IsNullOrEmpty(binding.AccessToken))
            {
                try
                {
                    var decryptedKey = _encryptionService.DecryptToken(binding.AccessToken);
                    _logger.LogInformation("从用户绑定获取Steam API Key: userId={UserId}", userId);
                    return decryptedKey;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解密用户{UserId}的Steam API Key失败，尝试从配置获取", userId);
                }
            }
            
            _logger.LogWarning("用户{UserId}未绑定Steam平台或API Key不存在", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库获取Steam API Key失败: userId={UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// 获取游戏成就列表(公开接口)
    /// </summary>
    /// <param name="gameId">游戏ID</param>
    [HttpGet("games/{gameId}/achievements")]
    [ProducesResponseType(typeof(ApiResponse<GameAchievementsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameAchievementsDto>>> GetGameAchievements(long gameId)
    {
        try
        {
            _logger.LogInformation("获取游戏成就列表: gameId={GameId}", gameId);

            var game = await _context.Games.FindAsync(gameId);
            if (game == null)
            {
                return NotFound(ApiResponse<GameAchievementsDto>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            // 获取成就列表
            var achievementsList = await _context.Achievements
                .Where(a => a.GameId == gameId)
                .ToListAsync();

            // 构建成就DTO列表（不再请求 Steam，全局解锁率统一为 0）
            var achievements = new List<AchievementDto>();
            foreach (var achievement in achievementsList)
            {
                achievements.Add(new AchievementDto
                {
                    AchievementId = achievement.AchievementId,
                    AchievementName = achievement.AchievementName,
                    DisplayName = achievement.DisplayName,
                    Description = achievement.Description,
                    Hidden = achievement.Hidden,
                    IconUnlocked = achievement.IconUnlocked,
                    IconLocked = achievement.IconLocked,
                    GlobalUnlockRate = 0.0
                });
            }

            var result = new GameAchievementsDto
            {
                GameId = gameId,
                GameName = game.Name,
                Achievements = achievements,
                TotalCount = achievements.Count
            };

            return Ok(ApiResponse<GameAchievementsDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏成就列表时发生错误: gameId={GameId}", gameId);
            return StatusCode(500, ApiResponse<GameAchievementsDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取用户成就总览(需要认证)
    /// </summary>
    [HttpGet("library/achievements")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserAchievementsOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserAchievementsOverviewDto>>> GetUserAchievementsOverview()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取用户成就总览: userId={UserId}", userId);

            if (userId <= 0)
            {
                _logger.LogWarning("无效的用户ID: {UserId}", userId);
                return BadRequest(ApiResponse<UserAchievementsOverviewDto>.ErrorResponse("ERR_INVALID_USER", "无效的用户ID"));
            }

            var library = await _context.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            // 获取最近解锁的成就（最近30天）
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            _logger.LogInformation("查询最近30天解锁的成就，userId={UserId}, thirtyDaysAgo={Date}", userId, thirtyDaysAgo);
            
            // 先统计用户总共有多少已解锁的成就
            var totalUnlockedCount = await _context.UserAchievements
                .CountAsync(ua => ua.UserId == userId && ua.Unlocked);
            _logger.LogInformation("用户 {UserId} 总共解锁了 {Count} 个成就", userId, totalUnlockedCount);
            
            // 先查询数据，然后在内存中格式化日期
            var recentUnlocksData = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked && ua.UnlockTime.HasValue && ua.UnlockTime.Value >= thirtyDaysAgo)
                .Join(_context.Achievements,
                    ua => ua.AchievementId,
                    a => a.AchievementId,
                    (ua, a) => new { UserAchievement = ua, Achievement = a })
                .Join(_context.Games,
                    x => x.Achievement.GameId,
                    g => g.GameId,
                    (x, g) => new { x.UserAchievement, x.Achievement, Game = g })
                .OrderByDescending(x => x.UserAchievement.UnlockTime)
                .Take(50) // 增加到50条，确保有足够的数据
                .ToListAsync();
            
            _logger.LogInformation("数据库查询到 {Count} 条最近30天解锁的成就记录", recentUnlocksData.Count);
            
            // 在内存中转换为 DTO 并格式化日期
            var recentUnlocks = recentUnlocksData.Select(x => new RecentUnlockDto
            {
                AchievementId = x.Achievement.AchievementId,
                GameId = x.Game.GameId,
                GameName = x.Game.Name ?? "",
                AchievementName = x.Achievement.AchievementName,
                DisplayName = x.Achievement.DisplayName ?? x.Achievement.AchievementName,
                UnlockTime = x.UserAchievement.UnlockTime.HasValue 
                    ? x.UserAchievement.UnlockTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : "",
                IconUnlocked = string.IsNullOrWhiteSpace(x.Achievement.IconUnlocked) ? "" : x.Achievement.IconUnlocked
            }).ToList();
            _logger.LogInformation("查询到 {Count} 个最近解锁的成就", recentUnlocks.Count);
            
            // 记录详细信息用于调试
            foreach (var unlock in recentUnlocks.Take(5))
            {
                _logger.LogInformation("最近解锁示例: AchievementId={Id}, GameName={Game}, DisplayName={Name}, IconUnlocked={Icon}", 
                    unlock.AchievementId, unlock.GameName, unlock.DisplayName, 
                    string.IsNullOrEmpty(unlock.IconUnlocked) ? "空" : unlock.IconUnlocked.Substring(0, Math.Min(50, unlock.IconUnlocked.Length)));
            }

            // 获取稀有成就（暂时返回所有已解锁的成就，GlobalUnlockRate 固定为 0）
            _logger.LogInformation("查询所有已解锁的成就，userId={UserId}", userId);
            
            // 先查询数据，然后在内存中格式化日期
            var rareAchievementsData = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked)
                .Join(_context.Achievements,
                    ua => ua.AchievementId,
                    a => a.AchievementId,
                    (ua, a) => new { UserAchievement = ua, Achievement = a })
                .Join(_context.Games,
                    x => x.Achievement.GameId,
                    g => g.GameId,
                    (x, g) => new { x.UserAchievement, x.Achievement, Game = g })
                .OrderByDescending(x => x.UserAchievement.UnlockTime)
                .Take(50) // 增加到50条，确保有足够的数据
                .ToListAsync();
            
            _logger.LogInformation("数据库查询到 {Count} 条已解锁的成就记录", rareAchievementsData.Count);
            
            // 在内存中转换为 DTO 并格式化日期
            var rareAchievements = rareAchievementsData.Select(x => new RareAchievementDto
            {
                AchievementId = x.Achievement.AchievementId,
                GameId = x.Game.GameId,
                GameName = x.Game.Name ?? "",
                AchievementName = x.Achievement.AchievementName,
                DisplayName = x.Achievement.DisplayName ?? x.Achievement.AchievementName,
                GlobalUnlockRate = 0.0,
                UnlockTime = x.UserAchievement.UnlockTime.HasValue
                    ? x.UserAchievement.UnlockTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : "",
                IconUnlocked = string.IsNullOrWhiteSpace(x.Achievement.IconUnlocked) ? "" : x.Achievement.IconUnlocked
            }).ToList();
            _logger.LogInformation("查询到 {Count} 个稀有成就", rareAchievements.Count);
            
            // 记录详细信息用于调试
            foreach (var rare in rareAchievements.Take(5))
            {
                _logger.LogInformation("稀有成就示例: AchievementId={Id}, GameName={Game}, DisplayName={Name}, IconUnlocked={Icon}", 
                    rare.AchievementId, rare.GameName, rare.DisplayName, 
                    string.IsNullOrEmpty(rare.IconUnlocked) ? "空" : rare.IconUnlocked.Substring(0, Math.Min(50, rare.IconUnlocked.Length)));
            }

            // 计算完美游戏数（成就完成率100%的游戏）
            // 获取用户已解锁的成就按游戏分组
            var userUnlockedByGame = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked)
                .Join(_context.Achievements,
                    ua => ua.AchievementId,
                    a => a.AchievementId,
                    (ua, a) => new { Achievement = a })
                .GroupBy(x => x.Achievement.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    UnlockedCount = g.Count()
                })
                .ToListAsync();

            // 获取这些游戏的成就总数
            var gameIds = userUnlockedByGame.Select(x => x.GameId).ToList();
            var gameAchievementTotals = await _context.Achievements
                .Where(a => gameIds.Contains(a.GameId))
                .GroupBy(a => a.GameId)
                .Select(g => new { GameId = g.Key, TotalCount = g.Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.TotalCount);

            // 计算完美游戏数
            var perfectGames = userUnlockedByGame
                .Count(x => gameAchievementTotals.ContainsKey(x.GameId) 
                    && gameAchievementTotals[x.GameId] > 0 
                    && x.UnlockedCount == gameAchievementTotals[x.GameId]);

            // 计算平均完成率（使用已获取的数据）
            var gameCompletionRates = userUnlockedByGame
                .Where(x => gameAchievementTotals.ContainsKey(x.GameId) && gameAchievementTotals[x.GameId] > 0)
                .Select(x => (double)x.UnlockedCount / gameAchievementTotals[x.GameId])
                .ToList();

            var averageCompletionRate = gameCompletionRates.Count > 0
                ? gameCompletionRates.Average()
                : 0.0;

            // 计算成就/小时
            var totalPlaytime = library?.TotalPlaytimeMinutes ?? 0;
            var unlockedCount = library?.UnlockedAchievements ?? 0;
            var achievementsPerHour = totalPlaytime > 0
                ? (double)unlockedCount / (totalPlaytime / 60.0)
                : 0.0;

            var result = new UserAchievementsOverviewDto
            {
                TotalAchievements = library?.TotalAchievements ?? 0,
                UnlockedAchievements = library?.UnlockedAchievements ?? 0,
                UnlockRate = library != null && library.TotalAchievements > 0
                    ? (double)(library.UnlockedAchievements ?? 0) / library.TotalAchievements.Value
                    : 0.0,
                PerfectGames = perfectGames,
                RecentUnlocks = recentUnlocks,
                RareAchievements = rareAchievements,
                Statistics = new AchievementStatisticsDto
                {
                    AverageCompletionRate = averageCompletionRate,
                    TotalPlaytime = totalPlaytime,
                    AchievementsPerHour = achievementsPerHour
                }
            };

            return Ok(ApiResponse<UserAchievementsOverviewDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户成就总览时发生错误");
            return StatusCode(500, ApiResponse<UserAchievementsOverviewDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取用户游戏成就(需要认证)
    /// 返回指定游戏的成就列表，包含当前用户是否已解锁、解锁时间等
    /// </summary>
    /// <param name="id">游戏ID</param>
    [HttpGet("library/games/{id}/achievements")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<GameAchievementsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GameAchievementsDto>>> GetUserGameAchievements(long id)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取用户游戏成就: userId={UserId}, gameId={GameId}", userId, id);

            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(ApiResponse<GameAchievementsDto>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            // 先获取该游戏的所有成就
            var achievementsList = await _context.Achievements
                .Where(a => a.GameId == id)
                .ToListAsync();

            // 获取当前用户在该游戏下已解锁的成就
            var achievementIds = achievementsList.Select(a => a.AchievementId).ToList();
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId && achievementIds.Contains(ua.AchievementId))
                .ToListAsync();

            var userAchievementDict = userAchievements
                .ToDictionary(ua => ua.AchievementId, ua => ua);

            // 组装带有个人解锁状态的成就列表（不再请求 Steam，全局解锁率统一为 0）
            var achievements = new List<AchievementDto>();

            foreach (var achievement in achievementsList)
            {
                userAchievementDict.TryGetValue(achievement.AchievementId, out var ua);
                var unlocked = ua?.Unlocked ?? false;
                string? unlockTimeStr = null;
                if (ua?.UnlockTime != null)
                {
                    unlockTimeStr = ua.UnlockTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                }

                achievements.Add(new AchievementDto
                {
                    AchievementId = achievement.AchievementId,
                    AchievementName = achievement.AchievementName,
                    DisplayName = achievement.DisplayName,
                    Description = achievement.Description,
                    Hidden = achievement.Hidden,
                    IconUnlocked = achievement.IconUnlocked,
                    IconLocked = achievement.IconLocked,
                    GlobalUnlockRate = 0.0,
                    Unlocked = unlocked,
                    UnlockTime = unlockTimeStr
                });
            }

            var result = new GameAchievementsDto
            {
                GameId = id,
                GameName = game.Name,
                Achievements = achievements,
                TotalCount = achievements.Count
            };

            return Ok(ApiResponse<GameAchievementsDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户游戏成就时发生错误: gameId={GameId}", id);
            return StatusCode(500, ApiResponse<GameAchievementsDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 同步成就数据(需要认证)
    /// </summary>
    /// <param name="request">同步请求</param>
    [HttpPost("library/achievements/sync")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SyncAchievementsResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SyncAchievementsResponseDto>>> SyncAchievements(
        [FromBody] SyncAchievementsRequestDto request)
    {
        try
        {
            var userId = request.UserId;
            _logger.LogInformation("同步成就数据: userId={UserId}, platformId={PlatformId}, gameId={GameId}",
                userId, request.PlatformId, request.GameId);

            // 1. 验证 userId 是否合法
            if (userId <= 0)
            {
                return BadRequest(ApiResponse<SyncAchievementsResponseDto>.ErrorResponse("BAD_REQUEST", "userId 参数无效，必须提供有效的用户ID"));
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var checkUserCommand = connection.CreateCommand();
            checkUserCommand.CommandText = "SELECT COUNT(*) FROM `user` WHERE `user_id` = @userId";
            var userIdParam = checkUserCommand.CreateParameter();
            userIdParam.ParameterName = "@userId";
            userIdParam.Value = userId;
            checkUserCommand.Parameters.Add(userIdParam);

            var userExists = Convert.ToInt32(await checkUserCommand.ExecuteScalarAsync()) > 0;

            if (!userExists)
            {
                _logger.LogWarning("用户不存在: userId={UserId}", userId);
                return BadRequest(ApiResponse<SyncAchievementsResponseDto>.ErrorResponse("BAD_REQUEST", $"用户ID {userId} 不存在，请先创建用户"));
            }

            // 2. 处理 platformId: 0 或 null 表示同步所有平台
            var platformId = request.PlatformId.HasValue && request.PlatformId.Value != 0 
                ? request.PlatformId.Value 
                : (int?)null;

            // 3. 处理 gameId: 0 或 null 表示同步所有游戏
            var gameId = request.GameId.HasValue && request.GameId.Value != 0 
                ? request.GameId.Value 
                : (long?)null;

            // 4. 获取同步前的解锁成就数
            var beforeUnlockedQuery = _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked);
            
            if (platformId.HasValue)
            {
                beforeUnlockedQuery = beforeUnlockedQuery.Where(ua => ua.PlatformId == platformId.Value);
            }
            
            if (gameId.HasValue)
            {
                // 通过成就表关联游戏
                beforeUnlockedQuery = beforeUnlockedQuery
                    .Where(ua => _context.Achievements.Any(a => a.AchievementId == ua.AchievementId && a.GameId == gameId.Value));
            }

            var beforeUnlocked = await beforeUnlockedQuery.CountAsync();

            // 5. 根据 userId 和 platformId 查找用户绑定的平台账号
            // 先通过 user_platform_binding 表获取该用户绑定的所有 platform_user_id 和 platform_id 组合
            var userPlatformBindings = await _context.UserPlatformBindings
                .Where(upb => upb.UserId == userId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync();

            if (userPlatformBindings.Count == 0)
            {
                _logger.LogWarning("用户 {UserId} 没有绑定的平台账号", userId);
                var emptyResult = new SyncAchievementsResponseDto
                {
                    SyncedGames = 0,
                    NewUnlocks = 0,
                    TotalUnlocked = beforeUnlocked,
                    SyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                return Ok(ApiResponse<SyncAchievementsResponseDto>.SuccessResponse(emptyResult, "未找到用户绑定的平台账号"));
            }

            // 根据 platformId 过滤绑定（如果指定了平台）
            if (platformId.HasValue)
            {
                userPlatformBindings = userPlatformBindings
                    .Where(b => b.PlatformId == platformId.Value)
                    .ToList();
            }

            if (userPlatformBindings.Count == 0)
            {
                _logger.LogWarning("用户 {UserId} 在平台 {PlatformId} 上没有绑定的平台账号", userId, platformId);
                var emptyResult = new SyncAchievementsResponseDto
                {
                    SyncedGames = 0,
                    NewUnlocks = 0,
                    TotalUnlocked = beforeUnlocked,
                    SyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                return Ok(ApiResponse<SyncAchievementsResponseDto>.SuccessResponse(emptyResult, $"用户在平台 {platformId} 上没有绑定的平台账号"));
            }

            // 只查询该用户绑定的平台账号对应的游戏记录
            // 为每个绑定组合单独查询，确保精确匹配，避免查询到其他用户的游戏
            var userPlatformGamesList = new List<(int PlatformId, long GameId, string PlatformUserId)>();
            
            foreach (var binding in userPlatformBindings)
            {
                var gamesQuery = _context.UserPlatformLibraries
                    .Where(upl => upl.PlatformUserId == binding.PlatformUserId && 
                                 upl.PlatformId == binding.PlatformId);

                if (gameId.HasValue)
                {
                    gamesQuery = gamesQuery.Where(upl => upl.GameId == gameId.Value);
                }

                var gamesForBinding = await gamesQuery
                    .Select(upl => new { upl.PlatformId, upl.GameId, upl.PlatformUserId })
                    .Distinct()
                    .ToListAsync();

                foreach (var game in gamesForBinding)
                {
                    userPlatformGamesList.Add((game.PlatformId, game.GameId, game.PlatformUserId));
                }
            }

            // 转换为匿名类型列表以保持与后续代码的兼容性
            var userPlatformGames = userPlatformGamesList
                .Select(x => new { PlatformId = x.PlatformId, GameId = x.GameId, PlatformUserId = x.PlatformUserId })
                .ToList();

            if (userPlatformGames.Count == 0)
            {
                _logger.LogWarning("用户 {UserId} 在平台 {PlatformId} 上没有游戏记录", userId, platformId);
                // 尝试从 player_platform 表查找用户绑定的平台账号
                var playerPlatforms = await _context.PlayerPlatforms
                    .Where(pp => platformId.HasValue ? pp.PlatformId == platformId.Value : true)
                    .Select(pp => new { pp.PlatformId, pp.PlatformUserId })
                    .Distinct()
                    .ToListAsync();

                if (playerPlatforms.Count == 0)
                {
                    _logger.LogWarning("用户 {UserId} 在平台 {PlatformId} 上没有绑定的平台账号", userId, platformId);
                    // 返回空结果
                    var emptyResult = new SyncAchievementsResponseDto
                    {
                        SyncedGames = 0,
                        NewUnlocks = 0,
                        TotalUnlocked = beforeUnlocked,
                        SyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };
                    return Ok(ApiResponse<SyncAchievementsResponseDto>.SuccessResponse(emptyResult, "未找到用户绑定的平台账号或游戏记录"));
                }

                // 如果找到了平台账号但没有游戏记录，尝试同步所有该平台账号的游戏
                // 这里需要根据实际情况处理，暂时返回提示
                var noGamesResult = new SyncAchievementsResponseDto
                {
                    SyncedGames = 0,
                    NewUnlocks = 0,
                    TotalUnlocked = beforeUnlocked,
                    SyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                return Ok(ApiResponse<SyncAchievementsResponseDto>.SuccessResponse(noGamesResult, "找到平台账号但未找到游戏记录，请先导入游戏库"));
            }

            // 6. 按平台分组，获取每个平台的 platform_user_id
            var platformGroups = userPlatformGames.GroupBy(x => new { x.PlatformId, x.PlatformUserId });
            int syncedGames = 0;
            int newUnlocks = 0;

            foreach (var platformGroup in platformGroups)
            {
                var currentPlatformId = platformGroup.Key.PlatformId;
                var platformUserId = platformGroup.Key.PlatformUserId;
                var gameIds = platformGroup.Select(x => x.GameId).Distinct().ToList();

                if (string.IsNullOrEmpty(platformUserId))
                {
                    _logger.LogWarning("平台账号ID为空: platformId={PlatformId}", currentPlatformId);
                    continue;
                }

                // 根据平台类型调用相应的 API
                if (currentPlatformId == 1) // Steam
                {
                    _logger.LogInformation("开始同步 Steam 成就: userId={UserId}, steamId={SteamId}, gameIds={GameIds}", 
                        userId, platformUserId, string.Join(",", gameIds));
                    var steamResult = await SyncSteamAchievementsAsync((int)userId, platformUserId, gameIds, gameId);
                    syncedGames += steamResult.SyncedGames;
                    newUnlocks += steamResult.NewUnlocks;
                    _logger.LogInformation("Steam 成就同步完成: syncedGames={SyncedGames}, newUnlocks={NewUnlocks}", 
                        steamResult.SyncedGames, steamResult.NewUnlocks);
                }
                // 其他平台（Epic, GOG, Xbox, PSN, Nintendo）的同步逻辑可以在这里添加
                // else if (currentPlatformId == 2) // Epic Games
                // else if (currentPlatformId == 5) // GOG
                // ...
            }

            // 7. 获取同步后的总解锁成就数
            var afterUnlockedQuery = _context.UserAchievements
                .Where(ua => ua.UserId == userId && ua.Unlocked);
            
            if (platformId.HasValue)
            {
                afterUnlockedQuery = afterUnlockedQuery.Where(ua => ua.PlatformId == platformId.Value);
            }
            
            if (gameId.HasValue)
            {
                afterUnlockedQuery = afterUnlockedQuery
                    .Where(ua => _context.Achievements.Any(a => a.AchievementId == ua.AchievementId && a.GameId == gameId.Value));
            }

            var totalUnlocked = await afterUnlockedQuery.CountAsync();

            // 如果 newUnlocks 为 0，使用差值计算
            if (newUnlocks == 0 && totalUnlocked > beforeUnlocked)
            {
                newUnlocks = totalUnlocked - beforeUnlocked;
            }

            var result = new SyncAchievementsResponseDto
            {
                SyncedGames = syncedGames,
                NewUnlocks = newUnlocks,
                TotalUnlocked = totalUnlocked,
                SyncTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return Ok(ApiResponse<SyncAchievementsResponseDto>.SuccessResponse(result, "成就同步成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步成就数据时发生错误");
            return StatusCode(500, ApiResponse<SyncAchievementsResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 同步 Steam 平台成就数据
    /// </summary>
    private async Task<(int SyncedGames, int NewUnlocks)> SyncSteamAchievementsAsync(
        int userId, string steamId, List<long> gameIds, long? specificGameId)
    {
        const int STEAM_PLATFORM_ID = 1;
        int syncedGames = 0;
        int newUnlocks = 0;

        try
        {
            // 过滤需要同步的游戏
            var gamesToSync = gameIds;
            if (specificGameId.HasValue)
            {
                gamesToSync = gamesToSync.Where(g => g == specificGameId.Value).ToList();
            }

            // 从数据库获取Steam API Key
            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Steam API Key 未配置: userId={UserId}, steamId={SteamId}", userId, steamId);
                return (0, 0);
            }

            var httpClient = _httpClientFactory.CreateClient();

            foreach (var gameId in gamesToSync)
            {
                _logger.LogInformation("处理游戏: gameId={GameId}", gameId);
                
                // 获取游戏的 Steam AppID
                var gamePlatform = await _context.GamePlatforms
                    .FirstOrDefaultAsync(gp => gp.GameId == gameId && gp.PlatformId == STEAM_PLATFORM_ID);

                int appId;
                if (gamePlatform == null)
                {
                    _logger.LogWarning("未找到游戏平台映射: gameId={GameId}, platformId={PlatformId}", gameId, STEAM_PLATFORM_ID);
                    // 尝试直接使用 gameId 作为 AppID（如果 gameId 本身就是 Steam AppID）
                    if (!int.TryParse(gameId.ToString(), out appId))
                    {
                        _logger.LogWarning("无法将 gameId 解析为 AppID: gameId={GameId}", gameId);
                        continue;
                    }
                    _logger.LogInformation("使用 gameId 作为 AppID: appId={AppId}", appId);
                }
                else
                {
                    if (!int.TryParse(gamePlatform.PlatformGameId, out appId))
                    {
                        _logger.LogWarning("无法解析 PlatformGameId: PlatformGameId={PlatformGameId}", gamePlatform.PlatformGameId);
                        continue;
                    }
                    _logger.LogInformation("从 game_platform 表获取 AppID: appId={AppId}", appId);
                }

                // 第一步：获取游戏成就架构信息（完整成就信息）
                var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={apiKey}&appid={appId}&l=schinese";
                _logger.LogInformation("调用 Steam API 获取成就架构: {Url}", schemaUrl);
                var schemaResponse = await httpClient.GetAsync(schemaUrl);

                Dictionary<string, Achievement> schemaAchievements = new();
                
                if (schemaResponse.IsSuccessStatusCode)
                {
                    var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug("GetSchemaForGame API 返回内容: {Content}", schemaContent);
                    var schemaDoc = JsonDocument.Parse(schemaContent);

                    if (schemaDoc.RootElement.TryGetProperty("game", out var gameData))
                    {
                        if (gameData.TryGetProperty("availableGameStats", out var stats))
                        {
                            if (stats.TryGetProperty("achievements", out var achievementsObj))
                            {
                                // 检查 achievements 是对象还是数组
                                if (achievementsObj.ValueKind == JsonValueKind.Array)
                                {
                                    // 处理数组格式的成就数据
                                    _logger.LogInformation("GetSchemaForGame 返回的 achievements 是数组格式，按数组格式处理: gameId={GameId}, appId={AppId}", 
                                        gameId, appId);
                                    
                                    foreach (var achElement in achievementsObj.EnumerateArray())
                                    {
                                        // 数组格式中，成就名称可能在 "name" 或 "apiname" 字段中
                                        string? achievementName = null;
                                        if (achElement.TryGetProperty("name", out var nameProp))
                                        {
                                            achievementName = nameProp.GetString();
                                        }
                                        else if (achElement.TryGetProperty("apiname", out var apiNameProp))
                                        {
                                            achievementName = apiNameProp.GetString();
                                        }
                                        
                                        if (string.IsNullOrEmpty(achievementName)) continue;

                                        if (!achElement.TryGetProperty("displayName", out var displayNameProp)) continue;
                                        var displayName = displayNameProp.GetString();
                                        if (string.IsNullOrEmpty(displayName)) continue;

                                        var description = achElement.TryGetProperty("description", out var descProp) 
                                            ? descProp.GetString() 
                                            : null;
                                        var hidden = achElement.TryGetProperty("hidden", out var hiddenProp) 
                                            ? hiddenProp.GetInt32() == 1 
                                            : false;
                                        var icon = achElement.TryGetProperty("icon", out var iconProp) 
                                            ? iconProp.GetString() ?? "" 
                                            : "";
                                        var iconGray = achElement.TryGetProperty("icongray", out var iconGrayProp) 
                                            ? iconGrayProp.GetString() ?? "" 
                                            : "";
                                        var defaultValue = achElement.TryGetProperty("defaultvalue", out var defaultValueProp) 
                                            ? defaultValueProp.GetInt32() 
                                            : 0;

                                        // 创建或更新成就记录
                                        var existingAchievement = await _context.Achievements
                                            .FirstOrDefaultAsync(a => a.GameId == gameId && a.AchievementName == achievementName);

                                        if (existingAchievement == null)
                                        {
                                            existingAchievement = new Achievement
                                            {
                                                GameId = gameId,
                                                AchievementName = achievementName,
                                                DisplayName = displayName,
                                                Description = description,
                                                Hidden = hidden,
                                                IconUnlocked = icon,
                                                IconLocked = iconGray
                                            };
                                            _context.Achievements.Add(existingAchievement);
                                            await _context.SaveChangesAsync(); // 保存以获取 AchievementId
                                            _logger.LogInformation("创建新成就（数组格式）: gameId={GameId}, achievementName={AchievementName}", 
                                                gameId, achievementName);
                                        }
                                        else
                                        {
                                            // 检查并更新缺失的字段（如果字段为空则更新）
                                            bool needsUpdate = false;
                                            
                                            // 更新 DisplayName（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName))
                                            {
                                                existingAchievement.DisplayName = displayName;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 Description（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description))
                                            {
                                                existingAchievement.Description = description;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 Hidden（如果当前值与API返回的不同）
                                            if (existingAchievement.Hidden != hidden)
                                            {
                                                existingAchievement.Hidden = hidden;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 IconUnlocked（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon))
                                            {
                                                existingAchievement.IconUnlocked = icon;
                                                needsUpdate = true;
                                                _logger.LogInformation("更新成就图标（解锁）: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                            
                                            // 更新 IconLocked（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray))
                                            {
                                                existingAchievement.IconLocked = iconGray;
                                                needsUpdate = true;
                                                _logger.LogInformation("更新成就图标（锁定）: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                            
                                            if (needsUpdate)
                                            {
                                                _logger.LogInformation("更新已有成就的缺失字段: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                        }

                                        schemaAchievements[achievementName] = existingAchievement;
                                    }

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("从 GetSchemaForGame（数组格式）获取到 {Count} 个成就: gameId={GameId}, appId={AppId}", 
                                        schemaAchievements.Count, gameId, appId);
                                }
                                else if (achievementsObj.ValueKind == JsonValueKind.Object)
                                {
                                    // 解析成就架构信息（对象格式）
                                    foreach (var achProp in achievementsObj.EnumerateObject())
                                    {
                                        var achKey = achProp.Name;
                                        var achValue = achProp.Value;

                                        if (!achValue.TryGetProperty("displayName", out var displayNameProp)) continue;
                                        var displayName = displayNameProp.GetString();
                                        if (string.IsNullOrEmpty(displayName)) continue;

                                        var achievementName = achKey;
                                        var description = achValue.TryGetProperty("description", out var descProp) 
                                            ? descProp.GetString() 
                                            : null;
                                        var hidden = achValue.TryGetProperty("hidden", out var hiddenProp) 
                                            ? hiddenProp.GetInt32() == 1 
                                            : false;
                                        var icon = achValue.TryGetProperty("icon", out var iconProp) 
                                            ? iconProp.GetString() ?? "" 
                                            : "";
                                        var iconGray = achValue.TryGetProperty("icongray", out var iconGrayProp) 
                                            ? iconGrayProp.GetString() ?? "" 
                                            : "";
                                        var defaultValue = achValue.TryGetProperty("defaultvalue", out var defaultValueProp) 
                                            ? defaultValueProp.GetInt32() 
                                            : 0;

                                        // 创建或更新成就记录
                                        var existingAchievement = await _context.Achievements
                                            .FirstOrDefaultAsync(a => a.GameId == gameId && a.AchievementName == achievementName);

                                        if (existingAchievement == null)
                                        {
                                            existingAchievement = new Achievement
                                            {
                                                GameId = gameId,
                                                AchievementName = achievementName,
                                                DisplayName = displayName,
                                                Description = description,
                                                Hidden = hidden,
                                                IconUnlocked = icon,
                                                IconLocked = iconGray
                                            };
                                            _context.Achievements.Add(existingAchievement);
                                            await _context.SaveChangesAsync(); // 保存以获取 AchievementId
                                            _logger.LogInformation("创建新成就: gameId={GameId}, achievementName={AchievementName}", 
                                                gameId, achievementName);
                                        }
                                        else
                                        {
                                            // 检查并更新缺失的字段（如果字段为空则更新）
                                            bool needsUpdate = false;
                                            
                                            // 更新 DisplayName（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName))
                                            {
                                                existingAchievement.DisplayName = displayName;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 Description（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description))
                                            {
                                                existingAchievement.Description = description;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 Hidden（如果当前值与API返回的不同，但优先保持已有值）
                                            // 注意：Hidden字段通常不会为空，所以这里直接更新以保持数据一致性
                                            if (existingAchievement.Hidden != hidden)
                                            {
                                                existingAchievement.Hidden = hidden;
                                                needsUpdate = true;
                                            }
                                            
                                            // 更新 IconUnlocked（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon))
                                            {
                                                existingAchievement.IconUnlocked = icon;
                                                needsUpdate = true;
                                                _logger.LogInformation("更新成就图标（解锁）: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                            
                                            // 更新 IconLocked（如果为空）
                                            if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray))
                                            {
                                                existingAchievement.IconLocked = iconGray;
                                                needsUpdate = true;
                                                _logger.LogInformation("更新成就图标（锁定）: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                            
                                            if (needsUpdate)
                                            {
                                                _logger.LogInformation("更新已有成就的缺失字段: gameId={GameId}, achievementName={AchievementName}", 
                                                    gameId, achievementName);
                                            }
                                        }

                                        schemaAchievements[achievementName] = existingAchievement;
                                    }

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("从 GetSchemaForGame 获取到 {Count} 个成就: gameId={GameId}, appId={AppId}", 
                                        schemaAchievements.Count, gameId, appId);
                                }
                                else
                                {
                                    _logger.LogWarning("GetSchemaForGame 返回的 achievements 格式未知: gameId={GameId}, appId={AppId}, ValueKind={ValueKind}", 
                                        gameId, appId, achievementsObj.ValueKind);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var errorContent = await schemaResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("GetSchemaForGame API 调用失败: gameId={GameId}, appId={AppId}, StatusCode={StatusCode}, Content={Content}", 
                        gameId, appId, schemaResponse.StatusCode, errorContent);
                }

                // 第二步：获取用户成就解锁情况
                var achievementsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={apiKey}&steamid={steamId}&appid={appId}&l=schinese";
                _logger.LogInformation("调用 Steam API 获取用户成就解锁情况: {Url}", achievementsUrl);
                var achievementsResponse = await httpClient.GetAsync(achievementsUrl);
                
                _logger.LogInformation("GetPlayerAchievements API 响应状态: {StatusCode}", achievementsResponse.StatusCode);

                if (achievementsResponse.IsSuccessStatusCode)
                {
                    var achievementsContent = await achievementsResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug("GetPlayerAchievements API 返回内容: {Content}", achievementsContent);
                    var achievementsDoc = JsonDocument.Parse(achievementsContent);

                    if (achievementsDoc.RootElement.TryGetProperty("playerstats", out var playerStats))
                    {
                        if (playerStats.TryGetProperty("achievements", out var achievementsArray))
                        {
                            var achievementsCount = achievementsArray.GetArrayLength();
                            _logger.LogInformation("找到 {Count} 个用户成就记录: gameId={GameId}, appId={AppId}", 
                                achievementsCount, gameId, appId);

                            // 批量加载该用户已有的成就记录
                            var allAchievementIds = schemaAchievements.Values.Select(a => a.AchievementId).ToList();
                            var existingUserAchievements = await _context.UserAchievements
                                .Where(ua => ua.UserId == userId 
                                    && ua.PlatformId == STEAM_PLATFORM_ID
                                    && allAchievementIds.Contains(ua.AchievementId))
                                .ToDictionaryAsync(ua => ua.AchievementId, ua => ua);

                            // 处理用户成就解锁数据
                            foreach (var achElement in achievementsArray.EnumerateArray())
                            {
                                // 提取成就名称（apiname）
                                if (!achElement.TryGetProperty("apiname", out var apiName)) continue;
                                var achievementName = apiName.GetString();
                                if (string.IsNullOrEmpty(achievementName)) continue;

                                // 如果架构中没有这个成就，跳过（可能游戏更新了但架构还没同步）
                                if (!schemaAchievements.TryGetValue(achievementName, out var achievement))
                                {
                                    _logger.LogWarning("成就未在架构中找到: gameId={GameId}, achievementName={AchievementName}", 
                                        gameId, achievementName);
                                    continue;
                                }

                                // 提取成就状态（achieved，注意：Steam API 返回的是 achieved 而不是 unlocked）
                                var achieved = achElement.TryGetProperty("achieved", out var achievedProp) && achievedProp.GetInt32() == 1;
                                
                                // 提取解锁时间（unlocktime）
                                var unlockTime = achElement.TryGetProperty("unlocktime", out var unlockTimeProp) && unlockTimeProp.GetInt64() > 0
                                    ? DateTimeOffset.FromUnixTimeSeconds(unlockTimeProp.GetInt64()).DateTime
                                    : (DateTime?)null;

                                // 从内存字典中查找用户成就记录
                                if (existingUserAchievements.TryGetValue(achievement.AchievementId, out var userAchievement))
                                {
                                    // 检查是否有新的解锁
                                    if (!userAchievement.Unlocked && achieved)
                                    {
                                        newUnlocks++;
                                    }
                                    // 更新现有记录
                                    userAchievement.Unlocked = achieved;
                                    userAchievement.UnlockTime = unlockTime;
                                }
                                else
                                {
                                    // 创建新用户成就记录
                                    if (achieved)
                                    {
                                        newUnlocks++;
                                    }
                                    _context.UserAchievements.Add(new UserAchievement
                                    {
                                        UserId = userId,
                                        AchievementId = achievement.AchievementId,
                                        PlatformId = STEAM_PLATFORM_ID,
                                        Unlocked = achieved,
                                        UnlockTime = unlockTime
                                    });
                                }
                            }

                            syncedGames++;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("游戏成就同步完成: gameId={GameId}, syncedGames={SyncedGames}, newUnlocks={NewUnlocks}", 
                                gameId, syncedGames, newUnlocks);
                        }
                        else
                        {
                            _logger.LogWarning("Steam API 返回数据中没有 achievements 数组: gameId={GameId}, appId={AppId}", 
                                gameId, appId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Steam API 返回数据中没有 playerstats 对象: gameId={GameId}, appId={AppId}", 
                            gameId, appId);
                    }
                }
                else
                {
                    var errorContent = await achievementsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("GetPlayerAchievements API 调用失败: gameId={GameId}, appId={AppId}, StatusCode={StatusCode}, Content={Content}", 
                        gameId, appId, achievementsResponse.StatusCode, errorContent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 Steam 成就数据失败: userId={UserId}, steamId={SteamId}", userId, steamId);
        }

        return (syncedGames, newUnlocks);
    }
}

