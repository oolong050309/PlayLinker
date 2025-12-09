using PlayLinker.Models.DTOs;
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
    private readonly string _nodePath;
    private readonly string _scriptsPath;
    private readonly string _tokensPath;

    public PsnService(IConfiguration configuration, ILogger<PsnService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;

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
    /// 检查令牌状态
    /// </summary>
    public async Task<PsnAuthResponseDto> CheckTokenStatus(string? tokensPath = null)
    {
        try
        {
            var tokenPath = GetTokenFilePath(tokensPath);
            var tokenExists = File.Exists(tokenPath);

            if (!tokenExists)
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "令牌文件不存在,需要首次认证",
                    TokenExists = false,
                    TokensPath = tokenPath
                };
            }

            // 尝试使用令牌获取数据(验证令牌有效性)
            var psnData = await GetPsnDataFromNode(tokensPath);
            
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
                    TokensPath = tokenPath,
                    AccountId = accountId,
                    OnlineId = onlineId
                };
            }
            else
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "令牌已过期或无效,需要重新认证",
                    TokenExists = true,
                    TokensPath = tokenPath
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
    public async Task<PsnAuthResponseDto> AuthenticatePsn(PsnAuthRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始PSN认证");

            if (string.IsNullOrWhiteSpace(request.Npsso))
            {
                return new PsnAuthResponseDto
                {
                    Success = false,
                    Message = "NPSSO令牌不能为空。请访问 https://ca.account.sony.com/api/v1/ssocookie 获取NPSSO",
                    TokenExists = false
                };
            }

            var tokenPath = GetTokenFilePath(request.TokensPath);
            
            // 如果强制重新认证,删除旧令牌
            if (request.ForceReauth && File.Exists(tokenPath))
            {
                File.Delete(tokenPath);
                _logger.LogInformation("已删除旧令牌文件");
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
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath
                };
            }
            _logger.LogInformation("Node.js环境检查通过");

            // 执行认证脚本
            var arguments = $"--npsso \"{request.Npsso}\" --tokens \"{tokenPath}\"";
            
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
                        TokenExists = File.Exists(tokenPath),
                        TokensPath = tokenPath
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
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath
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
                    return new PsnAuthResponseDto
                    {
                        Success = true,
                        Message = result.ContainsKey("message") ? result["message"].GetString() ?? "认证成功" : "认证成功",
                        AccountId = result.ContainsKey("accountId") ? result["accountId"].GetString() : null,
                        OnlineId = result.ContainsKey("onlineId") ? result["onlineId"].GetString() : null,
                        TokensPath = tokenPath,
                        TokenExists = true
                    };
                }
                else
                {
                    return new PsnAuthResponseDto
                    {
                        Success = false,
                        Message = result?.ContainsKey("message") == true ? result["message"].GetString() ?? "认证失败" : "认证失败",
                        TokenExists = File.Exists(tokenPath),
                        TokensPath = tokenPath
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
                    TokenExists = File.Exists(tokenPath),
                    TokensPath = tokenPath
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
    }

    /// <summary>
    /// 获取PSN数据
    /// </summary>
    private async Task<JsonDocument?> GetPsnDataFromNode(string? tokensPath = null)
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
    }

    /// <summary>
    /// 导入PSN数据
    /// </summary>
    public async Task<PsnImportResponseDto> ImportPsnData(PsnImportRequestDto request)
    {
        try
        {
            _logger.LogInformation("开始导入PSN数据: psnOnlineId={OnlineId}", request.PsnOnlineId);

            var taskId = $"psn_import_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // 获取PSN数据
            _logger.LogInformation("正在调用Node.js脚本获取PSN数据...");
            var psnData = await GetPsnDataFromNode();
            
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
    public async Task<PsnUserDto?> GetPsnUser(string onlineId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户信息: onlineId={OnlineId}", onlineId);

            var psnData = await GetPsnDataFromNode();
            
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
    public async Task<PsnGameDto?> GetPsnGame(string titleId)
    {
        try
        {
            _logger.LogInformation("获取PSN游戏信息: titleId={TitleId}", titleId);

            var psnData = await GetPsnDataFromNode();
            
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
    public async Task<List<PsnGameDto>> GetPsnUserGames(string onlineId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户游戏列表: onlineId={OnlineId}", onlineId);

            var psnData = await GetPsnDataFromNode();
            
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
    public async Task<PsnUserTrophiesResponseDto> GetPsnUserTrophies(string onlineId)
    {
        try
        {
            _logger.LogInformation("获取PSN用户奖杯: onlineId={OnlineId}", onlineId);

            var psnData = await GetPsnDataFromNode();
            
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
