using PlayLinker.Models.DTOs;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// Steam API集成服务实现
/// 实现与Steam Web API的交互功能
/// </summary>
public class SteamService : ISteamService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SteamService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public SteamService(HttpClient httpClient, IConfiguration configuration, ILogger<SteamService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = configuration["SteamAPI:ApiKey"] ?? "YOUR_STEAM_API_KEY_HERE";
        _baseUrl = configuration["SteamAPI:BaseUrl"] ?? "https://api.steampowered.com";
    }

    /// <summary>
    /// 导入Steam数据
    /// </summary>
    public async Task<SteamImportResponseDto> ImportSteamData(SteamImportRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始导入Steam数据: steamId={SteamId}", request.SteamId);

            // 生成任务ID
            var taskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            int gamesCount = 0;
            int achievementsCount = 0;
            int friendsCount = 0;

            // 调用Steam API获取用户游戏库
            if (request.ImportGames)
            {
                try
                {
                    var gamesUrl = $"{_baseUrl}/IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={request.SteamId}&include_appinfo=true&include_played_free_games=true";
                    var gamesResponse = await _httpClient.GetAsync(gamesUrl);
                    
                    if (gamesResponse.IsSuccessStatusCode)
                    {
                        var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                        var gamesDoc = JsonDocument.Parse(gamesContent);
                        
                        if (gamesDoc.RootElement.TryGetProperty("response", out var gamesResponseData))
                        {
                            if (gamesResponseData.TryGetProperty("games", out var gamesArray))
                            {
                                gamesCount = gamesArray.GetArrayLength();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取游戏库数据失败");
                }
            }

            // 获取成就数量（通过获取用户游戏列表并统计）
            if (request.ImportAchievements && gamesCount > 0)
            {
                try
                {
                    // 获取前几个游戏的成就信息来估算总数
                    var gamesUrl = $"{_baseUrl}/IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={request.SteamId}&include_appinfo=false";
                    var gamesResponse = await _httpClient.GetAsync(gamesUrl);
                    
                    if (gamesResponse.IsSuccessStatusCode)
                    {
                        var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                        var gamesDoc = JsonDocument.Parse(gamesContent);
                        
                        if (gamesDoc.RootElement.TryGetProperty("response", out var gamesResponseData))
                        {
                            if (gamesResponseData.TryGetProperty("games", out var gamesArray))
                            {
                                int totalAchievements = 0;
                                int processedGames = 0;
                                const int maxGamesToCheck = 10; // 最多检查10个游戏来估算
                                
                                foreach (var game in gamesArray.EnumerateArray())
                                {
                                    if (processedGames >= maxGamesToCheck) break;
                                    
                                    if (game.TryGetProperty("appid", out var appIdElement))
                                    {
                                        var appId = appIdElement.GetInt32();
                                        try
                                        {
                                            var schemaUrl = $"{_baseUrl}/ISteamUserStats/GetSchemaForGame/v2/?key={_apiKey}&appid={appId}";
                                            var schemaResponse = await _httpClient.GetAsync(schemaUrl);
                                            
                                            if (schemaResponse.IsSuccessStatusCode)
                                            {
                                                var schemaContent = await schemaResponse.Content.ReadAsStringAsync();
                                                var schemaDoc = JsonDocument.Parse(schemaContent);
                                                
                                                if (schemaDoc.RootElement.TryGetProperty("game", out var gameData))
                                                {
                                                    if (gameData.TryGetProperty("availableGameStats", out var stats))
                                                    {
                                                        if (stats.TryGetProperty("achievements", out var achievements))
                                                        {
                                                            totalAchievements += achievements.GetArrayLength();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch { }
                                        processedGames++;
                                    }
                                }
                                
                                // 估算总成就数
                                if (processedGames > 0)
                                {
                                    var avgAchievements = totalAchievements / processedGames;
                                    achievementsCount = avgAchievements * gamesCount;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取成就数据失败");
                }
            }

            // 获取好友数量
            if (request.ImportFriends)
            {
                try
                {
                    var friendsUrl = $"{_baseUrl}/ISteamUser/GetFriendList/v1/?key={_apiKey}&steamid={request.SteamId}&relationship=friend";
                    var friendsResponse = await _httpClient.GetAsync(friendsUrl);
                    
                    if (friendsResponse.IsSuccessStatusCode)
                    {
                        var friendsContent = await friendsResponse.Content.ReadAsStringAsync();
                        var friendsDoc = JsonDocument.Parse(friendsContent);
                        
                        if (friendsDoc.RootElement.TryGetProperty("friendslist", out var friendsList))
                        {
                            if (friendsList.TryGetProperty("friends", out var friendsArray))
                            {
                                friendsCount = friendsArray.GetArrayLength();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取好友列表失败");
                }
            }

            var result = new SteamImportResponseDto
            {
                TaskId = taskId,
                Status = "processing",
                EstimatedTime = (gamesCount / 10) + (achievementsCount / 100) + (friendsCount / 5), // 估算时间
                Items = new SteamImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount,
                    Friends = friendsCount
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Steam数据时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取Steam用户信息
    /// </summary>
    public async Task<SteamUserDto?> GetSteamUser(string steamId)
    {
        try
        {
            _logger.LogInformation("获取Steam用户信息: steamId={SteamId}", steamId);

            // 调用Steam API: ISteamUser/GetPlayerSummaries
            var summariesUrl = $"{_baseUrl}/ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={steamId}";
            var summariesResponse = await _httpClient.GetAsync(summariesUrl);

            if (!summariesResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("获取用户摘要失败: {StatusCode}", summariesResponse.StatusCode);
                return null;
            }

            var summariesContent = await summariesResponse.Content.ReadAsStringAsync();
            var summariesDoc = JsonDocument.Parse(summariesContent);

            if (!summariesDoc.RootElement.TryGetProperty("response", out var response))
            {
                return null;
            }

            if (!response.TryGetProperty("players", out var players) || players.GetArrayLength() == 0)
            {
                _logger.LogWarning("用户不存在或资料为私密状态");
                return null;
            }

            var player = players[0];
            var profileState = player.TryGetProperty("profilestate", out var ps) ? ps.GetInt32() : 0;
            var isPublic = profileState > 0;

            // 获取用户等级
            int level = 0;
            try
            {
                var levelUrl = $"{_baseUrl}/IPlayerService/GetSteamLevel/v1/?key={_apiKey}&steamid={steamId}";
                var levelResponse = await _httpClient.GetAsync(levelUrl);
                
                if (levelResponse.IsSuccessStatusCode)
                {
                    var levelContent = await levelResponse.Content.ReadAsStringAsync();
                    var levelDoc = JsonDocument.Parse(levelContent);
                    
                    if (levelDoc.RootElement.TryGetProperty("response", out var levelResponseData))
                    {
                        if (levelResponseData.TryGetProperty("player_level", out var playerLevel))
                        {
                            level = playerLevel.GetInt32();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取用户等级失败");
            }

            // 获取游戏数量
            int gamesOwned = 0;
            try
            {
                var gamesUrl = $"{_baseUrl}/IPlayerService/GetOwnedGames/v1/?key={_apiKey}&steamid={steamId}&include_appinfo=false";
                var gamesResponse = await _httpClient.GetAsync(gamesUrl);
                
                if (gamesResponse.IsSuccessStatusCode)
                {
                    var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                    var gamesDoc = JsonDocument.Parse(gamesContent);
                    
                    if (gamesDoc.RootElement.TryGetProperty("response", out var gamesResponseData))
                    {
                        if (gamesResponseData.TryGetProperty("games", out var gamesArray))
                        {
                            gamesOwned = gamesArray.GetArrayLength();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取游戏数量失败");
            }

            // 获取徽章数量
            int badges = 0;
            try
            {
                var badgesUrl = $"{_baseUrl}/IPlayerService/GetBadges/v1/?key={_apiKey}&steamid={steamId}";
                var badgesResponse = await _httpClient.GetAsync(badgesUrl);
                
                if (badgesResponse.IsSuccessStatusCode)
                {
                    var badgesContent = await badgesResponse.Content.ReadAsStringAsync();
                    var badgesDoc = JsonDocument.Parse(badgesContent);
                    
                    if (badgesDoc.RootElement.TryGetProperty("response", out var badgesResponseData))
                    {
                        if (badgesResponseData.TryGetProperty("badges", out var badgesArray))
                        {
                            badges = badgesArray.GetArrayLength();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取徽章数量失败");
            }

            var result = new SteamUserDto
            {
                SteamId = steamId,
                ProfileName = player.TryGetProperty("personaname", out var pn) ? pn.GetString() ?? "" : "",
                ProfileUrl = player.TryGetProperty("profileurl", out var pu) ? pu.GetString() ?? "" : $"https://steamcommunity.com/profiles/{steamId}",
                AvatarUrl = player.TryGetProperty("avatarfull", out var af) ? af.GetString() ?? "" : "",
                AccountCreated = player.TryGetProperty("timecreated", out var tc) 
                    ? DateTimeOffset.FromUnixTimeSeconds(tc.GetInt64()).ToString("yyyy-MM-ddTHH:mm:ssZ") 
                    : "",
                Country = player.TryGetProperty("loccountrycode", out var cc) ? cc.GetString() ?? "" : "",
                Level = level,
                GamesOwned = gamesOwned,
                Badges = badges,
                IsPublic = isPublic
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam用户信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取Steam游戏信息
    /// </summary>
    public async Task<SteamGameDto?> GetSteamGame(int appId)
    {
        try
        {
            _logger.LogInformation("获取Steam游戏信息: appId={AppId}", appId);

            // 调用Steam Store API: appdetails
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Steam API请求失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty(appId.ToString(), out var appData))
            {
                if (appData.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (appData.TryGetProperty("data", out var data))
                    {
                        // 解析开发商
                        var developers = new List<string>();
                        if (data.TryGetProperty("developers", out var devsArray))
                        {
                            foreach (var dev in devsArray.EnumerateArray())
                            {
                                developers.Add(dev.GetString() ?? "");
                            }
                        }

                        // 解析发行商
                        var publishers = new List<string>();
                        if (data.TryGetProperty("publishers", out var pubsArray))
                        {
                            foreach (var pub in pubsArray.EnumerateArray())
                            {
                                publishers.Add(pub.GetString() ?? "");
                            }
                        }

                        // 解析分类
                        var categories = new List<string>();
                        if (data.TryGetProperty("categories", out var catsArray))
                        {
                            foreach (var cat in catsArray.EnumerateArray())
                            {
                                if (cat.TryGetProperty("description", out var desc))
                                {
                                    categories.Add(desc.GetString() ?? "");
                                }
                            }
                        }

                        // 解析题材
                        var genres = new List<string>();
                        if (data.TryGetProperty("genres", out var genresArray))
                        {
                            foreach (var genre in genresArray.EnumerateArray())
                            {
                                if (genre.TryGetProperty("description", out var desc))
                                {
                                    genres.Add(desc.GetString() ?? "");
                                }
                            }
                        }

                        // 解析价格信息
                        SteamPriceDto? priceOverview = null;
                        if (data.TryGetProperty("price_overview", out var priceData))
                        {
                            priceOverview = new SteamPriceDto
                            {
                                Currency = priceData.TryGetProperty("currency", out var curr) ? curr.GetString() ?? "CNY" : "CNY",
                                Initial = priceData.TryGetProperty("initial", out var init) ? init.GetInt32() : 0,
                                Final = priceData.TryGetProperty("final", out var final) ? final.GetInt32() : 0,
                                DiscountPercent = priceData.TryGetProperty("discount_percent", out var disc) ? disc.GetInt32() : 0
                            };
                        }

                        // 解析成就信息
                        SteamAchievementsInfoDto? achievements = null;
                        if (data.TryGetProperty("achievements", out var achievementsData))
                        {
                            achievements = new SteamAchievementsInfoDto
                            {
                                Total = achievementsData.TryGetProperty("total", out var total) ? total.GetInt32() : 0
                            };
                        }

                        // 解析推荐数
                        SteamRecommendationsDto? recommendations = null;
                        if (data.TryGetProperty("recommendations", out var recData))
                        {
                            recommendations = new SteamRecommendationsDto
                            {
                                Total = recData.TryGetProperty("total", out var total) ? total.GetInt32() : 0
                            };
                        }

                        var result = new SteamGameDto
                        {
                            AppId = appId,
                            Name = data.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Type = data.TryGetProperty("type", out var type) ? type.GetString() ?? "game" : "game",
                            IsFree = data.TryGetProperty("is_free", out var isFree) ? isFree.GetBoolean() : false,
                            ShortDescription = data.TryGetProperty("short_description", out var sd) ? sd.GetString() : null,
                            DetailedDescription = data.TryGetProperty("detailed_description", out var dd) ? dd.GetString() : null,
                            HeaderImage = data.TryGetProperty("header_image", out var hi) ? hi.GetString() ?? "" : "",
                            Developers = developers,
                            Publishers = publishers,
                            Platforms = new PlatformSupportDto
                            {
                                Windows = data.TryGetProperty("platforms", out var platforms) && platforms.TryGetProperty("windows", out var win) ? win.GetBoolean() : false,
                                Mac = data.TryGetProperty("platforms", out var platforms2) && platforms2.TryGetProperty("mac", out var mac) ? mac.GetBoolean() : false,
                                Linux = data.TryGetProperty("platforms", out var platforms3) && platforms3.TryGetProperty("linux", out var linux) ? linux.GetBoolean() : false
                            },
                            Categories = categories,
                            Genres = genres,
                            ReleaseDate = data.TryGetProperty("release_date", out var rd) && rd.TryGetProperty("date", out var date) ? date.GetString() ?? "" : "",
                            RequiredAge = data.TryGetProperty("required_age", out var ra) ? ra.GetInt32() : 0,
                            PriceOverview = priceOverview,
                            Achievements = achievements,
                            Recommendations = recommendations
                        };

                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("Steam API返回success=false, 游戏可能不存在: appId={AppId}", appId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Steam游戏信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取游戏详情
    /// </summary>
    public async Task<object?> GetGameDetails(int appId)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=schinese&cc=cn";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏详情时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取最受欢迎的游戏
    /// </summary>
    public async Task<object?> GetMostPlayedGames(int count = 50)
    {
        try
        {
            var url = $"{_baseUrl}/ISteamChartsService/GetMostPlayedGames/v1/?key={_apiKey}&count={count}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最受欢迎游戏时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取游戏评价
    /// </summary>
    public async Task<object?> GetGameReviews(int appId)
    {
        try
        {
            var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&language=schinese&filter=recent&day_range=30&review_type=all&purchase_type=all";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏评价时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取游戏成就信息
    /// </summary>
    public async Task<object?> GetGameAchievements(int appId)
    {
        try
        {
            var url = $"{_baseUrl}/ISteamUserStats/GetSchemaForGame/v2/?key={_apiKey}&appid={appId}&l=schinese";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏成就信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取游戏新闻
    /// </summary>
    public async Task<object?> GetGameNews(int appId, int count = 20)
    {
        try
        {
            var url = $"{_baseUrl}/ISteamNews/GetNewsForApp/v2/?key={_apiKey}&appid={appId}&count={count}&maxlength=300";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏新闻时发生错误");
            return null;
        }
    }
}

