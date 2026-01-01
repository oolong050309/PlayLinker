using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly ITokenEncryptionService _encryptionService;
    private readonly PlayLinkerDbContext _context;
    private readonly string _pythonPath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public XboxService(
        IConfiguration configuration, 
        ILogger<XboxService> logger, 
        IWebHostEnvironment environment,
        ITokenEncryptionService encryptionService,
        PlayLinkerDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
        _context = context;

        // 获取Python路径（从配置或环境变量）
        _pythonPath = configuration["XboxAPI:PythonPath"] ?? "python";
        
        // 脚本路径：Backend/Python
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Python");
        
        // 令牌路径：Backend/Tokens
        _tokensPath = Path.Combine(environment.ContentRootPath, "Tokens");

        // 确保目录存在（用于临时文件）
        Directory.CreateDirectory(_scriptsPath);
        Directory.CreateDirectory(_tokensPath);

        _logger.LogInformation("XboxService 初始化: PythonPath={PythonPath}, ScriptsPath={ScriptsPath}, TokensPath={TokensPath}",
            _pythonPath, _scriptsPath, _tokensPath);
    }

    /// <summary>
    /// 从数据库加载令牌到临时文件
    /// </summary>
    private async Task<string?> LoadTokenFromDatabase(int userId, int platformId)
    {
        try
        {
            // 先尝试查找 BindingStatus 为 true 的绑定
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && b.BindingStatus == true);
            
            // 如果没找到，尝试查找任何有令牌的绑定（可能是 BindingStatus 为 null 或 false）
            if (binding == null)
            {
                binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && !string.IsNullOrEmpty(b.AccessToken));
                
                if (binding != null)
                {
                    _logger.LogWarning("找到绑定记录但BindingStatus不为true，将更新为true: UserId={UserId}, PlatformId={PlatformId}, BindingStatus={BindingStatus}", 
                        userId, platformId, binding.BindingStatus);
                    // 自动修复 BindingStatus
                    binding.BindingStatus = true;
                    await _context.SaveChangesAsync();
                }
            }
            
            if (binding == null)
            {
                // 检查是否存在绑定记录（即使没有令牌）
                var anyBinding = await _context.UserPlatformBindings
                    .AnyAsync(b => b.UserId == userId && b.PlatformId == platformId);
                
                if (anyBinding)
                {
                    _logger.LogWarning("用户{UserId}存在平台{PlatformId}的绑定记录，但令牌为空", userId, platformId);
                }
                else
                {
                    _logger.LogWarning("用户{UserId}未绑定平台{PlatformId}", userId, platformId);
                }
                return null;
            }
            
            if (string.IsNullOrEmpty(binding.AccessToken))
            {
                _logger.LogWarning("用户{UserId}的平台{PlatformId}绑定记录存在，但AccessToken为空", userId, platformId);
                return null;
            }
            
            // 解密令牌
            var decryptedToken = _encryptionService.DecryptToken(binding.AccessToken);
            
            if (string.IsNullOrEmpty(decryptedToken))
            {
                _logger.LogError("解密后的令牌为空: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
                return null;
            }
            
            // 写入临时文件（按用户ID区分）
            var tempFilePath = Path.Combine(_tokensPath, $"xbox_tokens_{userId}_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempFilePath, decryptedToken);
            
            _logger.LogInformation("令牌已从数据库加载到临时文件: UserId={UserId}, PlatformId={PlatformId}, TempFile={TempFile}", 
                userId, platformId, tempFilePath);
            return tempFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载令牌失败: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
            return null;
        }
    }

    /// <summary>
    /// 保存令牌到数据库（如果绑定不存在则创建）
    /// </summary>
    private async Task<bool> SaveTokenToDatabase(int userId, int platformId, string tokenJson, string? xuid = null)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId);
            
            if (binding != null)
            {
                // 更新现有绑定
                var encryptedToken = _encryptionService.EncryptToken(tokenJson);
                binding.AccessToken = encryptedToken;
                binding.BindingStatus = true; // 确保绑定状态为true
                binding.LastSyncTime = DateTime.UtcNow;
                binding.ExpireTime = DateTime.UtcNow.AddYears(1); // Xbox令牌有效期1年
                
                // 如果提供了XUID，更新PlatformUserId
                if (!string.IsNullOrEmpty(xuid))
                {
                    binding.PlatformUserId = xuid;
                }
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("令牌已更新到数据库: UserId={UserId}, PlatformId={PlatformId}, BindingStatus={BindingStatus}", 
                    userId, platformId, binding.BindingStatus);
                return true;
            }
            
            // 绑定不存在，需要创建
            // 如果xuid为空，使用临时标识符，后续可以通过同步更新
            var platformUserId = xuid ?? $"temp_{userId}_{platformId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            
            // 先确保PlayerPlatform记录存在（外键约束要求）
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == platformUserId && pp.PlatformId == platformId);
            
            if (playerPlatform == null)
            {
                // 注意：此时令牌还未保存，无法调用GetXboxUser（需要令牌）
                // 先使用基本信息创建PlayerPlatform记录，后续可以通过同步更新
                _logger.LogInformation("创建PlayerPlatform记录: PlatformUserId={PlatformUserId}", platformUserId);
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = platformUserId,
                    PlatformId = platformId,
                    ProfileName = xuid ?? $"临时用户_{userId}"  // 暂时使用临时名称，后续可以更新
                };
                _context.PlayerPlatforms.Add(playerPlatform);
                
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已创建PlayerPlatform记录: PlatformUserId={PlatformUserId}", platformUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建PlayerPlatform记录失败: PlatformUserId={PlatformUserId}", platformUserId);
                    throw; // 重新抛出异常，因为这是必需的
                }
            }
            
            // 确保playerPlatform已保存（重新查询以确保数据一致性）
            playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == platformUserId && pp.PlatformId == platformId);
            
            if (playerPlatform == null)
            {
                _logger.LogError("PlayerPlatform记录不存在，无法创建绑定: PlatformUserId={PlatformUserId}, PlatformId={PlatformId}", platformUserId, platformId);
                return false;
            }
            
            // 创建新的绑定记录
            var encryptedTokenForNewBinding = _encryptionService.EncryptToken(tokenJson);
            binding = new UserPlatformBinding
            {
                UserId = userId,
                PlatformId = platformId,
                PlatformUserId = platformUserId,  // 必须与playerPlatform.PlatformUserId完全一致
                AccessToken = encryptedTokenForNewBinding,
                BindingStatus = true,
                BindingTime = DateTime.UtcNow,
                LastSyncTime = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddYears(1) // Xbox令牌有效期1年
            };
            
            _context.UserPlatformBindings.Add(binding);
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存UserPlatformBinding失败: PlatformUserId={PlatformUserId}, PlatformId={PlatformId}, PlayerPlatform存在={PlayerPlatformExists}", 
                    platformUserId, platformId, playerPlatform != null);
                throw;
            }
            
            _logger.LogInformation("已创建绑定记录并保存令牌: UserId={UserId}, PlatformId={PlatformId}, PlatformUserId={PlatformUserId}, Xuid={Xuid}", 
                userId, platformId, platformUserId, xuid ?? "未提供");
            
            // 如果xuid为空，尝试从令牌中获取xuid并更新
            if (string.IsNullOrEmpty(xuid))
            {
                try
                {
                    // 尝试从令牌JSON中解析xuid
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                    if (tokenData.TryGetProperty("xsts_token", out var xstsToken) && 
                        xstsToken.TryGetProperty("xuid", out var xuidElement))
                    {
                        var extractedXuid = xuidElement.GetString();
                        if (!string.IsNullOrEmpty(extractedXuid) && extractedXuid != platformUserId)
                        {
                            _logger.LogInformation("从令牌中提取到XUID，更新绑定: {Xuid}", extractedXuid);
                            // 更新绑定和PlayerPlatform的PlatformUserId
                            binding.PlatformUserId = extractedXuid;
                            playerPlatform.PlatformUserId = extractedXuid;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("已更新PlatformUserId为XUID: {Xuid}", extractedXuid);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "尝试从令牌中提取XUID失败，将使用临时标识符");
                }
            }
            
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
    /// 检查令牌状态（从数据库）
    /// </summary>
    public async Task<XboxAuthResponseDto> CheckTokenStatus(int userId, int platformId = 7)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && b.BindingStatus == true);

            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                return new XboxAuthResponseDto
                {
                    Success = false,
                    Message = "用户未绑定Xbox平台或令牌不存在，需要首次认证",
                    TokenExists = false,
                    NeedsBrowserAuth = true
                };
            }

            // 尝试使用令牌获取数据（验证令牌有效性）
            var xboxData = await GetXboxDataFromPython(userId, platformId);
            
            if (xboxData != null && xboxData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var xuid = xboxData.RootElement.TryGetProperty("xuid", out var xuidProp) ? xuidProp.GetString() : null;
                return new XboxAuthResponseDto
                {
                    Success = true,
                    Message = "令牌有效",
                    TokenExists = true,
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
    /// 执行Xbox认证（首次认证，生成并保存令牌到数据库）
    /// </summary>
    public async Task<XboxAuthResponseDto> AuthenticateXbox(XboxAuthRequestDto request, int userId)
    {
        string? tempTokenPath = null;
        try
        {
            _logger.LogInformation("开始Xbox认证: userId={UserId}, OpenBrowser={OpenBrowser}", userId, request.OpenBrowser);

            // 使用临时文件进行认证
            tempTokenPath = Path.Combine(_tokensPath, $"xbox_tokens_auth_{userId}_{Guid.NewGuid():N}.json");
            
            // 如果强制重新认证，删除数据库中的旧令牌
            if (request.ForceReauth)
            {
                var binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 7);
                if (binding != null)
                {
                    binding.AccessToken = null;
                    binding.BindingStatus = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已删除数据库中的旧令牌");
                }
            }

            // 如果不需要打开浏览器，先检查令牌是否存在且有效
            if (!request.OpenBrowser)
            {
                var binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 7 && b.BindingStatus == true);
                
                if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
                {
                    return new XboxAuthResponseDto
                    {
                        Success = false,
                        Message = "令牌不存在，请先进行首次认证（设置 openBrowser=true）",
                        TokenExists = false,
                        NeedsBrowserAuth = true
                    };
                }

                // 尝试刷新令牌
                _logger.LogInformation("尝试刷新现有令牌");
                var xboxData = await GetXboxDataFromPython(userId, 7);
                
                if (xboxData != null && xboxData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    var refreshedXuid = xboxData.RootElement.TryGetProperty("xuid", out var xuidProp) ? xuidProp.GetString() : null;
                    return new XboxAuthResponseDto
                    {
                        Success = true,
                        Message = "令牌刷新成功",
                        TokenExists = true,
                        Xuid = refreshedXuid,
                        NeedsBrowserAuth = false
                    };
                }
                else
                {
                    return new XboxAuthResponseDto
                    {
                        Success = false,
                        Message = "令牌刷新失败，令牌可能已过期。请设置 openBrowser=true 重新认证",
                        TokenExists = true,
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
                    TokenExists = false,
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
            
            var arguments = $"--tokens \"{tempTokenPath}\" --port 8080";
            
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
                    
                    // 尝试从输出中解析错误信息
                    if (!string.IsNullOrEmpty(output))
                    {
                        try
                        {
                            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            var jsonLine = lines.LastOrDefault(l => l.Trim().StartsWith("{"));
                            
                            if (!string.IsNullOrEmpty(jsonLine))
                            {
                                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonLine);
                                if (result != null && result.ContainsKey("message"))
                                {
                                    var msg = result["message"].GetString();
                                    if (!string.IsNullOrEmpty(msg))
                                    {
                                        errorMessage = msg;
                                    }
                                    
                                    // 如果有更详细的错误信息，也包含进来
                                    if (result.ContainsKey("error_type"))
                                    {
                                        var errorType = result["error_type"].GetString();
                                        errorMessage = $"{errorType}: {errorMessage}";
                                    }
                                    
                                    if (result.ContainsKey("traceback"))
                                    {
                                        var traceback = result["traceback"].GetString();
                                        if (!string.IsNullOrEmpty(traceback))
                                        {
                                            _logger.LogError("Python脚本详细错误堆栈: {Traceback}", traceback);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "解析Python错误输出失败，使用原始错误信息");
                        }
                    }
                    
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
                        TokenExists = false,
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
                    TokenExists = false,
                    NeedsBrowserAuth = true
                };
            }

            // 解析输出
            string? xuid = null;
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
                    xuid = result.ContainsKey("xuid") ? result["xuid"].GetString() : null;
                    
                    // 认证成功，读取令牌文件并保存到数据库
                    if (File.Exists(tempTokenPath))
                    {
                        try
                        {
                            var tokenJson = await File.ReadAllTextAsync(tempTokenPath);
                            
                            // 如果xuid为空，尝试从令牌JSON中解析
                            if (string.IsNullOrEmpty(xuid))
                            {
                                try
                                {
                                    var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                                    if (tokenData.TryGetProperty("xsts_token", out var xstsToken) && 
                                        xstsToken.TryGetProperty("xuid", out var xuidElement))
                                    {
                                        xuid = xuidElement.GetString();
                                        _logger.LogInformation("从令牌中提取到XUID: {Xuid}", xuid);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "从令牌中提取XUID失败，将使用临时标识符");
                                }
                            }
                            
                            // 保存令牌到数据库（即使xuid为空也会保存，使用临时标识符）
                            var saveSuccess = await SaveTokenToDatabase(userId, 7, tokenJson, xuid);
                            if (saveSuccess)
                            {
                                _logger.LogInformation("令牌已保存到数据库: UserId={UserId}, Xuid={Xuid}", userId, xuid ?? "临时标识符");
                                
                                // 如果xuid仍然为空，尝试通过API获取
                                if (string.IsNullOrEmpty(xuid))
                                {
                                    try
                                    {
                                        // 使用刚保存的令牌获取用户信息
                                        var xboxUser = await GetXboxUser("me", userId);
                                        if (xboxUser != null && !string.IsNullOrEmpty(xboxUser.Xuid))
                                        {
                                            xuid = xboxUser.Xuid;
                                            _logger.LogInformation("通过API获取到XUID: {Xuid}", xuid);
                                            
                                            // 更新绑定记录中的PlatformUserId
                                            var binding = await _context.UserPlatformBindings
                                                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 7);
                                            if (binding != null && binding.PlatformUserId != xuid)
                                            {
                                                binding.PlatformUserId = xuid;
                                                await _context.SaveChangesAsync();
                                                _logger.LogInformation("已更新绑定记录中的PlatformUserId: {Xuid}", xuid);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "通过API获取XUID失败，将使用临时标识符");
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning("令牌保存失败，但认证已成功");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "保存令牌到数据库失败");
                        }
                    }
                    
                    return new XboxAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        Xuid = xuid,
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
                        TokenExists = false,
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
                    TokenExists = false,
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
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempTokenPath);
        }
    }

    /// <summary>
    /// 获取Xbox游戏成就数据
    /// </summary>
    private async Task<JsonDocument?> GetXboxGameAchievementsFromPython(int userId, string xuid, string titleId)
    {
        string? tempFilePath = null;
        try
        {
            // 从数据库加载令牌到临时文件
            tempFilePath = await LoadTokenFromDatabase(userId, 7);
            
            if (tempFilePath == null)
            {
                _logger.LogWarning("无法加载用户{UserId}的令牌", userId);
                return null;
            }

            var arguments = $"--tokens \"{tempFilePath}\" --xuid {xuid} --title-id {titleId}";
            var (exitCode, output, error) = await RunPythonScript("xbox_get_achievements.py", arguments);

            _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}", exitCode);
            
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Python输出长度: {Length} 字符", output.Length);
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
                    _logger.LogError("Python脚本没有输出任何内容，ExitCode={ExitCode}, Error={Error}", exitCode, error);
                    return null;
                }
                
                // 方法1：直接查找第一个 { 和最后一个 }，提取完整的 JSON
                var firstBraceIdx = output.IndexOf('{');
                var lastBraceIdx = output.LastIndexOf('}');
                
                if (firstBraceIdx >= 0 && lastBraceIdx > firstBraceIdx)
                {
                    var jsonContent = output.Substring(firstBraceIdx, lastBraceIdx - firstBraceIdx + 1);
                    _logger.LogInformation("从输出中提取 JSON: 起始位置={Start}, 结束位置={End}, 长度={Length}", 
                        firstBraceIdx, lastBraceIdx, jsonContent.Length);
                    
                    try
                    {
                        return JsonDocument.Parse(jsonContent);
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "直接提取的 JSON 解析失败，尝试清理后重新解析");
                        
                        // 方法2：清理可能的日志行后重新提取
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        var jsonLines = new List<string>();
                        bool inJson = false;
                        
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            
                            // 跳过日志行
                            if (trimmedLine.StartsWith("INFO:") || 
                                trimmedLine.StartsWith("WARNING:") || 
                                trimmedLine.StartsWith("ERROR:") ||
                                trimmedLine.StartsWith("DEBUG:"))
                            {
                                continue;
                            }
                            
                            // 检测 JSON 开始
                            if (!inJson && trimmedLine.StartsWith("{"))
                            {
                                inJson = true;
                            }
                            
                            if (inJson)
                            {
                                jsonLines.Add(line);
                                
                                // 如果行以 } 结尾，可能是 JSON 结束
                                if (trimmedLine.EndsWith("}"))
                                {
                                    // 检查是否所有括号都闭合
                                    var testContent = string.Join("\n", jsonLines);
                                    int testBraces = 0;
                                    foreach (var ch in testContent)
                                    {
                                        if (ch == '{') testBraces++;
                                        if (ch == '}') testBraces--;
                                    }
                                    if (testBraces == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        
                        var cleanedJson = string.Join("\n", jsonLines);
                        if (!string.IsNullOrWhiteSpace(cleanedJson))
                        {
                            _logger.LogInformation("使用清理后的 JSON: 长度={Length}", cleanedJson.Length);
                            return JsonDocument.Parse(cleanedJson);
                        }
                        
                        // 如果还是失败，记录错误并返回 null
                        _logger.LogError("无法从Python输出中提取有效的JSON内容");
                        _logger.LogDebug("原始输出前1000字符: {Output}", output.Length > 1000 ? output.Substring(0, 1000) : output);
                        _logger.LogDebug("原始输出后1000字符: {Output}", output.Length > 1000 ? output.Substring(output.Length - 1000) : output);
                        return null;
                    }
                }
                else
                {
                    _logger.LogError("无法在Python输出中找到JSON内容（未找到 { 或 }）");
                    _logger.LogDebug("原始输出前500字符: {Output}", output.Length > 500 ? output.Substring(0, 500) : output);
                    return null;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "解析Xbox成就数据JSON失败: ExitCode={ExitCode}", exitCode);
                _logger.LogDebug("输出的后1000字符: {Output}", output.Length > 1000 ? output.Substring(output.Length - 1000) : output);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析Xbox成就数据时发生未预期的错误: ExitCode={ExitCode}", exitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox游戏成就数据时发生错误");
            return null;
        }
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempFilePath);
        }
    }

    /// <summary>
    /// 获取Xbox数据（支持用户级令牌）
    /// </summary>
    private async Task<JsonDocument?> GetXboxDataFromPython(int userId, int platformId = 7)
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
            var (exitCode, output, error) = await RunPythonScript("xbox_get_data.py", arguments);

            _logger.LogInformation("Python脚本执行完成: ExitCode={ExitCode}", exitCode);
            
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Python输出长度: {Length} 字符", output.Length);
                _logger.LogInformation("Python完整输出: {Output}", output);
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
                    _logger.LogInformation("完整输出: {Output}", output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("错误信息: {Error}", error);
                    }
                    return null;
                }
                
                // 重新组合JSON字符串
                var jsonString = string.Join("\n", jsonLines);
                
                _logger.LogInformation("准备解析JSON，长度: {Length} 字符，内容: {JsonString}", jsonString.Length, jsonString);
                
                var doc = JsonDocument.Parse(jsonString);
                
                // 如果令牌被更新，保存回数据库
                if (File.Exists(tempFilePath))
                {
                    var updatedToken = await File.ReadAllTextAsync(tempFilePath);
                    if (updatedToken != await File.ReadAllTextAsync(tempFilePath))
                    {
                        await SaveTokenToDatabase(userId, platformId, updatedToken);
                    }
                }
                
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
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempFilePath);
        }
    }

    /// <summary>
    /// 导入Xbox数据
    /// </summary>
    public async Task<XboxImportResponseDto> ImportXboxData(XboxImportRequestDto request, int userId)
    {
        try
        {
            _logger.LogInformation("开始导入Xbox数据: xboxUserId={XboxUserId}, userId={UserId}", request.XboxUserId, userId);

            var taskId = $"import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取Xbox数据
            _logger.LogInformation("正在调用Python脚本获取Xbox数据...");
            var xboxData = await GetXboxDataFromPython(userId, 7);
            
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
    public async Task<XboxUserDto?> GetXboxUser(string xuid, int userId)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户信息: xuid={Xuid}, userId={UserId}", xuid, userId);

            var xboxData = await GetXboxDataFromPython(userId, 7);
            
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
    public async Task<XboxGameDto?> GetXboxGame(string titleId, int userId)
    {
        try
        {
            _logger.LogInformation("获取Xbox游戏信息: titleId={TitleId}, userId={UserId}", titleId, userId);

            var xboxData = await GetXboxDataFromPython(userId, 7);
            
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
    public async Task<List<XboxGameDto>> GetXboxUserGames(string xuid, int userId)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户游戏列表: xuid={Xuid}, userId={UserId}", xuid, userId);

            var xboxData = await GetXboxDataFromPython(userId, 7);
            
            if (xboxData == null)
            {
                return new List<XboxGameDto>();
            }

            var games = new List<XboxGameDto>();

            // 从title_history中提取游戏信息
            if (xboxData.RootElement.TryGetProperty("title_history", out var titleHistory))
            {
                _logger.LogInformation("找到title_history节点");
                
                // 检查是否有错误
                if (titleHistory.TryGetProperty("error", out var error))
                {
                    var errorMsg = error.GetString();
                    _logger.LogWarning("title_history包含错误信息: {Error}", errorMsg);
                }
                
                if (titleHistory.TryGetProperty("titles", out var titles))
                {
                    var titlesCount = titles.GetArrayLength();
                    _logger.LogInformation("找到 {Count} 个title记录", titlesCount);
                    
                    if (titlesCount == 0)
                    {
                        _logger.LogWarning("title_history.titles数组为空，可能用户没有游戏或API返回为空");
                    }
                    
                    int processedCount = 0;
                    int skippedCount = 0;
                    
                    foreach (var title in titles.EnumerateArray())
                    {
                        var titleId = title.TryGetProperty("title_id", out var tid) ? tid.GetString() ?? "" : "";
                        var name = title.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                        var type = title.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "game" : "game";
                        
                        _logger.LogDebug("处理title: TitleId={TitleId}, Name={Name}, Type={Type}", titleId, name, type);
                        
                        // 跳过非游戏类型（可选：如果需要只同步游戏）
                        // if (!string.IsNullOrEmpty(type) && type.ToLower() != "game")
                        // {
                        //     _logger.LogDebug("跳过非游戏类型: Type={Type}", type);
                        //     skippedCount++;
                        //     continue;
                        // }
                        
                        if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(name))
                        {
                            _logger.LogDebug("跳过无效title: TitleId={TitleId}, Name={Name}", titleId, name);
                            skippedCount++;
                            continue;
                        }

                        var game = new XboxGameDto
                        {
                            TitleId = titleId,
                            Name = name,
                            Type = type, // 使用上面已经定义的type变量
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
                        processedCount++;
                        _logger.LogDebug("已添加游戏: {Name} (TitleId: {TitleId})", game.Name, game.TitleId);
                    }
                    
                    _logger.LogInformation("处理完成: 总计={Total}, 已处理={Processed}, 已跳过={Skipped}", 
                        titlesCount, processedCount, skippedCount);
                }
                else
                {
                    _logger.LogWarning("title_history中没有titles数组");
                }
            }
            else
            {
                _logger.LogWarning("Xbox数据中没有title_history节点");
                // 输出所有可用的根节点，帮助调试
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var propertyNames = xboxData.RootElement.EnumerateObject().Select(p => p.Name).ToList();
                    _logger.LogDebug("可用的根节点: {Properties}", string.Join(", ", propertyNames));
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
    /// 获取Xbox游戏成就列表和玩家解锁状态
    /// </summary>
    public async Task<List<XboxGameAchievementDto>> GetXboxGameAchievements(string xuid, int userId, string titleId)
    {
        try
        {
            _logger.LogInformation("获取Xbox游戏成就: xuid={Xuid}, userId={UserId}, titleId={TitleId}", xuid, userId, titleId);

            var achievementsData = await GetXboxGameAchievementsFromPython(userId, xuid, titleId);
            
            if (achievementsData == null)
            {
                _logger.LogWarning("无法获取游戏成就数据: titleId={TitleId}", titleId);
                return new List<XboxGameAchievementDto>();
            }

            // 检查是否成功
            if (achievementsData.RootElement.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var errorMsg = achievementsData.RootElement.TryGetProperty("message", out var msg) 
                    ? msg.GetString() ?? "未知错误" 
                    : "未知错误";
                _logger.LogWarning("获取游戏成就失败: titleId={TitleId}, error={Error}", titleId, errorMsg);
                return new List<XboxGameAchievementDto>();
            }

            var achievements = new List<XboxGameAchievementDto>();

            if (achievementsData.RootElement.TryGetProperty("achievements", out var achievementsArray))
            {
                foreach (var achElement in achievementsArray.EnumerateArray())
                {
                    try
                    {
                        var ach = new XboxGameAchievementDto
                        {
                            Id = achElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
                            Name = achElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                            Description = achElement.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "",
                            LockedDescription = achElement.TryGetProperty("locked_description", out var lockedDescProp) ? lockedDescProp.GetString() ?? "" : "",
                            ProgressState = achElement.TryGetProperty("progress_state", out var stateProp) ? stateProp.GetString() ?? "" : "",
                            IsSecret = achElement.TryGetProperty("is_secret", out var secretProp) && secretProp.GetBoolean(),
                            IsUnlocked = achElement.TryGetProperty("is_unlocked", out var unlockedProp) && unlockedProp.GetBoolean(),
                            UnlockTime = achElement.TryGetProperty("unlock_time", out var timeProp) ? timeProp.GetString() : null,
                            Gamerscore = achElement.TryGetProperty("gamerscore", out var scoreProp) ? SafeGetInt32(scoreProp) : 0,
                            IconUnlocked = achElement.TryGetProperty("icon_unlocked", out var iconUnlockedProp) ? iconUnlockedProp.GetString() : null,
                            IconLocked = achElement.TryGetProperty("icon_locked", out var iconLockedProp) ? iconLockedProp.GetString() : null
                        };

                        if (!string.IsNullOrEmpty(ach.Id))
                        {
                            achievements.Add(ach);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "解析成就数据失败: titleId={TitleId}", titleId);
                    }
                }
            }

            _logger.LogInformation("成功获取 {Count} 个游戏成就: titleId={TitleId}", achievements.Count, titleId);
            return achievements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Xbox游戏成就时发生错误: titleId={TitleId}", titleId);
            return new List<XboxGameAchievementDto>();
        }
    }

    /// <summary>
    /// 获取Xbox用户成就
    /// </summary>
    public async Task<List<XboxUserAchievementDto>> GetXboxUserAchievements(string xuid, int userId)
    {
        try
        {
            _logger.LogInformation("获取Xbox用户成就: xuid={Xuid}, userId={UserId}", xuid, userId);

            var xboxData = await GetXboxDataFromPython(userId, 7);
            
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

