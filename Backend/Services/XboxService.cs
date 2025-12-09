using PlayLinker.Models.DTOs;
using System.Diagnostics;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// Xbox API集成服务实现
/// 通过Python脚本桥接Xbox Web API
/// </summary>
public class XboxService : IXboxService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<XboxService> _logger;
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public XboxService(IConfiguration configuration, ILogger<XboxService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;

        // 获取Python路径（从配置或环境变量）
        _pythonPath = configuration["XboxAPI:PythonPath"] ?? "python";
        
        // 脚本路径：Backend/Python
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Python");
        
        // 令牌路径：Backend/Tokens
        _tokensPath = Path.Combine(environment.ContentRootPath, "Tokens");

        // 确保目录存在
        Directory.CreateDirectory(_scriptsPath);
        Directory.CreateDirectory(_tokensPath);

        _logger.LogInformation("XboxService 初始化: PythonPath={PythonPath}, ScriptsPath={ScriptsPath}, TokensPath={TokensPath}",
            _pythonPath, _scriptsPath, _tokensPath);
    }

    /// <summary>
    /// 安全地从 JsonElement 获取整数值，支持数字和字符串两种格式
    /// </summary>
    private int SafeGetInt32(JsonElement element, int defaultValue = 0)
    {
        try
        {
            // 如果是数字类型，直接获取
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
            // 如果是字符串类型，尝试解析
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
            _logger.LogWarning(ex, "解析整数失败，使用默认值: {DefaultValue}", defaultValue);
        }
        
        return defaultValue;
    }

    /// <summary>
    /// 安全地从 JsonElement 获取长整数值，支持数字和字符串两种格式
    /// </summary>
    private long SafeGetInt64(JsonElement element, long defaultValue = 0)
    {
        try
        {
            // 如果是数字类型，直接获取
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt64();
            }
            // 如果是字符串类型，尝试解析
            else if (element.ValueKind == JsonValueKind.String)
            {
                var strValue = element.GetString();
                if (long.TryParse(strValue, out var longValue))
                {
                    return longValue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析长整数失败，使用默认值: {DefaultValue}", defaultValue);
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

        // 设置环境变量，确保Python使用UTF-8编码
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
                    _logger.LogWarning("【重要】请在浏览器中打开以下URL完成Xbox认证:");
                    _logger.LogWarning(">>> {AuthUrl}", authUrl);
                    _logger.LogWarning("如果浏览器未自动打开，请手动复制上面的URL到浏览器");
                    _logger.LogWarning("完成登录后页面会自动关闭，请耐心等待...");
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

        // 等待进程结束（最多5分钟）
        var exited = await Task.Run(() => process.WaitForExit(300000)); // 5分钟超时

        if (!exited)
        {
            try
            {
                process.Kill();
            }
            catch { }
            throw new TimeoutException("Python脚本执行超时（5分钟）");
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
        return Path.Combine(_tokensPath, "xbox_tokens.json");
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

            // 测试依赖
            startInfo.Arguments = "-c \"import xbox.webapi; print('xbox-webapi-python已安装')\"";
            using var depProcess = new Process { StartInfo = startInfo };
            depProcess.Start();
            var depOutput = await depProcess.StandardOutput.ReadToEndAsync();
            var depError = await depProcess.StandardError.ReadToEndAsync();
            await depProcess.WaitForExitAsync();

            if (depProcess.ExitCode != 0)
            {
                return (false, $"xbox-webapi-python未安装。请执行: pip install -r Backend/Python/requirements.txt\n详细错误: {depError}");
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
    public async Task<XboxAuthResponseDto> CheckTokenStatus(string? tokensPath = null)
    {
        try
        {
            var tokenPath = GetTokenFilePath(tokensPath);
            var tokenExists = File.Exists(tokenPath);

            if (!tokenExists)
            {
                return new XboxAuthResponseDto
                {
                    Success = false,
                    Message = "令牌文件不存在，需要首次认证",
                    TokenExists = false,
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }

            // 尝试使用令牌获取数据（验证令牌有效性）
            var xboxData = await GetXboxDataFromPython(tokensPath);
            
            if (xboxData != null && xboxData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var xuid = xboxData.RootElement.TryGetProperty("xuid", out var xuidProp) ? xuidProp.GetString() : null;
                return new XboxAuthResponseDto
                {
                    Success = true,
                    Message = "令牌有效",
                    TokenExists = true,
                    TokensPath = tokenPath,
                    Xuid = xuid,
                    NeedsBrowserAuth = false
                };
            }
            else
            {
                return new XboxAuthResponseDto
                {
                    Success = false,
                    Message = "令牌已过期或无效，需要重新认证",
                    TokenExists = true,
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return new XboxAuthResponseDto
            {
                Success = false,
                Message = $"检查失败: {ex.Message}",
                TokenExists = false,
                NeedsBrowserAuth = true
            };
        }
    }

    /// <summary>
    /// 执行Xbox认证
    /// </summary>
    public async Task<XboxAuthResponseDto> AuthenticateXbox(XboxAuthRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始Xbox认证, OpenBrowser={OpenBrowser}", request.OpenBrowser);

            var tokenPath = GetTokenFilePath(request.TokensPath);
            
            // 如果强制重新认证，删除旧令牌
            if (request.ForceReauth && File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
                _logger.LogInformation("已删除旧令牌文件");
            }

            // 如果不需要打开浏览器，先检查令牌是否存在且有效
            if (!request.OpenBrowser)
            {
                if (!File.Exists(tokenPath))
                {
                    return new XboxAuthResponseDto
                    {
                        Success = false,
                        Message = "令牌文件不存在，请先在本地环境完成首次认证，或设置 openBrowser=true",
                        TokenExists = false,
                        TokensPath = tokenPath,
                        NeedsBrowserAuth = true
                    };
                }

                // 尝试刷新令牌
                _logger.LogInformation("尝试刷新现有令牌");
                var xboxData = await GetXboxDataFromPython(tokenPath);
                
                if (xboxData != null && xboxData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    var xuid = xboxData.RootElement.TryGetProperty("xuid", out var xuidProp) ? xuidProp.GetString() : null;
                    return new XboxAuthResponseDto
                    {
                        Success = true,
                        Message = "令牌刷新成功",
                        TokenExists = true,
                        TokensPath = tokenPath,
                        Xuid = xuid,
                        NeedsBrowserAuth = false
                    };
                }
                else
                {
                    return new XboxAuthResponseDto
                    {
                        Success = false,
                        Message = "令牌刷新失败，令牌可能已过期。请设置 openBrowser=true 重新认证，或在本地完成认证后上传tokens文件",
                        TokenExists = true,
                        TokensPath = tokenPath,
                        NeedsBrowserAuth = true
                    };
                }
            }

            // 测试Python环境
            _logger.LogInformation("检查Python环境...");
            var (envSuccess, envMessage) = await TestPythonEnvironment();
            if (!envSuccess)
            {
                _logger.LogError("Python环境检查失败: {Message}", envMessage);
                return new XboxAuthResponseDto
                {
                    Success = false,
                    Message = $"Python环境问题: {envMessage}",
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath,
                    NeedsBrowserAuth = true
                };
            }
            _logger.LogInformation("Python环境检查通过");

            // 需要打开浏览器进行首次认证
            _logger.LogInformation("启动浏览器进行OAuth2认证");
            _logger.LogInformation("=" + new string('=', 80));
            _logger.LogInformation("Xbox认证需要在浏览器中完成，请注意查看浏览器窗口");
            _logger.LogInformation("如果浏览器未自动打开，请查看下方日志中的认证URL");
            _logger.LogInformation("=" + new string('=', 80));
            
            var arguments = $"--tokens \"{tokenPath}\" --port 8080";
            
            int exitCode;
            string output;
            string error;
            
            try
            {
                (exitCode, output, error) = await RunPythonScript("xbox_authenticate.py", arguments);

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
                    var errorMessage = !string.IsNullOrEmpty(error) ? error : "Python脚本执行失败，未返回错误信息";
                    
                    // 检查是否是依赖问题
                    if (error.Contains("ModuleNotFoundError") || error.Contains("ImportError"))
                    {
                        errorMessage = $"Python依赖缺失。请在Backend/Python目录执行: pip install -r requirements.txt\n原始错误: {error}";
                    }
                    else if (error.Contains("python") && error.Contains("not found"))
                    {
                        errorMessage = $"找不到Python。请检查appsettings.json中的PythonPath配置，或确保Python已安装并在PATH中。\n原始错误: {error}";
                    }
                    
                    _logger.LogError("Xbox认证失败: {ErrorMessage}", errorMessage);
                    return new XboxAuthResponseDto
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
                return new XboxAuthResponseDto
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
                // 输出可能包含多行，取最后一行JSON
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonLine = lines.LastOrDefault(l => l.Trim().StartsWith("{"));
                
                if (string.IsNullOrEmpty(jsonLine))
                {
                    throw new Exception("未找到JSON输出");
                }

                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonLine);
                
                if (result != null && result.ContainsKey("success") && result["success"].GetBoolean())
                {
                    return new XboxAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        Xuid = result.ContainsKey("xuid") ? result["xuid"].GetString() : null,
                        TokensPath = tokenPath,
                        TokenExists = true,
                        NeedsBrowserAuth = false
                    };
                }
                else if (result != null && result.ContainsKey("need_auth") && result["need_auth"].GetBoolean())
                {
                    // 需要浏览器认证
                    var authUrl = result.ContainsKey("auth_url") ? result["auth_url"].GetString() : null;
                    return new XboxAuthResponseDto
                    {
                        Success = false,
                        Message = "需要在浏览器中完成认证",
                        AuthUrl = authUrl,
                        TokensPath = tokenPath,
                        TokenExists = false,
                        NeedsBrowserAuth = true
                    };
                }
                else
                {
                    return new XboxAuthResponseDto
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
                return new XboxAuthResponseDto
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
            _logger.LogError(ex, "Xbox认证时发生错误");
            return new XboxAuthResponseDto
            {
                Success = false,
                Message = $"认证错误: {ex.Message}",
                TokenExists = false,
                NeedsBrowserAuth = true
            };
        }
    }

    /// <summary>
    /// 获取Xbox数据
    /// </summary>
    private async Task<JsonDocument?> GetXboxDataFromPython(string? tokensPath = null)
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
            var (exitCode, output, error) = await RunPythonScript("xbox_get_data.py", arguments);

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

            // 解析JSON输出（即使exitCode不为0也尝试解析，因为可能有错误信息）
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError("Python脚本没有输出任何内容，ExitCode={ExitCode}, Error={Error}", exitCode, error);
                    return null;
                }
                
                // 清理输出：移除可能的调试信息行（以INFO:, WARNING:, AUTH_URL:等开头的行）
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
                    _logger.LogError("未找到有效的JSON输出，ExitCode={ExitCode}", exitCode);
                    _logger.LogDebug("完整输出: {Output}", output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("错误信息: {Error}", error);
                    }
                    return null;
                }
                
                // 重新组合JSON字符串
                var jsonString = string.Join("\n", jsonLines);
                
                _logger.LogDebug("准备解析JSON，长度: {Length} 字符", jsonString.Length);
                
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
                    _logger.LogWarning("Python脚本返回非0退出码（{ExitCode}），但成功解析了JSON输出", exitCode);
                }
                
                _logger.LogInformation("成功解析Xbox数据JSON");
                return doc;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON解析失败: ExitCode={ExitCode}, OutputLength={OutputLength}, Error={Error}", 
                    exitCode, 
                    output.Length, 
                    error);
                // 记录更多调试信息
                _logger.LogDebug("输出的前1000字符: {Output}", output.Length > 1000 ? output.Substring(0, 1000) : output);
                _logger.LogDebug("输出的后1000字符: {Output}", output.Length > 1000 ? output.Substring(output.Length - 1000) : output);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析Xbox数据时发生未预期的错误: ExitCode={ExitCode}", exitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox数据时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 导入Xbox数据
    /// </summary>
    public async Task<XboxImportResponseDto> ImportXboxData(XboxImportRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始导入Xbox数据: xboxUserId={XboxUserId}", request.XboxUserId);

            var taskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取Xbox数据
            _logger.LogInformation("正在调用Python脚本获取Xbox数据...");
            var xboxData = await GetXboxDataFromPython();
            
            if (xboxData == null)
            {
                var errorMsg = "获取Xbox数据失败：Python脚本返回空数据。请检查：1) 令牌是否有效；2) 网络连接是否正常；3) Python环境是否配置正确。详细信息请查看服务器日志。";
                _logger.LogError(errorMsg);
                return new XboxImportResponseDto
                {
                    TaskId = taskId,
                    Status = "failed",
                    Message = errorMsg,
                    EstimatedTime = 0,
                    Items = new XboxImportItemsDto()
                };
            }

            // 检查返回的数据是否成功
            if (xboxData.RootElement.TryGetProperty("success", out var success))
            {
                if (!success.GetBoolean())
                {
                    var errorMsg = xboxData.RootElement.TryGetProperty("message", out var msg) 
                        ? msg.GetString() ?? "未知错误" 
                        : "未知错误";
                    var errorType = xboxData.RootElement.TryGetProperty("error", out var err) 
                        ? err.GetString() ?? "unknown" 
                        : "unknown";
                    
                    var fullErrorMsg = $"获取Xbox数据失败：{errorMsg} (错误类型: {errorType})";
                    _logger.LogError(fullErrorMsg);
                    
                    return new XboxImportResponseDto
                    {
                        TaskId = taskId,
                        Status = "failed",
                        Message = fullErrorMsg,
                        EstimatedTime = 0,
                        Items = new XboxImportItemsDto()
                    };
                }
            }

            // 解析数据并统计
            int gamesCount = 0;
            int achievementsCount = 0;
            int totalPlayTimeMinutes = 0;

            if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
            {
                if (titleHistory.TryGetProperty("titles", out var titles))
                {
                    gamesCount = titles.GetArrayLength();
                    
                    _logger.LogInformation("找到 {Count} 个游戏", gamesCount);
                    
                    // 统计成就数量和游戏时长
                    foreach (var title in titles.EnumerateArray())
                    {
                        var gameName = title.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Unknown" : "Unknown";
                        
                        if (title.TryGetProperty("achievement", out var achievement))
                        {
                            if (achievement.TryGetProperty("total_achievements", out var total))
                            {
                                var totalAch = SafeGetInt32(total);
                                achievementsCount += totalAch;
                            }
                        }
                        
                        // 统计游戏时长
                        if (title.TryGetProperty("game_time_minutes", out var gameTime))
                        {
                            var minutes = SafeGetInt32(gameTime);
                            totalPlayTimeMinutes += minutes;
                            _logger.LogDebug("游戏 {Name} 游玩时长: {Minutes} 分钟", gameName, minutes);
                        }
                    }
                }
            }
            
            _logger.LogInformation("统计完成: {GamesCount} 个游戏, {AchievementsCount} 个成就, 总游玩时长: {Hours} 小时", 
                gamesCount, achievementsCount, totalPlayTimeMinutes / 60.0);

            _logger.LogInformation("成功导入Xbox数据: {GamesCount} 个游戏, {AchievementsCount} 个成就", 
                gamesCount, achievementsCount);

            return new XboxImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {achievementsCount} 个成就",
                EstimatedTime = 0,
                Items = new XboxImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = achievementsCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入Xbox数据时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取Xbox用户信息
    /// </summary>
    public async Task<XboxUserDto?> GetXboxUser(string xuid)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户信息: xuid={Xuid}", xuid);

            var xboxData = await GetXboxDataFromPython();
            
            if (xboxData == null)
            {
                return null;
            }

            // 解析用户资料
            if (xboxData.RootElement.TryGetProperty("profile", out var profile))
            {
                var user = new XboxUserDto
                {
                    Xuid = profile.TryGetProperty("xuid", out var xuidProp) ? xuidProp.GetString() ?? "" : "",
                    Gamertag = profile.TryGetProperty("gamertag", out var gt) ? gt.GetString() ?? "" : "",
                    ProfileUrl = $"https://account.xbox.com/Profile?Gamertag={(profile.TryGetProperty("gamertag", out var gt2) ? gt2.GetString() : "")}",
                    AvatarUrl = profile.TryGetProperty("display_pic", out var dp) ? dp.GetString() ?? "" : "",
                    AccountCreated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), // Xbox API不直接提供此信息
                    Country = "",
                    Gamerscore = profile.TryGetProperty("gamer_score", out var gs) && int.TryParse(gs.GetString(), out var score) ? score : 0,
                    Tier = profile.TryGetProperty("account_tier", out var tier) ? tier.GetString() ?? "Gold" : "Gold",
                    GamesOwned = 0, // 需要从title_history计算
                    IsPublic = true
                };

                // 计算拥有游戏数量
                if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
                {
                    if (titleHistory.TryGetProperty("titles", out var titles))
                    {
                        user.GamesOwned = titles.GetArrayLength();
                    }
                }

                return user;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox用户信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取Xbox游戏信息
    /// </summary>
    public async Task<XboxGameDto?> GetXboxGame(string titleId)
    {
        try
        {
            _logger.LogInformation("获取Xbox游戏信息: titleId={TitleId}", titleId);

            var xboxData = await GetXboxDataFromPython();
            
            if (xboxData == null)
            {
                return null;
            }

            // 从title_history中查找游戏
            if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
            {
                if (titleHistory.TryGetProperty("titles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        if (title.TryGetProperty("title_id", out var tid) && tid.GetString() == titleId)
                        {
                            var game = new XboxGameDto
                            {
                                TitleId = titleId,
                                Name = title.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                                Type = title.TryGetProperty("type", out var type) ? type.GetString() ?? "game" : "game",
                                IsFree = false, // 需要额外API获取
                                HeaderImage = title.TryGetProperty("display_image", out var img) ? img.GetString() ?? "" : ""
                            };

                            // 解析详细信息
                            if (title.TryGetProperty("detail", out var detail))
                            {
                                game.ShortDescription = detail.TryGetProperty("short_description", out var sd) ? sd.GetString() : null;
                                game.DetailedDescription = detail.TryGetProperty("description", out var dd) ? dd.GetString() : null;
                                game.RequiredAge = detail.TryGetProperty("min_age", out var age) ? SafeGetInt32(age) : 0;
                                game.ReleaseDate = detail.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "";

                                // 开发商
                                if (detail.TryGetProperty("developer_name", out var dev) && !dev.ValueKind.Equals(System.Text.Json.JsonValueKind.Null))
                                {
                                    var devName = dev.GetString();
                                    if (!string.IsNullOrEmpty(devName))
                                    {
                                        game.Developers.Add(devName);
                                    }
                                }

                                // 发行商
                                if (detail.TryGetProperty("publisher_name", out var pub) && !pub.ValueKind.Equals(System.Text.Json.JsonValueKind.Null))
                                {
                                    var pubName = pub.GetString();
                                    if (!string.IsNullOrEmpty(pubName))
                                    {
                                        game.Publishers.Add(pubName);
                                    }
                                }

                                // 题材
                                if (detail.TryGetProperty("genres", out var genres) && genres.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var genre in genres.EnumerateArray())
                                    {
                                        var genreName = genre.GetString();
                                        if (!string.IsNullOrEmpty(genreName))
                                        {
                                            game.Genres.Add(genreName);
                                        }
                                    }
                                }

                                // 功能
                                if (detail.TryGetProperty("capabilities", out var caps) && caps.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var cap in caps.EnumerateArray())
                                    {
                                        var capName = cap.GetString();
                                        if (!string.IsNullOrEmpty(capName))
                                        {
                                            game.Categories.Add(capName);
                                        }
                                    }
                                }
                            }

                            // 解析成就信息
                            if (title.TryGetProperty("achievement", out var achievement))
                            {
                                game.Achievements = new XboxAchievementsInfoDto
                                {
                                    Total = achievement.TryGetProperty("total_achievements", out var total) ? SafeGetInt32(total) : 0
                                };
                            }

                            return game;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox游戏信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取Xbox用户的游戏列表（用于导入）
    /// </summary>
    public async Task<List<XboxGameDto>> GetXboxUserGames(string xuid)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户游戏列表: xuid={Xuid}", xuid);

            var xboxData = await GetXboxDataFromPython();
            
            if (xboxData == null)
            {
                return new List<XboxGameDto>();
            }

            var games = new List<XboxGameDto>();

            // 从title_history中提取游戏信息
            if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
            {
                if (titleHistory.TryGetProperty("titles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        var titleId = title.TryGetProperty("title_id", out var tid) ? tid.GetString() ?? "" : "";
                        var name = title.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                        
                        if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(name))
                        {
                            continue;
                        }

                        var game = new XboxGameDto
                        {
                            TitleId = titleId,
                            Name = name,
                            Type = title.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "game" : "game",
                            IsFree = false, // Xbox API 不直接提供此信息
                            HeaderImage = title.TryGetProperty("display_image", out var img) ? img.GetString() ?? "" : ""
                        };

                        // 解析详细信息
                        if (title.TryGetProperty("detail", out var detail))
                        {
                            game.ShortDescription = detail.TryGetProperty("short_description", out var sd) ? sd.GetString() : null;
                            game.DetailedDescription = detail.TryGetProperty("description", out var dd) ? dd.GetString() : null;
                            game.RequiredAge = detail.TryGetProperty("min_age", out var age) ? SafeGetInt32(age) : 0;
                            game.ReleaseDate = detail.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "";

                            // 开发商
                            if (detail.TryGetProperty("developer_name", out var dev) && dev.ValueKind != JsonValueKind.Null)
                            {
                                var devName = dev.GetString();
                                if (!string.IsNullOrEmpty(devName))
                                {
                                    game.Developers.Add(devName);
                                }
                            }

                            // 发行商
                            if (detail.TryGetProperty("publisher_name", out var pub) && pub.ValueKind != JsonValueKind.Null)
                            {
                                var pubName = pub.GetString();
                                if (!string.IsNullOrEmpty(pubName))
                                {
                                    game.Publishers.Add(pubName);
                                }
                            }

                            // 题材
                            if (detail.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var genre in genres.EnumerateArray())
                                {
                                    var genreName = genre.GetString();
                                    if (!string.IsNullOrEmpty(genreName))
                                    {
                                        game.Genres.Add(genreName);
                                    }
                                }
                            }

                            // 功能
                            if (detail.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var cap in caps.EnumerateArray())
                                {
                                    var capName = cap.GetString();
                                    if (!string.IsNullOrEmpty(capName))
                                    {
                                        game.Categories.Add(capName);
                                    }
                                }
                            }
                        }

                        // 解析成就信息
                        if (title.TryGetProperty("achievement", out var achievement))
                        {
                            game.Achievements = new XboxAchievementsInfoDto
                            {
                                Total = achievement.TryGetProperty("total_achievements", out var total) ? SafeGetInt32(total) : 0,
                                CurrentAchievements = achievement.TryGetProperty("current_achievements", out var current) ? SafeGetInt32(current) : 0,
                                CurrentGamerscore = achievement.TryGetProperty("current_gamerscore", out var cs) ? SafeGetInt32(cs) : 0
                            };
                        }

                        // 解析游戏历史（最后游玩时间）
                        if (title.TryGetProperty("title_history", out var titleHistoryInfo))
                        {
                            if (titleHistoryInfo.TryGetProperty("last_time_played", out var lastPlayed))
                            {
                                game.LastPlayed = lastPlayed.GetString();
                            }
                        }

                        // 解析游戏时长
                        if (title.TryGetProperty("game_time_minutes", out var gameTime))
                        {
                            game.PlayTimeMinutes = SafeGetInt32(gameTime);
                        }

                        games.Add(game);
                    }
                }
            }

            _logger.LogInformation("成功获取 {Count} 个Xbox游戏", games.Count);
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox用户游戏列表时发生错误");
            return new List<XboxGameDto>();
        }
    }

    /// <summary>
    /// 获取Xbox用户成就
    /// </summary>
    public async Task<List<XboxUserAchievementDto>> GetXboxUserAchievements(string xuid)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户成就: xuid={Xuid}", xuid);

            var xboxData = await GetXboxDataFromPython();
            
            if (xboxData == null)
            {
                return new List<XboxUserAchievementDto>();
            }

            var achievements = new List<XboxUserAchievementDto>();

            // 从title_history中提取成就信息
            if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
            {
                if (titleHistory.TryGetProperty("titles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        if (title.TryGetProperty("achievement", out var achievement))
                        {
                            var titleId = title.TryGetProperty("title_id", out var tid) ? tid.GetString() ?? "" : "";
                            var titleName = title.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
                            
                            var currentAch = achievement.TryGetProperty("current_achievements", out var current) ? SafeGetInt32(current) : 0;
                            var totalAch = achievement.TryGetProperty("total_achievements", out var total) ? SafeGetInt32(total) : 0;
                            var currentScore = achievement.TryGetProperty("current_gamerscore", out var cs) ? SafeGetInt32(cs) : 0;

                            // 注意：这里只能获取统计信息，无法获取每个成就的详细信息
                            // 如果需要详细成就信息，需要调用额外的Xbox API
                            var achDto = new XboxUserAchievementDto
                            {
                                AchievementId = $"{titleId}_summary",
                                GameId = 0, // 需要映射到本地数据库
                                GameName = titleName,
                                AchievementName = "成就统计",
                                DisplayName = $"{titleName} - 成就进度",
                                Description = $"已解锁 {currentAch}/{totalAch} 个成就",
                                Score = currentScore,
                                Unlocked = currentAch > 0,
                                UnlockTime = null,
                                IconUnlocked = "",
                                IconLocked = ""
                            };

                            achievements.Add(achDto);
                        }
                    }
                }
            }

            return achievements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox用户成就时发生错误");
            return new List<XboxUserAchievementDto>();
        }
    }
}

