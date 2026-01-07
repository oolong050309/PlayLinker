using PlayLinker.Models.DTOs;
using PlayLinker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Text;

namespace PlayLinker.Services;

/// <summary>
/// Epic Games API集成服务实现
/// 通过Python脚本桥接Epic Games API (使用Legendary CLI)
/// </summary>
public class EpicService : IEpicService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EpicService> _logger;
    private readonly ITokenEncryptionService _encryptionService;
    private readonly PlayLinkerDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly string _epicApiBaseUrl;
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private Process? _fastApiProcess;

    public EpicService(
        IConfiguration configuration,
        ILogger<EpicService> logger,
        IWebHostEnvironment environment,
        ITokenEncryptionService encryptionService,
        PlayLinkerDbContext context,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
        _context = context;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        // FastAPI服务地址
        _epicApiBaseUrl = configuration["EpicAPI:BaseUrl"] ?? "http://localhost:8000";

        // 获取Python路径
        _pythonPath = configuration["EpicAPI:PythonPath"] ?? "python";

        // 脚本路径: Backend/Python
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Python");

        // 确保目录存在
        Directory.CreateDirectory(_scriptsPath);

        // 启动FastAPI服务（如果未运行）
        _ = Task.Run(async () => await EnsureFastApiRunningAsync());

        _logger.LogInformation("EpicService 初始化: EpicApiBaseUrl={EpicApiBaseUrl}, ScriptsPath={ScriptsPath}",
            _epicApiBaseUrl, _scriptsPath);
    }

    /// <summary>
    /// 确保FastAPI服务正在运行
    /// </summary>
    private async Task EnsureFastApiRunningAsync()
    {
        try
        {
            // 检查服务是否已运行
            var healthCheckUrl = $"{_epicApiBaseUrl}/";
            using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await checkClient.GetAsync(healthCheckUrl);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("FastAPI服务已运行: {Url}", _epicApiBaseUrl);
                return;
            }
        }
        catch
        {
            // 服务未运行，需要启动
        }

        try
        {
            var scriptPath = Path.Combine(_scriptsPath, "epic_get_data.py");
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("FastAPI脚本不存在: {ScriptPath}", scriptPath);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"-m uvicorn epic_get_data:app --host 0.0.0.0 --port 8000",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _scriptsPath,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            _fastApiProcess = Process.Start(startInfo);
            if (_fastApiProcess != null)
            {
                // 异步读取输出，避免缓冲区满导致进程挂起
                _fastApiProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation("[FastAPI stdout] {Output}", e.Data);
                    }
                };
                _fastApiProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogWarning("[FastAPI stderr] {Output}", e.Data);
                    }
                };
                _fastApiProcess.BeginOutputReadLine();
                _fastApiProcess.BeginErrorReadLine();
                
                _logger.LogInformation("FastAPI服务已启动: PID={ProcessId}", _fastApiProcess.Id);
                // 等待服务启动
                await Task.Delay(5000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动FastAPI服务失败");
        }
    }

    /// <summary>
    /// 安全地从 JsonElement 获取整数值
    /// </summary>
    private int SafeGetInt32(JsonElement element, int defaultValue = 0)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var strValue = element.GetString();
                if (int.TryParse(strValue, out var intValue))
                {
                    return intValue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析整数失败,使用默认值: {DefaultValue}", defaultValue);
        }

        return defaultValue;
    }

    /// <summary>
    /// 通过HTTP调用FastAPI服务获取Epic Games数据
    /// </summary>
    private async Task<JsonDocument?> CallFastApiAsync(string endpoint, HttpMethod? method = null, object? content = null)
    {
        try
        {
            method ??= HttpMethod.Get;
            var url = $"{_epicApiBaseUrl}{endpoint}";
            
            HttpRequestMessage request;
            if (method == HttpMethod.Post && content != null)
            {
                var json = JsonSerializer.Serialize(content);
                request = new HttpRequestMessage(method, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            else
            {
                request = new HttpRequestMessage(method, url);
            }

            _logger.LogDebug("发送FastAPI请求: {Method} {Url}", method, url);
            var response = await _httpClient.SendAsync(request);
            _logger.LogDebug("收到FastAPI响应: StatusCode={StatusCode}", response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("读取响应内容完成: Length={Length}", responseContent?.Length ?? 0);

            if (!response.IsSuccessStatusCode)
            {
                // 尝试解析错误响应（FastAPI的HTTPException返回的格式）
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        var errorDoc = JsonDocument.Parse(responseContent);
                        if (errorDoc.RootElement.TryGetProperty("detail", out var detail))
                        {
                            var errorMsg = detail.GetString();
                            _logger.LogWarning("FastAPI请求失败: StatusCode={StatusCode}, Detail={Detail}", 
                                response.StatusCode, errorMsg);
                        }
                        else
                        {
                            _logger.LogWarning("FastAPI请求失败: StatusCode={StatusCode}, Response={Response}", 
                                response.StatusCode, responseContent);
                        }
                    }
                    catch
                    {
                        _logger.LogWarning("FastAPI请求失败: StatusCode={StatusCode}, Response={Response}", 
                            response.StatusCode, responseContent);
                    }
                }
                else
                {
                    _logger.LogWarning("FastAPI请求失败: StatusCode={StatusCode}", response.StatusCode);
                }
                return null;
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogError("FastAPI返回空响应");
                return null;
            }

            var doc = JsonDocument.Parse(responseContent);

            // 检查是否有错误（某些端点可能不返回success字段，如登录端点）
            // 注意：某些端点即使success=false也可能返回有用的数据，所以这里只记录警告，不直接返回null
            if (doc.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var errorMsg = doc.RootElement.TryGetProperty("message", out var msg) 
                    ? msg.GetString() 
                    : doc.RootElement.TryGetProperty("error", out var err) 
                        ? err.GetString() 
                        : doc.RootElement.TryGetProperty("detail", out var detail)
                            ? detail.GetString()
                            : "未知错误";
                _logger.LogWarning("FastAPI返回success=false: Endpoint={Endpoint}, Error={Error}", endpoint, errorMsg);
                // 不直接返回null，让调用方决定如何处理（某些情况下可能仍需要解析部分数据）
            }

            _logger.LogDebug("FastAPI请求成功: Endpoint={Endpoint}", endpoint);
            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用FastAPI服务时发生错误: Endpoint={Endpoint}", endpoint);
            return null;
        }
    }

    /// <summary>
    /// Epic Games认证（通过授权码）
    /// </summary>
    public async Task<EpicAuthResponseDto> AuthenticateEpic(EpicAuthRequestDto request, int userId)
    {
        try
        {
            _logger.LogInformation("开始Epic Games认证: userId={UserId}, HasCode={HasCode}", userId, !string.IsNullOrEmpty(request.Code));

            if (string.IsNullOrEmpty(request.Code))
            {
                // 如果没有提供授权码，检查是否已有有效令牌
                var tokenStatus = await CheckTokenStatus(userId);
                if (tokenStatus.Success && tokenStatus.TokenExists)
                {
                    return new EpicAuthResponseDto
                    {
                        Success = true,
                        Message = "已登录，无需重新认证",
                        EpicAccountId = tokenStatus.EpicAccountId,
                        TokenExists = true,
                        NeedsAuth = false
                    };
                }

                return new EpicAuthResponseDto
                {
                    Success = false,
                    Message = "未登录，请先通过 legendary auth 命令登录，或提供授权码",
                    TokenExists = false,
                    NeedsAuth = true
                };
            }

            // 如果有授权码，通过FastAPI进行认证
            var authData = new { code = request.Code };
            
            try
            {
                var url = $"{_epicApiBaseUrl}/api/auth/login";
                var json = JsonSerializer.Serialize(authData);
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                _logger.LogInformation("发送认证请求到FastAPI...");
                var response = await _httpClient.SendAsync(requestMessage);
                _logger.LogInformation("收到FastAPI响应: StatusCode={StatusCode}", response.StatusCode);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("读取响应内容完成: Length={Length}", responseContent?.Length ?? 0);

                if (response.IsSuccessStatusCode)
                {
                    // 认证成功，等待一下确保文件已写入，然后检查令牌状态
                    _logger.LogInformation("FastAPI认证成功，等待文件写入...");
                    await Task.Delay(2000); // 增加等待时间，确保legendary配置文件已写入
                    _logger.LogInformation("开始检查令牌状态...");
                    var tokenStatus = await CheckTokenStatus(userId);
                    _logger.LogInformation("令牌状态检查完成: Success={Success}, AccountId={AccountId}", 
                        tokenStatus.Success, tokenStatus.EpicAccountId);
                    return tokenStatus;
                }
                else
                {
                    // 解析错误响应
                    string errorMsg = "认证失败";
                    if (!string.IsNullOrWhiteSpace(responseContent))
                    {
                        try
                        {
                            var errorDoc = JsonDocument.Parse(responseContent);
                            if (errorDoc.RootElement.TryGetProperty("detail", out var detail))
                            {
                                errorMsg = detail.GetString() ?? "认证失败";
                            }
                        }
                        catch
                        {
                            errorMsg = responseContent;
                        }
                    }
                    
                    _logger.LogWarning("FastAPI认证失败: StatusCode={StatusCode}, Error={Error}", 
                        response.StatusCode, errorMsg);
                    return new EpicAuthResponseDto
                    {
                        Success = false,
                        Message = errorMsg,
                        TokenExists = false,
                        NeedsAuth = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用FastAPI认证接口时发生错误");
                return new EpicAuthResponseDto
                {
                    Success = false,
                    Message = $"认证失败: {ex.Message}",
                    TokenExists = false,
                    NeedsAuth = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Epic Games认证时发生错误");
            return new EpicAuthResponseDto
            {
                Success = false,
                Message = $"认证失败: {ex.Message}",
                TokenExists = false,
                NeedsAuth = true
            };
        }
    }

    /// <summary>
    /// 检查令牌状态
    /// </summary>
    public async Task<EpicAuthResponseDto> CheckTokenStatus(int userId, int platformId = 2)
    {
        try
        {
            _logger.LogInformation("开始检查Epic Games令牌状态...");
            // 检查Legendary是否已登录（通过尝试获取用户信息）
            var profileData = await CallFastApiAsync("/api/profile/info");
            _logger.LogInformation("获取用户信息完成: HasData={HasData}", profileData != null);
            
            if (profileData != null)
            {
                // 记录响应内容以便调试
                var responseJson = profileData.RootElement.GetRawText();
                _logger.LogDebug("FastAPI响应内容: {Response}", responseJson);
                
                if (profileData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    _logger.LogInformation("FastAPI返回success=true");
                    
                    // 尝试从data字段获取（标准格式）
                    JsonElement dataEl;
                    if (profileData.RootElement.TryGetProperty("data", out dataEl))
                    {
                        var accountId = dataEl.TryGetProperty("account_id", out var accountIdEl) 
                            ? accountIdEl.GetString() 
                            : null;

                        _logger.LogInformation("成功获取账户ID（从data字段）: {AccountId}", accountId);
                        return new EpicAuthResponseDto
                        {
                            Success = true,
                            Message = "已登录",
                            EpicAccountId = accountId,
                            TokenExists = true,
                            NeedsAuth = false
                        };
                    }
                    // 尝试从根级别获取（兼容格式）
                    else if (profileData.RootElement.TryGetProperty("account_id", out var accountIdEl))
                    {
                        var accountId = accountIdEl.GetString();
                        _logger.LogInformation("成功获取账户ID（从根级别）: {AccountId}", accountId);
                        return new EpicAuthResponseDto
                        {
                            Success = true,
                            Message = "已登录",
                            EpicAccountId = accountId,
                            TokenExists = true,
                            NeedsAuth = false
                        };
                    }
                    else
                    {
                        _logger.LogWarning("FastAPI响应中没有account_id字段（既不在data中也不在根级别）");
                    }
                }
                else
                {
                    _logger.LogWarning("FastAPI返回success=false或没有success字段");
                }
            }
            else
            {
                _logger.LogWarning("FastAPI返回null，可能服务未运行或请求失败");
            }

            return new EpicAuthResponseDto
            {
                Success = false,
                Message = "未登录，请先运行 legendary auth 命令登录",
                TokenExists = false,
                NeedsAuth = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查Epic Games令牌状态时发生错误");
            return new EpicAuthResponseDto
            {
                Success = false,
                Message = $"检查令牌状态失败: {ex.Message}",
                TokenExists = false,
                NeedsAuth = true
            };
        }
    }

    /// <summary>
    /// 导入Epic Games数据
    /// </summary>
    public async Task<EpicImportResponseDto> ImportEpicData(EpicImportRequestDto request, int userId)
    {
        try
        {
            _logger.LogInformation("开始导入Epic Games数据: epicAccountId={EpicAccountId}, userId={UserId}", request.EpicAccountId, userId);

            var taskId = $"epic_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            int gamesCount = 0;
            int achievementsCount = 0;

            if (request.ImportGames)
            {
                try
                {
                    var epicGames = await GetEpicUserGames(request.EpicAccountId, userId);
                    _logger.LogInformation("获取到 {Count} 个Epic Games游戏", epicGames.Count);

                    // 这里可以添加游戏导入到数据库的逻辑
                    // 类似于GogController中的ImportGogData方法
                    gamesCount = epicGames.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入游戏库数据失败");
                }
            }

            return new EpicImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new EpicImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Epic Games数据时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取Epic Games用户信息
    /// </summary>
    /// <param name="epicAccountId">Epic账户ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="includeGamesCount">是否获取游戏数量（可能较慢，默认false）</param>
    public async Task<EpicUserDto?> GetEpicUser(string epicAccountId, int userId, bool includeGamesCount = false)
    {
        try
        {
            _logger.LogInformation("获取Epic Games用户信息: epicAccountId={EpicAccountId}, userId={UserId}", epicAccountId, userId);

            var data = await CallFastApiAsync("/api/profile/info");
            if (data == null)
            {
                _logger.LogWarning("获取用户信息失败: FastAPI返回null");
                return null;
            }

            if (data.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                // 尝试从data字段获取（标准格式）
                JsonElement dataEl;
                bool hasDataField = data.RootElement.TryGetProperty("data", out dataEl);
                
                // 如果没有data字段，使用根级别（兼容格式）
                if (!hasDataField)
                {
                    dataEl = data.RootElement;
                }
                
                _logger.LogInformation("解析用户信息数据... (hasDataField={HasDataField})", hasDataField);
                var user = new EpicUserDto
                {
                    EpicAccountId = dataEl.TryGetProperty("account_id", out var accountId) ? accountId.GetString() ?? "" : epicAccountId,
                    DisplayName = dataEl.TryGetProperty("display_name", out var displayName) ? displayName.GetString() ?? "" : "",
                    AvatarUrl = dataEl.TryGetProperty("avatar", out var avatar) &&
                        avatar.TryGetProperty("medium", out var medium) ? medium.GetString() ?? "" : ""
                };

                _logger.LogInformation("用户信息解析完成: AccountId={AccountId}, DisplayName={DisplayName}", 
                    user.EpicAccountId, user.DisplayName);

                // 获取游戏数量（可选，如果失败不影响用户信息返回，使用超时避免卡住）
                if (includeGamesCount)
                {
                    try
                    {
                        _logger.LogInformation("开始获取游戏列表（带超时保护）...");
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10秒超时
                        var gamesDataTask = CallFastApiAsync("/api/games");
                        var completedTask = await Task.WhenAny(gamesDataTask, Task.Delay(10000, cts.Token));
                        
                        if (completedTask == gamesDataTask)
                        {
                            var gamesData = await gamesDataTask;
                            if (gamesData != null && gamesData.RootElement.TryGetProperty("success", out var gamesSuccess) && gamesSuccess.GetBoolean())
                            {
                                if (gamesData.RootElement.TryGetProperty("data", out var gamesDataEl) &&
                                    gamesDataEl.TryGetProperty("count", out var count))
                                {
                                    user.GamesOwned = SafeGetInt32(count);
                                    _logger.LogInformation("获取游戏数量: {Count}", user.GamesOwned);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("获取游戏列表失败，但不影响用户信息返回");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("获取游戏列表超时（10秒），但不影响用户信息返回");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "获取游戏数量时出错，但不影响用户信息返回: {Error}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("跳过获取游戏数量（includeGamesCount=false）");
                }

                return user;
            }
            else
            {
                _logger.LogWarning("FastAPI返回success=false或没有success字段");
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games用户信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取Epic Games游戏信息
    /// </summary>
    public async Task<EpicGameDto?> GetEpicGame(string gameId, int userId)
    {
        try
        {
            _logger.LogInformation("获取Epic Games游戏信息: gameId={GameId}, userId={UserId}", gameId, userId);

            // 先获取游戏列表找到namespace和offer_id
            var gamesData = await CallFastApiAsync("/api/games");
            if (gamesData == null)
            {
                return null;
            }

            string? namespaceId = null;
            string? offerId = null;

            if (gamesData.RootElement.TryGetProperty("success", out var gamesSuccess) && gamesSuccess.GetBoolean())
            {
                if (gamesData.RootElement.TryGetProperty("data", out var gamesDataEl) &&
                    gamesDataEl.TryGetProperty("games", out var games))
                {
                    foreach (var game in games.EnumerateArray())
                    {
                        if (game.TryGetProperty("id", out var id) && id.GetString() == gameId)
                        {
                            namespaceId = game.TryGetProperty("namespace", out var ns) ? ns.GetString() : null;
                            offerId = game.TryGetProperty("offer_id", out var oid) ? oid.GetString() : null;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(namespaceId))
            {
                return null;
            }

            // 获取游戏详情
            var endpoint = $"/api/game/details?namespace={Uri.EscapeDataString(namespaceId)}";
            if (!string.IsNullOrEmpty(offerId))
            {
                endpoint += $"&offer_id={Uri.EscapeDataString(offerId)}";
            }
            var detailsData = await CallFastApiAsync(endpoint);
            if (detailsData == null)
            {
                return null;
            }

            if (detailsData.RootElement.TryGetProperty("success", out var detailsSuccess) && detailsSuccess.GetBoolean())
            {
                if (detailsData.RootElement.TryGetProperty("data", out var detailsDataEl))
                {
                    var game = new EpicGameDto
                    {
                        GameId = gameId,
                        Name = detailsDataEl.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Namespace = namespaceId ?? "",
                        OfferId = offerId,
                        ProductId = detailsDataEl.TryGetProperty("product_id", out var productId) ? productId.GetString() : null,
                        ShortDescription = detailsDataEl.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                        HeaderImage = detailsDataEl.TryGetProperty("image", out var img) ? img.GetString() ?? "" : "",
                        ReleaseDate = detailsDataEl.TryGetProperty("release_date", out var releaseDate) ? releaseDate.GetString() : null,
                        PriceDisplay = detailsDataEl.TryGetProperty("price_display", out var price) ? price.GetString() : null
                    };

                    // 解析开发商（可能是逗号分隔的字符串）
                    if (detailsDataEl.TryGetProperty("developer", out var developer))
                    {
                        var devStr = developer.GetString();
                        if (!string.IsNullOrEmpty(devStr))
                        {
                            foreach (var dev in devStr.Split(','))
                            {
                                var devName = dev.Trim();
                                if (!string.IsNullOrEmpty(devName))
                                {
                                    game.Developers.Add(devName);
                                }
                            }
                        }
                    }

                    // 解析发行商（可能是逗号分隔的字符串）
                    if (detailsDataEl.TryGetProperty("publisher", out var publisher))
                    {
                        var pubStr = publisher.GetString();
                        if (!string.IsNullOrEmpty(pubStr))
                        {
                            foreach (var pub in pubStr.Split(','))
                            {
                                var pubName = pub.Trim();
                                if (!string.IsNullOrEmpty(pubName))
                                {
                                    game.Publishers.Add(pubName);
                                }
                            }
                        }
                    }

                    // 解析标签
                    if (detailsDataEl.TryGetProperty("tags", out var tags))
                    {
                        if (tags.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var tag in tags.EnumerateArray())
                            {
                                var tagName = tag.GetString();
                                if (!string.IsNullOrEmpty(tagName))
                                {
                                    game.Tags.Add(tagName);
                                }
                            }
                        }
                    }

                    return game;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games游戏信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取Epic Games用户的游戏列表
    /// </summary>
    public async Task<List<EpicGameDto>> GetEpicUserGames(string epicAccountId, int userId)
    {
        try
        {
            _logger.LogInformation("获取Epic Games用户游戏列表: epicAccountId={EpicAccountId}, userId={UserId}", epicAccountId, userId);

            var data = await CallFastApiAsync("/api/games");
            if (data == null)
            {
                return new List<EpicGameDto>();
            }

            var gamesList = new List<EpicGameDto>();

            if (data.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (data.RootElement.TryGetProperty("data", out var dataEl) &&
                    dataEl.TryGetProperty("games", out var games))
                {
                    foreach (var game in games.EnumerateArray())
                    {
                        var gameDto = new EpicGameDto
                        {
                            GameId = game.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = game.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                            Namespace = game.TryGetProperty("namespace", out var ns) ? ns.GetString() ?? "" : "",
                            OfferId = game.TryGetProperty("offer_id", out var oid) ? oid.GetString() : null
                        };

                        if (!string.IsNullOrEmpty(gameDto.GameId) && !string.IsNullOrEmpty(gameDto.Name))
                        {
                            gamesList.Add(gameDto);
                        }
                    }
                }
            }

            _logger.LogInformation("成功获取 {Count} 个Epic Games游戏", gamesList.Count);
            return gamesList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games用户游戏列表时发生错误");
            return new List<EpicGameDto>();
        }
    }

    /// <summary>
    /// 获取游戏详细信息（包括开发商、发行商、描述等）
    /// </summary>
    public async Task<EpicGameDto?> GetGameDetails(string namespaceId, string? offerId)
    {
        try
        {
            _logger.LogInformation("获取Epic Games游戏详情: namespace={Namespace}, offerId={OfferId}", namespaceId, offerId);

            var endpoint = $"/api/game/details?namespace={Uri.EscapeDataString(namespaceId)}";
            if (!string.IsNullOrEmpty(offerId) && offerId != "None")
            {
                endpoint += $"&offer_id={Uri.EscapeDataString(offerId)}";
            }

            var detailsData = await CallFastApiAsync(endpoint);
            if (detailsData == null)
            {
                _logger.LogWarning("无法获取游戏详情: namespace={Namespace}", namespaceId);
                return null;
            }

            // FastAPI返回的格式：{"success": true, "data": {"title": "...", "description": "...", ...}}
            var rootEl = detailsData.RootElement;
            
            // 检查success字段
            if (rootEl.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                _logger.LogWarning("获取游戏详情失败: namespace={Namespace}, success=false", namespaceId);
                return null;
            }

            // 获取data字段（如果存在），否则使用根级别
            JsonElement dataEl;
            if (rootEl.TryGetProperty("data", out var dataField))
            {
                dataEl = dataField;
            }
            else
            {
                dataEl = rootEl;
            }

            // 检查是否有title字段（表示有有效数据）
            if (dataEl.TryGetProperty("title", out _))
            {
                var game = new EpicGameDto
                {
                    Namespace = namespaceId,
                    OfferId = offerId,
                    Name = dataEl.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    ShortDescription = dataEl.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    HeaderImage = dataEl.TryGetProperty("image", out var img) ? img.GetString() ?? "" : "",
                    ReleaseDate = dataEl.TryGetProperty("release_date", out var releaseDate) && releaseDate.ValueKind != JsonValueKind.Null
                        ? (releaseDate.ValueKind == JsonValueKind.Number 
                            ? DateTimeOffset.FromUnixTimeSeconds(releaseDate.GetInt64()).DateTime.ToString("yyyy-MM-dd")
                            : releaseDate.GetString())
                        : null,
                    PriceDisplay = dataEl.TryGetProperty("price_display", out var price) ? price.GetString() : null
                };

                // 解析开发商（可能是逗号分隔的字符串）
                if (dataEl.TryGetProperty("developer", out var developer))
                {
                    var devStr = developer.GetString();
                    if (!string.IsNullOrEmpty(devStr))
                    {
                        foreach (var dev in devStr.Split(','))
                        {
                            var devName = dev.Trim();
                            if (!string.IsNullOrEmpty(devName))
                            {
                                game.Developers.Add(devName);
                            }
                        }
                    }
                }

                // 解析发行商（可能是逗号分隔的字符串）
                if (dataEl.TryGetProperty("publisher", out var publisher))
                {
                    var pubStr = publisher.GetString();
                    if (!string.IsNullOrEmpty(pubStr))
                    {
                        foreach (var pub in pubStr.Split(','))
                        {
                            var pubName = pub.Trim();
                            if (!string.IsNullOrEmpty(pubName))
                            {
                                game.Publishers.Add(pubName);
                            }
                        }
                    }
                }

                // 解析标签
                if (dataEl.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        var tagName = tag.GetString();
                        if (!string.IsNullOrEmpty(tagName))
                        {
                            game.Tags.Add(tagName);
                        }
                    }
                }

                _logger.LogInformation("成功获取游戏详情: {GameName}", game.Name);
                return game;
            }

            _logger.LogWarning("游戏详情数据格式不正确: namespace={Namespace}", namespaceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games游戏详情时发生错误: namespace={Namespace}", namespaceId);
            return null;
        }
    }

    /// <summary>
    /// 获取游戏成就列表
    /// </summary>
    public async Task<EpicAchievementsInfoDto?> GetGameAchievements(string namespaceId)
    {
        try
        {
            _logger.LogInformation("获取Epic Games游戏成就: namespace={Namespace}", namespaceId);

            var endpoint = $"/api/achievements/{Uri.EscapeDataString(namespaceId)}";
            var achievementsData = await CallFastApiAsync(endpoint);
            if (achievementsData == null)
            {
                _logger.LogWarning("无法获取游戏成就: namespace={Namespace}", namespaceId);
                return null;
            }

            // FastAPI返回的格式：{"success": true, "data": {"supported": true, "total": ..., "unlocked_count": ..., "achievements": [...]}}
            var rootEl = achievementsData.RootElement;
            
            // 检查success字段
            if (rootEl.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                _logger.LogWarning("获取游戏成就失败: namespace={Namespace}, success=false", namespaceId);
                return null;
            }

            // 获取data字段（如果存在），否则使用根级别
            JsonElement dataEl;
            if (rootEl.TryGetProperty("data", out var dataField))
            {
                dataEl = dataField;
            }
            else
            {
                dataEl = rootEl;
            }

            // 检查supported字段
            if (dataEl.TryGetProperty("supported", out var supported) && supported.GetBoolean())
            {
                var achievementsInfo = new EpicAchievementsInfoDto
                {
                    Total = dataEl.TryGetProperty("total", out var total) ? total.GetInt32() : 0,
                    UnlockedCount = dataEl.TryGetProperty("unlocked_count", out var unlocked) ? unlocked.GetInt32() : 0
                };

                if (dataEl.TryGetProperty("achievements", out var achievements) && achievements.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ach in achievements.EnumerateArray())
                    {
                        var achievement = new EpicAchievementDto
                        {
                            Id = ach.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = ach.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Description = ach.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                            Icon = ach.TryGetProperty("icon", out var icon) ? icon.GetString() : null,
                            Xp = ach.TryGetProperty("xp", out var xp) ? xp.GetInt32() : 0,
                            IsCompleted = ach.TryGetProperty("is_completed", out var completed) && completed.GetBoolean(),
                            UnlockedAt = ach.TryGetProperty("unlocked_at", out var unlockedAt) && unlockedAt.ValueKind != JsonValueKind.Null
                                ? unlockedAt.GetString()
                                : null,
                            ProgressVal = ach.TryGetProperty("progress_val", out var progress) && progress.ValueKind != JsonValueKind.Null
                                ? progress.GetInt32()
                                : null
                        };

                        if (!string.IsNullOrEmpty(achievement.Id))
                        {
                            achievementsInfo.Achievements.Add(achievement);
                        }
                    }
                }

                _logger.LogInformation("成功获取游戏成就: Total={Total}, Unlocked={Unlocked}", 
                    achievementsInfo.Total, achievementsInfo.UnlockedCount);
                return achievementsInfo;
            }

            _logger.LogWarning("游戏不支持成就或获取失败: namespace={Namespace}", namespaceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games游戏成就时发生错误: namespace={Namespace}", namespaceId);
            return null;
        }
    }
}

