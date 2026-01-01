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
            var userId = GetCurrentUserId();
            _logger.LogInformation("检查GOG令牌状态: userId={UserId}", userId);

            var result = await _gogService.CheckTokenStatus(userId, 5);

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
            var userId = GetCurrentUserId();
            _logger.LogInformation("开始GOG认证: userId={UserId}, HasRedirectUrl={HasRedirectUrl}", userId, !string.IsNullOrEmpty(request.RedirectUrl));

            var result = await _gogService.AuthenticateGog(request, userId);

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

            var userId = (int)request.UserId;
            _logger.LogInformation("导入GOG数据: userId={UserId}, gogUserId={GogUserId}", userId, request.GogUserId);

            // 初始化平台数据
            await InitializePlatformsAsync();

            // 获取GOG用户信息
            var gogUser = await _gogService.GetGogUser(request.GogUserId, userId);
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
                    UserId = (int)userId,
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
                // 更新绑定时，更新绑定时间和同步时间
                userPlatformBinding.PlatformUserId = gogUser.GogUserId;
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
                    // 获取完整的GOG游戏数据
                    _logger.LogInformation("开始导入GOG游戏数据...");
                    
                    var gogGames = await _gogService.GetGogUserGames(request.GogUserId, userId);
                    
                    _logger.LogInformation("开始处理 {Count} 个GOG游戏", gogGames.Count);
                    
                    foreach (var gogGame in gogGames)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(gogGame.Name))
                            {
                                _logger.LogWarning("跳过游戏：名称为空，GameId={GameId}", gogGame.GogGameId);
                                continue;
                            }
                            
                            // 查找是否已存在该GOG平台的该游戏
                            // 只通过GamePlatform匹配，不通过名称匹配，避免不同平台的同名游戏被错误关联
                            Game? game = null;
                            var existingGamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.PlatformId == GOG_PLATFORM_ID 
                                    && gp.PlatformGameId == gogGame.GogGameId);
                            
                            if (existingGamePlatform != null)
                            {
                                game = await _context.Games.FindAsync(existingGamePlatform.GameId);
                                _logger.LogInformation("找到已存在的游戏: GameId={GameId}, Name={Name}，将更新游戏信息", game?.GameId, game?.Name);
                            }

                            // 如果游戏已存在，更新游戏信息（只补充缺失的字段，不覆盖已有数据）
                            if (game != null)
                            {
                                bool hasChanges = false;
                                
                                // 更新游戏名称（如果当前名称为空或使用默认名称）
                                if (string.IsNullOrEmpty(game.Name) || game.Name.StartsWith("GOG Game "))
                                {
                                    game.Name = gogGame.Name;
                                    hasChanges = true;
                                }
                                
                                // 只有当字段为空时才更新，避免覆盖其他平台的数据
                                if (string.IsNullOrEmpty(game.ShortDescription) && !string.IsNullOrEmpty(gogGame.ShortDescription))
                                {
                                    game.ShortDescription = gogGame.ShortDescription;
                                    hasChanges = true;
                                }
                                
                                if (string.IsNullOrEmpty(game.DetailedDescription) && !string.IsNullOrEmpty(gogGame.DetailedDescription))
                                {
                                    game.DetailedDescription = gogGame.DetailedDescription;
                                    hasChanges = true;
                                }
                                
                                if (string.IsNullOrEmpty(game.HeaderImage) && !string.IsNullOrEmpty(gogGame.HeaderImage))
                                {
                                    // 确保图片URL是完整的（处理 // 开头的相对路径）
                                    var headerImage = gogGame.HeaderImage;
                                    if (headerImage.StartsWith("//"))
                                    {
                                        headerImage = "https:" + headerImage;
                                    }
                                    else if (!headerImage.StartsWith("http"))
                                    {
                                        headerImage = "https://" + headerImage;
                                    }
                                    
                                    // 如果URL没有扩展名，添加 .jpg
                                    if (!headerImage.Contains(".") || (!headerImage.EndsWith(".jpg") && !headerImage.EndsWith(".jpeg") && !headerImage.EndsWith(".png") && !headerImage.EndsWith(".webp")))
                                    {
                                        headerImage = headerImage.TrimEnd('/') + ".jpg";
                                    }
                                    
                                    game.HeaderImage = headerImage;
                                    game.CapsuleImage = headerImage;
                                    game.Background = headerImage;
                                    hasChanges = true;
                                }
                                
                                if (game.ReleaseDate == default(DateTime) && !string.IsNullOrEmpty(gogGame.ReleaseDate) 
                                    && DateTime.TryParse(gogGame.ReleaseDate, out var releaseDate))
                                {
                                    game.ReleaseDate = releaseDate;
                                    hasChanges = true;
                                }
                                
                                // 更新平台支持信息（如果当前没有设置）
                                if (!game.Windows && !game.Mac && !game.Linux)
                                {
                                    game.Windows = gogGame.Platforms?.Windows ?? false;
                                    game.Mac = gogGame.Platforms?.Mac ?? false;
                                    game.Linux = gogGame.Platforms?.Linux ?? false;
                                    hasChanges = true;
                                }
                                
                                if (hasChanges)
                                {
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("已更新游戏信息: GameId={GameId}, Name={Name}", game.GameId, game.Name);
                                }

                                // 只添加新的开发商和发行商关联，不删除已有的
                                if (gogGame.Developers.Count > 0)
                                {
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
                                }

                                if (gogGame.Publishers.Count > 0)
                                {
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
                                await _context.SaveChangesAsync();
                            }

                            // 如果游戏不存在，创建新游戏（即使名称相同，不同平台也应该创建新记录）
                            if (game == null)
                            {
                                // 创建新游戏
                                // 确保图片URL是完整的（处理 // 开头的相对路径）
                                var headerImage = gogGame.HeaderImage ?? "";
                                if (!string.IsNullOrEmpty(headerImage))
                                {
                                    if (headerImage.StartsWith("//"))
                                    {
                                        headerImage = "https:" + headerImage;
                                    }
                                    else if (!headerImage.StartsWith("http"))
                                    {
                                        headerImage = "https://" + headerImage;
                                    }
                                    
                                    // 如果URL没有扩展名，添加 .jpg
                                    if (!headerImage.Contains(".") || (!headerImage.EndsWith(".jpg") && !headerImage.EndsWith(".jpeg") && !headerImage.EndsWith(".png") && !headerImage.EndsWith(".webp")))
                                    {
                                        headerImage = headerImage.TrimEnd('/') + ".jpg";
                                    }
                                }
                                
                                game = new Game
                                {
                                    Name = gogGame.Name,
                                    IsFree = gogGame.IsFree,
                                    RequireAge = (byte?)gogGame.RequiredAge,
                                    ShortDescription = gogGame.ShortDescription,
                                    DetailedDescription = gogGame.DetailedDescription,
                                    HeaderImage = headerImage,
                                    CapsuleImage = headerImage,
                                    Background = headerImage,
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
                    
                    // 导入完成后，更新LastSyncTime
                    if (userPlatformBinding != null)
                    {
                        userPlatformBinding.LastSyncTime = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("已更新LastSyncTime: {LastSyncTime}", userPlatformBinding.LastSyncTime);
                    }
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
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取GOG用户信息: gogUserId={GogUserId}, userId={UserId}", gogUserId, userId);

            var result = await _gogService.GetGogUser(gogUserId, userId);

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
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取GOG游戏信息: gogGameId={GogGameId}, userId={UserId}", gogGameId, userId);

            var result = await _gogService.GetGogGame(gogGameId, userId);

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
