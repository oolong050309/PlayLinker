using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// PSN API集成服务实现
/// 通过Node.js脚本桥接psn-api
/// </summary>
public class PsnService : IPsnService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PsnService> _logger;
    private readonly ITokenEncryptionService _encryptionService;
    private readonly PlayLinkerDbContext _context;
    private readonly string _nodePath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public PsnService(
        IConfiguration configuration, 
        ILogger<PsnService> logger, 
        IWebHostEnvironment environment,
        ITokenEncryptionService encryptionService,
        PlayLinkerDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
        _context = context;

        // 获取Node.js路径(从配置或环境变量)
        _nodePath = configuration["PsnAPI:NodePath"] ?? "node";
        
        // 脚本路径: Backend/Scripts (PSN使用Node.js)
        _scriptsPath = Path.Combine(environment.ContentRootPath, "Scripts");
        
        // 令牌路径: Backend/Tokens
        _tokensPath = Path.Combine(environment.ContentRootPath, "Tokens");

        // 确保目录存在
        Directory.CreateDirectory(_scriptsPath);
        Directory.CreateDirectory(_tokensPath);

        _logger.LogInformation("PsnService 初始化: NodePath={NodePath}, ScriptsPath={ScriptsPath}, TokensPath={TokensPath}",
            _nodePath, _scriptsPath, _tokensPath);
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
    /// 执行Node.js脚本
    /// </summary>
    private async Task<(int exitCode, string output, string error)> RunNodeScript(string scriptName, string arguments)
    {
        var scriptPath = Path.Combine(_scriptsPath, scriptName);
        
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Node.js脚本不存在: {scriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _nodePath,
            Arguments = $"\"{scriptPath}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _scriptsPath,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        _logger.LogInformation("执行Node.js脚本: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                
                if (e.Data.StartsWith("INFO:") || e.Data.StartsWith("WARNING:"))
                {
                    _logger.LogInformation("[Node.js] {Message}", e.Data);
                }
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogWarning("[Node.js Error] {Message}", e.Data);
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
            throw new TimeoutException("Node.js脚本执行超时(5分钟)");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        
        _logger.LogInformation("Node.js脚本执行完成: ExitCode={ExitCode}, OutputLength={OutputLength}, ErrorLength={ErrorLength}", 
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
        return Path.Combine(_tokensPath, "psn_tokens.json");
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
            var tempFilePath = Path.Combine(_tokensPath, $"psn_tokens_{userId}_{Guid.NewGuid():N}.json");
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
    /// 保存令牌到数据库（如果绑定不存在则创建）
    /// </summary>
    private async Task<bool> SaveTokenToDatabase(int userId, int platformId, string tokenJson, string? onlineId = null)
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
                binding.LastSyncTime = DateTime.UtcNow;
                binding.ExpireTime = DateTime.UtcNow.AddYears(1);
                binding.BindingStatus = true;
                
                // 如果提供了OnlineId，更新PlatformUserId
                if (!string.IsNullOrEmpty(onlineId))
                {
                    binding.PlatformUserId = onlineId;
                }
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("令牌已更新到数据库: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
                return true;
            }
            
            // 绑定不存在，需要创建
            if (string.IsNullOrEmpty(onlineId))
            {
                _logger.LogWarning("未找到绑定记录且未提供OnlineId，无法创建绑定: UserId={UserId}, PlatformId={PlatformId}", userId, platformId);
                return false;
            }
            
            // 先确保PlayerPlatform记录存在（外键约束要求）
            var playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == onlineId && pp.PlatformId == platformId);
            
            if (playerPlatform == null)
            {
                // 注意：此时令牌还未保存，无法调用GetPsnUser（需要令牌）
                // 先使用基本信息创建PlayerPlatform记录，后续可以通过同步更新
                _logger.LogInformation("创建PlayerPlatform记录: OnlineId={OnlineId}", onlineId);
                playerPlatform = new PlayerPlatform
                {
                    PlatformUserId = onlineId,
                    PlatformId = platformId,
                    ProfileName = onlineId  // 暂时使用OnlineId作为ProfileName，后续可以更新
                };
                _context.PlayerPlatforms.Add(playerPlatform);
                
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已创建PlayerPlatform记录: OnlineId={OnlineId}", onlineId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "创建PlayerPlatform记录失败: OnlineId={OnlineId}", onlineId);
                    throw; // 重新抛出异常，因为这是必需的
                }
            }
            
            // 确保playerPlatform已保存（重新查询以确保数据一致性）
            playerPlatform = await _context.PlayerPlatforms
                .FirstOrDefaultAsync(pp => pp.PlatformUserId == onlineId && pp.PlatformId == platformId);

            if (playerPlatform == null)
            {
                _logger.LogError("PlayerPlatform记录不存在，无法创建绑定: OnlineId={OnlineId}, PlatformId={PlatformId}", onlineId, platformId);
                return false;
            }
            
            // 创建新的绑定记录
            var encryptedTokenForNewBinding = _encryptionService.EncryptToken(tokenJson);
            binding = new UserPlatformBinding
            {
                UserId = userId,
                PlatformId = platformId,
                PlatformUserId = onlineId,  // 必须与playerPlatform.PlatformUserId完全一致
                AccessToken = encryptedTokenForNewBinding,
                BindingStatus = true,
                BindingTime = DateTime.UtcNow,
                LastSyncTime = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddYears(1) // PSN令牌有效期1年
            };
            
            _context.UserPlatformBindings.Add(binding);
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存UserPlatformBinding失败: OnlineId={OnlineId}, PlatformId={PlatformId}, PlayerPlatform存在={PlayerPlatformExists}", 
                    onlineId, platformId, playerPlatform != null);
                throw;
            }
            
            _logger.LogInformation("已创建绑定记录并保存令牌: UserId={UserId}, PlatformId={PlatformId}, OnlineId={OnlineId}", userId, platformId, onlineId);
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
    /// 测试Node.js环境
    /// </summary>
    private async Task<(bool success, string message)> TestNodeEnvironment()
    {
        try
        {
            // 测试Node.js是否可用
            var startInfo = new ProcessStartInfo
            {
                FileName = _nodePath,
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
                return (false, $"Node.js执行失败: {error}");
            }

            _logger.LogInformation("Node.js版本: {Version}", output.Trim());

            // 测试依赖 - 设置工作目录为Scripts文件夹
            startInfo.Arguments = "-e \"require('psn-api'); console.log('psn-api已安装')\"";
            startInfo.WorkingDirectory = _scriptsPath; // 关键：设置工作目录
            using var depProcess = new Process { StartInfo = startInfo };
            depProcess.Start();
            var depOutput = await depProcess.StandardOutput.ReadToEndAsync();
            var depError = await depProcess.StandardError.ReadToEndAsync();
            await depProcess.WaitForExitAsync();

            if (depProcess.ExitCode != 0)
            {
                return (false, $"psn-api未安装。请在Backend/Scripts目录执行: npm install\n详细错误: {depError}");
            }

            _logger.LogInformation("依赖检查: {Result}", depOutput.Trim());
            return (true, "Node.js环境正常");
        }
        catch (Exception ex)
        {
            return (false, $"测试Node.js环境失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查令牌状态（从数据库）
    /// </summary>
    public async Task<PsnAuthResponseDto> CheckTokenStatus(int userId, int platformId = 6)
    {
        try
        {
            var binding = await _context.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == platformId && b.BindingStatus == true);

            if (binding == null || string.IsNullOrEmpty(binding.AccessToken))
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "用户未绑定PSN平台或令牌不存在，需要首次认证",
                    TokenExists = false
                };
            }

            // 尝试使用令牌获取数据（验证令牌有效性）
            var psnData = await GetPsnDataFromNode(userId, platformId);
            
            if (psnData != null && psnData.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var accountId = psnData.RootElement.TryGetProperty("accountId", out var accId) ? accId.GetString() : null;
                var onlineId = psnData.RootElement.TryGetProperty("profile", out var profile) && 
                              profile.TryGetProperty("onlineId", out var oid) ? oid.GetString() : null;
                
                return new PsnAuthResponseDto
                {
                    Success = true,
                    Message = "令牌有效",
                    TokenExists = true,
                    AccountId = accountId,
                    OnlineId = onlineId
                };
            }
            else
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "令牌已过期或无效，需要重新认证",
                    TokenExists = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查令牌状态时发生错误");
            return new PsnAuthResponseDto
            {
                Success = false,
                Message = $"检查失败: {ex.Message}",
                TokenExists = false
            };
        }
    }

    /// <summary>
    /// 执行PSN认证
    /// </summary>
    public async Task<PsnAuthResponseDto> AuthenticatePsn(PsnAuthRequestDto request, int userId)
    {
        string? tempTokenPath = null;
        try
        {
            _logger.LogInformation("开始PSN认证: userId={UserId}", userId);

            if (string.IsNullOrWhiteSpace(request.Npsso))
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "NPSSO令牌不能为空。请访问 https://ca.account.sony.com/api/v1/ssocookie 获取NPSSO",
                    TokenExists = false
                };
            }

            // 使用临时文件进行认证
            tempTokenPath = Path.Combine(_tokensPath, $"psn_tokens_auth_{userId}_{Guid.NewGuid():N}.json");
            
            // 如果强制重新认证，删除数据库中的旧令牌
            if (request.ForceReauth)
            {
                var binding = await _context.UserPlatformBindings
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 6);
                if (binding != null)
                {
                    binding.AccessToken = null;
                    binding.BindingStatus = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("已删除数据库中的旧令牌");
                }
            }

            // 测试Node.js环境
            _logger.LogInformation("检查Node.js环境...");
            var (envSuccess, envMessage) = await TestNodeEnvironment();
            if (!envSuccess)
            {
                    _logger.LogError("Node.js环境检查失败: {Message}", envMessage);
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = $"Node.js环境问题: {envMessage}",
                    TokenExists = false
                };
            }
            _logger.LogInformation("Node.js环境检查通过");

            // 执行认证脚本
            var arguments = $"--npsso \"{request.Npsso}\" --tokens \"{tempTokenPath}\"";
            
            int exitCode;
            string output;
            string error;
            
            try
            {
                (exitCode, output, error) = await RunNodeScript("psn_authenticate.js", arguments);

                _logger.LogInformation("Node.js脚本执行完成: ExitCode={ExitCode}", exitCode);
                
                if (!string.IsNullOrEmpty(output))
                {
                    _logger.LogInformation("Node.js输出: {Output}", output);
                }
                
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError("Node.js错误输出: {Error}", error);
                }

                if (exitCode != 0)
                {
                    var errorMessage = !string.IsNullOrEmpty(error) ? error : "Node.js脚本执行失败,未返回错误信息";
                    
                    // 检查是否是依赖问题
                    if (error.Contains("Cannot find module") || error.Contains("Error: Cannot find module"))
                    {
                        errorMessage = $"Node.js依赖缺失。请在Backend/Scripts目录执行: npm install\n原始错误: {error}";
                    }
                    else if (error.Contains("node") && error.Contains("not found"))
                    {
                        errorMessage = $"找不到Node.js。请检查appsettings.json中的NodePath配置,或确保Node.js已安装并在PATH中。\n原始错误: {error}";
                    }
                    
                    _logger.LogError("PSN认证失败: {ErrorMessage}", errorMessage);
                    return new PsnAuthResponseDto
                    {
                        Success = false,
                        Message = errorMessage,
                        TokenExists = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行Node.js脚本时发生异常");
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = $"执行认证脚本失败: {ex.Message}",
                    TokenExists = false
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
                    // 获取OnlineId（用于创建绑定）
                    string? onlineId = result.ContainsKey("onlineId") ? result["onlineId"].GetString() : null;
                    
                    // 认证成功，读取令牌文件并保存到数据库（需要onlineId来创建绑定记录）
                    if (File.Exists(tempTokenPath))
                    {
                        try
                        {
                            var tokenJson = await File.ReadAllTextAsync(tempTokenPath);
                            var saveSuccess = await SaveTokenToDatabase(userId, 6, tokenJson, onlineId);
                            if (saveSuccess)
                            {
                                _logger.LogInformation("令牌已保存到数据库: OnlineId={OnlineId}", onlineId);
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
                    
                    return new PsnAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        AccountId = result.ContainsKey("accountId") ? result["accountId"].GetString() : null,
                        OnlineId = onlineId,
                        TokenExists = true
                    };
                }
                else
                {
                    return new PsnAuthResponseDto
                    {
                        Success = false,
                        Message = result?.ContainsKey("message") == true ? result["message"].GetString() ?? "认证失败" : "认证失败",
                        TokenExists = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析认证结果失败: {Output}", output);
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = $"解析认证结果失败: {ex.Message}",
                    TokenExists = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PSN认证时发生错误");
            return new PsnAuthResponseDto
            {
                Success = false,
                Message = $"认证错误: {ex.Message}",
                TokenExists = false
            };
        }
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempTokenPath);
        }
    }

    /// <summary>
    /// 获取PSN数据
    /// </summary>
    /// <summary>
    /// 获取PSN数据（支持用户级令牌）
    /// </summary>
    private async Task<JsonDocument?> GetPsnDataFromNode(int userId, int platformId = 6)
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
            var (exitCode, output, error) = await RunNodeScript("psn_get_data.js", arguments);

            _logger.LogInformation("Node.js脚本执行完成: ExitCode={ExitCode}", exitCode);
            
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogInformation("Node.js输出长度: {Length} 字符", output.Length);
                _logger.LogDebug("Node.js完整输出: {Output}", output);
            }
            else
            {
                _logger.LogWarning("Node.js输出为空");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Node.js错误输出: {Error}", error);
            }

            // 解析JSON输出
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogError("Node.js脚本没有输出任何内容,ExitCode={ExitCode}, Error={Error}", exitCode, error);
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
                    _logger.LogError("Node.js脚本返回错误: Type={ErrorType}, Message={ErrorMessage}", errorType, errorMsg);
                }
                
                if (exitCode != 0)
                {
                    _logger.LogWarning("Node.js脚本返回非0退出码({ExitCode}),但成功解析了JSON输出", exitCode);
                }
                
                _logger.LogInformation("成功解析PSN数据JSON");
                
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
                _logger.LogError(ex, "解析PSN数据时发生未预期的错误: ExitCode={ExitCode}", exitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN数据时发生错误");
            return null;
        }
        finally
        {
            // 清理临时文件
            CleanupTempTokenFile(tempFilePath);
        }
    }

    /// <summary>
    /// 导入PSN数据
    /// </summary>
    public async Task<PsnImportResponseDto> ImportPsnData(PsnImportRequestDto request, int userId)
    {
        try
        {
            _logger.LogInformation("开始导入PSN数据: psnOnlineId={OnlineId}, userId={UserId}", request.PsnOnlineId, userId);

            var taskId = $"psn_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取PSN数据
            _logger.LogInformation("正在调用Node.js脚本获取PSN数据...");
            var psnData = await GetPsnDataFromNode(userId, 6);
            
            if (psnData == null)
            {
                var errorMsg = "获取PSN数据失败:Node.js脚本返回空数据。请检查:1) 令牌是否有效；2) 网络连接是否正常；3) Node.js环境是否配置正确。详细信息请查看服务器日志。";
                _logger.LogError(errorMsg);
                return new PsnImportResponseDto
                {
                    TaskId = taskId,
                    Status = "failed",
                    Message = errorMsg,
                    EstimatedTime = 0,
                    Items = new PsnImportItemsDto()
                };
            }

            // 检查返回的数据是否成功
            if (psnData.RootElement.TryGetProperty("success", out var success))
            {
                if (!success.GetBoolean())
                {
                    var errorMsg = psnData.RootElement.TryGetProperty("message", out var msg) 
                        ? msg.GetString() ?? "未知错误" 
                        : "未知错误";
                    var errorType = psnData.RootElement.TryGetProperty("error", out var err) 
                        ? err.GetString() ?? "unknown" 
                        : "unknown";
                    
                    var fullErrorMsg = $"获取PSN数据失败:{errorMsg} (错误类型: {errorType})";
                    _logger.LogError(fullErrorMsg);
                    
                    return new PsnImportResponseDto
                    {
                        TaskId = taskId,
                        Status = "failed",
                        Message = fullErrorMsg,
                        EstimatedTime = 0,
                        Items = new PsnImportItemsDto()
                    };
                }
            }

            // 解析数据并统计
            int gamesCount = 0;
            int trophiesCount = 0;

            // 统计游戏数量
            if (psnData.RootElement.TryGetProperty("userTitles", out var userTitles))
            {
                if (userTitles.TryGetProperty("trophyTitles", out var trophyTitles))
                {
                    gamesCount = trophyTitles.GetArrayLength();
                    _logger.LogInformation("找到 {Count} 个游戏", gamesCount);
                }
            }

            // 统计奖杯数量
            if (psnData.RootElement.TryGetProperty("trophySummary", out var trophySummary))
            {
                if (trophySummary.TryGetProperty("earnedTrophies", out var earnedTrophies))
                {
                    var bronze = earnedTrophies.TryGetProperty("bronze", out var b) ? SafeGetInt32(b) : 0;
                    var silver = earnedTrophies.TryGetProperty("silver", out var s) ? SafeGetInt32(s) : 0;
                    var gold = earnedTrophies.TryGetProperty("gold", out var g) ? SafeGetInt32(g) : 0;
                    var platinum = earnedTrophies.TryGetProperty("platinum", out var p) ? SafeGetInt32(p) : 0;
                    trophiesCount = bronze + silver + gold + platinum;
                }
            }
            
            _logger.LogInformation("统计完成: {GamesCount} 个游戏, {TrophiesCount} 个奖杯", 
                gamesCount, trophiesCount);

            _logger.LogInformation("成功导入PSN数据: {GamesCount} 个游戏, {TrophiesCount} 个奖杯", 
                gamesCount, trophiesCount);

            return new PsnImportResponseDto
            {
                TaskId = taskId,
                Status = "completed",
                Message = $"成功导入 {gamesCount} 个游戏和 {trophiesCount} 个奖杯",
                EstimatedTime = 0,
                Items = new PsnImportItemsDto
                {
                    Games = gamesCount,
                    Achievements = trophiesCount
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入PSN数据时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取PSN用户信息
    /// </summary>
    public async Task<PsnUserDto?> GetPsnUser(string onlineId, int userId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户信息: onlineId={OnlineId}, userId={UserId}", onlineId, userId);

            var psnData = await GetPsnDataFromNode(userId, 6);
            
            if (psnData == null)
            {
                return null;
            }

            // 解析用户资料
            if (psnData.RootElement.TryGetProperty("profile", out var profile))
            {
                var user = new PsnUserDto
                {
                    OnlineId = profile.TryGetProperty("onlineId", out var oid) ? oid.GetString() ?? "" : "",
                    ProfileUrl = $"https://psnprofiles.com/{onlineId}",
                    AvatarUrl = profile.TryGetProperty("avatarUrls", out var avatars) && 
                               avatars.ValueKind == JsonValueKind.Array && avatars.GetArrayLength() > 0
                               ? avatars[0].TryGetProperty("avatarUrl", out var url) ? url.GetString() ?? "" : ""
                               : "",
                    AccountCreated = null,
                    Country = "",
                    GamesOwned = 0,
                    Level = 0,
                    IsPublic = true
                };

                // 获取奖杯统计
                if (psnData.RootElement.TryGetProperty("trophySummary", out var trophySummary))
                {
                    if (trophySummary.TryGetProperty("trophyLevel", out var level))
                    {
                        user.Level = SafeGetInt32(level);
                    }

                    if (trophySummary.TryGetProperty("earnedTrophies", out var earnedTrophies))
                    {
                        user.TrophySummary = new PsnTrophySummaryDto
                        {
                            Bronze = earnedTrophies.TryGetProperty("bronze", out var bronze) ? SafeGetInt32(bronze) : 0,
                            Silver = earnedTrophies.TryGetProperty("silver", out var silver) ? SafeGetInt32(silver) : 0,
                            Gold = earnedTrophies.TryGetProperty("gold", out var gold) ? SafeGetInt32(gold) : 0,
                            Platinum = earnedTrophies.TryGetProperty("platinum", out var platinum) ? SafeGetInt32(platinum) : 0
                        };
                        user.TrophySummary.Total = user.TrophySummary.Bronze + user.TrophySummary.Silver + 
                                                    user.TrophySummary.Gold + user.TrophySummary.Platinum;
                    }
                }

                // 计算拥有游戏数量
                if (psnData.RootElement.TryGetProperty("userTitles", out var userTitles))
                {
                    if (userTitles.TryGetProperty("trophyTitles", out var titles))
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
            _logger.LogError(ex, "获取PSN用户信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取PSN游戏信息
    /// </summary>
    public async Task<PsnGameDto?> GetPsnGame(string titleId, int userId)
    {
        try
        {
            _logger.LogInformation("获取PSN游戏信息: titleId={TitleId}, userId={UserId}", titleId, userId);

            var psnData = await GetPsnDataFromNode(userId, 6);
            
            if (psnData == null)
            {
                return null;
            }

            // 从userTitles中查找游戏
            if (psnData.RootElement.TryGetProperty("userTitles", out var userTitles))
            {
                if (userTitles.TryGetProperty("trophyTitles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        if (title.TryGetProperty("npCommunicationId", out var npId) && npId.GetString() == titleId)
                        {
                            var game = new PsnGameDto
                            {
                                TitleId = titleId,
                                Name = title.TryGetProperty("trophyTitleName", out var name) ? name.GetString() ?? "" : "",
                                Type = "game",
                                IsFree = false,
                                HeaderImage = title.TryGetProperty("trophyTitleIconUrl", out var icon) ? icon.GetString() ?? "" : "",
                                TrophyTitlePlatform = title.TryGetProperty("trophyTitlePlatform", out var platform) ? platform.GetString() : null,
                                Progress = title.TryGetProperty("progress", out var progress) ? SafeGetInt32(progress) : 0
                            };

                            // 解析成就信息
                            if (title.TryGetProperty("definedTrophies", out var definedTrophies))
                            {
                                var bronze = definedTrophies.TryGetProperty("bronze", out var b) ? SafeGetInt32(b) : 0;
                                var silver = definedTrophies.TryGetProperty("silver", out var s) ? SafeGetInt32(s) : 0;
                                var gold = definedTrophies.TryGetProperty("gold", out var g) ? SafeGetInt32(g) : 0;
                                var platinum = definedTrophies.TryGetProperty("platinum", out var p) ? SafeGetInt32(p) : 0;
                                
                                game.Achievements = new PsnAchievementsInfoDto
                                {
                                    Total = bronze + silver + gold + platinum
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
            _logger.LogError(ex, "获取PSN游戏信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 获取PSN用户的游戏列表(用于导入)
    /// </summary>
    public async Task<List<PsnGameDto>> GetPsnUserGames(string onlineId, int userId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户游戏列表: onlineId={OnlineId}, userId={UserId}", onlineId, userId);

            var psnData = await GetPsnDataFromNode(userId, 6);
            
            if (psnData == null)
            {
                return new List<PsnGameDto>();
            }

            var games = new List<PsnGameDto>();

            // 从userTitles中提取游戏信息
            if (psnData.RootElement.TryGetProperty("userTitles", out var userTitles))
            {
                if (userTitles.TryGetProperty("trophyTitles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        var npCommunicationId = title.TryGetProperty("npCommunicationId", out var npId) ? npId.GetString() ?? "" : "";
                        var gameName = title.TryGetProperty("trophyTitleName", out var nameEl) ? nameEl.GetString() ?? "" : "";
                        
                        if (string.IsNullOrEmpty(npCommunicationId) || string.IsNullOrEmpty(gameName))
                        {
                            continue;
                        }

                        var game = new PsnGameDto
                        {
                            TitleId = npCommunicationId,
                            Name = gameName,
                            Type = "game",
                            IsFree = false,
                            HeaderImage = title.TryGetProperty("trophyTitleIconUrl", out var icon) ? icon.GetString() ?? "" : "",
                            TrophyTitlePlatform = title.TryGetProperty("trophyTitlePlatform", out var platform) ? platform.GetString() : null,
                            Progress = title.TryGetProperty("progress", out var progress) ? SafeGetInt32(progress) : 0
                        };

                        // 解析成就(奖杯)信息
                        if (title.TryGetProperty("definedTrophies", out var definedTrophies))
                        {
                            var bronze = definedTrophies.TryGetProperty("bronze", out var b) ? SafeGetInt32(b) : 0;
                            var silver = definedTrophies.TryGetProperty("silver", out var s) ? SafeGetInt32(s) : 0;
                            var gold = definedTrophies.TryGetProperty("gold", out var g) ? SafeGetInt32(g) : 0;
                            var platinum = definedTrophies.TryGetProperty("platinum", out var p) ? SafeGetInt32(p) : 0;
                            
                            game.Achievements = new PsnAchievementsInfoDto
                            {
                                Total = bronze + silver + gold + platinum
                            };
                        }

                        games.Add(game);
                    }
                }
            }

            _logger.LogInformation("成功获取 {Count} 个PSN游戏", games.Count);
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN用户游戏列表时发生错误");
            return new List<PsnGameDto>();
        }
    }

    /// <summary>
    /// 获取PSN用户奖杯
    /// </summary>
    public async Task<PsnUserTrophiesResponseDto> GetPsnUserTrophies(string onlineId, int userId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户奖杯: onlineId={OnlineId}, userId={UserId}", onlineId, userId);

            var psnData = await GetPsnDataFromNode(userId, 6);
            
            if (psnData == null)
            {
                return new PsnUserTrophiesResponseDto();
            }

            var trophies = new List<PsnUserTrophyDto>();

            // 注意:这里只能获取统计信息,无法获取每个奖杯的详细信息
            // 如果需要详细奖杯信息,需要为每个游戏单独调用API
            if (psnData.RootElement.TryGetProperty("userTitles", out var userTitles))
            {
                if (userTitles.TryGetProperty("trophyTitles", out var titles))
                {
                    foreach (var title in titles.EnumerateArray())
                    {
                        var npCommunicationId = title.TryGetProperty("npCommunicationId", out var npId) ? npId.GetString() ?? "" : "";
                        var gameName = title.TryGetProperty("trophyTitleName", out var name) ? name.GetString() ?? "" : "";

                        // 创建奖杯统计记录
                        if (title.TryGetProperty("earnedTrophies", out var earnedTrophies))
                        {
                            var bronze = earnedTrophies.TryGetProperty("bronze", out var b) ? SafeGetInt32(b) : 0;
                            var silver = earnedTrophies.TryGetProperty("silver", out var s) ? SafeGetInt32(s) : 0;
                            var gold = earnedTrophies.TryGetProperty("gold", out var g) ? SafeGetInt32(g) : 0;
                            var platinum = earnedTrophies.TryGetProperty("platinum", out var p) ? SafeGetInt32(p) : 0;

                            // 为每种类型创建一个统计记录
                            if (bronze > 0)
                            {
                                trophies.Add(new PsnUserTrophyDto
                                {
                                    TrophyId = $"{npCommunicationId}_bronze",
                                    GameId = 0,
                                    GameName = gameName,
                                    AchievementName = "bronze_trophies",
                                    DisplayName = $"{gameName} - 铜杯",
                                    Description = $"已获得 {bronze} 个铜杯",
                                    Type = "bronze",
                                    Score = bronze * 15,
                                    Unlocked = true,
                                    IconUnlocked = "",
                                    IconLocked = "",
                                    Rarity = "common"
                                });
                            }

                            if (silver > 0)
                            {
                                trophies.Add(new PsnUserTrophyDto
                                {
                                    TrophyId = $"{npCommunicationId}_silver",
                                    GameId = 0,
                                    GameName = gameName,
                                    AchievementName = "silver_trophies",
                                    DisplayName = $"{gameName} - 银杯",
                                    Description = $"已获得 {silver} 个银杯",
                                    Type = "silver",
                                    Score = silver * 30,
                                    Unlocked = true,
                                    IconUnlocked = "",
                                    IconLocked = "",
                                    Rarity = "rare"
                                });
                            }

                            if (gold > 0)
                            {
                                trophies.Add(new PsnUserTrophyDto
                                {
                                    TrophyId = $"{npCommunicationId}_gold",
                                    GameId = 0,
                                    GameName = gameName,
                                    AchievementName = "gold_trophies",
                                    DisplayName = $"{gameName} - 金杯",
                                    Description = $"已获得 {gold} 个金杯",
                                    Type = "gold",
                                    Score = gold * 90,
                                    Unlocked = true,
                                    IconUnlocked = "",
                                    IconLocked = "",
                                    Rarity = "very_rare"
                                });
                            }

                            if (platinum > 0)
                            {
                                trophies.Add(new PsnUserTrophyDto
                                {
                                    TrophyId = $"{npCommunicationId}_platinum",
                                    GameId = 0,
                                    GameName = gameName,
                                    AchievementName = "platinum_trophy",
                                    DisplayName = $"{gameName} - 白金杯",
                                    Description = $"已获得 {platinum} 个白金杯",
                                    Type = "platinum",
                                    Score = platinum * 300,
                                    Unlocked = true,
                                    IconUnlocked = "",
                                    IconLocked = "",
                                    Rarity = "ultra_rare"
                                });
                            }
                        }
                    }
                }
            }

            return new PsnUserTrophiesResponseDto
            {
                Items = trophies,
                Total = trophies.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PSN用户奖杯时发生错误");
            return new PsnUserTrophiesResponseDto();
        }
    }
}
