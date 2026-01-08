using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private const int EPIC_PLATFORM_ID = 2; // Epic Games平台ID

    public EpicController(IEpicService epicService, PlayLinkerDbContext context, ILogger<EpicController> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _epicService = epicService;
        _context = context;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
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
    private async Task InitializePlatformsAsync(PlayLinkerDbContext? context = null)
    {
        var dbContext = context ?? _context;
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
        
        var existingPlatforms = await dbContext.Platforms
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

        var connection = dbContext.Database.GetDbConnection();
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
                _logger.LogInformation("认证成功，开始获取用户信息并创建绑定: EpicAccountId={EpicAccountId}", result.EpicAccountId);
                
                try
                {
                    var epicUser = await _epicService.GetEpicUser(result.EpicAccountId, userId);
                    if (epicUser != null)
                    {
                        _logger.LogInformation("成功获取用户信息: DisplayName={DisplayName}", epicUser.DisplayName);
                        
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
                            _logger.LogInformation("创建PlayerPlatform记录成功");
                        }
                        else
                        {
                            playerPlatform.ProfileName = epicUser.DisplayName;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("更新PlayerPlatform记录成功");
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
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("创建UserPlatformBinding记录成功");
                        }
                        else
                        {
                            // 更新绑定时，更新绑定时间和同步时间
                            userPlatformBinding.PlatformUserId = epicUser.EpicAccountId;
                            userPlatformBinding.BindingStatus = true;
                            userPlatformBinding.BindingTime = DateTime.UtcNow; // 更新绑定时间
                            userPlatformBinding.LastSyncTime = DateTime.UtcNow; // 更新同步时间
                            userPlatformBinding.ExpireTime = DateTime.UtcNow.AddYears(1); // 更新过期时间
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("更新UserPlatformBinding记录成功");
                        }

                        // 认证成功后，自动导入游戏和成就数据（异步执行，不阻塞响应）
                        // 使用新的 scope 来避免 DbContext 被释放的问题
                        var epicAccountIdForImport = epicUser.EpicAccountId;
                        var userIdForImport = userId;
                        _ = Task.Run(async () =>
                        {
                            // 创建新的 scope 来获取新的 DbContext 和服务实例
                            using var scope = _serviceScopeFactory.CreateScope();
                            var scopedContext = scope.ServiceProvider.GetRequiredService<PlayLinkerDbContext>();
                            var scopedEpicService = scope.ServiceProvider.GetRequiredService<IEpicService>();
                            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<EpicController>>();
                            
                            try
                            {
                                scopedLogger.LogInformation("开始自动导入Epic Games游戏和成就数据...");
                                
                                var importRequest = new EpicImportRequestDto
                                {
                                    EpicAccountId = epicAccountIdForImport,
                                    UserId = userIdForImport,
                                    ImportGames = true,
                                    ImportAchievements = true
                                };

                                await ImportEpicDataInternalAsync(importRequest, scopedContext, scopedEpicService, scopedLogger);
                                scopedLogger.LogInformation("Epic Games游戏和成就数据自动导入完成");
                            }
                            catch (Exception ex)
                            {
                                scopedLogger.LogError(ex, "自动导入Epic Games数据时发生错误");
                            }
                        });
                    }
                    else
                    {
                        _logger.LogWarning("获取用户信息失败，但认证已成功");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建绑定记录时发生错误，但认证已成功");
                    // 即使创建绑定失败，认证仍然成功
                }
            }
            else
            {
                _logger.LogWarning("认证成功但EpicAccountId为空");
            }

            return Ok(ApiResponse<EpicAuthResponseDto>.SuccessResponse(result, "Epic Games认证成功，正在后台导入游戏和成就数据..."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Epic Games认证时发生错误");
            return StatusCode(500, ApiResponse<EpicAuthResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 内部方法：导入Epic Games数据（供认证成功后异步调用，使用传入的scope服务）
    /// </summary>
    private async Task<EpicImportResponseDto> ImportEpicDataInternalAsync(
        EpicImportRequestDto request, 
        PlayLinkerDbContext context, 
        IEpicService epicService, 
        ILogger<EpicController> logger)
    {
        await InitializePlatformsAsync(context);
        
        var userId = (int)request.UserId;
        logger.LogInformation("开始导入Epic Games数据（内部方法）: epicAccountId={EpicAccountId}, userId={UserId}", 
            request.EpicAccountId, userId);

        // 获取用户信息
        var epicUser = await epicService.GetEpicUser(request.EpicAccountId, userId);
        if (epicUser == null)
        {
            logger.LogWarning("无法获取Epic Games用户信息: epicAccountId={EpicAccountId}", request.EpicAccountId);
            return new EpicImportResponseDto
            {
                TaskId = $"epic_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Status = "failed",
                Message = "无法获取Epic Games用户信息，请确保已通过 legendary auth 登录",
                EstimatedTime = 0,
                Items = new EpicImportItemsDto { Games = 0, Achievements = 0 }
            };
        }

        // 创建或更新PlayerPlatform记录
        var playerPlatform = await context.PlayerPlatforms
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
            context.PlayerPlatforms.Add(playerPlatform);
        }
        else
        {
            playerPlatform.ProfileName = epicUser.DisplayName;
        }
        await context.SaveChangesAsync();

        // 创建或更新用户平台绑定记录
        var userPlatformBinding = await context.UserPlatformBindings
            .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == EPIC_PLATFORM_ID);

        if (userPlatformBinding == null)
        {
            userPlatformBinding = new UserPlatformBinding
            {
                UserId = userId, // userId 已经是 int 类型
                PlatformId = EPIC_PLATFORM_ID,
                PlatformUserId = epicUser.EpicAccountId,
                BindingStatus = true,
                BindingTime = DateTime.UtcNow,
                LastSyncTime = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddYears(1)
            };
            context.UserPlatformBindings.Add(userPlatformBinding);
        }
        else
        {
            // 更新绑定时，更新绑定时间和同步时间
            userPlatformBinding.PlatformUserId = epicUser.EpicAccountId;
            userPlatformBinding.BindingStatus = true;
            userPlatformBinding.BindingTime = DateTime.UtcNow; // 更新绑定时间
            userPlatformBinding.LastSyncTime = DateTime.UtcNow; // 更新同步时间
            userPlatformBinding.ExpireTime = DateTime.UtcNow.AddYears(1); // 更新过期时间
        }
        await context.SaveChangesAsync();

        // 导入游戏库数据
        int gamesCount = 0;
        int achievementsCount = 0;
        int failedGamesCount = 0;
        List<string> failedGameNames = new();

        if (request.ImportGames)
        {
            try
            {
                logger.LogInformation("开始导入Epic Games游戏数据...");

                var epicGames = await epicService.GetEpicUserGames(request.EpicAccountId, userId);
                logger.LogInformation("获取到 {Count} 个Epic Games游戏，开始逐个导入...", epicGames.Count);

                int totalGames = epicGames.Count;
                int currentIndex = 0;
                
                foreach (var epicGame in epicGames)
                {
                    currentIndex++;
                    try
                    {
                        logger.LogInformation("正在导入游戏 {CurrentIndex}/{TotalGames}: {GameName}", 
                            currentIndex, totalGames, epicGame.Name);
                        // 获取游戏详细信息和成就
                        EpicGameDto? gameDetails = null;
                        EpicAchievementsInfoDto? achievementsInfo = null;

                        if (!string.IsNullOrEmpty(epicGame.Namespace))
                        {
                            try
                            {
                                // 获取游戏详情
                                gameDetails = await epicService.GetGameDetails(epicGame.Namespace, epicGame.OfferId);
                                
                                // 获取成就信息
                                if (request.ImportAchievements)
                                {
                                    achievementsInfo = await epicService.GetGameAchievements(epicGame.Namespace);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "获取游戏详情或成就失败: {GameName}, namespace={Namespace}", 
                                    epicGame.Name, epicGame.Namespace);
                            }
                        }

                        // 使用详细信息更新游戏数据（如果有）
                        if (gameDetails != null && !string.IsNullOrEmpty(gameDetails.Name))
                        {
                            epicGame.Name = gameDetails.Name;
                            epicGame.ShortDescription = gameDetails.ShortDescription ?? epicGame.ShortDescription;
                            epicGame.HeaderImage = gameDetails.HeaderImage ?? epicGame.HeaderImage;
                            epicGame.Developers = gameDetails.Developers;
                            epicGame.Publishers = gameDetails.Publishers;
                            epicGame.ReleaseDate = gameDetails.ReleaseDate;
                        }

                        // 更新成就信息
                        if (achievementsInfo != null)
                        {
                            epicGame.Achievements = achievementsInfo;
                        }

                        // 先通过游戏名称查找是否已存在同名游戏（不同平台的同名游戏共享同一个game_id）
                        Game? game = await context.Games
                            .FirstOrDefaultAsync(g => g.Name == epicGame.Name);

                        // 如果游戏已存在，只补充缺失的信息，不覆盖已有数据
                        if (game != null && gameDetails != null)
                        {
                            bool hasChanges = false;
                            
                            // 只有当字段为空时才更新，避免覆盖其他平台的数据
                            if (string.IsNullOrEmpty(game.ShortDescription) && !string.IsNullOrEmpty(gameDetails.ShortDescription))
                            {
                                game.ShortDescription = gameDetails.ShortDescription;
                                hasChanges = true;
                            }
                            if (string.IsNullOrEmpty(game.HeaderImage) && !string.IsNullOrEmpty(gameDetails.HeaderImage))
                            {
                                game.HeaderImage = gameDetails.HeaderImage;
                                game.CapsuleImage = gameDetails.HeaderImage;
                                game.Background = gameDetails.HeaderImage;
                                hasChanges = true;
                            }
                            if (game.ReleaseDate == default(DateTime) && !string.IsNullOrEmpty(gameDetails.ReleaseDate) 
                                && DateTime.TryParse(gameDetails.ReleaseDate, out var releaseDate))
                            {
                                game.ReleaseDate = releaseDate;
                                hasChanges = true;
                            }
                            
                            if (hasChanges)
                            {
                                await context.SaveChangesAsync();
                            }

                            // 只添加新的开发商和发行商关联，不删除已有的
                            if (gameDetails.Developers.Count > 0)
                            {
                                foreach (var devName in gameDetails.Developers)
                                {
                                    if (string.IsNullOrEmpty(devName)) continue;
                                    var truncatedName = devName.Length > 20 ? devName.Substring(0, 20) : devName;
                                    var developer = await context.Developers.FirstOrDefaultAsync(d => d.Name == truncatedName);
                                    if (developer == null)
                                    {
                                        developer = new Developer { Name = truncatedName };
                                        context.Developers.Add(developer);
                                        await context.SaveChangesAsync();
                                    }
                                    if (!await context.GameDevelopers.AnyAsync(gd => gd.GameId == game.GameId && gd.DeveloperId == developer.DeveloperId))
                                    {
                                        context.GameDevelopers.Add(new GameDeveloper { GameId = game.GameId, DeveloperId = developer.DeveloperId });
                                    }
                                }
                            }

                            if (gameDetails.Publishers.Count > 0)
                            {
                                foreach (var pubName in gameDetails.Publishers)
                                {
                                    if (string.IsNullOrEmpty(pubName)) continue;
                                    var truncatedName = pubName.Length > 20 ? pubName.Substring(0, 20) : pubName;
                                    var publisher = await context.Publishers.FirstOrDefaultAsync(p => p.Name == truncatedName);
                                    if (publisher == null)
                                    {
                                        publisher = new Publisher { Name = truncatedName };
                                        context.Publishers.Add(publisher);
                                        await context.SaveChangesAsync();
                                    }
                                    if (!await context.GamePublishers.AnyAsync(gp => gp.GameId == game.GameId && gp.PublisherId == publisher.PublisherId))
                                    {
                                        context.GamePublishers.Add(new GamePublisher { GameId = game.GameId, PublisherId = publisher.PublisherId });
                                    }
                                }
                            }
                            await context.SaveChangesAsync();
                        }

                        // 如果游戏不存在，创建新游戏
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
                                    ? releaseDate : default(DateTime),
                                ReviewScore = 0,
                                ReviewScoreDesc = "",
                                NumReviews = 0,
                                TotalPositive = 0
                            };
                            context.Games.Add(game);
                            await context.SaveChangesAsync();

                            // 添加开发商
                            foreach (var devName in epicGame.Developers)
                            {
                                if (string.IsNullOrEmpty(devName)) continue;
                                var truncatedName = devName.Length > 20 ? devName.Substring(0, 20) : devName;
                                var developer = await context.Developers.FirstOrDefaultAsync(d => d.Name == truncatedName);
                                if (developer == null)
                                {
                                    developer = new Developer { Name = truncatedName };
                                    context.Developers.Add(developer);
                                    await context.SaveChangesAsync();
                                }
                                if (!await context.GameDevelopers.AnyAsync(gd => gd.GameId == game.GameId && gd.DeveloperId == developer.DeveloperId))
                                {
                                    context.GameDevelopers.Add(new GameDeveloper { GameId = game.GameId, DeveloperId = developer.DeveloperId });
                                }
                            }

                            // 添加发行商
                            foreach (var pubName in epicGame.Publishers)
                            {
                                if (string.IsNullOrEmpty(pubName)) continue;
                                var truncatedName = pubName.Length > 20 ? pubName.Substring(0, 20) : pubName;
                                var publisher = await context.Publishers.FirstOrDefaultAsync(p => p.Name == truncatedName);
                                if (publisher == null)
                                {
                                    publisher = new Publisher { Name = truncatedName };
                                    context.Publishers.Add(publisher);
                                    await context.SaveChangesAsync();
                                }
                                if (!await context.GamePublishers.AnyAsync(gp => gp.GameId == game.GameId && gp.PublisherId == publisher.PublisherId))
                                {
                                    context.GamePublishers.Add(new GamePublisher { GameId = game.GameId, PublisherId = publisher.PublisherId });
                                }
                            }
                        }

                        // 创建或更新游戏平台映射（如果该平台映射不存在）
                        var gamePlatform = await context.GamePlatforms
                            .FirstOrDefaultAsync(gp => gp.GameId == game.GameId && gp.PlatformId == EPIC_PLATFORM_ID);
                        
                        if (gamePlatform == null)
                        {
                            context.GamePlatforms.Add(new GamePlatform
                            {
                                GameId = game.GameId,
                                PlatformId = EPIC_PLATFORM_ID,
                                PlatformGameId = epicGame.GameId,
                                GamePlatformUrl = $"https://store.epicgames.com/zh-CN/p/{epicGame.Namespace}"
                            });
                            await context.SaveChangesAsync();
                        }
                        else if (gamePlatform.PlatformGameId != epicGame.GameId)
                        {
                            // 更新平台游戏ID（如果不同）
                            gamePlatform.PlatformGameId = epicGame.GameId;
                            gamePlatform.GamePlatformUrl = $"https://store.epicgames.com/zh-CN/p/{epicGame.Namespace}";
                            await context.SaveChangesAsync();
                        }

                        // 创建或更新用户平台游戏库记录
                        var totalAchievements = epicGame.Achievements?.Total ?? 0;
                        var unlockedAchievements = epicGame.Achievements?.UnlockedCount ?? 0;

                        var userGame = await context.UserPlatformLibraries
                            .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.EpicAccountId
                                && upl.PlatformId == EPIC_PLATFORM_ID
                                && upl.GameId == game.GameId);

                        if (userGame == null)
                        {
                            context.UserPlatformLibraries.Add(new UserPlatformLibrary
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

                        await context.SaveChangesAsync();
                        gamesCount++;

                        // 保存成就数据
                        if (request.ImportAchievements && achievementsInfo != null && achievementsInfo.Achievements.Count > 0)
                        {
                            try
                            {
                                foreach (var achievement in achievementsInfo.Achievements)
                                {
                                    // 查找或创建成就
                                    var existingAchievement = await context.Achievements
                                        .FirstOrDefaultAsync(a => a.GameId == game.GameId 
                                            && a.AchievementName == achievement.Id);

                                    if (existingAchievement == null)
                                    {
                                        existingAchievement = new Achievement
                                        {
                                            GameId = game.GameId,
                                            PlatformId = EPIC_PLATFORM_ID,
                                            AchievementName = achievement.Id,
                                            DisplayName = achievement.Name,
                                            Description = achievement.Description,
                                            IconUnlocked = achievement.Icon ?? "",
                                            IconLocked = achievement.Icon ?? "", // Epic Games通常只有一个图标
                                            Hidden = false
                                        };
                                        context.Achievements.Add(existingAchievement);
                                        await context.SaveChangesAsync();
                                    }
                                    else
                                    {
                                        // 更新成就信息
                                        existingAchievement.DisplayName = achievement.Name;
                                        existingAchievement.Description = achievement.Description;
                                        if (!string.IsNullOrEmpty(achievement.Icon))
                                        {
                                            existingAchievement.IconUnlocked = achievement.Icon;
                                            existingAchievement.IconLocked = achievement.Icon;
                                        }
                                    }

                                    // 创建或更新用户成就记录
                                    var userAchievement = await context.UserAchievements
                                        .FirstOrDefaultAsync(ua => ua.UserId == userId
                                            && ua.AchievementId == existingAchievement.AchievementId
                                            && ua.PlatformId == EPIC_PLATFORM_ID);

                                    DateTime? unlockTime = null;
                                    if (!string.IsNullOrEmpty(achievement.UnlockedAt) 
                                        && DateTime.TryParse(achievement.UnlockedAt, out var unlockedDate))
                                    {
                                        unlockTime = unlockedDate;
                                    }

                                    if (userAchievement == null)
                                    {
                                        context.UserAchievements.Add(new UserAchievement
                                        {
                                            UserId = userId,
                                            AchievementId = existingAchievement.AchievementId,
                                            PlatformId = EPIC_PLATFORM_ID,
                                            Unlocked = achievement.IsCompleted,
                                            UnlockTime = unlockTime,
                                            CreatedAt = DateTime.UtcNow
                                        });
                                    }
                                    else
                                    {
                                        userAchievement.Unlocked = achievement.IsCompleted;
                                        if (unlockTime.HasValue)
                                        {
                                            userAchievement.UnlockTime = unlockTime;
                                        }
                                    }
                                }

                                await context.SaveChangesAsync();
                                achievementsCount += achievementsInfo.Achievements.Count;
                                logger.LogInformation("成功保存 {Count} 个成就: {GameName}", 
                                    achievementsInfo.Achievements.Count, epicGame.Name);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "保存成就数据失败: {GameName}", epicGame.Name);
                            }
                        }
                        else if (totalAchievements > 0)
                        {
                            // 如果没有获取到详细成就列表，但游戏有成就总数，只统计总数
                            achievementsCount += totalAchievements;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedGamesCount++;
                        failedGameNames.Add(epicGame.Name ?? "未知游戏");
                        logger.LogError(ex, "导入游戏失败 ({FailedCount}/{TotalGames}): {GameName}", 
                            failedGamesCount, totalGames, epicGame.Name);
                        // 继续处理下一个游戏，不中断整个导入过程
                    }
                }

                logger.LogInformation("Epic Games游戏导入完成: 成功={SuccessCount}, 失败={FailedCount}, 总计={TotalCount}", 
                    gamesCount, failedGamesCount, totalGames);
                
                if (failedGamesCount > 0)
                {
                    logger.LogWarning("以下 {Count} 个游戏导入失败: {FailedGames}", 
                        failedGamesCount, string.Join(", ", failedGameNames.Take(10))); // 只记录前10个失败的游戏名
                }
                
                // 导入完成后，更新LastSyncTime
                var binding = await context.UserPlatformBindings
                    .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == EPIC_PLATFORM_ID);
                if (binding != null)
                {
                    binding.LastSyncTime = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    logger.LogInformation("已更新LastSyncTime: {LastSyncTime}", binding.LastSyncTime);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入游戏库数据时发生严重错误");
                // 即使发生严重错误，也返回已导入的数据
            }
        }

        // 构建响应消息
        string message;
        if (failedGamesCount > 0)
        {
            message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就，{failedGamesCount} 个游戏导入失败";
        }
        else
        {
            message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就";
        }

        var result = new EpicImportResponseDto
        {
            TaskId = $"epic_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            Status = failedGamesCount > 0 && gamesCount == 0 ? "failed" : "completed", // 如果所有游戏都失败，状态为failed
            Message = message,
            EstimatedTime = 0,
            Items = new EpicImportItemsDto
            {
                Games = gamesCount,
                Achievements = achievementsCount
            }
        };

        return result;
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

            var result = await ImportEpicDataInternalAsync(request, _context, _epicService, _logger);
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

