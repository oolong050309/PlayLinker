using PlayLinker.Models.DTOs;
using System.Diagnostics;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// GOG API集成服务实现
/// 通过Python脚本桥接GOG API
/// </summary>
public class GogService : IGogService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GogService> _logger;
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public GogService(IConfiguration configuration, ILogger<GogService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;

        // 获取Python路径(从配置或环境变量)
        _pythonPath = configuration["GogAPI:PythonPath"] ?? "python";
        
        // 脚本路径: Backend/Python
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Python");
        
        // 令牌路径: Backend/Tokens
        _tokensPath = Path.Combine(environment.ContentRootPath, "Tokens");

        // 确保目录存在
        Directory.CreateDirectory(_scriptsPath);
        Directory.CreateDirectory(_tokensPath);

        _logger.LogInformation("GogService 初始化: PythonPath={PythonPath}, ScriptsPath={ScriptsPath}, TokensPath={TokensPath}",
            _pythonPath, _scriptsPath, _tokensPath);
    }

    /// <summary>
    /// 安全地从 JsonElement 获取整数值,支持数字和字符串两种格式
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

        // 设置环境变量,确保Python使用UTF-8编码
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        _logger.LogInformation("执行Python脚本: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                
                // 实时输出重要信息到日志
                if (e.Data.StartsWith("AUTH_URL:"))
                {
                    var authUrl = e.Data.Substring("AUTH_URL:".Length).Trim();
                    _logger.LogWarning("=" + new string('=', 80));
                    _logger.LogWarning("【重要】请在浏览器中打开以下URL完成GOG认证:");
                    _logger.LogWarning(">>> {AuthUrl}", authUrl);
                    _logger.LogWarning("登录完成后,复制浏览器地址栏的完整URL");
                    _logger.LogWarning("=" + new string('=', 80));
                }
                else if (e.Data.StartsWith("INFO:") || e.Data.StartsWith("WARNING:"))
                {
                    _logger.LogInformation("[Python] {Message}", e.Data);
                }
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("[Python Error] {Message}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 等待进程结束(最多5分钟)
        var exited = await Task.Run(() => process.WaitForExit(300000)); // 5分钟超时

        if (!exited)
        {
            try
            {
                process.Kill();
            }
            catch { }
            throw new TimeoutException("Python脚本执行超时(5分钟)");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        
        _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}, OutputLength={OutputLength}, ErrorLength={ErrorLength}", 
            process.ExitCode, output.Length, error.Length);

        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// 获取令牌文件路径
    /// </summary>
    private string GetTokenFilePath(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            return customPath;
        }
        return Path.Combine(_tokensPath, "gog_tokens.json");
    }

    /// <summary>
    /// 测试Python环境
    /// </summary>
    private async Task<(bool success, string message)> TestPythonEnvironment()
    {
        try
        {
            // 测试Python是否可用
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return (false, $"Python执行失败: {error}");
            }

            _logger.LogInformation("Python版本: {Version}", output.Trim());

            // 测试依赖(requests库)
            startInfo.Arguments = "-c \"import requests; print('requests已安装')\"";
            using var depProcess = new Process { StartInfo = startInfo };
            depProcess.Start();
            var depOutput = await depProcess.StandardOutput.ReadToEndAsync();
            var depError = await depProcess.StandardError.ReadToEndAsync();
            await depProcess.WaitForExitAsync();

            if (depProcess.ExitCode != 0)
            {
                return (false, $"requests库未安装。请执行: pip install requests\n详细错误: {depError}");
            }

            _logger.LogInformation("依赖检查: {Result}", depOutput.Trim());
            return (true, "Python环境正常");
        }
        catch (Exception ex)
        {
            return (false, $"测试Python环境失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查令牌状态
    /// </summary>
    public async Task<GogAuthResponseDto> CheckTokenStatus(string? tokensPath = null)
    {
        try
        {
            var tokenPath = GetTokenFilePath(tokensPath);
            var tokenExists = File.Exists(tokenPath);

            if (!tokenExists)
            {
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "令牌文件不存在,需要首次认证",
                    TokenExists = false,
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }

            // 尝试使用令牌获取数据(验证令牌有效性)
            var gogData = await GetGogDataFromPython(tokensPath);
            
            if (gogData != null && gogData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var userId = gogData.RootElement.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
                return new GogAuthResponseDto
                {
                    Success = true,
                    Message = "令牌有效",
                    TokenExists = true,
                    TokensPath = tokenPath,
                    UserId = userId,
                    NeedsBrowserAuth = false
                };
            }
            else
            {
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "令牌已过期或无效,需要重新认证",
                    TokenExists = true,
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return new GogAuthResponseDto
            {
                Success = false,
                Message = $"检查失败: {ex.Message}",
                TokenExists = false,
                NeedsBrowserAuth = true
            };
        }
    }

    /// <summary>
    /// 从重定向URL中提取授权码
    /// </summary>
    private string? ExtractAuthCodeFromUrl(string redirectUrl)
    {
        try
        {
            var uri = new Uri(redirectUrl);
            var queryParams = uri.Query.TrimStart('?').Split('&');
            
            foreach (var param in queryParams)
            {
                var parts = param.Split('=');
                if (parts.Length == 2 && parts[0] == "code")
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析重定向URL失败: {Url}", redirectUrl);
            return null;
        }
    }

    /// <summary>
    /// 执行GOG认证
    /// </summary>
    public async Task<GogAuthResponseDto> AuthenticateGog(GogAuthRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始GOG认证, HasRedirectUrl={HasRedirectUrl}", !string.IsNullOrEmpty(request.RedirectUrl));

            var tokenPath = GetTokenFilePath(request.TokensPath);
            
            // 如果强制重新认证,删除旧令牌
            if (request.ForceReauth && File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
                _logger.LogInformation("已删除旧令牌文件");
            }

            // 如果没有提供重定向URL,先尝试刷新现有令牌或返回认证URL
            if (string.IsNullOrEmpty(request.RedirectUrl))
            {
                // 如果有现有令牌,尝试刷新
                if (File.Exists(tokenPath) && !request.ForceReauth)
                {
                    _logger.LogInformation("尝试刷新现有令牌");
                    var gogData = await GetGogDataFromPython(tokenPath);
                    
                    if (gogData != null && gogData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        var userId = gogData.RootElement.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
                        return new GogAuthResponseDto
                        {
                            Success = true,
                            Message = "令牌刷新成功",
                            TokenExists = true,
                            TokensPath = tokenPath,
                            UserId = userId,
                            NeedsBrowserAuth = false
                        };
                    }
                }
                
                // 没有令牌或刷新失败,返回认证URL
                _logger.LogInformation("生成认证URL");
                var authUrl = "https://auth.gog.com/auth?client_id=46899977096215655&redirect_uri=https://embed.gog.com/on_login_success?origin=client&response_type=code&layout=client2";
                
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "请在浏览器中打开authUrl完成登录，登录成功后，将浏览器地址栏的完整URL复制下来，作为redirectUrl参数再次调用此接口",
                    TokenExists = false,
                    TokensPath = tokenPath,
                    AuthUrl = authUrl,
                    NeedsBrowserAuth = true
                };
            }

            // 提供了重定向URL,从中提取授权码
            _logger.LogInformation("从重定向URL中提取授权码");
            var authCode = ExtractAuthCodeFromUrl(request.RedirectUrl);
            
            if (string.IsNullOrEmpty(authCode))
            {
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "无法从提供的URL中提取授权码，请确保URL格式正确。正确格式示例: https://embed.gog.com/on_login_success?origin=client&code=xxxxx",
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }

            _logger.LogInformation("成功提取授权码，长度: {Length}", authCode.Length);
            
            // 测试Python环境
            _logger.LogInformation("检查Python环境...");
            var (envSuccess, envMessage) = await TestPythonEnvironment();
            if (!envSuccess)
            {
                _logger.LogError("Python环境检查失败: {Message}", envMessage);
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = $"Python环境问题: {envMessage}",
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }
            _logger.LogInformation("Python环境检查通过");
            
            var arguments = $"--tokens \"{tokenPath}\" --auth-code \"{authCode}\"";
            
            int exitCode;
            string output;
            string error;
            
            try
            {
                (exitCode, output, error) = await RunPythonScript("gog_authenticate.py", arguments);

                _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}", exitCode);
                
                if (!string.IsNullOrEmpty(output))
                {
                    _logger.LogInformation("Python输出: {Output}", output);
                }
                
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError("Python错误输出: {Error}", error);
                }

                if (exitCode != 0)
                {
                    var errorMessage = !string.IsNullOrEmpty(error) ? error : "Python脚本执行失败,未返回错误信息";
                    
                    // 检查是否是依赖问题
                    if (error.Contains("ModuleNotFoundError") || error.Contains("ImportError"))
                    {
                        errorMessage = $"Python依赖缺失。请执行: pip install requests\n原始错误: {error}";
                    }
                    else if (error.Contains("python") && error.Contains("not found"))
                    {
                        errorMessage = $"找不到Python。请检查appsettings.json中的PythonPath配置,或确保Python已安装并在PATH中。\n原始错误: {error}";
                    }
                    
                    _logger.LogError("GOG认证失败: {ErrorMessage}", errorMessage);
                    return new GogAuthResponseDto
                    {
                        Success = false,
                        Message = errorMessage,
                        TokenExists = File.Exists(tokenPath),
                        TokensPath = tokenPath,
                        NeedsBrowserAuth = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行Python脚本时发生异常");
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = $"执行认证脚本失败: {ex.Message}",
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }

            // 解析输出
            try
            {
                // 输出可能包含多行,取最后一行JSON
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonLine = lines.LastOrDefault(l => l.Trim().StartsWith("{"));
                
                if (string.IsNullOrEmpty(jsonLine))
                {
                    throw new Exception("未找到JSON输出");
                }

                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonLine);
                
                if (result != null && result.ContainsKey("success") && result["success"].GetBoolean())
                {
                    return new GogAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        UserId = result.ContainsKey("userId") ? result["userId"].GetString() : null,
                        TokensPath = tokenPath,
                        TokenExists = true,
                        NeedsBrowserAuth = false
                    };
                }
                else
                {
                    return new GogAuthResponseDto
                    {
                        Success = false,
                        Message = result?.ContainsKey("message") == true ? result["message"].GetString() ?? "认证失败" : "认证失败",
                        TokenExists = File.Exists(tokenPath),
                        TokensPath = tokenPath,
                        NeedsBrowserAuth = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析认证结果失败: {Output}", output);
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = $"解析认证结果失败: {ex.Message}",
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GOG认证时发生错误");
            return new GogAuthResponseDto
            {
                Success = false,
                Message = $"认证错误: {ex.Message}",
                TokenExists = false,
                NeedsBrowserAuth = true
            };
        }
    }

    /// <summary>
    /// 获取GOG数据
    /// </summary>
    private async Task<JsonDocument?> GetGogDataFromPython(string? tokensPath = null)
    {
        try
        {
            var tokenPath = GetTokenFilePath(tokensPath);
            
            if (!File.Exists(tokenPath))
            {
                _logger.LogWarning("令牌文件不存在: {TokenPath}", tokenPath);
                return null;
            }

            var arguments = $"--tokens \"{tokenPath}\"";
            var (exitCode, output, error) = await RunPythonScript("gog_get_data.py", arguments);

            _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}", exitCode);
            
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Python输出长度: {Length} 字符", output.Length);
                _logger.LogDebug("Python完整输出: {Output}", output);
            }
            else
            {
                _logger.LogWarning("Python输出为空");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Python错误输出: {Error}", error);
            }

            // 解析JSON输出
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError("Python脚本没有输出任何内容,ExitCode={ExitCode}, Error={Error}", exitCode, error);
                    return null;
                }
                
                // 清理输出:移除可能的调试信息行
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var jsonLines = new List<string>();
                bool inJson = false;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // 跳过调试信息行
                    if (trimmedLine.StartsWith("INFO:") || 
                        trimmedLine.StartsWith("WARNING:") || 
                        trimmedLine.StartsWith("ERROR:") || 
                        trimmedLine.StartsWith("AUTH_URL:") ||
                        trimmedLine.StartsWith("DEBUG:"))
                    {
                        continue;
                    }
                    
                    // 检测JSON开始
                    if (trimmedLine.StartsWith("{"))
                    {
                        inJson = true;
                    }
                    
                    // 收集JSON内容
                    if (inJson)
                    {
                        jsonLines.Add(line);
                    }
                }
                
                if (jsonLines.Count == 0)
                {
                    _logger.LogError("未找到有效的JSON输出,ExitCode={ExitCode}", exitCode);
                    _logger.LogDebug("完整输出: {Output}", output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("错误信息: {Error}", error);
                    }
                    return null;
                }
                
                // 重新组合JSON字符串
                var jsonString = string.Join("\n", jsonLines);
                
                _logger.LogDebug("准备解析JSON,长度: {Length} 字符", jsonString.Length);
                
                var doc = JsonDocument.Parse(jsonString);
                
                // 检查是否有错误信息
                if (doc.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
                {
                    var errorMsg = doc.RootElement.TryGetProperty("message", out var msg) 
                        ? msg.GetString() 
                        : "未知错误";
                    var errorType = doc.RootElement.TryGetProperty("error", out var err) 
                        ? err.GetString() 
                        : "unknown";
                    _logger.LogError("Python脚本返回错误: Type={ErrorType}, Message={ErrorMessage}", errorType, errorMsg);
                }
                
                if (exitCode != 0)
                {
                    _logger.LogWarning("Python脚本返回非0退出码({ExitCode}),但成功解析了JSON输出", exitCode);
                }
                
                _logger.LogInformation("成功解析GOG数据JSON");
                return doc;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON解析失败: ExitCode={ExitCode}, OutputLength={OutputLength}, Error={Error}", 
                    exitCode, 
                    output.Length, 
                    error);
                _logger.LogDebug("输出的前1000字符: {Output}", output.Length > 1000 ? output.Substring(0, 1000) : output);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析GOG数据时发生未预期的错误: ExitCode={ExitCode}", exitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG数据时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 导入GOG数据
    /// </summary>
    public async Task<GogImportResponseDto> ImportGogData(GogImportRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始导入GOG数据: gogUserId={GogUserId}", request.GogUserId);

            var taskId = $"gog_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取GOG数据
            _logger.LogInformation("正在调用Python脚本获取GOG数据...");
            var gogData = await GetGogDataFromPython();
            
            if (gogData == null)
            {
                var errorMsg = "获取GOG数据失败:Python脚本返回空数据。请检查:1) 令牌是否有效；2) 网络连接是否正常；3) Python环境是否配置正确。详细信息请查看服务器日志。";
                _logger.LogError(errorMsg);
                return new GogImportResponseDto
                {
                    TaskId = taskId,
                    Status = "failed",
                    Message = errorMsg,
                    EstimatedTime = 0,
                    Items = new GogImportItemsDto()
                };
            }

            // 检查返回的数据是否成功
            if (gogData.RootElement.TryGetProperty("success", out var success))
            {
                if (!success.GetBoolean())
                {
                    var errorMsg = gogData.RootElement.TryGetProperty("message", out var msg) 
                        ? msg.GetString() ?? "未知错误" 
                        : "未知错误";
                    var errorType = gogData.RootElement.TryGetProperty("error", out var err) 
                        ? err.GetString() ?? "unknown" 
                        : "unknown";
                    
                    var fullErrorMsg = $"获取GOG数据失败:{errorMsg} (错误类型: {errorType})";
                    _logger.LogError(fullErrorMsg);
                    
                    return new GogImportResponseDto
                    {
                        TaskId = taskId,
                        Status = "failed",
                        Message = fullErrorMsg,
                        EstimatedTime = 0,
                        Items = new GogImportItemsDto()
                    };
                }
            }

            // 解析数据并统计
            int gamesCount = 0;
            int achievementsCount = 0;

            if (gogData.RootElement.TryGetProperty("games", out var games))
            {
                gamesCount = games.GetArrayLength();
                _logger.LogInformation("找到 {Count} 个游戏", gamesCount);
                
                // 统计成就数量
                foreach (var game in games.EnumerateArray())
                {
                    if (game.TryGetProperty("achievements", out var achievements))
                    {
                        if (achievements.TryGetProperty("items", out var items))
                        {
                            achievementsCount += items.GetArrayLength();
                        }
                        else if (achievements.TryGetProperty("total_count", out var total))
                        {
                            achievementsCount += SafeGetInt32(total);
                        }
                    }
                }
            }
            
            _logger.LogInformation("统计完成: {GamesCount} 个游戏, {AchievementsCount} 个成就", 
                gamesCount, achievementsCount);

            return new GogImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new GogImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入GOG数据时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取GOG用户信息
    /// </summary>
    public async Task<GogUserDto?> GetGogUser(string gogUserId)
    {
        try
        {
            _logger.LogInformation("获取GOG用户信息: gogUserId={GogUserId}", gogUserId);

            var gogData = await GetGogDataFromPython();
            
            if (gogData == null)
            {
                return null;
            }

            // 解析用户资料
            if (gogData.RootElement.TryGetProperty("userData", out var userData))
            {
                var user = new GogUserDto
                {
                    GogUserId = userData.TryGetProperty("galaxyUserId", out var galaxyId) ? galaxyId.GetString() ?? "" : gogUserId,
                    Username = userData.TryGetProperty("username", out var username) ? username.GetString() ?? "" : "",
                    ProfileUrl = $"https://www.gog.com/u/{(userData.TryGetProperty("username", out var un) ? un.GetString() : gogUserId)}",
                    AvatarUrl = userData.TryGetProperty("avatar", out var avatar) ? avatar.GetString() ?? "" : "",
                    Country = userData.TryGetProperty("country", out var country) ? country.GetString() ?? "" : "",
                    GamesOwned = 0,
                    IsPublic = true
                };

                // 计算拥有游戏数量
                if (gogData.RootElement.TryGetProperty("ownedGames", out var ownedGames))
                {
                    if (ownedGames.TryGetProperty("owned", out var owned) && owned.ValueKind == JsonValueKind.Array)
                    {
                        user.GamesOwned = owned.GetArrayLength();
                    }
                }

                return user;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG用户信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取GOG游戏信息
    /// </summary>
    public async Task<GogGameDto?> GetGogGame(string gogGameId)
    {
        try
        {
            _logger.LogInformation("获取GOG游戏信息: gogGameId={GogGameId}", gogGameId);

            var gogData = await GetGogDataFromPython();
            
            if (gogData == null)
            {
                return null;
            }

            // 从games数组中查找游戏
            if (gogData.RootElement.TryGetProperty("games", out var games))
            {
                foreach (var game in games.EnumerateArray())
                {
                    if (game.TryGetProperty("gameId", out var gameId) && gameId.GetString() == gogGameId)
                    {
                        var gameDto = new GogGameDto
                        {
                            GogGameId = gogGameId
                        };

                        // 解析游戏详情
                        if (game.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null)
                        {
                            gameDto.Name = details.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
                            gameDto.HeaderImage = details.TryGetProperty("backgroundImage", out var bgImg) ? bgImg.GetString() ?? "" : "";
                        }

                        // 解析成就信息
                        if (game.TryGetProperty("achievements", out var achievements) && achievements.ValueKind != JsonValueKind.Null)
                        {
                            var totalCount = achievements.TryGetProperty("total_count", out var total) ? SafeGetInt32(total) : 0;
                            gameDto.Achievements = new GogAchievementsInfoDto
                            {
                                Total = totalCount,
                                CurrentAchievements = 0 // GOG API不直接提供已解锁数量
                            };
                        }

                        // 解析游玩时长
                        if (game.TryGetProperty("playTimeMinutes", out var playTime))
                        {
                            gameDto.PlayTimeMinutes = SafeGetInt32(playTime);
                        }

                        return gameDto;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG游戏信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取GOG用户的游戏列表(用于导入)
    /// </summary>
    public async Task<List<GogGameDto>> GetGogUserGames(string gogUserId)
    {
        try
        {
            _logger.LogInformation("获取GOG用户游戏列表: gogUserId={GogUserId}", gogUserId);

            var gogData = await GetGogDataFromPython();
            
            if (gogData == null)
            {
                return new List<GogGameDto>();
            }

            var gamesList = new List<GogGameDto>();

            // 从games数组中提取游戏信息
            if (gogData.RootElement.TryGetProperty("games", out var games))
            {
                foreach (var game in games.EnumerateArray())
                {
                    var gogGameId = game.TryGetProperty("gameId", out var gameId) ? gameId.GetString() ?? "" : "";
                    
                    if (string.IsNullOrEmpty(gogGameId))
                    {
                        continue;
                    }

                    var gameDto = new GogGameDto
                    {
                        GogGameId = gogGameId,
                        Type = "game"
                    };

                    // 解析游戏详情
                    if (game.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null)
                    {
                        gameDto.Name = details.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
                        gameDto.HeaderImage = details.TryGetProperty("backgroundImage", out var bgImg) ? bgImg.GetString() ?? "" : "";
                    }

                    // 解析成就信息
                    if (game.TryGetProperty("achievements", out var achievements) && achievements.ValueKind != JsonValueKind.Null)
                    {
                        var totalCount = achievements.TryGetProperty("total_count", out var total) ? SafeGetInt32(total) : 0;
                        gameDto.Achievements = new GogAchievementsInfoDto
                        {
                            Total = totalCount,
                            CurrentAchievements = 0
                        };
                    }

                    // 解析游玩时长
                    if (game.TryGetProperty("playTimeMinutes", out var playTime))
                    {
                        gameDto.PlayTimeMinutes = SafeGetInt32(playTime);
                    }

                    gamesList.Add(gameDto);
                }
            }

            _logger.LogInformation("成功获取 {Count} 个GOG游戏", gamesList.Count);
            return gamesList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取GOG用户游戏列表时发生错误");
            return new List<GogGameDto>();
        }
    }
}


