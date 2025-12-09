using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

/// <summary>
/// GOG API集成控制器
/// 提供GOG数据导入、用户信息查询、游戏信息查询等功能
/// </summary>
[ApiController]
[Route("api/v1/gog")]
[Authorize]
public class GogController : ControllerBase
{
    private readonly IGogService _gogService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<GogController> _logger;
    private const int GOG_PLATFORM_ID = 5; // GOG平台ID

    public GogController(IGogService gogService, PlayLinkerDbContext context, ILogger<GogController> logger)
    {
        _gogService = gogService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 初始化平台数据
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

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        foreach (var platformInfo in platforms)
        {
            var exists = await _context.Platforms
                .AnyAsync(p => p.PlatformId == platformInfo.Id || p.PlatformName == platformInfo.Name);

            if (!exists)
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
    }

    /// <summary>
    /// 检查GOG令牌状态
    /// </summary>
    /// <remarks>
    /// 检查是否已有有效的GOG认证令牌。
    /// 建议在调用认证接口前先调用此接口,了解当前状态。
    /// </remarks>
    [HttpGet("token-status")]
    [ProducesResponseType(typeof(ApiResponse<GogAuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GogAuthResponseDto>>> CheckTokenStatus()
    {
        try
        {
            _logger.LogInformation("检查GOG令牌状态");

            var result = await _gogService.CheckTokenStatus();

            return Ok(ApiResponse<GogAuthResponseDto>.SuccessResponse(result, 
                result.Success ? "令牌有效" : "令牌无效或不存在"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return StatusCode(500, ApiResponse<GogAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// GOG认证
    /// </summary>
    /// <param name="request">认证请求</param>
    /// <remarks>
    /// <para><b>认证流程(两步):</b></para>
    /// 
    /// <para><b>步骤1: 获取认证URL</b></para>
    /// <code>
    /// POST /api/v1/gog/authenticate
    /// {
    ///   "forceReauth": false
    /// }
    /// </code>
    /// <para>响应示例:</para>
    /// <code>
    /// {
    ///   "success": false,
    ///   "message": "请在浏览器中打开authUrl完成登录...",
    ///   "authUrl": "https://auth.gog.com/auth?client_id=...",
    ///   "needsBrowserAuth": true
    /// }
    /// </code>
    /// <para>1. 复制响应中的 authUrl</para>
    /// <para>2. 在浏览器中打开此URL</para>
    /// <para>3. 登录你的GOG账户</para>
    /// <para>4. 登录成功后，浏览器会跳转到类似这样的URL:</para>
    /// <para>   https://embed.gog.com/on_login_success?origin=client&amp;code=xxxxx</para>
    /// <para>5. 复制浏览器地址栏的<b>完整URL</b></para>
    /// 
    /// <para><b>步骤2: 提供重定向URL完成认证</b></para>
    /// <code>
    /// POST /api/v1/gog/authenticate
    /// {
    ///   "redirectUrl": "https://embed.gog.com/on_login_success?origin=client&amp;code=xxxxx"
    /// }
    /// </code>
    /// <para>系统会自动从URL中提取授权码并完成认证</para>
    /// 
    /// <para><b>刷新令牌(已有令牌时)</b>:</para>
    /// <code>
    /// {
    ///   "forceReauth": false
    /// }
    /// </code>
    /// <para>如果已有有效令牌,会自动刷新,无需提供redirectUrl</para>
    /// 
    /// <para><b>注意事项:</b></para>
    /// <para>- 只需复制完整URL，不需要手动提取授权码</para>
    /// <para>- 令牌保存在 Backend/Tokens/gog_tokens.json</para>
    /// <para>- 认证前建议先调用 GET /api/v1/gog/token-status 检查令牌状态</para>
    /// </remarks>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(ApiResponse<GogAuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GogAuthResponseDto>>> Authenticate(
        [FromBody] GogAuthRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始GOG认证, HasRedirectUrl={HasRedirectUrl}", !string.IsNullOrEmpty(request.RedirectUrl));

            var result = await _gogService.AuthenticateGog(request);

            if (!result.Success)
            {
                return Ok(ApiResponse<GogAuthResponseDto>.SuccessResponse(result, result.Message));
            }

            return Ok(ApiResponse<GogAuthResponseDto>.SuccessResponse(result, "GOG认证成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOG认证时发生错误");
            return StatusCode(500, ApiResponse<GogAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 导入GOG数据
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<GogImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GogImportResponseDto>>> ImportGogData(
        [FromBody] GogImportRequestDto request)
    {
        try
        {
            // 验证 userId
            if (request.UserId <= 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", "userId 参数无效,必须提供有效的用户ID"));
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
                return BadRequest(ApiResponse<object>.ErrorResponse("BAD_REQUEST", $"用户ID {request.UserId} 不存在,请先创建用户"));
            }

            var userId = request.UserId;
            _logger.LogInformation("导入GOG数据: userId={UserId}, gogUserId={GogUserId}", userId, request.GogUserId);

            // 初始化平台数据
            await InitializePlatformsAsync();

            // 获取GOG用户信息
            var gogUser = await _gogService.GetGogUser(request.GogUserId);
            if (gogUser == null)
            {
                return BadRequest(ApiResponse<GogImportResponseDto>.ErrorResponse("ERR_GOG_USER_NOT_FOUND", "GOG用户不存在或令牌无效,请先进行认证"));
            }

            // 存储或更新PlayerPlatform信息
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == gogUser.GogUserId && pp.PlatformId == GOG_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = gogUser.GogUserId,
                    PlatformId = GOG_PLATFORM_ID,
                    ProfileName = gogUser.Username,
                    ProfileUrl = gogUser.ProfileUrl,
                    AccountCreated = string.IsNullOrEmpty(gogUser.AccountCreated) ? null : DateTime.TryParse(gogUser.AccountCreated, out var created) ? created : null,
                    Country = gogUser.Country
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = gogUser.Username;
                playerPlatform.ProfileUrl = gogUser.ProfileUrl;
                playerPlatform.Country = gogUser.Country;
            }
            await _context.SaveChangesAsync();

            // 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == GOG_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = userId,
                    PlatformId = GOG_PLATFORM_ID,
                    PlatformUserId = gogUser.GogUserId,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1)
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                userPlatformBinding.PlatformUserId = gogUser.GogUserId;
                userPlatformBinding.BindingStatus = true;
                userPlatformBinding.LastSyncTime = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            // 导入游戏库数据
            int gamesCount = 0;
            int achievementsCount = 0;
            
            if (request.ImportGames)
            {
                try
                {
                    // 获取完整的GOG游戏数据
                    _logger.LogInformation("开始导入GOG游戏数据...");
                    
                    var gogGames = await _gogService.GetGogUserGames(request.GogUserId);
                    
                    foreach (var gogGame in gogGames)
                    {
                        try
                        {
                            // 查找或创建游戏
                            var game = await _context.Games
                                .FirstOrDefaultAsync(g => g.Name == gogGame.Name);

                            if (game == null)
                            {
                                // 创建新游戏
                                game = new Game
                                {
                                    Name = gogGame.Name,
                                    IsFree = gogGame.IsFree,
                                    RequireAge = (byte?)gogGame.RequiredAge,
                                    ShortDescription = gogGame.ShortDescription,
                                    DetailedDescription = gogGame.DetailedDescription,
                                    HeaderImage = gogGame.HeaderImage,
                                    CapsuleImage = gogGame.HeaderImage,
                                    Background = gogGame.HeaderImage,
                                    Windows = gogGame.Platforms.Windows,
                                    Mac = gogGame.Platforms.Mac,
                                    Linux = gogGame.Platforms.Linux,
                                    ReleaseDate = string.IsNullOrEmpty(gogGame.ReleaseDate) ? DateTime.UtcNow : DateTime.TryParse(gogGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                    ReviewScore = 0,
                                    ReviewScoreDesc = "",
                                    NumReviews = 0,
                                    TotalPositive = 0
                                };
                                _context.Games.Add(game);
                                await _context.SaveChangesAsync();

                                // 添加开发商
                                foreach (var devName in gogGame.Developers)
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
                                foreach (var pubName in gogGame.Publishers)
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
                            if (!await _context.GamePlatforms.AnyAsync(gp => gp.GameId == game.GameId && gp.PlatformId == GOG_PLATFORM_ID))
                            {
                                _context.GamePlatforms.Add(new GamePlatform
                                {
                                    GameId = game.GameId,
                                    PlatformId = GOG_PLATFORM_ID,
                                    PlatformGameId = gogGame.GogGameId,
                                    GamePlatformUrl = $"https://www.gog.com/game/{gogGame.GogGameId}"
                                });
                            }

                            // 创建或更新用户平台游戏库记录
                            var totalAchievements = gogGame.Achievements?.Total ?? 0;
                            var unlockedAchievements = gogGame.Achievements?.CurrentAchievements ?? 0;
                            
                            var userGame = await _context.UserPlatformLibraries
                                .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.GogUserId 
                                    && upl.PlatformId == GOG_PLATFORM_ID 
                                    && upl.GameId == game.GameId);

                            if (userGame == null)
                            {
                                _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                {
                                    PlatformUserId = request.GogUserId,
                                    PlatformId = GOG_PLATFORM_ID,
                                    GameId = game.GameId,
                                    PlaytimeMinutes = gogGame.PlayTimeMinutes,
                                    LastPlayed = null, // GOG API可能不提供此信息
                                    AchievementsTotal = totalAchievements,
                                    AchievementsUnlocked = unlockedAchievements
                                });
                            }
                            else
                            {
                                // 更新游戏库记录
                                userGame.PlaytimeMinutes = gogGame.PlayTimeMinutes;
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
                            _logger.LogError(ex, "导入游戏失败: {GameName}", gogGame.Name);
                        }
                    }
                    
                    _logger.LogInformation("成功导入 {Count} 个GOG游戏", gamesCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入游戏库数据失败");
                }
            }

            var result = new GogImportResponseDto
            {
                TaskId = $"gog_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new GogImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };

            return Ok(ApiResponse<GogImportResponseDto>.SuccessResponse(result, "GOG数据导入完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入GOG数据时发生错误");
            return StatusCode(500, ApiResponse<GogImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取GOG用户信息
    /// </summary>
    /// <param name="gogUserId">GOG用户ID</param>
    [HttpGet("user/{gogUserId}")]
    [ProducesResponseType(typeof(ApiResponse<GogUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GogUserDto>>> GetGogUser(string gogUserId)
    {
        try
        {
            _logger.LogInformation("获取GOG用户信息: gogUserId={GogUserId}", gogUserId);

            var result = await _gogService.GetGogUser(gogUserId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GOG_USER_NOT_FOUND", "GOG用户不存在或令牌无效"));
            }

            return Ok(ApiResponse<GogUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG用户信息时发生错误");
            return StatusCode(500, ApiResponse<GogUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取GOG游戏信息
    /// </summary>
    /// <param name="gogGameId">GOG游戏ID</param>
    [HttpGet("games/{gogGameId}")]
    [ProducesResponseType(typeof(ApiResponse<GogGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GogGameDto>>> GetGogGame(string gogGameId)
    {
        try
        {
            _logger.LogInformation("获取GOG游戏信息: gogGameId={GogGameId}", gogGameId);

            var result = await _gogService.GetGogGame(gogGameId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GOG_GAME_NOT_FOUND", "GOG游戏不存在"));
            }

            return Ok(ApiResponse<GogGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG游戏信息时发生错误");
            return StatusCode(500, ApiResponse<GogGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}
