using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.Text.Json;

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

    public AchievementsController(PlayLinkerDbContext context, ILogger<AchievementsController> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _httpClientFactory = httpClientFactory;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
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

            // 获取Steam全局成就解锁率
            var achievements = new List<AchievementDto>();
            var steamBaseUrl = _configuration["SteamAPI:BaseUrl"] ?? "https://api.steampowered.com";
            
            // 尝试获取游戏的Steam AppID
            int? appId = null;
            try
            {
                // 从game_platform表查询Steam平台映射
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT platform_game_id FROM game_platform WHERE game_id = @gameId AND platform_id = 1 LIMIT 1";
                var param = command.CreateParameter();
                param.ParameterName = "@gameId";
                param.Value = gameId;
                command.Parameters.Add(param);
                
                var platformGameIdResult = await command.ExecuteScalarAsync();
                if (platformGameIdResult != null && int.TryParse(platformGameIdResult.ToString(), out int parsedAppId))
                {
                    appId = parsedAppId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取Steam AppID失败");
            }

            // 获取全局成就解锁率（如果有AppID）
            Dictionary<string, double> globalRates = new();
            if (appId.HasValue)
            {
                try
                {
                    var url = $"{steamBaseUrl}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId.Value}";
                    var response = await _httpClient.GetAsync(url);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var jsonDoc = JsonDocument.Parse(content);
                        
                        if (jsonDoc.RootElement.TryGetProperty("achievementpercentages", out var percentages))
                        {
                            if (percentages.TryGetProperty("achievements", out var achievementsArray))
                            {
                                foreach (var ach in achievementsArray.EnumerateArray())
                                {
                                    if (ach.TryGetProperty("name", out var name) && 
                                        ach.TryGetProperty("percent", out var percent))
                                    {
                                        var achName = name.GetString();
                                        if (!string.IsNullOrEmpty(achName))
                                        {
                                            globalRates[achName] = percent.GetDouble() / 100.0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取Steam全局成就解锁率失败");
                }
            }

            // 构建成就DTO列表
            foreach (var achievement in achievementsList)
            {
                var globalUnlockRate = globalRates.TryGetValue(achievement.AchievementName, out var rate) ? rate : 0.0;

                achievements.Add(new AchievementDto
                {
                    AchievementId = achievement.AchievementId,
                    AchievementName = achievement.AchievementName,
                    DisplayName = achievement.DisplayName,
                    Description = achievement.Description,
                    Hidden = achievement.Hidden,
                    IconUnlocked = achievement.IconUnlocked,
                    IconLocked = achievement.IconLocked,
                    GlobalUnlockRate = globalUnlockRate
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
    /// <param name="userId">用户ID</param>
    [HttpGet("library/achievements")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserAchievementsOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserAchievementsOverviewDto>>> GetUserAchievementsOverview([FromQuery] int userId)
    {
        try
        {
            _logger.LogInformation("获取用户成就总览: userId={UserId}", userId);

            // 验证 userId 是否合法
            if (userId <= 0)
            {
                return BadRequest(ApiResponse<UserAchievementsOverviewDto>.ErrorResponse("BAD_REQUEST", "userId 参数无效，必须提供有效的用户ID"));
            }

            // 验证用户是否存在
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
                return BadRequest(ApiResponse<UserAchievementsOverviewDto>.ErrorResponse("BAD_REQUEST", $"用户ID {userId} 不存在，请先创建用户"));
            }

            var library = await _context.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            var result = new UserAchievementsOverviewDto
            {
                TotalAchievements = library?.TotalAchievements ?? 0,
                UnlockedAchievements = library?.UnlockedAchievements ?? 0,
                UnlockRate = library != null && library.TotalAchievements > 0
                    ? (double)(library.UnlockedAchievements ?? 0) / library.TotalAchievements.Value
                    : 0.0,
                PerfectGames = 0,
                RecentUnlocks = new List<RecentUnlockDto>(),
                RareAchievements = new List<RareAchievementDto>(),
                Statistics = new AchievementStatisticsDto
                {
                    AverageCompletionRate = 0.45,
                    TotalPlaytime = library?.TotalPlaytimeMinutes ?? 0,
                    AchievementsPerHour = 0.014
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

            var achievements = await _context.Achievements
                .Where(a => a.GameId == id)
                .Select(a => new AchievementDto
                {
                    AchievementId = a.AchievementId,
                    AchievementName = a.AchievementName,
                    DisplayName = a.DisplayName,
                    Description = a.Description,
                    Hidden = a.Hidden,
                    IconUnlocked = a.IconUnlocked,
                    IconLocked = a.IconLocked,
                    GlobalUnlockRate = 0.5,
                    Unlocked = false, // 应该从user_achievements表查询
                    UnlockTime = null
                })
                .ToListAsync();

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
            // 直接从 user_platform_library 表查找用户在该平台上的游戏
            var platformLibraryQuery = _context.UserPlatformLibraries.AsQueryable();

            if (platformId.HasValue)
            {
                platformLibraryQuery = platformLibraryQuery.Where(upl => upl.PlatformId == platformId.Value);
            }

            if (gameId.HasValue)
            {
                platformLibraryQuery = platformLibraryQuery.Where(upl => upl.GameId == gameId.Value);
            }

            // 获取用户在该平台上的游戏列表（通过 platform_user_id 关联）
            // 注意：这里需要找到该用户对应的 platform_user_id
            // 可以通过 player_platform 表查找，或者通过 user_platform_library 表查找
            var userPlatformGames = await platformLibraryQuery
                .Select(upl => new { upl.PlatformId, upl.GameId, upl.PlatformUserId })
                .Distinct()
                .ToListAsync();

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
                    var steamResult = await SyncSteamAchievementsAsync(userId, platformUserId, gameIds, gameId);
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

            var apiKey = _configuration["SteamAPI:ApiKey"] ?? "";
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Steam API Key 未配置");
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

                // 获取用户成就数据
                var achievementsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={apiKey}&steamid={steamId}&appid={appId}&l=schinese";
                _logger.LogInformation("调用 Steam API: {Url}", achievementsUrl);
                var achievementsResponse = await httpClient.GetAsync(achievementsUrl);
                
                _logger.LogInformation("Steam API 响应状态: {StatusCode}", achievementsResponse.StatusCode);

                if (achievementsResponse.IsSuccessStatusCode)
                {
                    var achievementsContent = await achievementsResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug("Steam API 返回内容: {Content}", achievementsContent);
                    var achievementsDoc = JsonDocument.Parse(achievementsContent);

                    if (achievementsDoc.RootElement.TryGetProperty("playerstats", out var playerStats))
                    {
                        if (playerStats.TryGetProperty("achievements", out var achievementsArray))
                        {
                            var achievementsCount = achievementsArray.GetArrayLength();
                            _logger.LogInformation("找到 {Count} 个成就", achievementsCount);
                            
                            // 批量加载该游戏的所有成就，避免N+1查询问题
                            var gameAchievements = await _context.Achievements
                                .Where(a => a.GameId == gameId)
                                .ToDictionaryAsync(a => a.AchievementName, a => a);

                            // 第一遍：收集需要创建的新成就
                            var newAchievementsToCreate = new List<Achievement>();
                            foreach (var achElement in achievementsArray.EnumerateArray())
                            {
                                if (!achElement.TryGetProperty("apiname", out var apiName)) continue;
                                var achievementName = apiName.GetString();
                                if (string.IsNullOrEmpty(achievementName)) continue;

                                // 如果数据库中不存在该成就，准备创建
                                if (!gameAchievements.ContainsKey(achievementName))
                                {
                                    var displayName = achElement.TryGetProperty("name", out var nameProp) 
                                        ? nameProp.GetString() ?? achievementName 
                                        : achievementName;
                                    var description = achElement.TryGetProperty("description", out var descProp) 
                                        ? descProp.GetString() 
                                        : null;

                                    var newAchievement = new Achievement
                                    {
                                        GameId = gameId,
                                        AchievementName = achievementName,
                                        DisplayName = displayName ?? achievementName,
                                        Description = description,
                                        Hidden = false, // 默认不隐藏
                                        IconUnlocked = "", // 默认空，后续可以通过 GetSchemaForGame API 获取
                                        IconLocked = "" // 默认空，后续可以通过 GetSchemaForGame API 获取
                                    };
                                    newAchievementsToCreate.Add(newAchievement);
                                    gameAchievements[achievementName] = newAchievement; // 预先添加到字典
                                }
                            }

                            // 批量创建新成就
                            if (newAchievementsToCreate.Count > 0)
                            {
                                _context.Achievements.AddRange(newAchievementsToCreate);
                                await _context.SaveChangesAsync(); // 保存以获取 AchievementId
                                _logger.LogInformation("批量创建 {Count} 个新成就: gameId={GameId}", 
                                    newAchievementsToCreate.Count, gameId);
                            }

                            // 批量加载该用户已有的成就记录（包括新创建的成就）
                            var allAchievementIds = gameAchievements.Values.Select(a => a.AchievementId).ToList();
                            var existingUserAchievements = await _context.UserAchievements
                                .Where(ua => ua.UserId == userId 
                                    && ua.PlatformId == STEAM_PLATFORM_ID
                                    && allAchievementIds.Contains(ua.AchievementId))
                                .ToDictionaryAsync(ua => ua.AchievementId, ua => ua);

                            // 第二遍：处理用户成就数据
                            foreach (var achElement in achievementsArray.EnumerateArray())
                            {
                                // 提取成就名称（apiname）
                                if (!achElement.TryGetProperty("apiname", out var apiName)) continue;
                                var achievementName = apiName.GetString();
                                if (string.IsNullOrEmpty(achievementName)) continue;

                                // 提取成就状态（achieved，注意：Steam API 返回的是 achieved 而不是 unlocked）
                                var achieved = achElement.TryGetProperty("achieved", out var achievedProp) && achievedProp.GetInt32() == 1;
                                
                                // 提取解锁时间（unlocktime）
                                var unlockTime = achElement.TryGetProperty("unlocktime", out var unlockTimeProp) && unlockTimeProp.GetInt64() > 0
                                    ? DateTimeOffset.FromUnixTimeSeconds(unlockTimeProp.GetInt64()).DateTime
                                    : (DateTime?)null;

                                // 从内存字典中查找成就（应该已经存在）
                                if (!gameAchievements.TryGetValue(achievementName, out var achievement))
                                {
                                    _logger.LogWarning("成就未找到: gameId={GameId}, achievementName={AchievementName}", 
                                        gameId, achievementName);
                                    continue;
                                }

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
                            _logger.LogWarning("Steam API 返回数据中没有 achievements 数组");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Steam API 返回数据中没有 playerstats 对象");
                    }
                }
                else
                {
                    var errorContent = await achievementsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Steam API 调用失败: StatusCode={StatusCode}, Content={Content}", 
                        achievementsResponse.StatusCode, errorContent);
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

