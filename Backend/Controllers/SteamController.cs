using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlayLinker.Controllers;

/// <summary>
/// Steam API集成控制器
/// 提供Steam数据导入、用户信息查询、游戏信息查询等功能
/// </summary>
[ApiController]
[Route("api/v1/steam")]
[Authorize]
public class SteamController : ControllerBase
{
    private readonly ISteamService _steamService;
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<SteamController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenEncryptionService _encryptionService;
    private const int STEAM_PLATFORM_ID = 1; // Steam平台ID

    public SteamController(
        ISteamService steamService, 
        PlayLinkerDbContext context, 
        ILogger<SteamController> logger, 
        IConfiguration configuration, 
        IHttpClientFactory httpClientFactory,
        ITokenEncryptionService encryptionService)
    {
        _steamService = steamService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
    }

    // 获取当前用户ID
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    /// <summary>
    /// 从 Steam Store API 响应中提取 Metacritic 评分信息
    /// </summary>
    /// <param name="appId">Steam AppID</param>
    /// <param name="jsonContent">Steam Store API 的 JSON 响应内容</param>
    /// <returns>元组：(评分, URL)，如果不存在则返回 (0, null)</returns>
    private (int score, string? url) ExtractMetacriticInfo(int appId, string jsonContent)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonContent);
            if (jsonDoc.RootElement.TryGetProperty(appId.ToString(), out var appData))
            {
                if (appData.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (appData.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("metacritic", out var metacritic))
                        {
                            var score = metacritic.TryGetProperty("score", out var scoreProp) 
                                ? scoreProp.GetInt32() 
                                : 0;
                            var url = metacritic.TryGetProperty("url", out var urlProp) 
                                ? urlProp.GetString() 
                                : null;
                            return (score, url);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 Metacritic 信息失败: appId={AppId}", appId);
        }
        return (0, null);
    }

    /// <summary>
    /// 从第三方 API 获取游戏的评论数据
    /// </summary>
    /// <param name="appId">Steam AppID</param>
    /// <returns>元组：(总评论数, 好评数)，如果获取失败则返回 (0, 0)</returns>
    private async Task<(int totalReviews, int totalPositive)> GetGameReviewsFromThirdPartyApiAsync(int appId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"https://games-popularity.com/swagger/api/game/latest/{appId}";
            _logger.LogInformation("调用第三方API获取游戏评论数据: {Url}", url);
            
            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                
                int totalReviews = 0;
                int totalPositive = 0;
                
                // 根据实际 API 响应结构解析数据
                // reviews 对象包含: reviewsAll, reviewsNegative, reviewsPositive
                if (jsonDoc.RootElement.TryGetProperty("reviews", out var reviewsObj))
                {
                    if (reviewsObj.TryGetProperty("reviewsAll", out var reviewsAllProp))
                    {
                        totalReviews = reviewsAllProp.GetInt32();
                    }
                    
                    if (reviewsObj.TryGetProperty("reviewsPositive", out var reviewsPositiveProp))
                    {
                        totalPositive = reviewsPositiveProp.GetInt32();
                    }
                }
                
                _logger.LogInformation("从第三方API获取到评论数据: appId={AppId}, totalReviews={TotalReviews}, totalPositive={TotalPositive}", 
                    appId, totalReviews, totalPositive);
                
                return (totalReviews, totalPositive);
            }
            else
            {
                _logger.LogWarning("第三方API请求失败: appId={AppId}, StatusCode={StatusCode}", appId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从第三方API获取游戏评论数据失败: appId={AppId}", appId);
        }
        
        return (0, 0);
    }

    /// <summary>
    /// 解析 supported_languages 字符串，提取语言名称
    /// 去除 HTML 标签、特殊字符和说明文字
    /// </summary>
    private List<string> ParseSupportedLanguages(string? supportedLanguages)
    {
        var languages = new List<string>();
        if (string.IsNullOrEmpty(supportedLanguages))
        {
            return languages;
        }

        // 解码 HTML 实体（如 \u003C 转为 <）
        var decoded = System.Net.WebUtility.HtmlDecode(supportedLanguages);
        
        // 移除 HTML 标签（如 <strong>、</strong>、<br> 等）
        decoded = System.Text.RegularExpressions.Regex.Replace(decoded, @"<[^>]+>", "");
        
        // 移除特殊字符（如 *）
        decoded = decoded.Replace("*", "");
        
        // 按逗号分割
        var parts = decoded.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // 跳过说明文字（包含"具有"、"支持"等关键词的说明性文字）
            if (string.IsNullOrWhiteSpace(trimmed) || 
                trimmed.Contains("具有") || 
                trimmed.Contains("支持") ||
                trimmed.Contains("音频") ||
                trimmed.Length < 2)
            {
                continue;
            }
            languages.Add(trimmed);
        }
        
        return languages;
    }

    // 从数据库获取Steam API Key
    private async Task<string?> GetSteamApiKeyAsync(int userId)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == STEAM_PLATFORM_ID && b.BindingStatus == true);
            
            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                _logger.LogWarning("用户{UserId}未绑定Steam平台或API Key不存在", userId);
                return null;
            }
            
            return _encryptionService.DecryptToken(binding.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库获取Steam API Key失败");
            return null;
        }
    }

    /// <summary>
    /// 初始化平台数据，确保所有平台都已存在
    /// 根据API文档中的平台列表初始化platforms表
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
            // 检查平台是否已存在
            var exists = await _context.Platforms
                .AnyAsync(p => p.PlatformId == platformInfo.Id || p.PlatformName == platformInfo.Name);

            if (!exists)
            {
                // 使用原始SQL插入，确保platform_id正确设置
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
    /// 导入Steam数据
    /// 将Steam用户数据导入到数据库中
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<SteamImportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SteamImportResponseDto>>> ImportSteamData(
        [FromBody] SteamImportRequestDto request)
    {
        try
        {
            // 验证 userId 是否提供
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
            _logger.LogInformation("导入Steam数据: userId={UserId}, steamId={SteamId}", userId, request.SteamId);

            // 初始化平台数据（确保Steam平台存在）
            await InitializePlatformsAsync();

            // 生成任务ID
            var taskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            int gamesCount = 0;
            int achievementsCount = 0;
            int friendsCount = 0;

            // 1. 获取并存储Steam用户信息
            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(ApiResponse<SteamImportResponseDto>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", "未找到Steam API Key，请先绑定Steam平台"));
            }
            var steamUser = await _steamService.GetSteamUser(request.SteamId, apiKey);
            if (steamUser == null)
            {
                return BadRequest(ApiResponse<SteamImportResponseDto>.ErrorResponse("ERR_STEAM_USER_NOT_FOUND", "Steam用户不存在或资料为私密状态"));
            }

            // 存储或更新PlayerPlatform信息
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.SteamId && pp.PlatformId == STEAM_PLATFORM_ID);

            if (playerPlatform == null)
            {
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = request.SteamId,
                    PlatformId = STEAM_PLATFORM_ID,
                    ProfileName = steamUser.ProfileName,
                    ProfileUrl = steamUser.ProfileUrl,
                    AccountCreated = DateTime.TryParse(steamUser.AccountCreated, out var created) ? created : null,
                    Country = steamUser.Country
                };
                _context.PlayerPlatforms.Add(playerPlatform);
            }
            else
            {
                playerPlatform.ProfileName = steamUser.ProfileName;
                playerPlatform.ProfileUrl = steamUser.ProfileUrl;
                playerPlatform.Country = steamUser.Country;
            }
            await _context.SaveChangesAsync();

            // 1.5. 创建或更新用户平台绑定记录
            var userPlatformBinding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(upb => upb.UserId == userId && upb.PlatformId == STEAM_PLATFORM_ID);

            if (userPlatformBinding == null)
            {
                userPlatformBinding = new UserPlatformBinding
                {
                    UserId = (int)userId,
                    PlatformId = STEAM_PLATFORM_ID,
                    PlatformUserId = request.SteamId,
                    BindingStatus = true,
                    BindingTime = DateTime.UtcNow,
                    LastSyncTime = DateTime.UtcNow,
                    ExpireTime = DateTime.UtcNow.AddYears(1) // 默认1年过期，可根据实际需求调整
                };
                _context.UserPlatformBindings.Add(userPlatformBinding);
            }
            else
            {
                // 更新绑定时，更新绑定时间和同步时间
                userPlatformBinding.PlatformUserId = request.SteamId;
                userPlatformBinding.BindingStatus = true;
                userPlatformBinding.BindingTime = DateTime.UtcNow; // 更新绑定时间
                userPlatformBinding.LastSyncTime = DateTime.UtcNow; // 更新同步时间
                userPlatformBinding.ExpireTime = DateTime.UtcNow.AddYears(1); // 更新过期时间
            }
            await _context.SaveChangesAsync();

            // 2. 导入游戏库数据
            if (request.ImportGames)
            {
                try
                {
                    // 使用已获取的apiKey
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogWarning("Steam API Key 未找到，跳过游戏导入");
                        gamesCount = 0;
                    }
                    else
                    {
                        var gamesUrl = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={request.SteamId}&include_appinfo=true&include_played_free_games=true";
                        var httpClient = _httpClientFactory.CreateClient();
                        var gamesResponse = await httpClient.GetAsync(gamesUrl);

                        if (gamesResponse.IsSuccessStatusCode)
                        {
                            var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                            var gamesDoc = JsonDocument.Parse(gamesContent);

                            if (gamesDoc.RootElement.TryGetProperty("response", out var gamesResponseData))
                            {
                                if (gamesResponseData.TryGetProperty("games", out var gamesArray))
                                {
                                    foreach (var gameElement in gamesArray.EnumerateArray())
                                    {
                                        if (!gameElement.TryGetProperty("appid", out var appIdElement)) continue;
                                        var appId = appIdElement.GetInt32();

                                        // 获取游戏详细信息（同时获取原始响应以提取 Metacritic 信息）
                                        var gameUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
                                        var gameResponse = await httpClient.GetAsync(gameUrl);
                                        
                                        var (metacriticScore, metacriticUrl) = (0, (string?)null);
                                        string? gameContent = null;
                                        
                                        if (gameResponse.IsSuccessStatusCode)
                                        {
                                            gameContent = await gameResponse.Content.ReadAsStringAsync();
                                            (metacriticScore, metacriticUrl) = ExtractMetacriticInfo(appId, gameContent);
                                        }

                                        // 使用 SteamService 解析游戏数据
                                        var steamGame = await _steamService.GetSteamGame(appId, apiKey);
                                        if (steamGame == null) continue;

                                        // 从第三方 API 获取评论数据
                                        var (totalReviews, totalPositive) = await GetGameReviewsFromThirdPartyApiAsync(appId);

                                        // 先通过游戏名称查找是否已存在同名游戏（不同平台的同名游戏共享同一个game_id）
                                        Game? game = await _context.Games
                                            .FirstOrDefaultAsync(g => g.Name == steamGame.Name);

                                        // 如果游戏不存在，创建新游戏
                                        if (game == null)
                                        {
                                            // 创建新游戏
                                            game = new Game
                                            {
                                                Name = steamGame.Name,
                                                IsFree = steamGame.IsFree,
                                                RequireAge = (byte?)steamGame.RequiredAge,
                                                ShortDescription = steamGame.ShortDescription,
                                                DetailedDescription = steamGame.DetailedDescription,
                                                HeaderImage = steamGame.HeaderImage,
                                                CapsuleImage = steamGame.HeaderImage, // 使用headerImage作为capsule
                                                Background = steamGame.HeaderImage, // 使用headerImage作为background
                                                Windows = steamGame.Platforms.Windows,
                                                Mac = steamGame.Platforms.Mac,
                                                Linux = steamGame.Platforms.Linux,
                                                ReleaseDate = DateTime.TryParse(steamGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                                ReviewScore = metacriticScore,
                                                ReviewScoreDesc = metacriticUrl ?? "",
                                                NumReviews = totalReviews,
                                                TotalPositive = totalPositive
                                            };
                                            _context.Games.Add(game);
                                            await _context.SaveChangesAsync();

                                            // 添加开发商
                                            foreach (var devName in steamGame.Developers)
                                            {
                                                if (string.IsNullOrEmpty(devName)) continue;
                                                // 截断名称到20个字符（数据库限制）
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
                                            foreach (var pubName in steamGame.Publishers)
                                            {
                                                if (string.IsNullOrEmpty(pubName)) continue;
                                                // 截断名称到20个字符（数据库限制）
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

                                        // 创建或更新游戏平台映射（如果该平台映射不存在）
                                        var gamePlatform = await _context.GamePlatforms
                                            .FirstOrDefaultAsync(gp => gp.GameId == game.GameId && gp.PlatformId == STEAM_PLATFORM_ID);
                                        
                                        if (gamePlatform == null)
                                        {
                                            _context.GamePlatforms.Add(new GamePlatform
                                            {
                                                GameId = game.GameId,
                                                PlatformId = STEAM_PLATFORM_ID,
                                                PlatformGameId = appId.ToString(),
                                                GamePlatformUrl = $"https://store.steampowered.com/app/{appId}"
                                            });
                                            await _context.SaveChangesAsync();
                                        }
                                        else if (gamePlatform.PlatformGameId != appId.ToString())
                                        {
                                            // 更新平台游戏ID（如果不同）
                                            gamePlatform.PlatformGameId = appId.ToString();
                                            gamePlatform.GamePlatformUrl = $"https://store.steampowered.com/app/{appId}";
                                            await _context.SaveChangesAsync();
                                        }

                                        // 处理 categories（如果游戏的 categories 为空，则从 Steam API 获取并存储）
                                        var existingCategories = await _context.GameCategories
                                            .Where(gc => gc.GameId == game.GameId)
                                            .Select(gc => gc.CategoryId)
                                            .ToListAsync();
                                        
                                        if (existingCategories.Count == 0 && steamGame.Categories.Count > 0)
                                        {
                                            var categoriesToAdd = new List<GameCategory>();
                                            
                                            foreach (var categoryName in steamGame.Categories)
                                            {
                                                if (string.IsNullOrEmpty(categoryName)) continue;
                                                
                                                // 截断名称到50个字符（数据库限制）
                                                var truncatedName = categoryName.Length > 50 ? categoryName.Substring(0, 50) : categoryName;
                                                
                                                var category = await _context.Categories
                                                    .FirstOrDefaultAsync(c => c.Name == truncatedName);
                                                
                                                if (category == null)
                                                {
                                                    category = new Category { Name = truncatedName };
                                                    _context.Categories.Add(category);
                                                    await _context.SaveChangesAsync();
                                                }
                                                
                                                // 检查是否已存在（包括已添加到列表中的）
                                                if (!existingCategories.Contains(category.CategoryId) && 
                                                    !categoriesToAdd.Any(gc => gc.CategoryId == category.CategoryId))
                                                {
                                                    categoriesToAdd.Add(new GameCategory 
                                                    { 
                                                        GameId = game.GameId, 
                                                        CategoryId = category.CategoryId 
                                                    });
                                                }
                                            }
                                            
                                            // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                            if (categoriesToAdd.Any())
                                            {
                                                var categoryIdsToAdd = categoriesToAdd.Select(gc => gc.CategoryId).ToList();
                                                var alreadyExists = await _context.GameCategories
                                                    .Where(gc => gc.GameId == game.GameId && categoryIdsToAdd.Contains(gc.CategoryId))
                                                    .Select(gc => gc.CategoryId)
                                                    .ToListAsync();
                                                
                                                var finalCategoriesToAdd = categoriesToAdd
                                                    .Where(gc => !alreadyExists.Contains(gc.CategoryId))
                                                    .ToList();
                                                
                                                if (finalCategoriesToAdd.Any())
                                                {
                                                    _context.GameCategories.AddRange(finalCategoriesToAdd);
                                            await _context.SaveChangesAsync();
                                            _logger.LogInformation("为游戏添加 categories: gameId={GameId}, count={Count}", 
                                                        game.GameId, finalCategoriesToAdd.Count);
                                                }
                                            }
                                        }

                                        // 处理 genres（如果游戏的 genres 为空，则从 Steam API 获取并存储）
                                        var existingGenres = await _context.GameGenres
                                            .Where(gg => gg.GameId == game.GameId)
                                            .Select(gg => gg.GenreId)
                                            .ToListAsync();
                                        
                                        if (existingGenres.Count == 0 && steamGame.Genres.Count > 0)
                                        {
                                            var genresToAdd = new List<GameGenre>();
                                            
                                            foreach (var genreName in steamGame.Genres)
                                            {
                                                if (string.IsNullOrEmpty(genreName)) continue;
                                                
                                                // 截断名称到20个字符（数据库限制）
                                                var truncatedName = genreName.Length > 20 ? genreName.Substring(0, 20) : genreName;
                                                
                                                var genre = await _context.Genres
                                                    .FirstOrDefaultAsync(g => g.Name == truncatedName);
                                                
                                                if (genre == null)
                                                {
                                                    genre = new Genre { Name = truncatedName };
                                                    _context.Genres.Add(genre);
                                                    await _context.SaveChangesAsync();
                                                }
                                                
                                                // 检查是否已存在（包括已添加到列表中的）
                                                if (!existingGenres.Contains(genre.GenreId) && 
                                                    !genresToAdd.Any(gg => gg.GenreId == genre.GenreId))
                                                {
                                                    genresToAdd.Add(new GameGenre 
                                                    { 
                                                        GameId = game.GameId, 
                                                        GenreId = genre.GenreId 
                                                    });
                                                }
                                            }
                                            
                                            // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                            if (genresToAdd.Any())
                                            {
                                                var genreIdsToAdd = genresToAdd.Select(gg => gg.GenreId).ToList();
                                                var alreadyExists = await _context.GameGenres
                                                    .Where(gg => gg.GameId == game.GameId && genreIdsToAdd.Contains(gg.GenreId))
                                                    .Select(gg => gg.GenreId)
                                                    .ToListAsync();
                                                
                                                var finalGenresToAdd = genresToAdd
                                                    .Where(gg => !alreadyExists.Contains(gg.GenreId))
                                                    .ToList();
                                                
                                                if (finalGenresToAdd.Any())
                                                {
                                                    _context.GameGenres.AddRange(finalGenresToAdd);
                                            await _context.SaveChangesAsync();
                                            _logger.LogInformation("为游戏添加 genres: gameId={GameId}, count={Count}", 
                                                        game.GameId, finalGenresToAdd.Count);
                                                }
                                            }
                                        }

                                        // 处理 supported_languages（如果游戏的 languages 为空，则从 Steam API 获取并存储）
                                        var existingLanguages = await _context.GameLanguages
                                            .Where(gl => gl.GameId == game.GameId)
                                            .Select(gl => gl.LanguageId)
                                            .ToListAsync();
                                        
                                        if (existingLanguages.Count == 0 && !string.IsNullOrEmpty(steamGame.SupportedLanguages))
                                        {
                                            var parsedLanguages = ParseSupportedLanguages(steamGame.SupportedLanguages);
                                            var languagesToAdd = new List<GameLanguage>();
                                            
                                            foreach (var languageName in parsedLanguages)
                                            {
                                                if (string.IsNullOrEmpty(languageName)) continue;
                                                
                                                // 截断名称到50个字符（数据库限制）
                                                var truncatedName = languageName.Length > 50 ? languageName.Substring(0, 50) : languageName;
                                                
                                                var language = await _context.Languages
                                                    .FirstOrDefaultAsync(l => l.LanguageName == truncatedName);
                                                
                                                if (language == null)
                                                {
                                                    language = new Language { LanguageName = truncatedName };
                                                    _context.Languages.Add(language);
                                                    await _context.SaveChangesAsync();
                                                }
                                                
                                                // 检查是否已存在（包括已添加到列表中的）
                                                if (!existingLanguages.Contains(language.LanguageId) && 
                                                    !languagesToAdd.Any(gl => gl.LanguageId == language.LanguageId))
                                                {
                                                    languagesToAdd.Add(new GameLanguage 
                                                    { 
                                                        GameId = game.GameId, 
                                                        LanguageId = language.LanguageId 
                                                    });
                                                }
                                            }
                                            
                                            // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                            if (languagesToAdd.Any())
                                            {
                                                var languageIdsToAdd = languagesToAdd.Select(gl => gl.LanguageId).ToList();
                                                var alreadyExists = await _context.GameLanguages
                                                    .Where(gl => gl.GameId == game.GameId && languageIdsToAdd.Contains(gl.LanguageId))
                                                    .Select(gl => gl.LanguageId)
                                                    .ToListAsync();
                                                
                                                var finalLanguagesToAdd = languagesToAdd
                                                    .Where(gl => !alreadyExists.Contains(gl.LanguageId))
                                                    .ToList();
                                                
                                                if (finalLanguagesToAdd.Any())
                                                {
                                                    _context.GameLanguages.AddRange(finalLanguagesToAdd);
                                            await _context.SaveChangesAsync();
                                            _logger.LogInformation("为游戏添加 languages: gameId={GameId}, count={Count}", 
                                                        game.GameId, finalLanguagesToAdd.Count);
                                                }
                                            }
                                        }

                                        // 创建或更新用户平台游戏库记录
                                        var playtimeMinutes = gameElement.TryGetProperty("playtime_forever", out var playtime) ? playtime.GetInt32() : 0;
                                        var lastPlayed = gameElement.TryGetProperty("rtime_last_played", out var rtime) && rtime.GetInt64() > 0
                                            ? DateTimeOffset.FromUnixTimeSeconds(rtime.GetInt64()).DateTime
                                            : (DateTime?)null;

                                        var userGame = await _context.UserPlatformLibraries
                                            .FirstOrDefaultAsync(upl => upl.PlatformUserId == request.SteamId 
                                                && upl.PlatformId == STEAM_PLATFORM_ID 
                                                && upl.GameId == game.GameId);

                                        if (userGame == null)
                                        {
                                            _context.UserPlatformLibraries.Add(new UserPlatformLibrary
                                            {
                                                PlatformUserId = request.SteamId,
                                                PlatformId = STEAM_PLATFORM_ID,
                                                GameId = game.GameId,
                                                PlaytimeMinutes = playtimeMinutes,
                                                LastPlayed = lastPlayed
                                            });
                                        }
                                        else
                                        {
                                            userGame.PlaytimeMinutes = playtimeMinutes;
                                            userGame.LastPlayed = lastPlayed;
                                        }

                                        gamesCount++;
                                    }

                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "导入游戏库数据失败");
                }
            }

            // 3. 导入成就数据
            if (request.ImportAchievements)
            {
                try
                {
                    // 获取用户游戏列表
                    // 通过 user_platform_binding 验证，确保只查询当前用户绑定的平台账号的游戏
                    // 这样可以避免查询到其他用户的游戏，即使他们使用了相同的 SteamId
                    var userGames = await _context.UserPlatformLibraries
                        .Where(upl => upl.PlatformUserId == request.SteamId && 
                                     upl.PlatformId == STEAM_PLATFORM_ID &&
                                     _context.UserPlatformBindings.Any(upb => 
                                         upb.UserId == userId && 
                                         upb.PlatformId == STEAM_PLATFORM_ID && 
                                         upb.PlatformUserId == upl.PlatformUserId && 
                                         upb.BindingStatus == true))
                        .ToListAsync();

                    // 使用已获取的apiKey
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogWarning("Steam API Key 未配置");
                    }
                    else
                    {
                        var httpClient = _httpClientFactory.CreateClient();

                        foreach (var userGame in userGames)
                        {
                            // 获取游戏的Steam AppID
                            var gamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.GameId == userGame.GameId && gp.PlatformId == STEAM_PLATFORM_ID);

                            int appId;
                            if (gamePlatform == null)
                            {
                                // 尝试直接使用 gameId 作为 AppID（如果 gameId 本身就是 Steam AppID）
                                if (!int.TryParse(userGame.GameId.ToString(), out appId))
                                {
                                    _logger.LogWarning("无法获取游戏的 Steam AppID: gameId={GameId}", userGame.GameId);
                                    continue;
                                }
                                _logger.LogInformation("使用 gameId 作为 AppID: gameId={GameId}, appId={AppId}", userGame.GameId, appId);
                            }
                            else
                            {
                                if (!int.TryParse(gamePlatform.PlatformGameId, out appId))
                                {
                                    _logger.LogWarning("无法解析 PlatformGameId: PlatformGameId={PlatformGameId}", gamePlatform.PlatformGameId);
                                    continue;
                                }
                            }

                            // 第一步：获取游戏成就架构信息（完整成就信息）
                            var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={apiKey}&appid={appId}&l=schinese";
                            _logger.LogInformation("调用 Steam API 获取成就架构: {Url}", schemaUrl);
                            var schemaResponse = await httpClient.GetAsync(schemaUrl);

                            Dictionary<string, Achievement> schemaAchievements = new();
                            
                            if (schemaResponse.IsSuccessStatusCode)
                            {
                                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
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
                                                    userGame.GameId, appId);
                                                
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
                                                        .FirstOrDefaultAsync(a => a.GameId == userGame.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = userGame.GameId,
                                                            PlatformId = STEAM_PLATFORM_ID,
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
                                                            userGame.GameId, achievementName);
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
                                                                userGame.GameId, achievementName);
                                                        }
                                                        
                                                        // 更新 IconLocked（如果为空）
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray))
                                                        {
                                                            existingAchievement.IconLocked = iconGray;
                                                            needsUpdate = true;
                                                            _logger.LogInformation("更新成就图标（锁定）: gameId={GameId}, achievementName={AchievementName}", 
                                                                userGame.GameId, achievementName);
                                                        }
                                                        
                                                        if (needsUpdate)
                                                        {
                                                            _logger.LogInformation("更新已有成就的缺失字段: gameId={GameId}, achievementName={AchievementName}", 
                                                                userGame.GameId, achievementName);
                                                        }
                                                    }

                                                    schemaAchievements[achievementName] = existingAchievement;
                                                }

                                                await _context.SaveChangesAsync();
                                                _logger.LogInformation("从 GetSchemaForGame（数组格式）获取到 {Count} 个成就: gameId={GameId}, appId={AppId}", 
                                                    schemaAchievements.Count, userGame.GameId, appId);
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
                                                        .FirstOrDefaultAsync(a => a.GameId == userGame.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = userGame.GameId,
                                                            PlatformId = STEAM_PLATFORM_ID,
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
                                                            userGame.GameId, achievementName);
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
                                                                userGame.GameId, achievementName);
                                                        }
                                                        
                                                        // 更新 IconLocked（如果为空）
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray))
                                                        {
                                                            existingAchievement.IconLocked = iconGray;
                                                            needsUpdate = true;
                                                            _logger.LogInformation("更新成就图标（锁定）: gameId={GameId}, achievementName={AchievementName}", 
                                                                userGame.GameId, achievementName);
                                                        }
                                                        
                                                        if (needsUpdate)
                                                        {
                                                            _logger.LogInformation("更新已有成就的缺失字段: gameId={GameId}, achievementName={AchievementName}", 
                                                                userGame.GameId, achievementName);
                                                        }
                                                    }

                                                    schemaAchievements[achievementName] = existingAchievement;
                                                }

                                                await _context.SaveChangesAsync();
                                                _logger.LogInformation("从 GetSchemaForGame 获取到 {Count} 个成就: gameId={GameId}, appId={AppId}", 
                                                    schemaAchievements.Count, userGame.GameId, appId);
                                            }
                                            else
                                            {
                                                _logger.LogWarning("GetSchemaForGame 返回的 achievements 格式未知: gameId={GameId}, appId={AppId}, ValueKind={ValueKind}", 
                                                    userGame.GameId, appId, achievementsObj.ValueKind);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var errorContent = await schemaResponse.Content.ReadAsStringAsync();
                                _logger.LogWarning("GetSchemaForGame API 调用失败: gameId={GameId}, appId={AppId}, StatusCode={StatusCode}, Content={Content}", 
                                    userGame.GameId, appId, schemaResponse.StatusCode, errorContent);
                            }

                            // 第二步：获取用户成就解锁情况
                            var achievementsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={apiKey}&steamid={request.SteamId}&appid={appId}&l=schinese";
                            _logger.LogInformation("调用 Steam API 获取用户成就解锁情况: {Url}", achievementsUrl);
                            var achievementsResponse = await httpClient.GetAsync(achievementsUrl);

                            if (achievementsResponse.IsSuccessStatusCode)
                            {
                                var achievementsContent = await achievementsResponse.Content.ReadAsStringAsync();
                                var achievementsDoc = JsonDocument.Parse(achievementsContent);

                                if (achievementsDoc.RootElement.TryGetProperty("playerstats", out var playerStats))
                                {
                                    if (playerStats.TryGetProperty("achievements", out var achievementsArray))
                                    {
                                        var achievementsCountInGame = achievementsArray.GetArrayLength();
                                        _logger.LogInformation("找到 {Count} 个用户成就记录: gameId={GameId}, appId={AppId}", 
                                            achievementsCountInGame, userGame.GameId, appId);

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
                                                    userGame.GameId, achievementName);
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
                                                // 更新现有记录
                                                userAchievement.Unlocked = achieved;
                                                userAchievement.UnlockTime = unlockTime;
                                            }
                                            else
                                            {
                                                // 创建新用户成就记录
                                                _context.UserAchievements.Add(new UserAchievement
                                                {
                                                    UserId = (int)userId,
                                                    AchievementId = achievement.AchievementId,
                                                    PlatformId = STEAM_PLATFORM_ID,
                                                    Unlocked = achieved,
                                                    UnlockTime = unlockTime
                                                });
                                                achievementsCount++;
                                            }
                                        }

                                        await _context.SaveChangesAsync();
                                        _logger.LogInformation("用户成就解锁数据更新完成: gameId={GameId}, achievementsCount={Count}", 
                                            userGame.GameId, achievementsCount);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Steam API 返回数据中没有 achievements 数组: gameId={GameId}, appId={AppId}", 
                                            userGame.GameId, appId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Steam API 返回数据中没有 playerstats 对象: gameId={GameId}, appId={AppId}", 
                                        userGame.GameId, appId);
                                }
                            }
                            else
                            {
                                var errorContent = await achievementsResponse.Content.ReadAsStringAsync();
                                _logger.LogWarning("GetPlayerAchievements API 调用失败: gameId={GameId}, appId={AppId}, StatusCode={StatusCode}, Content={Content}", 
                                    userGame.GameId, appId, achievementsResponse.StatusCode, errorContent);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "导入成就数据失败");
                }
            }

            // 4. 更新用户游戏库统计（统计该用户在所有平台的数据，而不仅仅是Steam）
            // 获取该用户绑定的所有平台账号的 platform_user_id 列表
            var userPlatformUserIds = await _context.UserPlatformBindings
                .Where(upb => upb.UserId == (int)userId && upb.BindingStatus == true) // 修改点：1.加上(int)强转  2.加上 == true 显式判断
                .Select(upb => upb.PlatformUserId)
                .ToListAsync();

            if (userPlatformUserIds.Count == 0) // List的Count是属性，这里不需要加括号，保持原样即可
            {
                _logger.LogWarning("用户 {UserId} 没有绑定的平台账号", userId);
            }

            // 统计所有平台的游戏数据（使用 platform_user_id 列表）
            var allPlatformGames = await _context.UserPlatformLibraries
                .Where(upl => userPlatformUserIds.Contains(upl.PlatformUserId))
                .Select(upl => new { upl.GameId, upl.PlaytimeMinutes, upl.LastPlayed })
                .ToListAsync();

            // 去重统计游戏数量（不同平台可能有相同游戏）
            var uniqueGameIds = allPlatformGames.Select(g => g.GameId).Distinct().ToList();
            var totalGamesOwned = allPlatformGames.Count(); // 所有平台的游戏记录总数
            var totalPlaytimeAllPlatforms = allPlatformGames.Sum(g => g.PlaytimeMinutes);

            // 统计所有平台的解锁成就数
            var unlockedAchievementsAllPlatforms = await _context.UserAchievements
                .CountAsync(ua => ua.UserId == userId && ua.Unlocked);

            // 统计所有平台的成就总数
            var totalAchievementsAllPlatforms = await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .Distinct()
                .CountAsync();

            // 计算最近游玩的游戏数量（最近30天）
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentlyPlayedCount = allPlatformGames
                .Where(g => g.LastPlayed.HasValue && g.LastPlayed.Value >= thirtyDaysAgo)
                .Select(g => g.GameId)
                .Distinct()
                .Count();

            // 计算最近30天的游戏时长
            var recentPlaytimeMinutes = allPlatformGames
                .Where(g => g.LastPlayed.HasValue && g.LastPlayed.Value >= thirtyDaysAgo)
                .Sum(g => g.PlaytimeMinutes);

            var userLibrary = await _context.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            if (userLibrary == null)
            {
                userLibrary = new UserGameLibrary
                {
                    UserId = (int)userId,
                    TotalGamesOwned = totalGamesOwned,
                    GamesPlayed = uniqueGameIds.Count(), // 去重后的游戏数量
                    TotalPlaytimeMinutes = totalPlaytimeAllPlatforms,
                    TotalAchievements = totalAchievementsAllPlatforms,
                    UnlockedAchievements = unlockedAchievementsAllPlatforms,
                    RecentlyPlayedCount = recentlyPlayedCount,
                    RecentPlaytimeMinutes = recentPlaytimeMinutes
                };
                _context.UserGameLibraries.Add(userLibrary);
            }
            else
            {
                userLibrary.TotalGamesOwned = totalGamesOwned;
                userLibrary.GamesPlayed = uniqueGameIds.Count(); // 去重后的游戏数量
                userLibrary.TotalPlaytimeMinutes = totalPlaytimeAllPlatforms;
                userLibrary.TotalAchievements = totalAchievementsAllPlatforms;
                userLibrary.UnlockedAchievements = unlockedAchievementsAllPlatforms;
                userLibrary.RecentlyPlayedCount = recentlyPlayedCount;
                userLibrary.RecentPlaytimeMinutes = recentPlaytimeMinutes;
            }
            await _context.SaveChangesAsync();

            var result = new SteamImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                EstimatedTime = 0,
                Items = new SteamImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount,
                    Friends = friendsCount
                }
            };

            return Ok(ApiResponse<SteamImportResponseDto>.SuccessResponse(result, "Steam数据导入成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Steam数据时发生错误");
            return StatusCode(500, ApiResponse<SteamImportResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Steam用户信息
    /// </summary>
    /// <param name="steamId">Steam用户ID</param>
    [HttpGet("user/{steamId}")]
    [ProducesResponseType(typeof(ApiResponse<SteamUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SteamUserDto>>> GetSteamUser(string steamId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Steam用户信息: steamId={SteamId}, userId={UserId}", steamId, userId);

            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", "未找到Steam API Key，请先绑定Steam平台"));
            }
            var result = await _steamService.GetSteamUser(steamId, apiKey);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_USER_NOT_FOUND", "Steam用户不存在或资料为私密状态"));
            }

            return Ok(ApiResponse<SteamUserDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam用户信息时发生错误");
            return StatusCode(500, ApiResponse<SteamUserDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 从第三方API获取热门游戏ID列表
    /// </summary>
    private async Task<HashSet<int>> GetHotGameIdsFromThirdPartyApiAsync()
    {
        var gameIds = new HashSet<int>();
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            // 获取愿望单排名
            var wishlistUrl = "https://games-popularity.com/swagger/api/top-wishlist";
            _logger.LogInformation("调用第三方API获取愿望单排名: {Url}", wishlistUrl);
            var wishlistResponse = await httpClient.GetAsync(wishlistUrl);
            
            if (wishlistResponse.IsSuccessStatusCode)
            {
                var wishlistContent = await wishlistResponse.Content.ReadAsStringAsync();
                var wishlistDoc = JsonDocument.Parse(wishlistContent);
                
                if (wishlistDoc.RootElement.TryGetProperty("data", out var wishlistData))
                {
                    foreach (var item in wishlistData.EnumerateArray())
                    {
                        if (item.TryGetProperty("steamId", out var steamIdProp))
                        {
                            var steamIdStr = steamIdProp.GetString();
                            if (int.TryParse(steamIdStr, out var appId))
                            {
                                gameIds.Add(appId);
                            }
                        }
                    }
                    _logger.LogInformation("从愿望单排名获取到 {Count} 个游戏ID", gameIds.Count);
                }
            }
            else
            {
                _logger.LogWarning("获取愿望单排名失败: StatusCode={StatusCode}", wishlistResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取愿望单排名时发生错误");
        }

        try
        {
            // 获取销量榜排名
            var topSellersUrl = "https://games-popularity.com/swagger/api/top-sellers";
            _logger.LogInformation("调用第三方API获取销量榜排名: {Url}", topSellersUrl);
            var topSellersResponse = await httpClient.GetAsync(topSellersUrl);
            
            if (topSellersResponse.IsSuccessStatusCode)
            {
                var topSellersContent = await topSellersResponse.Content.ReadAsStringAsync();
                var topSellersDoc = JsonDocument.Parse(topSellersContent);
                
                if (topSellersDoc.RootElement.TryGetProperty("data", out var topSellersData))
                {
                    int beforeCount = gameIds.Count;
                    foreach (var item in topSellersData.EnumerateArray())
                    {
                        if (item.TryGetProperty("steamId", out var steamIdProp))
                        {
                            var steamIdStr = steamIdProp.GetString();
                            if (int.TryParse(steamIdStr, out var appId))
                            {
                                gameIds.Add(appId);
                            }
                        }
                    }
                    _logger.LogInformation("从销量榜排名获取到 {Count} 个新游戏ID，总计 {TotalCount} 个游戏ID", 
                        gameIds.Count - beforeCount, gameIds.Count);
                }
            }
            else
            {
                _logger.LogWarning("获取销量榜排名失败: StatusCode={StatusCode}", topSellersResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取销量榜排名时发生错误");
        }

        return gameIds;
    }

    /// <summary>
    /// 导入热门Steam游戏
    /// 从第三方API获取愿望单和销量榜排名，然后导入这些游戏
    /// </summary>
    /// <param name="request">导入请求</param>
    [HttpPost("importHotGames")]
    [ProducesResponseType(typeof(ApiResponse<SteamImportHotGamesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SteamImportHotGamesResponseDto>>> ImportHotGames(
        [FromBody] SteamImportHotGamesRequestDto? request = null)
    {
        try
        {
            _logger.LogInformation("开始导入热门Steam游戏");

            // 初始化平台数据（确保Steam平台存在）
            await InitializePlatformsAsync();

            // 生成任务ID
            var taskId = $"importHotGames_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            int gamesCount = 0;
            int achievementsCount = 0;
            var errors = new List<string>();

            // 获取Steam API Key
            int? userId = null;
            if (request?.UserId.HasValue == true && request.UserId.Value > 0)
            {
                userId = (int)request.UserId.Value;
            }
            else
            {
                userId = GetCurrentUserId();
            }

            var apiKey = await GetSteamApiKeyAsync(userId ?? 0);
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(ApiResponse<SteamImportHotGamesResponseDto>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", 
                    "未找到Steam API Key，请先绑定Steam平台或提供有效的userId"));
            }

            // 从第三方API获取热门游戏ID
            var hotGameIds = await GetHotGameIdsFromThirdPartyApiAsync();
            _logger.LogInformation("获取到 {Count} 个热门游戏ID", hotGameIds.Count);

            if (hotGameIds.Count == 0)
            {
                return BadRequest(ApiResponse<SteamImportHotGamesResponseDto>.ErrorResponse("ERR_NO_GAMES_FOUND", 
                    "未能从第三方API获取到游戏ID"));
            }

            int wishlistCount = 0;
            int topSellersCount = 0;

            // 统计愿望单和销量榜的数量（这里简化处理，实际可以从API响应中区分）
            // 由于两个API返回的数据结构相同，我们无法直接区分，所以这里只统计总数
            var httpClient = _httpClientFactory.CreateClient();
            
            try
            {
                var wishlistUrl = "https://games-popularity.com/swagger/api/top-wishlist";
                var wishlistResponse = await httpClient.GetAsync(wishlistUrl);
                if (wishlistResponse.IsSuccessStatusCode)
                {
                    var wishlistContent = await wishlistResponse.Content.ReadAsStringAsync();
                    var wishlistDoc = JsonDocument.Parse(wishlistContent);
                    if (wishlistDoc.RootElement.TryGetProperty("data", out var wishlistData))
                    {
                        wishlistCount = wishlistData.GetArrayLength();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计愿望单数量失败");
            }

            try
            {
                var topSellersUrl = "https://games-popularity.com/swagger/api/top-sellers";
                var topSellersResponse = await httpClient.GetAsync(topSellersUrl);
                if (topSellersResponse.IsSuccessStatusCode)
                {
                    var topSellersContent = await topSellersResponse.Content.ReadAsStringAsync();
                    var topSellersDoc = JsonDocument.Parse(topSellersContent);
                    if (topSellersDoc.RootElement.TryGetProperty("data", out var topSellersData))
                    {
                        topSellersCount = topSellersData.GetArrayLength();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计销量榜数量失败");
            }

            // 逐个处理游戏：先检查是否存在，不存在则导入游戏数据和成就
            int consecutiveTooManyRequests = 0;
            const int MAX_CONSECUTIVE_TOO_MANY_REQUESTS = 10;
            bool shouldStop = false;

            foreach (var appId in hotGameIds)
            {
                if (shouldStop)
                {
                    _logger.LogWarning("连续 {Count} 个游戏发生 TooManyRequests 错误，停止导入", MAX_CONSECUTIVE_TOO_MANY_REQUESTS);
                    errors.Add($"连续 {MAX_CONSECUTIVE_TOO_MANY_REQUESTS} 个游戏发生 TooManyRequests 错误，已停止导入");
                    break;
                }

                try
                {
                    // 1. 先检查该Steam平台的该游戏是否已存在（通过 Steam AppID 在 game_platform 表中查找）
                    var existingGamePlatform = await _context.GamePlatforms
                        .FirstOrDefaultAsync(gp => gp.PlatformId == STEAM_PLATFORM_ID && gp.PlatformGameId == appId.ToString());

                    if (existingGamePlatform != null)
                    {
                        _logger.LogInformation("该Steam游戏已存在，跳过: appId={AppId}, gameId={GameId}", appId, existingGamePlatform.GameId);
                        continue;
                    }

                    // 2. 如果不存在，则按照【先获取游戏数据，再获取游戏成就】的顺序执行
                    Game? game = null;

                    // 2.1 先获取游戏数据
                    if (request?.ImportGames != false)
                    {
                        try
                        {
                            // 直接调用 HTTP 请求以捕获 TooManyRequests 错误
                            var gameUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
                            var gameResponse = await httpClient.GetAsync(gameUrl);
                            
                            // 检查是否是 TooManyRequests 错误
                            if (gameResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                consecutiveTooManyRequests++;
                                _logger.LogWarning("Steam API请求失败: TooManyRequests, appId={AppId}, 连续失败次数={Count}", appId, consecutiveTooManyRequests);
                                errors.Add($"游戏 {appId}: Steam API请求失败: TooManyRequests");
                                
                                if (consecutiveTooManyRequests >= MAX_CONSECUTIVE_TOO_MANY_REQUESTS)
                                {
                                    shouldStop = true;
                                    continue;
                                }
                                continue;
                            }

                            // 重置连续失败计数
                            consecutiveTooManyRequests = 0;

                            if (!gameResponse.IsSuccessStatusCode)
                            {
                                _logger.LogWarning("无法获取游戏信息: appId={AppId}, StatusCode={StatusCode}", appId, gameResponse.StatusCode);
                                errors.Add($"游戏 {appId}: 无法获取游戏信息 (StatusCode: {gameResponse.StatusCode})");
                                continue;
                            }

                            // 获取 Metacritic 评分信息（从 Steam Store API 原始响应中提取）
                            var gameContent = await gameResponse.Content.ReadAsStringAsync();
                            var (metacriticScore, metacriticUrl) = ExtractMetacriticInfo(appId, gameContent);

                            // 使用 SteamService 解析游戏数据
                            var steamGame = await _steamService.GetSteamGame(appId, apiKey);
                            
                            if (steamGame == null)
                            {
                                _logger.LogWarning("无法解析游戏信息: appId={AppId}", appId);
                                errors.Add($"游戏 {appId}: 无法解析游戏信息");
                                continue;
                            }

                            // 从第三方 API 获取评论数据
                            var (totalReviews, totalPositive) = await GetGameReviewsFromThirdPartyApiAsync(appId);

                            // 先通过游戏名称查找是否已存在同名游戏（不同平台的同名游戏共享同一个game_id）
                            game = await _context.Games
                                .FirstOrDefaultAsync(g => g.Name == steamGame.Name);

                            // 如果游戏不存在，创建新游戏
                            if (game == null)
                            {
                                // 创建新游戏
                                game = new Game
                                {
                                    Name = steamGame.Name,
                                    IsFree = steamGame.IsFree,
                                    RequireAge = (byte?)steamGame.RequiredAge,
                                    ShortDescription = steamGame.ShortDescription,
                                    DetailedDescription = steamGame.DetailedDescription,
                                    HeaderImage = steamGame.HeaderImage,
                                    CapsuleImage = steamGame.HeaderImage,
                                    Background = steamGame.HeaderImage,
                                    Windows = steamGame.Platforms.Windows,
                                    Mac = steamGame.Platforms.Mac,
                                    Linux = steamGame.Platforms.Linux,
                                    ReleaseDate = DateTime.TryParse(steamGame.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.UtcNow,
                                    ReviewScore = metacriticScore,
                                    ReviewScoreDesc = metacriticUrl ?? "",
                                    NumReviews = totalReviews,
                                    TotalPositive = totalPositive
                                };
                                _context.Games.Add(game);
                                await _context.SaveChangesAsync();

                                // 添加开发商
                                foreach (var devName in steamGame.Developers)
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
                                foreach (var pubName in steamGame.Publishers)
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

                            // 创建游戏平台映射（如果该平台映射不存在）
                            var gamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.GameId == game.GameId && gp.PlatformId == STEAM_PLATFORM_ID);
                            
                            if (gamePlatform == null)
                            {
                                _context.GamePlatforms.Add(new GamePlatform
                                {
                                    GameId = game.GameId,
                                    PlatformId = STEAM_PLATFORM_ID,
                                    PlatformGameId = appId.ToString(),
                                    GamePlatformUrl = $"https://store.steampowered.com/app/{appId}"
                                });
                                await _context.SaveChangesAsync();
                            }
                            else if (gamePlatform.PlatformGameId != appId.ToString())
                            {
                                // 更新平台游戏ID（如果不同）
                                gamePlatform.PlatformGameId = appId.ToString();
                                gamePlatform.GamePlatformUrl = $"https://store.steampowered.com/app/{appId}";
                                await _context.SaveChangesAsync();
                            }

                            // 处理 categories、genres、languages
                            var existingCategories = await _context.GameCategories
                                .Where(gc => gc.GameId == game.GameId)
                                .Select(gc => gc.CategoryId)
                                .ToListAsync();
                            
                            if (existingCategories.Count == 0 && steamGame.Categories.Count > 0)
                            {
                                var categoriesToAdd = new List<GameCategory>();
                                
                                foreach (var categoryName in steamGame.Categories)
                                {
                                    if (string.IsNullOrEmpty(categoryName)) continue;
                                    var truncatedName = categoryName.Length > 50 ? categoryName.Substring(0, 50) : categoryName;
                                    var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == truncatedName);
                                    if (category == null)
                                    {
                                        category = new Category { Name = truncatedName };
                                        _context.Categories.Add(category);
                                        await _context.SaveChangesAsync();
                                    }
                                    
                                    // 检查是否已存在（包括已添加到列表中的）
                                    if (!existingCategories.Contains(category.CategoryId) && 
                                        !categoriesToAdd.Any(gc => gc.CategoryId == category.CategoryId))
                                    {
                                        categoriesToAdd.Add(new GameCategory { GameId = game.GameId, CategoryId = category.CategoryId });
                                    }
                                }
                                
                                // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                if (categoriesToAdd.Any())
                                {
                                    var categoryIdsToAdd = categoriesToAdd.Select(gc => gc.CategoryId).ToList();
                                    var alreadyExists = await _context.GameCategories
                                        .Where(gc => gc.GameId == game.GameId && categoryIdsToAdd.Contains(gc.CategoryId))
                                        .Select(gc => gc.CategoryId)
                                        .ToListAsync();
                                    
                                    var finalCategoriesToAdd = categoriesToAdd
                                        .Where(gc => !alreadyExists.Contains(gc.CategoryId))
                                        .ToList();
                                    
                                    if (finalCategoriesToAdd.Any())
                                    {
                                        _context.GameCategories.AddRange(finalCategoriesToAdd);
                                await _context.SaveChangesAsync();
                                    }
                                }
                            }

                            var existingGenres = await _context.GameGenres
                                .Where(gg => gg.GameId == game.GameId)
                                .Select(gg => gg.GenreId)
                                .ToListAsync();
                            
                            if (existingGenres.Count == 0 && steamGame.Genres.Count > 0)
                            {
                                var genresToAdd = new List<GameGenre>();
                                
                                foreach (var genreName in steamGame.Genres)
                                {
                                    if (string.IsNullOrEmpty(genreName)) continue;
                                    var truncatedName = genreName.Length > 20 ? genreName.Substring(0, 20) : genreName;
                                    var genre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == truncatedName);
                                    if (genre == null)
                                    {
                                        genre = new Genre { Name = truncatedName };
                                        _context.Genres.Add(genre);
                                        await _context.SaveChangesAsync();
                                    }
                                    
                                    // 检查是否已存在（包括已添加到列表中的）
                                    if (!existingGenres.Contains(genre.GenreId) && 
                                        !genresToAdd.Any(gg => gg.GenreId == genre.GenreId))
                                    {
                                        genresToAdd.Add(new GameGenre { GameId = game.GameId, GenreId = genre.GenreId });
                                    }
                                }
                                
                                // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                if (genresToAdd.Any())
                                {
                                    var genreIdsToAdd = genresToAdd.Select(gg => gg.GenreId).ToList();
                                    var alreadyExists = await _context.GameGenres
                                        .Where(gg => gg.GameId == game.GameId && genreIdsToAdd.Contains(gg.GenreId))
                                        .Select(gg => gg.GenreId)
                                        .ToListAsync();
                                    
                                    var finalGenresToAdd = genresToAdd
                                        .Where(gg => !alreadyExists.Contains(gg.GenreId))
                                        .ToList();
                                    
                                    if (finalGenresToAdd.Any())
                                    {
                                        _context.GameGenres.AddRange(finalGenresToAdd);
                                await _context.SaveChangesAsync();
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(steamGame.SupportedLanguages))
                            {
                                var existingLanguages = await _context.GameLanguages
                                    .Where(gl => gl.GameId == game.GameId)
                                    .Select(gl => gl.LanguageId)
                                    .ToListAsync();
                                
                                if (existingLanguages.Count == 0)
                                {
                                    var parsedLanguages = ParseSupportedLanguages(steamGame.SupportedLanguages);
                                    var languagesToAdd = new List<GameLanguage>();
                                    
                                    foreach (var languageName in parsedLanguages)
                                    {
                                        if (string.IsNullOrEmpty(languageName)) continue;
                                        var truncatedName = languageName.Length > 50 ? languageName.Substring(0, 50) : languageName;
                                        var language = await _context.Languages.FirstOrDefaultAsync(l => l.LanguageName == truncatedName);
                                        if (language == null)
                                        {
                                            language = new Language { LanguageName = truncatedName };
                                            _context.Languages.Add(language);
                                            await _context.SaveChangesAsync();
                                        }
                                        
                                        // 检查是否已存在（包括已添加到列表中的）
                                        if (!existingLanguages.Contains(language.LanguageId) && 
                                            !languagesToAdd.Any(gl => gl.LanguageId == language.LanguageId))
                                        {
                                            languagesToAdd.Add(new GameLanguage { GameId = game.GameId, LanguageId = language.LanguageId });
                                        }
                                    }
                                    
                                    // 批量添加前再次检查数据库中是否已存在（防止并发问题）
                                    if (languagesToAdd.Any())
                                    {
                                        var languageIdsToAdd = languagesToAdd.Select(gl => gl.LanguageId).ToList();
                                        var alreadyExists = await _context.GameLanguages
                                            .Where(gl => gl.GameId == game.GameId && languageIdsToAdd.Contains(gl.LanguageId))
                                            .Select(gl => gl.LanguageId)
                                            .ToListAsync();
                                        
                                        var finalLanguagesToAdd = languagesToAdd
                                            .Where(gl => !alreadyExists.Contains(gl.LanguageId))
                                            .ToList();
                                        
                                        if (finalLanguagesToAdd.Any())
                                        {
                                            _context.GameLanguages.AddRange(finalLanguagesToAdd);
                                    await _context.SaveChangesAsync();
                                        }
                                    }
                                }
                            }

                            gamesCount++;
                            _logger.LogInformation("成功导入游戏: appId={AppId}, gameId={GameId}, name={Name}", appId, game.GameId, game.Name);
                        }
                        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("429") || httpEx.Message.Contains("TooManyRequests"))
                        {
                            consecutiveTooManyRequests++;
                            _logger.LogWarning("Steam API请求失败: TooManyRequests, appId={AppId}, 连续失败次数={Count}", appId, consecutiveTooManyRequests);
                            errors.Add($"游戏 {appId}: Steam API请求失败: TooManyRequests");
                            
                            if (consecutiveTooManyRequests >= MAX_CONSECUTIVE_TOO_MANY_REQUESTS)
                            {
                                shouldStop = true;
                                continue;
                            }
                            continue;
                        }
                        catch (Exception ex)
                        {
                            consecutiveTooManyRequests = 0; // 重置计数（非 TooManyRequests 错误）
                            _logger.LogError(ex, "导入游戏时发生错误: appId={AppId}", appId);
                            errors.Add($"游戏 {appId}: {ex.Message}");
                            continue;
                        }
                    }

                    // 2.2 再获取游戏成就（如果游戏已创建或已存在）
                    if (request?.ImportAchievements != false && game != null)
                    {
                        try
                        {
                            // 获取游戏平台映射（应该已经存在）
                            var gamePlatform = await _context.GamePlatforms
                                .FirstOrDefaultAsync(gp => gp.GameId == game.GameId && gp.PlatformId == STEAM_PLATFORM_ID);

                            if (gamePlatform == null)
                            {
                                _logger.LogWarning("未找到游戏平台映射: appId={AppId}, gameId={GameId}", appId, game.GameId);
                                continue;
                            }

                            // 获取游戏成就架构信息
                            var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={apiKey}&appid={appId}&l=schinese";
                            var schemaResponse = await httpClient.GetAsync(schemaUrl);

                            // 检查是否是 TooManyRequests 错误
                            if (schemaResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                consecutiveTooManyRequests++;
                                _logger.LogWarning("Steam API请求失败: TooManyRequests, appId={AppId}, 连续失败次数={Count}", appId, consecutiveTooManyRequests);
                                errors.Add($"游戏 {appId} 成就导入失败: Steam API请求失败: TooManyRequests");
                                
                                if (consecutiveTooManyRequests >= MAX_CONSECUTIVE_TOO_MANY_REQUESTS)
                                {
                                    shouldStop = true;
                                    continue;
                                }
                                continue;
                            }

                            // 重置连续失败计数
                            consecutiveTooManyRequests = 0;

                            if (schemaResponse.IsSuccessStatusCode)
                            {
                                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                                var schemaDoc = JsonDocument.Parse(schemaContent);

                                if (schemaDoc.RootElement.TryGetProperty("game", out var gameData))
                                {
                                    if (gameData.TryGetProperty("availableGameStats", out var stats))
                                    {
                                        if (stats.TryGetProperty("achievements", out var achievementsObj))
                                        {
                                            // 处理成就数据
                                            if (achievementsObj.ValueKind == JsonValueKind.Array)
                                            {
                                                foreach (var achElement in achievementsObj.EnumerateArray())
                                                {
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

                                                    var existingAchievement = await _context.Achievements
                                                        .FirstOrDefaultAsync(a => a.GameId == gamePlatform.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = gamePlatform.GameId,
                                                            AchievementName = achievementName,
                                                            DisplayName = displayName,
                                                            Description = description,
                                                            Hidden = hidden,
                                                            IconUnlocked = icon,
                                                            IconLocked = iconGray
                                                        };
                                                        _context.Achievements.Add(existingAchievement);
                                                        achievementsCount++;
                                                    }
                                                    else
                                                    {
                                                        // 更新已有成就的缺失字段
                                                        if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) { existingAchievement.DisplayName = displayName; }
                                                        if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) { existingAchievement.Description = description; }
                                                        if (existingAchievement.Hidden != hidden) { existingAchievement.Hidden = hidden; }
                                                        if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) { existingAchievement.IconUnlocked = icon; }
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) { existingAchievement.IconLocked = iconGray; }
                                                    }
                                                }
                                            }
                                            else if (achievementsObj.ValueKind == JsonValueKind.Object)
                                            {
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

                                                    var existingAchievement = await _context.Achievements
                                                        .FirstOrDefaultAsync(a => a.GameId == gamePlatform.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = gamePlatform.GameId,
                                                            AchievementName = achievementName,
                                                            DisplayName = displayName,
                                                            Description = description,
                                                            Hidden = hidden,
                                                            IconUnlocked = icon,
                                                            IconLocked = iconGray
                                                        };
                                                        _context.Achievements.Add(existingAchievement);
                                                        achievementsCount++;
                                                    }
                                                    else
                                                    {
                                                        // 更新已有成就的缺失字段
                                                        if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) { existingAchievement.DisplayName = displayName; }
                                                        if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) { existingAchievement.Description = description; }
                                                        if (existingAchievement.Hidden != hidden) { existingAchievement.Hidden = hidden; }
                                                        if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) { existingAchievement.IconUnlocked = icon; }
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) { existingAchievement.IconLocked = iconGray; }
                                                    }
                                                }
                                            }

                                            await _context.SaveChangesAsync();
                                            _logger.LogInformation("成功导入游戏成就: appId={AppId}, gameId={GameId}", appId, gamePlatform.GameId);
                                        }
                                    }
                                }
                            }
                        }
                        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("429") || httpEx.Message.Contains("TooManyRequests"))
                        {
                            consecutiveTooManyRequests++;
                            _logger.LogWarning("Steam API请求失败: TooManyRequests, appId={AppId}, 连续失败次数={Count}", appId, consecutiveTooManyRequests);
                            errors.Add($"游戏 {appId} 成就导入失败: Steam API请求失败: TooManyRequests");
                            
                            if (consecutiveTooManyRequests >= MAX_CONSECUTIVE_TOO_MANY_REQUESTS)
                            {
                                shouldStop = true;
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            consecutiveTooManyRequests = 0; // 重置计数（非 TooManyRequests 错误）
                            _logger.LogError(ex, "导入游戏成就时发生错误: appId={AppId}", appId);
                            errors.Add($"游戏 {appId} 成就导入失败: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    consecutiveTooManyRequests = 0; // 重置计数（非 TooManyRequests 错误）
                    _logger.LogError(ex, "处理游戏时发生错误: appId={AppId}", appId);
                    errors.Add($"游戏 {appId}: {ex.Message}");
                }
            }

            var result = new SteamImportHotGamesResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                WishlistGamesCount = wishlistCount,
                TopSellersGamesCount = topSellersCount,
                TotalUniqueGames = hotGameIds.Count,
                Items = new SteamImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount,
                    Friends = 0
                },
                Errors = errors
            };

            return Ok(ApiResponse<SteamImportHotGamesResponseDto>.SuccessResponse(result, "热门游戏导入完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入热门Steam游戏时发生错误");
            return StatusCode(500, ApiResponse<SteamImportHotGamesResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取Steam游戏信息
    /// </summary>
    /// <param name="appId">Steam AppID</param>
    [HttpGet("games/{appId}")]
    [ProducesResponseType(typeof(ApiResponse<SteamGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SteamGameDto>>> GetSteamGame(int appId)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("获取Steam游戏信息: appId={AppId}, userId={UserId}", appId, userId);

            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", "未找到Steam API Key，请先绑定Steam平台"));
            }
            var result = await _steamService.GetSteamGame(appId, apiKey);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_STEAM_GAME_NOT_FOUND", "Steam游戏不存在"));
            }

            return Ok(ApiResponse<SteamGameDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam游戏信息时发生错误");
            return StatusCode(500, ApiResponse<SteamGameDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新所有 Steam 游戏信息
    /// 批量更新 games 表中所有 Steam 平台游戏的信息
    /// </summary>
    /// <param name="request">更新请求</param>
    [HttpPost("updateAll")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UpdateAllSteamGamesResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<UpdateAllSteamGamesResponseDto>>> UpdateAllSteamGames(
        [FromBody] UpdateAllSteamGamesRequestDto? request = null)
    {
        try
        {
            _logger.LogInformation("开始更新所有 Steam 游戏信息: updateAchievement={UpdateAchievement}", 
                request?.UpdateAchievement ?? false);

            // 获取 Steam API Key
            var userId = GetCurrentUserId();
            var apiKey = await GetSteamApiKeyAsync(userId);
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_STEAM_API_KEY_NOT_FOUND", 
                    "未找到Steam API Key，请先绑定Steam平台"));
            }

            // 获取所有 Steam 平台游戏
            var steamGames = await _context.GamePlatforms
                .Where(gp => gp.PlatformId == STEAM_PLATFORM_ID)
                .Include(gp => gp.Game)
                .ToListAsync();

            _logger.LogInformation("找到 {Count} 个 Steam 游戏需要更新", steamGames.Count);

            int updatedGames = 0;
            int failedGames = 0;
            int updatedAchievements = 0;
            var errors = new List<string>();
            var httpClient = _httpClientFactory.CreateClient();

            foreach (var gamePlatform in steamGames)
            {
                try
                {
                    if (gamePlatform.Game == null)
                    {
                        _logger.LogWarning("游戏不存在: gameId={GameId}", gamePlatform.GameId);
                        failedGames++;
                        errors.Add($"游戏 {gamePlatform.GameId}: 游戏记录不存在");
                        continue;
                    }

                    // 获取 Steam AppID
                    if (!int.TryParse(gamePlatform.PlatformGameId, out var appId))
                    {
                        _logger.LogWarning("无法解析 Steam AppID: PlatformGameId={PlatformGameId}", 
                            gamePlatform.PlatformGameId);
                        failedGames++;
                        errors.Add($"游戏 {gamePlatform.GameId}: 无法解析 Steam AppID");
                        continue;
                    }

                    // 获取游戏详细信息
                    var gameUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
                    var gameResponse = await httpClient.GetAsync(gameUrl);
                    
                    if (!gameResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Steam API 请求失败: appId={AppId}, StatusCode={StatusCode}", 
                            appId, gameResponse.StatusCode);
                        failedGames++;
                        errors.Add($"游戏 {gamePlatform.GameId} (AppID: {appId}): Steam API 请求失败");
                        continue;
                    }

                    var gameContent = await gameResponse.Content.ReadAsStringAsync();
                    var (metacriticScore, metacriticUrl) = ExtractMetacriticInfo(appId, gameContent);

                    // 使用 SteamService 解析游戏数据
                    var steamGame = await _steamService.GetSteamGame(appId, apiKey);
                    if (steamGame == null)
                    {
                        _logger.LogWarning("无法从 Steam API 获取游戏信息: appId={AppId}", appId);
                        failedGames++;
                        errors.Add($"游戏 {gamePlatform.GameId} (AppID: {appId}): 无法获取游戏信息");
                        continue;
                    }

                    // 从第三方 API 获取评论数据
                    var (totalReviews, totalPositive) = await GetGameReviewsFromThirdPartyApiAsync(appId);

                    // 更新游戏信息
                    var game = gamePlatform.Game;
                    game.Name = steamGame.Name;
                    game.IsFree = steamGame.IsFree;
                    game.RequireAge = (byte?)steamGame.RequiredAge;
                    game.ShortDescription = steamGame.ShortDescription;
                    game.DetailedDescription = steamGame.DetailedDescription;
                    game.HeaderImage = steamGame.HeaderImage;
                    game.CapsuleImage = steamGame.HeaderImage;
                    game.Background = steamGame.HeaderImage;
                    game.Windows = steamGame.Platforms.Windows;
                    game.Mac = steamGame.Platforms.Mac;
                    game.Linux = steamGame.Platforms.Linux;
                    if (DateTime.TryParse(steamGame.ReleaseDate, out var releaseDate))
                    {
                        game.ReleaseDate = releaseDate;
                    }
                    game.ReviewScore = metacriticScore;
                    game.ReviewScoreDesc = metacriticUrl ?? "";
                    game.NumReviews = totalReviews;
                    game.TotalPositive = totalPositive;

                    await _context.SaveChangesAsync();
                    updatedGames++;
                    _logger.LogInformation("成功更新游戏: gameId={GameId}, name={Name}", game.GameId, game.Name);

                    // 如果需要更新成就
                    if (request?.UpdateAchievement == true)
                    {
                        try
                        {
                            // 获取游戏成就架构信息
                            var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={apiKey}&appid={appId}&l=schinese";
                            var schemaResponse = await httpClient.GetAsync(schemaUrl);

                            if (schemaResponse.IsSuccessStatusCode)
                            {
                                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                                var schemaDoc = JsonDocument.Parse(schemaContent);

                                if (schemaDoc.RootElement.TryGetProperty("game", out var gameData))
                                {
                                    if (gameData.TryGetProperty("availableGameStats", out var stats))
                                    {
                                        if (stats.TryGetProperty("achievements", out var achievementsObj))
                                        {
                                            int achievementsCount = 0;

                                            // 处理成就数据（数组格式）
                                            if (achievementsObj.ValueKind == JsonValueKind.Array)
                                            {
                                                foreach (var achElement in achievementsObj.EnumerateArray())
                                                {
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

                                                    var existingAchievement = await _context.Achievements
                                                        .FirstOrDefaultAsync(a => a.GameId == game.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = game.GameId,
                                                            PlatformId = STEAM_PLATFORM_ID,
                                                            AchievementName = achievementName,
                                                            DisplayName = displayName,
                                                            Description = description,
                                                            Hidden = hidden,
                                                            IconUnlocked = icon,
                                                            IconLocked = iconGray
                                                        };
                                                        _context.Achievements.Add(existingAchievement);
                                                        achievementsCount++;
                                                    }
                                                    else
                                                    {
                                                        // 更新已有成就的缺失字段
                                                        if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) 
                                                            existingAchievement.DisplayName = displayName;
                                                        if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) 
                                                            existingAchievement.Description = description;
                                                        if (existingAchievement.Hidden != hidden) 
                                                            existingAchievement.Hidden = hidden;
                                                        if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) 
                                                            existingAchievement.IconUnlocked = icon;
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) 
                                                            existingAchievement.IconLocked = iconGray;
                                                    }
                                                }
                                            }
                                            // 处理成就数据（对象格式）
                                            else if (achievementsObj.ValueKind == JsonValueKind.Object)
                                            {
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

                                                    var existingAchievement = await _context.Achievements
                                                        .FirstOrDefaultAsync(a => a.GameId == game.GameId && a.AchievementName == achievementName);

                                                    if (existingAchievement == null)
                                                    {
                                                        existingAchievement = new Achievement
                                                        {
                                                            GameId = game.GameId,
                                                            PlatformId = STEAM_PLATFORM_ID,
                                                            AchievementName = achievementName,
                                                            DisplayName = displayName,
                                                            Description = description,
                                                            Hidden = hidden,
                                                            IconUnlocked = icon,
                                                            IconLocked = iconGray
                                                        };
                                                        _context.Achievements.Add(existingAchievement);
                                                        achievementsCount++;
                                                    }
                                                    else
                                                    {
                                                        // 更新已有成就的缺失字段
                                                        if (string.IsNullOrEmpty(existingAchievement.DisplayName) && !string.IsNullOrEmpty(displayName)) 
                                                            existingAchievement.DisplayName = displayName;
                                                        if (string.IsNullOrEmpty(existingAchievement.Description) && !string.IsNullOrEmpty(description)) 
                                                            existingAchievement.Description = description;
                                                        if (existingAchievement.Hidden != hidden) 
                                                            existingAchievement.Hidden = hidden;
                                                        if (string.IsNullOrEmpty(existingAchievement.IconUnlocked) && !string.IsNullOrEmpty(icon)) 
                                                            existingAchievement.IconUnlocked = icon;
                                                        if (string.IsNullOrEmpty(existingAchievement.IconLocked) && !string.IsNullOrEmpty(iconGray)) 
                                                            existingAchievement.IconLocked = iconGray;
                                                    }
                                                }
                                            }

                                            await _context.SaveChangesAsync();
                                            if (achievementsCount > 0)
                                            {
                                                updatedAchievements++;
                                            }
                                            _logger.LogInformation("成功更新游戏成就: gameId={GameId}, achievementsCount={Count}", 
                                                game.GameId, achievementsCount);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "更新游戏成就失败: gameId={GameId}", game.GameId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新游戏失败: gameId={GameId}", gamePlatform.GameId);
                    failedGames++;
                    errors.Add($"游戏 {gamePlatform.GameId}: {ex.Message}");
                }
            }

            var result = new UpdateAllSteamGamesResponseDto
            {
                TotalGames = steamGames.Count,
                UpdatedGames = updatedGames,
                FailedGames = failedGames,
                UpdatedAchievements = updatedAchievements,
                Errors = errors
            };

            return Ok(ApiResponse<UpdateAllSteamGamesResponseDto>.SuccessResponse(result, 
                $"批量更新完成: 成功 {updatedGames} 个，失败 {failedGames} 个"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新所有 Steam 游戏时发生错误");
            return StatusCode(500, ApiResponse<UpdateAllSteamGamesResponseDto>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

