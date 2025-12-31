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
/// Epic Games API集成控制器
/// 提供Epic Games数据导入、用户信息查询、游戏信息查询等功能
/// </summary>
[ApiController]
[Route("api/v1/epic")]
[Authorize]
public class EpicController : ControllerBase
{
    private readonly IEpicService _epicService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<EpicController> _logger;
    private const int EPIC_PLATFORM_ID = 2; // Epic Games平台ID

    public EpicController(IEpicService epicService, PlayLinkerDbContext context, ILogger<EpicController> logger)
    {
        _epicService = epicService;
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

        var platformIds = platforms.Select(p => p.Id).ToList();
        var platformNames = platforms.Select(p => p.Name).ToList();
        
        var existingPlatforms = await _context.Platforms
            .Where(p => platformIds.Contains(p.PlatformId) || platformNames.Contains(p.PlatformName))
            .Select(p => new { p.PlatformId, p.PlatformName })
            .ToListAsync();

        var existingIds = existingPlatforms.Select(p => p.PlatformId).ToHashSet();
        var existingNames = existingPlatforms.Select(p => p.PlatformName).ToHashSet();

        var platformsToInsert = platforms
            .Where(p => !existingIds.Contains(p.Id) && !existingNames.Contains(p.Name))
            .ToList();

        if (platformsToInsert.Count == 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

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
    /// 检查Epic Games令牌状态
    /// </summary>
    /// <remarks>
    /// 检查是否已通过Legendary CLI登录Epic Games账户。
    /// 注意：Epic Games认证需要通过命令行工具 `legendary auth` 完成。
    /// </remarks>
    [HttpGet("token-status")]
    [ProducesResponseType(typeof(ApiResponse<EpicAuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EpicAuthResponseDto>>> CheckTokenStatus()
    {
        try
        {
            await InitializePlatformsAsync();
            var userId = GetCurrentUserId();
            _logger.LogInformation("检查Epic Games令牌状态: userId={UserId}", userId);

            var result = await _epicService.CheckTokenStatus(userId, EPIC_PLATFORM_ID);

            return Ok(ApiResponse<EpicAuthResponseDto>.SuccessResponse(result,
                result.Success ? "已登录" : "未登录"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return StatusCode(500, ApiResponse<EpicAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// Epic Games认证
    /// </summary>
    /// <remarks>
    /// Epic Games认证需要通过Legendary CLI完成。
    /// 
    /// <para><b>方式1: 使用授权码（推荐）</b></para>
    /// <para>1. 访问: https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&amp;responseType=code</para>
    /// <para>2. 登录后复制URL中的code参数</para>
    /// <para>3. 调用此接口并传入code</para>
    /// 
    /// <para><b>方式2: 命令行认证</b></para>
    /// <para>在服务器上运行: legendary auth</para>
    /// <para>然后调用此接口（不传code）检查状态</para>
    /// </remarks>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(ApiResponse<EpicAuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<EpicAuthResponseDto>>> Authenticate(
        [FromBody] EpicAuthRequestDto request)
    {
        try
        {
            await InitializePlatformsAsync();
            var userId = GetCurrentUserId();
            _logger.LogInformation("开始Epic Games认证: userId={UserId}, HasCode={HasCode}", userId, !string.IsNullOrEmpty(request.Code));

            var result = await _epicService.AuthenticateEpic(request, userId);

            if (!result.Success)
            {
                return Ok(ApiResponse<EpicAuthResponseDto>.SuccessResponse(result, result.Message));
            }

            // 认证成功，获取用户信息并创建绑定
            if (!string.IsNullOrEmpty(result.EpicAccountId))
            {
                var epicUser = await _epicService.GetEpicUser(result.EpicAccountId, userId);
                if (epicUser != null)
                {
                    // 创建或更新PlayerPlatform记录
                    var playerPlatform = await _context.PlayerPlatforms
                        .FirstOrDefaultAsync(pp => pp.PlatformUserId == epicUser.EpicAccountId && pp.PlatformId == EPIC_PLATFORM_ID);

                    if (playerPlatform == null)
                    {
                        playerPlatform = new PlayerPlatform
                        {
                            PlatformUserId = epicUser.EpicAccountId,
                            PlatformId = EPIC_PLATFORM_ID,
                            ProfileName = epicUser.DisplayName,
                            ProfileUrl = $"https://www.epicgames.com/account/personal?productName=&lang=zh-CN"
                        };
                        _context.PlayerPlatforms.Add(playerPlatform);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        playerPlatform.ProfileName = epicUser.DisplayName;
                        await _context.SaveChangesAsync();
                    }

                    // 创建或更新用户平台绑定记录
                    var userPlatformBinding = await _context.UserPlatformBindings
                        .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == EPIC_PLATFORM_ID);

                    if (userPlatformBinding == null)
                    {
                        userPlatformBinding = new UserPlatformBinding
                        {
                            UserId = userId,
                            PlatformId = EPIC_PLATFORM_ID,
                            PlatformUserId = epicUser.EpicAccountId,
                            BindingStatus = true,
                            BindingTime = DateTime.UtcNow,
                            LastSyncTime = DateTime.UtcNow,
                            ExpireTime = DateTime.UtcNow.AddYears(1)
                        };
                        _context.UserPlatformBindings.Add(userPlatformBinding);
                    }
                    else
                    {
                        userPlatformBinding.PlatformUserId = epicUser.EpicAccountId;
                        userPlatformBinding.BindingStatus = true;
                        userPlatformBinding.LastSyncTime = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(ApiResponse<EpicAuthResponseDto>.SuccessResponse(result, "Epic Games认证成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Epic Games认证时发生错误");
            return StatusCode(500, ApiResponse<EpicAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 导入Epic Games数据
    /// </summary>
    /// <remarks>
    /// 导入Epic Games游戏库和成就数据。
    /// 
    /// <para><b>前置条件：</b></para>
    /// <para>1. 必须已通过 `legendary auth` 命令登录Epic Games账户</para>
    /// <para>2. 确保Legendary CLI已安装并在PATH中</para>
    /// 
    /// <para><b>注意事项：</b></para>
    /// <para>- 游戏详情和成就数据需要访问Epic服务器，可能需要代理</para>
    /// <para>- 首次导入可能需要较长时间</para>
    /// </remarks>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<EpicImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<EpicImportResponseDto>>> ImportEpicData(
        [FromBody] EpicImportRequestDto request)
    {
        try
        {
            await InitializePlatformsAsync();
            
            // 验证 userId
            var userId = GetCurrentUserId();
            if (request.UserId > 0 && request.UserId != userId)
            {
                _logger.LogWarning("请求中的userId与当前用户ID不匹配: RequestUserId={RequestUserId}, CurrentUserId={CurrentUserId}", 
                    request.UserId, userId);
            }
            // 使用当前用户ID，忽略请求中的userId（安全考虑）
            request.UserId = userId;
            
            _logger.LogInformation("开始导入Epic Games数据: epicAccountId={EpicAccountId}, userId={UserId}", 
                request.EpicAccountId, userId);

            // 获取用户信息
            var epicUser = await _epicService.GetEpicUser(request.EpicAccountId, userId);
            if (epicUser == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_EPIC_USER_NOT_FOUND", 
                    "无法获取Epic Games用户信息，请确保已通过 legendary auth 登录"));
            }

            // 创建或更新PlayerPlatform记录
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == epicUser.EpicAccountId && pp.PlatformId == EPIC_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = epicUser.EpicAccountId,
                    PlatformId = EPIC_PLATFORM_ID,
                    ProfileName = epicUser.DisplayName,
                    ProfileUrl = $"https://www.epicgames.com/account/personal?productName=&lang=zh-CN"
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = epicUser.DisplayName;
            }
            await _context.SaveChangesAsync();

            // 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == EPIC_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = userId,
                    PlatformId = EPIC_PLATFORM_ID,
                    PlatformUserId = epicUser.EpicAccountId,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1)
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                userPlatformBinding.PlatformUserId = epicUser.EpicAccountId;
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
                    _logger.LogInformation("开始导入Epic Games游戏数据...");

                    var epicGames = await _epicService.GetEpicUserGames(request.EpicAccountId, userId);

                    foreach (var epicGame in epicGames)
                    {
                        try
                        {
                            // 查找或创建游戏
                            Game? game = null;

                            // 先尝试通过GamePlatform的PlatformGameId匹配
                            var existingGamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.PlatformId == EPIC_PLATFORM_ID
                                    && gp.PlatformGameId == epicGame.GameId);

                            if (existingGamePlatform != null)
                            {
                                game = await _context.Games.FindAsync(existingGamePlatform.GameId);
                            }

                            // 如果没找到，再通过名称查找
                            if (game == null)
                            {
                                game = await _context.Games
                                    .FirstOrDefaultAsync(g => g.Name == epicGame.Name);
                            }

                            if (game == null)
                            {
                                // 创建新游戏
                                game = new Game
                                {
                                    Name = epicGame.Name,
                                    IsFree = epicGame.IsFree,
                                    RequireAge = null,
                                    ShortDescription = epicGame.ShortDescription,
                                    DetailedDescription = epicGame.DetailedDescription,
                                    HeaderImage = epicGame.HeaderImage,
                                    CapsuleImage = epicGame.HeaderImage,
                                    Background = epicGame.HeaderImage,
                                    Windows = epicGame.Platforms.Windows,
                                    Mac = epicGame.Platforms.Mac,
                                    Linux = epicGame.Platforms.Linux,
                                    ReleaseDate = !string.IsNullOrEmpty(epicGame.ReleaseDate) && 
                                        DateTime.TryParse(epicGame.ReleaseDate, out var releaseDate) 
                                        ? releaseDate : DateTime.UtcNow,
                                    ReviewScore = 0,
                                    ReviewScoreDesc = "",
                                    NumReviews = 0,
                                    TotalPositive = 0
                                };
                                _context.Games.Add(game);
                                await _context.SaveChangesAsync();

                                // 添加开发商
                                foreach (var devName in epicGame.Developers)
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
                                foreach (var pubName in epicGame.Publishers)
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
                            if (!await _context.GamePlatforms.AnyAsync(gp => gp.GameId == game.GameId && gp.PlatformId == EPIC_PLATFORM_ID))
                            {
                                _context.GamePlatforms.Add(new GamePlatform
                                {
                                    GameId = game.GameId,
                                    PlatformId = EPIC_PLATFORM_ID,
                                    PlatformGameId = epicGame.GameId,
                                    GamePlatformUrl = $"https://store.epicgames.com/zh-CN/p/{epicGame.Namespace}"
                                });
                            }

                            // 创建或更新用户平台游戏库记录
                            var totalAchievements = epicGame.Achievements?.Total ?? 0;
                            var unlockedAchievements = epicGame.Achievements?.UnlockedCount ?? 0;

                            var userGame = await _context.UserPlatformLibraries
                                .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.EpicAccountId
                                    && upl.PlatformId == EPIC_PLATFORM_ID
                                    && upl.GameId == game.GameId);

                            if (userGame == null)
                            {
                                _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                {
                                    PlatformUserId = request.EpicAccountId,
                                    PlatformId = EPIC_PLATFORM_ID,
                                    GameId = game.GameId,
                                    PlaytimeMinutes = 0, // Epic Games不提供游玩时长
                                    LastPlayed = null,
                                    AchievementsTotal = totalAchievements,
                                    AchievementsUnlocked = unlockedAchievements
                                });
                            }
                            else
                            {
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
                            _logger.LogError(ex, "导入游戏失败: {GameName}", epicGame.Name);
                        }
                    }

                    _logger.LogInformation("成功导入 {Count} 个Epic Games游戏", gamesCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入游戏库数据失败");
                }
            }

            var result = new EpicImportResponseDto
            {
                TaskId = $"epic_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new EpicImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };

            return Ok(ApiResponse<EpicImportResponseDto>.SuccessResponse(result, "Epic Games数据导入完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Epic Games数据时发生错误");
            return StatusCode(500, ApiResponse<EpicImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Epic Games用户信息
    /// </summary>
    /// <param name="epicAccountId">Epic账户ID</param>
    [HttpGet("user/{epicAccountId}")]
    [ProducesResponseType(typeof(ApiResponse<EpicUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EpicUserDto>>> GetEpicUser(string epicAccountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Epic Games用户信息: epicAccountId={EpicAccountId}, userId={UserId}", epicAccountId, userId);

            var result = await _epicService.GetEpicUser(epicAccountId, userId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_EPIC_USER_NOT_FOUND", 
                    "Epic Games用户不存在或未登录，请先运行 legendary auth 命令登录"));
            }

            return Ok(ApiResponse<EpicUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games用户信息时发生错误");
            return StatusCode(500, ApiResponse<EpicUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Epic Games游戏信息
    /// </summary>
    /// <param name="gameId">游戏ID (app_name)</param>
    [HttpGet("games/{gameId}")]
    [ProducesResponseType(typeof(ApiResponse<EpicGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EpicGameDto>>> GetEpicGame(string gameId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Epic Games游戏信息: gameId={GameId}, userId={UserId}", gameId, userId);

            var result = await _epicService.GetEpicGame(gameId, userId);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_EPIC_GAME_NOT_FOUND", "Epic Games游戏不存在"));
            }

            return Ok(ApiResponse<EpicGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games游戏信息时发生错误");
            return StatusCode(500, ApiResponse<EpicGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

