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
/// PSN API集成控制器
/// 提供PSN数据导入、用户信息查询、游戏信息查询、奖杯数据等功能
/// </summary>
[ApiController]
[Route("api/v1/psn")]
[Authorize]
public class PsnController : ControllerBase
{
    private readonly IPsnService _psnService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<PsnController> _logger;
    private const int PSN_PLATFORM_ID = 6; // PSN平台ID

    public PsnController(IPsnService psnService, PlayLinkerDbContext context, ILogger<PsnController> logger)
    {
        _psnService = psnService;
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
    /// 检查PSN令牌状态
    /// </summary>
    /// <remarks>
    /// 检查是否已有有效的PSN认证令牌。
    /// 建议在调用认证接口前先调用此接口,了解当前状态。
    /// </remarks>
    [HttpGet("token-status")]
    [ProducesResponseType(typeof(ApiResponse<PsnAuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PsnAuthResponseDto>>> CheckTokenStatus()
    {
        try
        {
            _logger.LogInformation("检查PSN令牌状态");

            var result = await _psnService.CheckTokenStatus();

            return Ok(ApiResponse<PsnAuthResponseDto>.SuccessResponse(result, 
                result.Success ? "令牌有效" : "令牌无效或不存在"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return StatusCode(500, ApiResponse<PsnAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// PSN认证
    /// </summary>
    /// <param name="request">认证请求</param>
    /// <remarks>
    /// <para><b>如何获取NPSSO:</b></para>
    /// <para>1. 在浏览器中登录 PlayStation 账户</para>
    /// <para>2. 访问: https://ca.account.sony.com/api/v1/ssocookie</para>
    /// <para>3. 复制返回的 npsso 值(64个字符的字符串)</para>
    /// <para>4. 将 npsso 值填入请求的 npsso 字段</para>
    /// 
    /// <para><b>使用场景:</b></para>
    /// <para>1. <b>首次认证</b>:</para>
    /// <code>
    /// {
    ///   "npsso": "你的64位NPSSO字符串",
    ///   "forceReauth": false
    /// }
    /// </code>
    /// <para>令牌会保存到 Backend/Tokens/psn_tokens.json</para>
    /// 
    /// <para>2. <b>刷新令牌</b>:</para>
    /// <para>令牌会自动刷新,无需手动操作</para>
    /// 
    /// <para>3. <b>服务器部署建议</b>:</para>
    /// <para>- 在本地完成首次认证</para>
    /// <para>- 将生成的 Backend/Tokens/psn_tokens.json 文件上传到服务器</para>
    /// <para>- 令牌会自动刷新</para>
    /// 
    /// <para><b>注意事项:</b></para>
    /// <para>- NPSSO有效期约2个月</para>
    /// <para>- 令牌过期后需要重新获取NPSSO进行认证</para>
    /// <para>- 认证前建议先调用 GET /api/v1/psn/token-status 检查令牌状态</para>
    /// </remarks>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(ApiResponse<PsnAuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PsnAuthResponseDto>>> Authenticate(
        [FromBody] PsnAuthRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始PSN认证");

            var result = await _psnService.AuthenticatePsn(request);

            if (!result.Success)
            {
                return Ok(ApiResponse<PsnAuthResponseDto>.SuccessResponse(result, result.Message));
            }

            return Ok(ApiResponse<PsnAuthResponseDto>.SuccessResponse(result, "PSN认证成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PSN认证时发生错误");
            return StatusCode(500, ApiResponse<PsnAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 导入PSN数据
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<PsnImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PsnImportResponseDto>>> ImportPsnData(
        [FromBody] PsnImportRequestDto request)
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
            _logger.LogInformation("导入PSN数据: userId={UserId}, psnOnlineId={OnlineId}", userId, request.PsnOnlineId);

            // 初始化平台数据
            await InitializePlatformsAsync();

            // 获取PSN用户信息
            var psnUser = await _psnService.GetPsnUser(request.PsnOnlineId);
            if (psnUser == null)
            {
                return BadRequest(ApiResponse<PsnImportResponseDto>.ErrorResponse("ERR_PSN_USER_NOT_FOUND", "PSN用户不存在或令牌无效,请先进行认证"));
            }

            // 存储或更新PlayerPlatform信息
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.ProfileName == psnUser.OnlineId && pp.PlatformId == PSN_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = psnUser.OnlineId,
                    PlatformId = PSN_PLATFORM_ID,
                    ProfileName = psnUser.OnlineId,
                    ProfileUrl = psnUser.ProfileUrl,
                    AccountCreated = string.IsNullOrEmpty(psnUser.AccountCreated) ? null : DateTime.TryParse(psnUser.AccountCreated, out var created) ? created : null,
                    Country = psnUser.Country
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = psnUser.OnlineId;
                playerPlatform.ProfileUrl = psnUser.ProfileUrl;
                playerPlatform.Country = psnUser.Country;
            }
            await _context.SaveChangesAsync();

            // 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == PSN_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = userId,
                    PlatformId = PSN_PLATFORM_ID,
                    PlatformUserId = psnUser.OnlineId,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1)
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                userPlatformBinding.PlatformUserId = psnUser.OnlineId;
                userPlatformBinding.BindingStatus = true;
                userPlatformBinding.LastSyncTime = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            // 导入游戏库数据
            int gamesCount = 0;
            int trophiesCount = 0;
            
            if (request.ImportGames)
            {
                try
                {
                    // 获取完整的PSN游戏数据
                    _logger.LogInformation("开始导入PSN游戏数据...");
                    
                    var psnGames = await _psnService.GetPsnUserGames(request.PsnOnlineId);
                    
                    foreach (var psnGame in psnGames)
                    {
                        try
                        {
                            // 查找或创建游戏
                            var game = await _context.Games
                                .FirstOrDefaultAsync(g => g.Name == psnGame.Name);

                            if (game == null)
                            {
                                // 创建新游戏
                                game = new Game
                                {
                                    Name = psnGame.Name,
                                    IsFree = psnGame.IsFree,
                                    RequireAge = (byte?)psnGame.RequiredAge,
                                    ShortDescription = psnGame.ShortDescription,
                                    DetailedDescription = psnGame.DetailedDescription,
                                    HeaderImage = psnGame.HeaderImage,
                                    CapsuleImage = psnGame.HeaderImage,
                                    Background = psnGame.HeaderImage,
                                    Windows = false,
                                    Mac = false,
                                    Linux = false,
                                    ReleaseDate = string.IsNullOrEmpty(psnGame.ReleaseDate) ? DateTime.UtcNow : DateTime.TryParse(psnGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                    ReviewScore = 0,
                                    ReviewScoreDesc = "",
                                    NumReviews = 0,
                                    TotalPositive = 0
                                };
                                _context.Games.Add(game);
                                await _context.SaveChangesAsync();

                                // 添加开发商
                                foreach (var devName in psnGame.Developers)
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
                                foreach (var pubName in psnGame.Publishers)
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
                            if (!await _context.GamePlatforms.AnyAsync(gp => gp.GameId == game.GameId && gp.PlatformId == PSN_PLATFORM_ID))
                            {
                                _context.GamePlatforms.Add(new GamePlatform
                                {
                                    GameId = game.GameId,
                                    PlatformId = PSN_PLATFORM_ID,
                                    PlatformGameId = psnGame.TitleId,
                                    GamePlatformUrl = $"https://store.playstation.com/concept/{psnGame.TitleId}"
                                });
                            }

                            // 创建或更新用户平台游戏库记录
                            var totalAchievements = psnGame.Achievements?.Total ?? 0;
                            var unlockedAchievements = (int)Math.Round(totalAchievements * psnGame.Progress / 100.0);
                            
                            var userGame = await _context.UserPlatformLibraries
                                .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.PsnOnlineId 
                                    && upl.PlatformId == PSN_PLATFORM_ID 
                                    && upl.GameId == game.GameId);

                            if (userGame == null)
                            {
                                _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                {
                                    PlatformUserId = request.PsnOnlineId,
                                    PlatformId = PSN_PLATFORM_ID,
                                    GameId = game.GameId,
                                    PlaytimeMinutes = 0, // PSN不提供游戏时长
                                    LastPlayed = null,
                                    AchievementsTotal = totalAchievements,
                                    AchievementsUnlocked = unlockedAchievements
                                });
                            }
                            else
                            {
                                // 更新游戏库记录
                                userGame.AchievementsTotal = totalAchievements;
                                userGame.AchievementsUnlocked = unlockedAchievements;
                            }

                            await _context.SaveChangesAsync();
                            gamesCount++;
                            
                            if (totalAchievements > 0)
                            {
                                trophiesCount += totalAchievements;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "导入游戏失败: {GameName}", psnGame.Name);
                        }
                    }
                    
                    _logger.LogInformation("成功导入 {Count} 个PSN游戏", gamesCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入游戏库数据失败");
                }
            }

            var result = new PsnImportResponseDto
            {
                TaskId = $"psn_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {trophiesCount} 个奖杯",
                EstimatedTime = 0,
                Items = new PsnImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = trophiesCount
                }
            };

            return Ok(ApiResponse<PsnImportResponseDto>.SuccessResponse(result, "PSN数据导入完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入PSN数据时发生错误");
            return StatusCode(500, ApiResponse<PsnImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取PSN用户信息
    /// </summary>
    /// <param name="onlineId">PSN在线ID</param>
    [HttpGet("user/{onlineId}")]
    [ProducesResponseType(typeof(ApiResponse<PsnUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PsnUserDto>>> GetPsnUser(string onlineId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户信息: onlineId={OnlineId}", onlineId);

            var result = await _psnService.GetPsnUser(onlineId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_PSN_USER_NOT_FOUND", "PSN用户不存在或令牌无效"));
            }

            return Ok(ApiResponse<PsnUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN用户信息时发生错误");
            return StatusCode(500, ApiResponse<PsnUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取PSN游戏信息
    /// </summary>
    /// <param name="titleId">PSN游戏标题ID(npCommunicationId)</param>
    [HttpGet("games/{titleId}")]
    [ProducesResponseType(typeof(ApiResponse<PsnGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PsnGameDto>>> GetPsnGame(string titleId)
    {
        try
        {
            _logger.LogInformation("获取PSN游戏信息: titleId={TitleId}", titleId);

            var result = await _psnService.GetPsnGame(titleId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_PSN_GAME_NOT_FOUND", "PSN游戏不存在"));
            }

            return Ok(ApiResponse<PsnGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN游戏信息时发生错误");
            return StatusCode(500, ApiResponse<PsnGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取PSN用户奖杯
    /// </summary>
    /// <param name="onlineId">PSN在线ID</param>
    [HttpGet("user/{onlineId}/trophies")]
    [ProducesResponseType(typeof(ApiResponse<PsnUserTrophiesResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PsnUserTrophiesResponseDto>>> GetPsnUserTrophies(string onlineId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户奖杯: onlineId={OnlineId}", onlineId);

            var result = await _psnService.GetPsnUserTrophies(onlineId);

            return Ok(ApiResponse<PsnUserTrophiesResponseDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN用户奖杯时发生错误");
            return StatusCode(500, ApiResponse<PsnUserTrophiesResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}
