using PlayLinker.Models.DTOs;
using PlayLinker.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly ITokenEncryptionService _encryptionService;
    private readonly PlayLinkerDbContext _context;
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public GogService(
        IConfiguration configuration, 
        ILogger<GogService> logger, 
        IWebHostEnvironment environment,
        ITokenEncryptionService encryptionService,
        PlayLinkerDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
        _context = context;

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
    /// 从数据库加载令牌到临时文件
    /// </summary>
    private async Task<string?> LoadTokenFromDatabase(int userId, int platformId)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && b.BindingStatus == true);
            
            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                _logger.LogWarning("用户{UserId}未绑定平台{PlatformId}或令牌为空", userId, platformId);
                return null;
            }
            
            var decryptedToken = _encryptionService.DecryptToken(binding.AccessToken);
            var tempFilePath = Path.Combine(_tokensPath, $"gog_tokens_{userId}_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempFilePath, decryptedToken);
            
            _logger.LogInformation("令牌已从数据库加载到临时文件: {TempFile}", tempFilePath);
            return tempFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载令牌失败");
            return null;
        }
    }

    /// <summary>
    /// 保存令牌到数据库
    /// </summary>
    private async Task<bool> SaveTokenToDatabase(int userId, int platformId, string tokenJson)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId);
            
            if (binding == null)
            {
                _logger.LogWarning("未找到绑定记录: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
                return false;
            }
            
            var encryptedToken = _encryptionService.EncryptToken(tokenJson);
            binding.AccessToken = encryptedToken;
            binding.LastSyncTime = DateTime.UtcNow;
            binding.ExpireTime = DateTime.UtcNow.AddYears(1);
            binding.BindingStatus = true;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("令牌已保存到数据库: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存令牌到数据库失败");
            return false;
        }
    }

    /// <summary>
    /// 清理临时令牌文件
    /// </summary>
    private void CleanupTempTokenFile(string? tempFilePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
                _logger.LogInformation("临时令牌文件已删除: {TempFile}", tempFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除临时令牌文件失败: {TempFile}", tempFilePath);
        }
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
    /// 检查令牌状态（从数据库）
    /// </summary>
    public async Task<GogAuthResponseDto> CheckTokenStatus(int userId, int platformId = 5)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && b.BindingStatus == true);

            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "用户未绑定GOG平台或令牌不存在，需要首次认证",
                    TokenExists = false,
                    NeedsBrowserAuth = true
                };
            }

            // 尝试使用令牌获取数据（验证令牌有效性）
            var gogData = await GetGogDataFromPython(userId, platformId);
            
            if (gogData != null && gogData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var gogUserId = gogData.RootElement.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
                return new GogAuthResponseDto
                {
                    Success = true,
                    Message = "令牌有效",
                    TokenExists = true,
                    UserId = gogUserId,
                    NeedsBrowserAuth = false
                };
            }
            else
            {
                return new GogAuthResponseDto
                {
                    Success = false,
                    Message = "令牌已过期或无效，需要重新认证",
                    TokenExists = true,
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
    public async Task<GogAuthResponseDto> AuthenticateGog(GogAuthRequestDto request, int userId)
    {
        string? tempTokenPath = null;
        try
        {
            _logger.LogInformation("开始GOG认证: userId={UserId}, HasRedirectUrl={HasRedirectUrl}", userId, !string.IsNullOrEmpty(request.RedirectUrl));

            // 使用临时文件进行认证
            tempTokenPath = Path.Combine(_tokensPath, $"gog_tokens_auth_{userId}_{Guid.NewGuid():N}.json");
            
            // 如果强制重新认证，删除数据库中的旧令牌
            if (request.ForceReauth)
            {
                var binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 5);
                if (binding != null)
            {
                    binding.AccessToken = null;
                    binding.BindingStatus = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已删除数据库中的旧令牌");
                }
            }

            // 如果没有提供重定向URL,先尝试刷新现有令牌或返回认证URL
            if (string.IsNullOrEmpty(request.RedirectUrl))
            {
                // 尝试从数据库加载令牌并刷新
                var binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 5 && b.BindingStatus == true);
                
                if (binding != null && !string.IsNullOrEmpty(binding.AccessToken) && !request.ForceReauth)
                {
                    _logger.LogInformation("尝试刷新现有令牌");
                    var gogData = await GetGogDataFromPython(userId, 5);
                    
                    if (gogData != null && gogData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        var gogUserId = gogData.RootElement.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
                        return new GogAuthResponseDto
                        {
                            Success = true,
                            Message = "令牌刷新成功",
                            TokenExists = true,
                            UserId = gogUserId,
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
                    TokenExists = false,
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
                    TokenExists = false,
                    NeedsBrowserAuth = true
                };
            }
            _logger.LogInformation("Python环境检查通过");
            
            var arguments = $"--tokens \"{tempTokenPath}\" --auth-code \"{authCode}\"";
            
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
                        TokenExists = false,
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
                    TokenExists = false,
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
                    // 认证成功，读取令牌文件并保存到数据库
                    if (File.Exists(tempTokenPath))
                    {
                        try
                        {
                            var tokenJson = await File.ReadAllTextAsync(tempTokenPath);
                            await SaveTokenToDatabase(userId, 5, tokenJson);
                            _logger.LogInformation("令牌已保存到数据库");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "保存令牌到数据库失败");
                        }
                    }
                    
                    return new GogAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        UserId = result.ContainsKey("userId") ? result["userId"].GetString() : null,
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
                        TokenExists = false,
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
                    TokenExists = false,
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
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempTokenPath);
        }
    }

    /// <summary>
    /// 获取GOG数据（支持用户级令牌）
    /// </summary>
    private async Task<JsonDocument?> GetGogDataFromPython(int userId, int platformId = 5)
    {
        string? tempFilePath = null;
        try
        {
            // 从数据库加载令牌到临时文件
            tempFilePath = await LoadTokenFromDatabase(userId, platformId);
            
            if (tempFilePath == null)
            {
                _logger.LogWarning("无法加载用户{UserId}的令牌", userId);
                return null;
            }

            var arguments = $"--tokens \"{tempFilePath}\"";
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
                
                // 成功后保存更新的令牌
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        var updatedToken = await File.ReadAllTextAsync(tempFilePath);
                        await SaveTokenToDatabase(userId, platformId, updatedToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "保存更新的令牌失败，但数据获取成功");
                    }
                }
                
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
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempFilePath);
        }
    }

    /// <summary>
    /// 导入GOG数据
    /// </summary>
    public async Task<GogImportResponseDto> ImportGogData(GogImportRequestDto request, int userId)
    {
        try
        {
            _logger.LogInformation("开始导入GOG数据: gogUserId={GogUserId}, userId={UserId}", request.GogUserId, userId);

            var taskId = $"gog_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取GOG数据
            _logger.LogInformation("正在调用Python脚本获取GOG数据...");
            var gogData = await GetGogDataFromPython(userId, 5);
            
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

            // 打印原始数据
            var rawJson = gogData.RootElement.GetRawText();
            _logger.LogInformation("========== GOG原始数据开始 ==========");
            _logger.LogInformation("{RawJson}", rawJson);
            _logger.LogInformation("========== GOG原始数据结束 ==========");

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
    public async Task<GogUserDto?> GetGogUser(string gogUserId, int userId)
    {
        try
        {
            _logger.LogInformation("获取GOG用户信息: gogUserId={GogUserId}, userId={UserId}", gogUserId, userId);

            var gogData = await GetGogDataFromPython(userId, 5);
            
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
    public async Task<GogGameDto?> GetGogGame(string gogGameId, int userId)
    {
        try
        {
            _logger.LogInformation("获取GOG游戏信息: gogGameId={GogGameId}, userId={UserId}", gogGameId, userId);

            var gogData = await GetGogDataFromPython(userId, 5);
            
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
    public async Task<List<GogGameDto>> GetGogUserGames(string gogUserId, int userId)
    {
        try
        {
            _logger.LogInformation("获取GOG用户游戏列表: gogUserId={GogUserId}, userId={UserId}", gogUserId, userId);

            var gogData = await GetGogDataFromPython(userId, 5);
            
            if (gogData == null)
            {
                return new List<GogGameDto>();
            }

            // 打印原始数据
            var rawJson = gogData.RootElement.GetRawText();
            _logger.LogInformation("========== GOG游戏列表原始数据开始 ==========");
            _logger.LogInformation("{RawJson}", rawJson);
            _logger.LogInformation("========== GOG游戏列表原始数据结束 ==========");

            var gamesList = new List<GogGameDto>();

            // 从games数组中提取游戏信息
            if (gogData.RootElement.TryGetProperty("games", out var games))
            {
                var totalGames = games.GetArrayLength();
                _logger.LogInformation("开始解析 {Count} 个GOG游戏", totalGames);
                
                // 打印每个游戏的原始JSON
                int gameIndex = 0;
                foreach (var game in games.EnumerateArray())
                {
                    gameIndex++;
                    var gameRawJson = game.GetRawText();
                    _logger.LogInformation("========== 游戏 {Index}/{Total} 原始数据开始 ==========", gameIndex, totalGames);
                    _logger.LogInformation("{GameRawJson}", gameRawJson);
                    _logger.LogInformation("========== 游戏 {Index}/{Total} 原始数据结束 ==========", gameIndex, totalGames);
                }
                
                int parsedCount = 0;
                int skippedCount = 0;
                
                foreach (var game in games.EnumerateArray())
                {
                    var gogGameId = game.TryGetProperty("gameId", out var gameId) ? gameId.GetString() ?? "" : "";
                    
                    if (string.IsNullOrEmpty(gogGameId))
                    {
                        _logger.LogWarning("跳过游戏：gameId为空");
                        skippedCount++;
                        continue;
                    }

                    _logger.LogInformation("正在解析游戏: GameId={GameId}", gogGameId);

                    var gameDto = new GogGameDto
                    {
                        GogGameId = gogGameId,
                        Type = "game"
                    };

                    // 解析游戏详情
                    if (game.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null)
                    {
                        gameDto.Name = details.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
                        
                        // 处理背景图片：如果是相对路径，添加 https: 前缀
                        if (details.TryGetProperty("backgroundImage", out var bgImg))
                        {
                            var bgImgStr = bgImg.GetString() ?? "";
                            if (!string.IsNullOrEmpty(bgImgStr))
                            {
                                if (bgImgStr.StartsWith("//"))
                                {
                                    gameDto.HeaderImage = "https:" + bgImgStr;
                                }
                                else if (!bgImgStr.StartsWith("http"))
                                {
                                    gameDto.HeaderImage = "https://" + bgImgStr;
                                }
                                else
                                {
                                    gameDto.HeaderImage = bgImgStr;
                                }
                            }
                        }
                        
                        gameDto.ShortDescription = details.TryGetProperty("shortDescription", out var shortDesc) ? shortDesc.GetString() : null;
                        gameDto.DetailedDescription = details.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                        
                        // 解析发布日期：从 releaseTimestamp 转换
                        if (details.TryGetProperty("releaseTimestamp", out var releaseTimestamp))
                        {
                            if (releaseTimestamp.ValueKind == JsonValueKind.Number && releaseTimestamp.TryGetInt64(out var timestamp))
                            {
                                // Unix时间戳转换为日期字符串
                                var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                                gameDto.ReleaseDate = dateTime.ToString("yyyy-MM-dd");
                            }
                            else if (releaseTimestamp.ValueKind == JsonValueKind.String)
                            {
                                gameDto.ReleaseDate = releaseTimestamp.GetString() ?? "";
                            }
                        }
                        // 如果没有 releaseTimestamp，尝试 releaseDate
                        else if (details.TryGetProperty("releaseDate", out var releaseDate))
                        {
                            gameDto.ReleaseDate = releaseDate.GetString() ?? "";
                        }
                        
                        _logger.LogInformation("游戏详情: GameId={GameId}, Name={Name}, ReleaseDate={ReleaseDate}", 
                            gogGameId, gameDto.Name, gameDto.ReleaseDate);
                        
                        // 解析年龄限制
                        if (details.TryGetProperty("ageLimit", out var ageLimit))
                        {
                            gameDto.RequiredAge = SafeGetInt32(ageLimit);
                        }
                        
                        // 解析开发商（如果存在）
                        if (details.TryGetProperty("developers", out var developers) && developers.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var dev in developers.EnumerateArray())
                            {
                                var devName = dev.GetString();
                                if (!string.IsNullOrEmpty(devName))
                                {
                                    gameDto.Developers.Add(devName);
                                }
                            }
                            if (gameDto.Developers.Count > 0)
                            {
                                _logger.LogInformation("游戏开发商: GameId={GameId}, Developers=[{Developers}]", 
                                    gogGameId, string.Join(", ", gameDto.Developers));
                            }
                        }
                        
                        // 解析发行商（如果存在）
                        if (details.TryGetProperty("publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var pub in publishers.EnumerateArray())
                            {
                                var pubName = pub.GetString();
                                if (!string.IsNullOrEmpty(pubName))
                                {
                                    gameDto.Publishers.Add(pubName);
                                }
                            }
                            if (gameDto.Publishers.Count > 0)
                            {
                                _logger.LogInformation("游戏发行商: GameId={GameId}, Publishers=[{Publishers}]", 
                                    gogGameId, string.Join(", ", gameDto.Publishers));
                            }
                        }
                        
                        // 解析平台支持：从 simpleGalaxyInstallers 或 downloads 推断
                        bool hasWindows = false;
                        bool hasMac = false;
                        bool hasLinux = false;
                        
                        // 从 simpleGalaxyInstallers 推断
                        if (details.TryGetProperty("simpleGalaxyInstallers", out var installers) && installers.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var installer in installers.EnumerateArray())
                            {
                                if (installer.TryGetProperty("os", out var os))
                                {
                                    var osStr = os.GetString()?.ToLower() ?? "";
                                    if (osStr == "windows") hasWindows = true;
                                    else if (osStr == "mac") hasMac = true;
                                    else if (osStr == "linux") hasLinux = true;
                                }
                            }
                        }
                        
                        // 从 downloads 推断（如果 simpleGalaxyInstallers 没有信息）
                        if (!hasWindows && !hasMac && !hasLinux && details.TryGetProperty("downloads", out var downloads) && downloads.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var langDownload in downloads.EnumerateArray())
                            {
                                if (langDownload.ValueKind == JsonValueKind.Array && langDownload.GetArrayLength() >= 2)
                                {
                                    var downloadObj = langDownload[1];
                                    if (downloadObj.TryGetProperty("windows", out _)) hasWindows = true;
                                    if (downloadObj.TryGetProperty("mac", out _)) hasMac = true;
                                    if (downloadObj.TryGetProperty("linux", out _)) hasLinux = true;
                                }
                            }
                        }
                        
                        gameDto.Platforms = new PlatformSupportDto
                        {
                            Windows = hasWindows,
                            Mac = hasMac,
                            Linux = hasLinux
                        };
                        
                        _logger.LogInformation("游戏平台支持: GameId={GameId}, Windows={Windows}, Mac={Mac}, Linux={Linux}", 
                            gogGameId, hasWindows, hasMac, hasLinux);
                        
                        // 解析题材（genres，如果存在）
                        if (details.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var genre in genres.EnumerateArray())
                            {
                                var genreName = genre.GetString();
                                if (!string.IsNullOrEmpty(genreName))
                                {
                                    gameDto.Genres.Add(genreName);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 如果没有details，至少设置默认平台支持
                        gameDto.Platforms = new PlatformSupportDto
                        {
                            Windows = true,
                            Mac = false,
                            Linux = false
                        };
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
                        _logger.LogInformation("游戏成就: GameId={GameId}, TotalAchievements={Total}", 
                            gogGameId, totalCount);
                    }

                    // 解析游玩时长
                    if (game.TryGetProperty("playTimeMinutes", out var playTime))
                    {
                        gameDto.PlayTimeMinutes = SafeGetInt32(playTime);
                        _logger.LogInformation("游戏时长: GameId={GameId}, PlayTimeMinutes={PlayTime}", 
                            gogGameId, gameDto.PlayTimeMinutes);
                    }

                    // 检查游戏名称是否为空
                    if (string.IsNullOrEmpty(gameDto.Name))
                    {
                        _logger.LogWarning("游戏 {GameId} 的名称为空，尝试从其他字段获取", gogGameId);
                        // 如果名称为空，尝试使用gameId作为名称
                        gameDto.Name = $"GOG Game {gogGameId}";
                    }
                    
                    gamesList.Add(gameDto);
                    parsedCount++;
                    _logger.LogInformation("游戏解析完成: GameId={GameId}, Name={Name}, Developers={DevCount}, Publishers={PubCount}, Achievements={Achievements}, PlayTime={PlayTime}分钟", 
                        gogGameId, gameDto.Name, gameDto.Developers.Count, gameDto.Publishers.Count, 
                        gameDto.Achievements?.Total ?? 0, gameDto.PlayTimeMinutes);
                }
                
                _logger.LogInformation("游戏解析完成: 总计={Total}, 已解析={Parsed}, 已跳过={Skipped}", 
                    totalGames, parsedCount, skippedCount);
            }
            else
            {
                _logger.LogWarning("GOG数据中没有games数组");
                // 输出所有可用的根节点，帮助调试
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var propertyNames = gogData.RootElement.EnumerateObject().Select(p => p.Name).ToList();
                    _logger.LogDebug("可用的根节点: {Properties}", string.Join(", ", propertyNames));
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


