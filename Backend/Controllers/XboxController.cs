using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Text.Json;

namespace PlayLinker.Controllers;

/// <summary>
/// Xbox API集成控制器
/// 提供Xbox数据导入、用户信息查询、游戏信息查询等功能
/// </summary>
[ApiController]
[Route("api/v1/xbox")]
[Authorize]
public class XboxController : ControllerBase
{
    private readonly IXboxService _xboxService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<XboxController> _logger;
    private const int XBOX_PLATFORM_ID = 7; // Xbox平台ID

    public XboxController(IXboxService xboxService, PlayLinkerDbContext context, ILogger<XboxController> logger)
    {
        _xboxService = xboxService;
        _context = context;
        _logger = logger;
    }

    // 获取当前用户ID
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    /// <summary>
    /// 初始化平台数据（优化版本：批量检查，减少数据库查询）
    /// </summary>
    private async Task InitializePlatformsAsync()
    {
        var platforms = new[]
        {
            new { Id = 1, Name = "Steam", Description = "Valve旗下游戏平台" },
            new { Id = 2, Name = "Epic Games", Description = "Epic Games商店" },
            new { Id = 3, Name = "Origin", Description = "EA游戏平台" },
            new { Id = 4, Name = "Uplay", Description = "Ubisoft游戏平台" },
            new { Id = 5, Name = "GOG", Description = "GOG游戏平台" },
            new { Id = 6, Name = "PSN", Description = "PlayStation Network" },
            new { Id = 7, Name = "Xbox", Description = "Xbox游戏平台" },
            new { Id = 8, Name = "Nintendo Switch", Description = "任天堂Switch平台" }
        };

        // 批量检查所有平台ID和名称，只查询一次
        var platformIds = platforms.Select(p => p.Id).ToList();
        var platformNames = platforms.Select(p => p.Name).ToList();
        
        var existingPlatforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId) || platformNames.Contains(p.PlatformName))
            .Select(p => new { p.PlatformId, p.PlatformName })
            .ToListAsync();

        var existingIds = existingPlatforms.Select(p => p.PlatformId).ToHashSet();
        var existingNames = existingPlatforms.Select(p => p.PlatformName).ToHashSet();

        // 只插入不存在的平台
        var platformsToInsert = platforms
            .Where(p => !existingIds.Contains(p.Id) && !existingNames.Contains(p.Name))
            .ToList();

        if (platformsToInsert.Count == 0)
        {
            return; // 所有平台都已存在，无需操作
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // 批量插入（使用ON DUPLICATE KEY UPDATE避免重复）
        foreach (var platformInfo in platformsToInsert)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO platforms (platform_id, platform_name, description, status) 
                VALUES (@id, @name, @desc, 1)
                ON DUPLICATE KEY UPDATE platform_name = VALUES(platform_name), description = VALUES(description)";
            
            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = platformInfo.Id;
            command.Parameters.Add(idParam);

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = platformInfo.Name;
            command.Parameters.Add(nameParam);

            var descParam = command.CreateParameter();
            descParam.ParameterName = "@desc";
            descParam.Value = platformInfo.Description ?? "";
            command.Parameters.Add(descParam);

            try
            {
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("创建平台: {PlatformName} (ID: {PlatformId})", platformInfo.Name, platformInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "创建平台失败: {PlatformName} (ID: {PlatformId})", platformInfo.Name, platformInfo.Id);
            }
        }
    }

    /// <summary>
    /// 检查Xbox令牌状态
    /// </summary>
    /// <remarks>
    /// 检查是否已有有效的Xbox认证令牌。
    /// 建议在调用认证接口前先调用此接口，了解当前状态。
    /// </remarks>
    [HttpGet("token-status")]
    [ProducesResponseType(typeof(ApiResponse<XboxAuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<XboxAuthResponseDto>>> CheckTokenStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("检查Xbox令牌状态: userId={UserId}", userId);

            var result = await _xboxService.CheckTokenStatus(userId, 7);

            return Ok(ApiResponse<XboxAuthResponseDto>.SuccessResponse(result, 
                result.Success ? "令牌有效" : "令牌无效或不存在"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return StatusCode(500, ApiResponse<XboxAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// Xbox认证
    /// </summary>
    /// <param name="request">认证请求</param>
    /// <remarks>
    /// <para><b>使用场景：</b></para>
    /// <para>1. <b>本地开发环境首次认证</b>（推荐）：</para>
    /// <code>
    /// {
    ///   "openBrowser": true,
    ///   "forceReauth": false
    /// }
    /// </code>
    /// <para>会打开浏览器进行OAuth2登录，完成后令牌保存到 Backend/Tokens/xbox_tokens.json</para>
    /// 
    /// <para>2. <b>服务器环境刷新令牌</b>：</para>
    /// <code>
    /// {
    ///   "openBrowser": false,
    ///   "forceReauth": false
    /// }
    /// </code>
    /// <para>使用已有令牌进行刷新，不需要浏览器</para>
    /// 
    /// <para>3. <b>服务器部署建议</b>：</para>
    /// <para>- 在本地开发环境完成首次认证（openBrowser=true）</para>
    /// <para>- 将生成的 Backend/Tokens/xbox_tokens.json 文件上传到服务器</para>
    /// <para>- 服务器上只需要刷新令牌（openBrowser=false）</para>
    /// 
    /// <para><b>注意事项：</b></para>
    /// <para>- 首次认证必须在有图形界面的环境中进行（会打开浏览器）</para>
    /// <para>- 令牌会自动刷新，但长期不使用可能需要重新认证</para>
    /// <para>- 认证前建议先调用 GET /api/v1/xbox/token-status 检查令牌状态</para>
    /// </remarks>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(ApiResponse<XboxAuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<XboxAuthResponseDto>>> Authenticate(
        [FromBody] XboxAuthRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("开始Xbox认证: userId={UserId}, OpenBrowser={OpenBrowser}", userId, request.OpenBrowser);

            var result = await _xboxService.AuthenticateXbox(request, userId);

            if (!result.Success)
            {
                return Ok(ApiResponse<XboxAuthResponseDto>.SuccessResponse(result, result.Message));
            }

            return Ok(ApiResponse<XboxAuthResponseDto>.SuccessResponse(result, "Xbox认证成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xbox认证时发生错误");
            return StatusCode(500, ApiResponse<XboxAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 导入Xbox数据
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<XboxImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<XboxImportResponseDto>>> ImportXboxData(
        [FromBody] XboxImportRequestDto request)
    {
        try
        {
            // 验证 userId
            if (request.UserId <= 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", "userId 参数无效，必须提供有效的用户ID"));
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
            userIdParam.Value = request.UserId;
            checkUserCommand.Parameters.Add(userIdParam);

            var userExists = Convert.ToInt32(await checkUserCommand.ExecuteScalarAsync()) > 0;

            if (!userExists)
            {
                _logger.LogWarning("用户不存在: userId={UserId}", request.UserId);
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", $"用户ID {request.UserId} 不存在，请先创建用户"));
            }

            var userId = (int)request.UserId;
            _logger.LogInformation("导入Xbox数据: userId={UserId}, xboxUserId={XboxUserId}", userId, request.XboxUserId);

            // 初始化平台数据
            await InitializePlatformsAsync();

            // 获取Xbox用户信息
            var xboxUser = await _xboxService.GetXboxUser(request.XboxUserId, userId);
            if (xboxUser == null)
            {
                return BadRequest(ApiResponse<XboxImportResponseDto>.ErrorResponse("ERR_XBOX_USER_NOT_FOUND", "Xbox用户不存在或令牌无效，请先进行认证"));
            }

            // 存储或更新PlayerPlatform信息
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == xboxUser.Xuid && pp.PlatformId == XBOX_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = xboxUser.Xuid,
                    PlatformId = XBOX_PLATFORM_ID,
                    ProfileName = xboxUser.Gamertag,
                    ProfileUrl = xboxUser.ProfileUrl,
                    AccountCreated = DateTime.TryParse(xboxUser.AccountCreated, out var created) ? created : null,
                    Country = xboxUser.Country
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = xboxUser.Gamertag;
                playerPlatform.ProfileUrl = xboxUser.ProfileUrl;
                playerPlatform.Country = xboxUser.Country;
            }
            await _context.SaveChangesAsync();

            // 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == XBOX_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = (int)userId,
                    PlatformId = XBOX_PLATFORM_ID,
                    PlatformUserId = xboxUser.Xuid,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1)
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                // 更新绑定时，更新绑定时间和同步时间
                userPlatformBinding.PlatformUserId = xboxUser.Xuid;
                userPlatformBinding.BindingStatus = true;
                userPlatformBinding.BindingTime = DateTime.UtcNow; // 更新绑定时间
                userPlatformBinding.LastSyncTime = DateTime.UtcNow; // 更新同步时间
                userPlatformBinding.ExpireTime = DateTime.UtcNow.AddYears(1); // 更新过期时间
            }
            await _context.SaveChangesAsync();

            // 导入游戏库数据
            int gamesCount = 0;
            int achievementsCount = 0;
            
            if (request.ImportGames)
            {
                try
                {
                    // 获取完整的Xbox游戏数据
                    _logger.LogInformation("开始导入Xbox游戏数据...");
                    
                    var xboxGames = await _xboxService.GetXboxUserGames(request.XboxUserId, userId);
                    
                    foreach (var xboxGame in xboxGames)
                    {
                        try
                        {
                            // 查找是否已存在该Xbox平台的该游戏
                            // 只通过GamePlatform匹配，不通过名称匹配，避免不同平台的同名游戏被错误关联
                            Game? game = null;
                            var existingGamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.PlatformId == XBOX_PLATFORM_ID
                                    && gp.PlatformGameId == xboxGame.TitleId);

                            if (existingGamePlatform != null)
                            {
                                game = await _context.Games.FindAsync(existingGamePlatform.GameId);
                            }

                            // 如果游戏不存在，创建新游戏（即使名称相同，不同平台也应该创建新记录）
                            if (game == null)
                            {
                                // 创建新游戏
                                game = new Game
                                {
                                    Name = xboxGame.Name,
                                    IsFree = xboxGame.IsFree,
                                    RequireAge = (byte?)xboxGame.RequiredAge,
                                    ShortDescription = xboxGame.ShortDescription,
                                    DetailedDescription = xboxGame.DetailedDescription,
                                    HeaderImage = xboxGame.HeaderImage,
                                    CapsuleImage = xboxGame.HeaderImage,
                                    Background = xboxGame.HeaderImage,
                                    Windows = true, // Xbox游戏大多支持Windows
                                    Mac = false,
                                    Linux = false,
                                    ReleaseDate = DateTime.TryParse(xboxGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                    ReviewScore = 0,
                                    ReviewScoreDesc = "",
                                    NumReviews = 0,
                                    TotalPositive = 0
                                };
                                _context.Games.Add(game);
                                await _context.SaveChangesAsync();

                                // 添加开发商
                                foreach (var devName in xboxGame.Developers)
                                {
                                    if (string.IsNullOrEmpty(devName)) continue;
                                    var truncatedName = devName.Length > 20 ? devName.Substring(0, 20) : devName;
                                    var developer = await _context.Developers.FirstOrDefaultAsync(d => d.Name == truncatedName);
                                    if (developer == null)
                                    {
                                        developer = new Developer { Name = truncatedName };
                                        _context.Developers.Add(developer);
                                        await _context.SaveChangesAsync();
                                    }
                                    if (!await _context.GameDevelopers.AnyAsync(gd => gd.GameId == game.GameId && gd.DeveloperId == developer.DeveloperId))
                                    {
                                        _context.GameDevelopers.Add(new GameDeveloper { GameId = game.GameId, DeveloperId = developer.DeveloperId });
                                    }
                                }

                                // 添加发行商
                                foreach (var pubName in xboxGame.Publishers)
                                {
                                    if (string.IsNullOrEmpty(pubName)) continue;
                                    var truncatedName = pubName.Length > 20 ? pubName.Substring(0, 20) : pubName;
                                    var publisher = await _context.Publishers.FirstOrDefaultAsync(p => p.Name == truncatedName);
                                    if (publisher == null)
                                    {
                                        publisher = new Publisher { Name = truncatedName };
                                        _context.Publishers.Add(publisher);
                                        await _context.SaveChangesAsync();
                                    }
                                    if (!await _context.GamePublishers.AnyAsync(gp => gp.GameId == game.GameId && gp.PublisherId == publisher.PublisherId))
                                    {
                                        _context.GamePublishers.Add(new GamePublisher { GameId = game.GameId, PublisherId = publisher.PublisherId });
                                    }
                                }
                            }

                            // 创建或更新游戏平台映射
                            if (!await _context.GamePlatforms.AnyAsync(gp => gp.GameId == game.GameId && gp.PlatformId == XBOX_PLATFORM_ID))
                            {
                                _context.GamePlatforms.Add(new GamePlatform
                                {
                                    GameId = game.GameId,
                                    PlatformId = XBOX_PLATFORM_ID,
                                    PlatformGameId = xboxGame.TitleId,
                                    GamePlatformUrl = $"https://www.xbox.com/games/store/-/{xboxGame.TitleId}"
                                });
                            }

                            // 创建或更新用户平台游戏库记录
                            var lastPlayedDate = string.IsNullOrEmpty(xboxGame.LastPlayed) 
                                ? (DateTime?)null 
                                : DateTime.TryParse(xboxGame.LastPlayed, out var lp) ? lp : null;
                            
                            var totalAchievements = xboxGame.Achievements?.Total ?? 0;
                            var unlockedAchievements = xboxGame.Achievements?.CurrentAchievements ?? 0;
                            
                            var userGame = await _context.UserPlatformLibraries
                                .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.XboxUserId 
                                    && upl.PlatformId == XBOX_PLATFORM_ID 
                                    && upl.GameId == game.GameId);

                            if (userGame == null)
                            {
                                _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                {
                                    PlatformUserId = request.XboxUserId,
                                    PlatformId = XBOX_PLATFORM_ID,
                                    GameId = game.GameId,
                                    PlaytimeMinutes = xboxGame.PlayTimeMinutes,
                                    LastPlayed = lastPlayedDate,
                                    AchievementsTotal = totalAchievements,
                                    AchievementsUnlocked = unlockedAchievements
                                });
                            }
                            else
                            {
                                // 更新游戏库记录
                                userGame.PlaytimeMinutes = xboxGame.PlayTimeMinutes;
                                userGame.LastPlayed = lastPlayedDate;
                                userGame.AchievementsTotal = totalAchievements;
                                userGame.AchievementsUnlocked = unlockedAchievements;
                            }

                            await _context.SaveChangesAsync();
                            gamesCount++;
                            
                            if (totalAchievements > 0)
                            {
                                achievementsCount += totalAchievements;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "导入游戏失败: {GameName}", xboxGame.Name);
                        }
                    }
                    
                    _logger.LogInformation("成功导入 {Count} 个Xbox游戏", gamesCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入游戏库数据失败");
                }
            }

            // 导入成就数据
            if (request.ImportAchievements)
            {
                try
                {
                    _logger.LogInformation("开始导入Xbox游戏成就数据...");
                    
                    // 获取用户游戏列表（只获取已导入的游戏）
                    var userGames = await _context.UserPlatformLibraries
                        .Where(upl => upl.PlatformUserId == request.XboxUserId && upl.PlatformId == XBOX_PLATFORM_ID)
                        .Include(upl => upl.Game)
                        .ToListAsync();
                    
                    if (userGames.Count == 0)
                    {
                        _logger.LogWarning("用户没有已导入的游戏，无法导入成就数据");
                    }
                    else
                    {
                        int achievementsImported = 0;
                        int userAchievementsUpdated = 0;
                        
                        foreach (var userGame in userGames)
                        {
                            try
                            {
                                // 获取游戏的 Xbox TitleId
                                var gamePlatform = await _context.GamePlatforms
                                    .FirstOrDefaultAsync(gp => gp.GameId == userGame.GameId && gp.PlatformId == XBOX_PLATFORM_ID);
                                
                                if (gamePlatform == null || string.IsNullOrEmpty(gamePlatform.PlatformGameId))
                                {
                                    _logger.LogWarning("游戏 {GameId} 没有Xbox平台映射，跳过成就导入", userGame.GameId);
                                    continue;
                                }
                                
                                var titleId = gamePlatform.PlatformGameId;
                                _logger.LogInformation("开始导入游戏成就: gameId={GameId}, titleId={TitleId}, gameName={GameName}", 
                                    userGame.GameId, titleId, userGame.Game.Name);
                                
                                // 调用服务获取游戏成就
                                var achievementsData = await _xboxService.GetXboxGameAchievements(request.XboxUserId, userId, titleId);
                                
                                if (achievementsData == null || achievementsData.Count == 0)
                                {
                                    _logger.LogWarning("游戏 {GameId} (titleId={TitleId}) 没有获取到成就数据", userGame.GameId, titleId);
                                    continue;
                                }
                                
                                // 处理每个成就
                                foreach (var achData in achievementsData)
                                {
                                    try
                                    {
                                        // 查找或创建成就记录（成就是游戏级别的，不区分平台）
                                        var achievement = await _context.Achievements
                                            .FirstOrDefaultAsync(a => a.GameId == userGame.GameId && a.AchievementName == achData.Id);
                                        
                                        if (achievement == null)
                                        {
                                            // 创建新成就记录
                                            achievement = new Achievement
                                            {
                                                GameId = userGame.GameId,
                                                PlatformId = XBOX_PLATFORM_ID,
                                                AchievementName = achData.Id,
                                                DisplayName = achData.Name,
                                                Description = achData.Description,
                                                Hidden = achData.IsSecret,
                                                IconUnlocked = achData.IconUnlocked ?? "",
                                                IconLocked = achData.IconLocked ?? ""
                                            };
                                            _context.Achievements.Add(achievement);
                                            await _context.SaveChangesAsync(); // 保存以获取 AchievementId
                                            achievementsImported++;
                                            _logger.LogInformation("创建新成就: gameId={GameId}, achievementId={AchievementId}, name={Name}", 
                                                userGame.GameId, achievement.AchievementId, achData.Name);
                                        }
                                        else
                                        {
                                            // 更新已有成就的缺失字段
                                            bool needsUpdate = false;
                                            
                                            if (string.IsNullOrEmpty(achievement.DisplayName) && !string.IsNullOrEmpty(achData.Name))
                                            {
                                                achievement.DisplayName = achData.Name;
                                                needsUpdate = true;
                                            }
                                            
                                            if (string.IsNullOrEmpty(achievement.Description) && !string.IsNullOrEmpty(achData.Description))
                                            {
                                                achievement.Description = achData.Description;
                                                needsUpdate = true;
                                            }
                                            
                                            if (string.IsNullOrEmpty(achievement.IconUnlocked) && !string.IsNullOrEmpty(achData.IconUnlocked))
                                            {
                                                achievement.IconUnlocked = achData.IconUnlocked;
                                                needsUpdate = true;
                                            }
                                            
                                            if (string.IsNullOrEmpty(achievement.IconLocked) && !string.IsNullOrEmpty(achData.IconLocked))
                                            {
                                                achievement.IconLocked = achData.IconLocked;
                                                needsUpdate = true;
                                            }
                                            
                                            if (needsUpdate)
                                            {
                                                await _context.SaveChangesAsync();
                                            }
                                        }
                                        
                                        // 创建或更新用户成就解锁记录
                                        var userAchievement = await _context.UserAchievements
                                            .FirstOrDefaultAsync(ua => ua.UserId == userId 
                                                && ua.AchievementId == achievement.AchievementId 
                                                && ua.PlatformId == XBOX_PLATFORM_ID);
                                        
                                        var unlockTime = achData.IsUnlocked && !string.IsNullOrEmpty(achData.UnlockTime)
                                            ? DateTime.TryParse(achData.UnlockTime, out var dt) ? dt : (DateTime?)null
                                            : null;
                                        
                                        if (userAchievement == null)
                                        {
                                            // 创建新用户成就记录
                                            userAchievement = new UserAchievement
                                            {
                                                UserId = userId,
                                                AchievementId = achievement.AchievementId,
                                                PlatformId = XBOX_PLATFORM_ID,
                                                Unlocked = achData.IsUnlocked,
                                                UnlockTime = unlockTime
                                            };
                                            _context.UserAchievements.Add(userAchievement);
                                            userAchievementsUpdated++;
                                        }
                                        else
                                        {
                                            // 更新现有记录
                                            bool wasUnlocked = userAchievement.Unlocked;
                                            userAchievement.Unlocked = achData.IsUnlocked;
                                            userAchievement.UnlockTime = unlockTime;
                                            
                                            // 如果状态发生变化，记录更新
                                            if (wasUnlocked != achData.IsUnlocked)
                                            {
                                                userAchievementsUpdated++;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "处理成就失败: gameId={GameId}, achievementId={AchievementId}", 
                                            userGame.GameId, achData.Id);
                                    }
                                }
                                
                                // 批量保存用户成就记录
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("游戏 {GameId} 成就导入完成: 成就数={AchievementsCount}, 用户成就更新数={UserAchievementsCount}", 
                                    userGame.GameId, achievementsData.Count, userAchievementsUpdated);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "导入游戏成就失败: gameId={GameId}", userGame.GameId);
                            }
                        }
                        
                        achievementsCount = achievementsImported;
                        _logger.LogInformation("成功导入 {Count} 个成就和 {UserAchievementsCount} 条用户成就记录", 
                            achievementsImported, userAchievementsUpdated);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入成就数据失败");
                }
            }

            var result = new XboxImportResponseDto
            {
                TaskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new XboxImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };

            return Ok(ApiResponse<XboxImportResponseDto>.SuccessResponse(result, "Xbox数据导入完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Xbox数据时发生错误");
            return StatusCode(500, ApiResponse<XboxImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Xbox用户信息
    /// </summary>
    /// <param name="xuid">Xbox用户ID（XUID）</param>
    [HttpGet("user/{xuid}")]
    [ProducesResponseType(typeof(ApiResponse<XboxUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<XboxUserDto>>> GetXboxUser(string xuid)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Xbox用户信息: xuid={Xuid}, userId={UserId}", xuid, userId);

            var result = await _xboxService.GetXboxUser(xuid, userId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_XBOX_USER_NOT_FOUND", "Xbox用户不存在或令牌无效"));
            }

            return Ok(ApiResponse<XboxUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox用户信息时发生错误");
            return StatusCode(500, ApiResponse<XboxUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Xbox游戏信息
    /// </summary>
    /// <param name="titleId">Xbox游戏标题ID</param>
    [HttpGet("games/{titleId}")]
    [ProducesResponseType(typeof(ApiResponse<XboxGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<XboxGameDto>>> GetXboxGame(string titleId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Xbox游戏信息: titleId={TitleId}, userId={UserId}", titleId, userId);

            var result = await _xboxService.GetXboxGame(titleId, userId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_XBOX_GAME_NOT_FOUND", "Xbox游戏不存在"));
            }

            return Ok(ApiResponse<XboxGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox游戏信息时发生错误");
            return StatusCode(500, ApiResponse<XboxGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Xbox用户成就
    /// </summary>
    /// <param name="xuid">Xbox用户ID</param>
    [HttpGet("user/{xuid}/achievements")]
    [ProducesResponseType(typeof(ApiResponse<List<XboxUserAchievementDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<XboxUserAchievementDto>>>> GetXboxUserAchievements(string xuid)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Xbox用户成就: xuid={Xuid}, userId={UserId}", xuid, userId);

            var result = await _xboxService.GetXboxUserAchievements(xuid, userId);

            return Ok(ApiResponse<List<XboxUserAchievementDto>>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox用户成就时发生错误");
            return StatusCode(500, ApiResponse<List<XboxUserAchievementDto>>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

