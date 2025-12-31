using PlayLinker.Models.DTOs;
using PlayLinker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;
using System.Text.Json;

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
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public EpicService(
        IConfiguration configuration,
        ILogger<EpicService> logger,
        IWebHostEnvironment environment,
        ITokenEncryptionService encryptionService,
        PlayLinkerDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
        _context = context;

        // 获取Python路径
        _pythonPath = configuration["EpicAPI:PythonPath"] ?? "python";

        // 脚本路径: Backend/Python
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Python");

        // 令牌路径: Backend/Tokens
        _tokensPath = Path.Combine(environment.ContentRootPath, "Tokens");

        // 确保目录存在
        Directory.CreateDirectory(_scriptsPath);
        Directory.CreateDirectory(_tokensPath);

        _logger.LogInformation("EpicService 初始化: PythonPath={PythonPath}, ScriptsPath={ScriptsPath}, TokensPath={TokensPath}",
            _pythonPath, _scriptsPath, _tokensPath);
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
    /// 执行Python脚本
    /// </summary>
    private async Task<(int exitCode, string output, string error)> RunPythonScript(string scriptName, string arguments)
    {
        var scriptPath = Path.Combine(_scriptsPath, scriptName);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Python脚本不存在: {scriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{scriptPath}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _scriptsPath,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("无法启动Python进程");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行Python脚本失败: {ScriptName}", scriptName);
            throw;
        }
    }

    /// <summary>
    /// 从Python脚本获取Epic Games数据
    /// </summary>
    private async Task<JsonDocument?> GetEpicDataFromPython(string action, string? namespaceId = null, string? offerId = null, string? gameId = null)
    {
        try
        {
            var arguments = $"--action {action}";
            if (!string.IsNullOrEmpty(namespaceId))
            {
                arguments += $" --namespace \"{namespaceId}\"";
            }
            if (!string.IsNullOrEmpty(offerId))
            {
                arguments += $" --offer-id \"{offerId}\"";
            }
            if (!string.IsNullOrEmpty(gameId))
            {
                arguments += $" --game-id \"{gameId}\"";
            }

            var (exitCode, output, error) = await RunPythonScript("epic_get_data.py", arguments);

            _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}, OutputLength={Length}", exitCode, output.Length);

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Python错误输出: {Error}", error);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogError("Python脚本没有输出任何内容");
                return null;
            }

            // 清理输出：移除可能的调试信息行
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLines = new List<string>();
            bool inJson = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("INFO:") || trimmedLine.StartsWith("WARNING:") || trimmedLine.StartsWith("ERROR:"))
                {
                    continue;
                }
                if (trimmedLine.StartsWith("{"))
                {
                    inJson = true;
                }
                if (inJson)
                {
                    jsonLines.Add(line);
                }
            }

            if (jsonLines.Count == 0)
            {
                _logger.LogError("未找到有效的JSON输出");
                return null;
            }

            var jsonString = string.Join("\n", jsonLines);
            var doc = JsonDocument.Parse(jsonString);

            // 检查是否有错误
            if (doc.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var errorMsg = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : "未知错误";
                _logger.LogError("Python脚本返回错误: {Error}", errorMsg);
                return null;
            }

            _logger.LogInformation("成功解析Epic Games数据JSON");
            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Epic Games数据时发生错误");
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

            // 如果有授权码，调用Python脚本进行认证
            var arguments = $"--action auth --code \"{request.Code}\"";
            var (exitCode, output, error) = await RunPythonScript("epic_get_data.py", arguments);

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // 解析输出
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var jsonLines = new List<string>();
                bool inJson = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("INFO:") || trimmedLine.StartsWith("WARNING:") || trimmedLine.StartsWith("ERROR:"))
                    {
                        continue;
                    }
                    if (trimmedLine.StartsWith("{"))
                    {
                        inJson = true;
                    }
                    if (inJson)
                    {
                        jsonLines.Add(line);
                    }
                }

                if (jsonLines.Count > 0)
                {
                    var jsonString = string.Join("\n", jsonLines);
                    var doc = JsonDocument.Parse(jsonString);

                    if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        // 认证成功，检查令牌状态
                        var tokenStatus = await CheckTokenStatus(userId);
                        return tokenStatus;
                    }
                }
            }

            return new EpicAuthResponseDto
            {
                Success = false,
                Message = $"认证失败: {error ?? "未知错误"}",
                TokenExists = false,
                NeedsAuth = true
            };
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
            // 检查Legendary是否已登录（通过尝试获取游戏列表）
            var data = await GetEpicDataFromPython("games");
            if (data != null && data.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                // 尝试获取用户信息
                var profileData = await GetEpicDataFromPython("profile");
                if (profileData != null && profileData.RootElement.TryGetProperty("success", out var profileSuccess) && profileSuccess.GetBoolean())
                {
                    var accountId = profileData.RootElement.TryGetProperty("data", out var profileDataEl) &&
                        profileDataEl.TryGetProperty("account_id", out var accountIdEl) ? accountIdEl.GetString() : null;

                    return new EpicAuthResponseDto
                    {
                        Success = true,
                        Message = "已登录",
                        EpicAccountId = accountId,
                        TokenExists = true,
                        NeedsAuth = false
                    };
                }
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
    public async Task<EpicUserDto?> GetEpicUser(string epicAccountId, int userId)
    {
        try
        {
            _logger.LogInformation("获取Epic Games用户信息: epicAccountId={EpicAccountId}, userId={UserId}", epicAccountId, userId);

            var data = await GetEpicDataFromPython("profile");
            if (data == null)
            {
                return null;
            }

            if (data.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (data.RootElement.TryGetProperty("data", out var dataEl))
                {
                    var user = new EpicUserDto
                    {
                        EpicAccountId = dataEl.TryGetProperty("account_id", out var accountId) ? accountId.GetString() ?? "" : epicAccountId,
                        DisplayName = dataEl.TryGetProperty("display_name", out var displayName) ? displayName.GetString() ?? "" : "",
                        AvatarUrl = dataEl.TryGetProperty("avatar", out var avatar) &&
                            avatar.TryGetProperty("medium", out var medium) ? medium.GetString() ?? "" : ""
                    };

                    // 获取游戏数量
                    var gamesData = await GetEpicDataFromPython("games");
                    if (gamesData != null && gamesData.RootElement.TryGetProperty("success", out var gamesSuccess) && gamesSuccess.GetBoolean())
                    {
                        if (gamesData.RootElement.TryGetProperty("data", out var gamesDataEl) &&
                            gamesDataEl.TryGetProperty("count", out var count))
                        {
                            user.GamesOwned = SafeGetInt32(count);
                        }
                    }

                    return user;
                }
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
            var gamesData = await GetEpicDataFromPython("games");
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
            var detailsData = await GetEpicDataFromPython("game-details", namespaceId, offerId, null);
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

                    // 解析开发商
                    if (detailsDataEl.TryGetProperty("developers", out var developers) && developers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var dev in developers.EnumerateArray())
                        {
                            var devName = dev.GetString();
                            if (!string.IsNullOrEmpty(devName))
                            {
                                game.Developers.Add(devName);
                            }
                        }
                    }

                    // 解析发行商
                    if (detailsDataEl.TryGetProperty("publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pub in publishers.EnumerateArray())
                        {
                            var pubName = pub.GetString();
                            if (!string.IsNullOrEmpty(pubName))
                            {
                                game.Publishers.Add(pubName);
                            }
                        }
                    }

                    // 解析标签
                    if (detailsDataEl.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
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

            var data = await GetEpicDataFromPython("games");
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
}

