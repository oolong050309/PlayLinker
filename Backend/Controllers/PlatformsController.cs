using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using PlayLinker.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Linq;

namespace PlayLinker.Controllers;

/// <summary>
/// 平台绑定控制器
/// </summary>
[ApiController]
[Route("api/v1/platforms")]
[Authorize]
public class PlatformsController : ControllerBase
{
    private readonly PlayLinkerDbContext _dbContext;
    private readonly ILogger<PlatformsController> _logger;
    private readonly ITokenEncryptionService _encryptionService;
    private readonly ISteamService _steamService;
    private readonly IXboxService _xboxService;
    private readonly IPsnService _psnService;
    private readonly IGogService _gogService;

    public PlatformsController(
        PlayLinkerDbContext dbContext, 
        ILogger<PlatformsController> logger,
        ITokenEncryptionService encryptionService,
        ISteamService steamService,
        IXboxService xboxService,
        IPsnService psnService,
        IGogService gogService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _encryptionService = encryptionService;
        _steamService = steamService;
        _xboxService = xboxService;
        _psnService = psnService;
        _gogService = gogService;
    }

    /// <summary>
    /// 初始化平台数据，确保所有平台都已存在
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

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        foreach (var platformInfo in platforms)
        {
            var exists = await _dbContext.Platforms
                .AnyAsync(p => p.PlatformId == platformInfo.Id || p.PlatformName == platformInfo.Name);

            if (!exists)
            {
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
    /// 获取OAuth URL
    /// </summary>
    /// <param name="platform">平台名称 (steam|epic|origin|uplay|gog)</param>
    /// <summary>
    /// 绑定平台
    /// </summary>
    /// <param name="request">绑定请求</param>
    [SwaggerOperation(Summary = "绑定平台", Description = "统一平台绑定接口，根据PlatformId自动选择绑定逻辑。\n\n" +
        "**Steam (PlatformId=1)**: 需要SteamId + ApiKey\n" +
        "**Xbox (PlatformId=7)**: 需要XboxUserId + AccessToken + RefreshToken\n" +
        "**PSN (PlatformId=6)**: 需要PsnOnlineId + AccessToken + RefreshToken\n" +
        "**GOG (PlatformId=5)**: 需要GogUserId + AccessToken + RefreshToken")]
    [HttpPost("bind")]
    [ProducesResponseType(typeof(ApiResponse<PlatformBindResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PlatformBindResponseDto>>> BindPlatform([FromBody] PlatformBindRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 初始化平台数据
            await InitializePlatformsAsync();

            // 验证平台是否存在
            var platform = await _dbContext.Platforms.FirstOrDefaultAsync(p => p.PlatformId == request.PlatformId);
            if (platform == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_PLATFORM_NOT_FOUND", "平台不存在"));
            }

            // 检查是否已绑定
            var existingBinding = await _dbContext.UserPlatformBindings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == request.PlatformId && b.BindingStatus == true);

            if (existingBinding != null)
            {
                return Conflict(ApiResponse<object>.ErrorResponse("ERR_ALREADY_BOUND", "平台已绑定，请先解绑后再重新绑定"));
            }

            // 根据平台ID选择绑定逻辑
            PlatformBindResponseDto? response = null;
            
            switch (request.PlatformId)
            {
                case 1: // Steam
                    response = await BindSteamPlatform(userId, request);
                    break;
                case 7: // Xbox
                    response = await BindXboxPlatform(userId, request);
                    break;
                case 6: // PSN
                    response = await BindPsnPlatform(userId, request);
                    break;
                case 5: // GOG
                    response = await BindGogPlatform(userId, request);
                    break;
                default:
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_PLATFORM_NOT_SUPPORTED", 
                        $"平台 {platform.PlatformName} 暂不支持通过此接口绑定"));
            }

            if (response == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_BIND_FAILED", "平台绑定失败"));
            }

            _logger.LogInformation("平台绑定成功: user {UserId}, platform {PlatformId}", userId, request.PlatformId);
            return CreatedAtAction(nameof(BindPlatform), ApiResponse<PlatformBindResponseDto>.SuccessResponse(response, $"{platform.PlatformName}平台绑定成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绑定平台时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 绑定Steam平台
    /// </summary>
    private async Task<PlatformBindResponseDto?> BindSteamPlatform(int userId, PlatformBindRequestDto request)
    {
        // 验证必需参数
        if (string.IsNullOrEmpty(request.SteamId) || string.IsNullOrEmpty(request.ApiKey))
        {
            throw new ArgumentException("Steam绑定需要提供SteamId和ApiKey");
        }

        // 验证Steam API Key是否有效
        var testUser = await _steamService.GetSteamUser(request.SteamId, request.ApiKey);
        if (testUser == null)
        {
            throw new ArgumentException("Steam API Key无效或Steam用户不存在");
        }

        // 先创建或更新PlayerPlatform记录（外键约束要求）
        var playerPlatform = await _dbContext.PlayerPlatforms
            .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.SteamId && pp.PlatformId == 1);

        if (playerPlatform == null)
        {
            playerPlatform = new PlayerPlatform
            {
                PlatformUserId = request.SteamId,
                PlatformId = 1, // Steam
                ProfileName = testUser.ProfileName ?? request.SteamId,
                ProfileUrl = testUser.ProfileUrl,
                AccountCreated = DateTime.TryParse(testUser.AccountCreated, out var created) ? created : null,
                Country = testUser.Country
            };
            _dbContext.PlayerPlatforms.Add(playerPlatform);
        }
        else
        {
            playerPlatform.ProfileName = testUser.ProfileName ?? request.SteamId;
            playerPlatform.ProfileUrl = testUser.ProfileUrl;
            playerPlatform.Country = testUser.Country;
        }
        await _dbContext.SaveChangesAsync();

        // 加密API Key
        var encryptedApiKey = _encryptionService.EncryptToken(request.ApiKey);

        // 查找现有绑定记录（不区分 BindingStatus），避免唯一索引冲突
        var existingBinding = await _dbContext.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 1);

        UserPlatformBinding binding;
        if (existingBinding != null)
        {
            // 已有记录：视为“重新绑定/更新Key”，直接更新
            binding = existingBinding;
            binding.PlatformUserId = request.SteamId;
            binding.AccessToken = encryptedApiKey;
            binding.BindingStatus = true;
            binding.BindingTime = DateTime.UtcNow;
            binding.ExpireTime = DateTime.UtcNow.AddYears(10);

            _dbContext.UserPlatformBindings.Update(binding);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            // 创建新的绑定记录
            binding = new UserPlatformBinding
        {
            UserId = userId,
            PlatformId = 1, // Steam
            PlatformUserId = request.SteamId,
            AccessToken = encryptedApiKey,
            BindingStatus = true,
            BindingTime = DateTime.UtcNow,
            ExpireTime = DateTime.UtcNow.AddYears(10) // Steam API Key长期有效
        };

        _dbContext.UserPlatformBindings.Add(binding);
        await _dbContext.SaveChangesAsync();
        }

        return new PlatformBindResponseDto
        {
            BindingId = binding.BindingId,
            PlatformName = "Steam",
            PlatformUserId = request.SteamId,
            BindingTime = binding.BindingTime ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// 绑定Xbox平台
    /// </summary>
    private async Task<PlatformBindResponseDto?> BindXboxPlatform(int userId, PlatformBindRequestDto request)
    {
        // 验证必需参数
        if (string.IsNullOrEmpty(request.XboxUserId))
        {
            throw new ArgumentException("Xbox绑定需要提供XboxUserId");
        }

        // 先创建或更新PlayerPlatform记录（外键约束要求）
        var playerPlatform = await _dbContext.PlayerPlatforms
            .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.XboxUserId && pp.PlatformId == 7);

        if (playerPlatform == null)
        {
            // 尝试获取Xbox用户信息（如果有令牌）
            string? profileName = request.XboxUserId;
            string? profileUrl = null;
            
            if (!string.IsNullOrEmpty(request.AccessToken))
            {
                try
                {
                    var xboxUser = await _xboxService.GetXboxUser(request.XboxUserId, userId);
                    if (xboxUser != null)
                    {
                        profileName = xboxUser.Gamertag ?? request.XboxUserId;
                        profileUrl = xboxUser.ProfileUrl;
                    }
                }
                catch
                {
                    // 如果获取失败，使用默认值
                }
            }

            playerPlatform = new PlayerPlatform
            {
                PlatformUserId = request.XboxUserId,
                PlatformId = 7, // Xbox
                ProfileName = profileName ?? request.XboxUserId,
                ProfileUrl = profileUrl
            };
            _dbContext.PlayerPlatforms.Add(playerPlatform);
        }
        await _dbContext.SaveChangesAsync();

        // 如果提供了令牌，加密存储
        string? encryptedAccessToken = null;
        string? encryptedRefreshToken = null;

        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            encryptedAccessToken = _encryptionService.EncryptToken(request.AccessToken);
        }
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            encryptedRefreshToken = _encryptionService.EncryptToken(request.RefreshToken);
        }

        // 创建绑定记录
        var existingBinding = await _dbContext.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 7);

        UserPlatformBinding binding;
        if (existingBinding != null)
        {
            binding = existingBinding;
            binding.PlatformUserId = request.XboxUserId;
            binding.AccessToken = encryptedAccessToken;
            binding.RefreshToken = encryptedRefreshToken;
            binding.BindingStatus = true;
            binding.BindingTime = DateTime.UtcNow;
            binding.ExpireTime = DateTime.UtcNow.AddDays(30);

            _dbContext.UserPlatformBindings.Update(binding);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            binding = new UserPlatformBinding
        {
            UserId = userId,
            PlatformId = 7, // Xbox
            PlatformUserId = request.XboxUserId,
            AccessToken = encryptedAccessToken,
            RefreshToken = encryptedRefreshToken,
            BindingStatus = true,
            BindingTime = DateTime.UtcNow,
            ExpireTime = DateTime.UtcNow.AddDays(30) // 令牌通常30天过期
        };

        _dbContext.UserPlatformBindings.Add(binding);
        await _dbContext.SaveChangesAsync();
        }

        return new PlatformBindResponseDto
        {
            BindingId = binding.BindingId,
            PlatformName = "Xbox",
            PlatformUserId = request.XboxUserId,
            BindingTime = binding.BindingTime ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// 绑定PSN平台
    /// </summary>
    private async Task<PlatformBindResponseDto?> BindPsnPlatform(int userId, PlatformBindRequestDto request)
    {
        // 验证必需参数
        if (string.IsNullOrEmpty(request.PsnOnlineId))
        {
            throw new ArgumentException("PSN绑定需要提供PsnOnlineId");
        }

        // 先创建或更新PlayerPlatform记录（外键约束要求）
        var playerPlatform = await _dbContext.PlayerPlatforms
            .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.PsnOnlineId && pp.PlatformId == 6);

        if (playerPlatform == null)
        {
            // 尝试获取PSN用户信息（如果有令牌）
            string? profileName = request.PsnOnlineId;
            string? profileUrl = null;
            
            if (!string.IsNullOrEmpty(request.AccessToken))
            {
                try
                {
                    var psnUser = await _psnService.GetPsnUser(request.PsnOnlineId, userId);
                    if (psnUser != null)
                    {
                        profileName = psnUser.OnlineId ?? request.PsnOnlineId;
                        profileUrl = psnUser.ProfileUrl;
                    }
                }
                catch
                {
                    // 如果获取失败，使用默认值
                }
            }

            playerPlatform = new PlayerPlatform
            {
                PlatformUserId = request.PsnOnlineId,
                PlatformId = 6, // PSN
                ProfileName = profileName ?? request.PsnOnlineId,
                ProfileUrl = profileUrl
            };
            _dbContext.PlayerPlatforms.Add(playerPlatform);
        }
        await _dbContext.SaveChangesAsync();

        // 如果提供了令牌，加密存储
        string? encryptedAccessToken = null;
        string? encryptedRefreshToken = null;

        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            encryptedAccessToken = _encryptionService.EncryptToken(request.AccessToken);
        }
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            encryptedRefreshToken = _encryptionService.EncryptToken(request.RefreshToken);
        }

        // 创建绑定记录
        var existingBinding = await _dbContext.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 6);

        UserPlatformBinding binding;
        if (existingBinding != null)
        {
            binding = existingBinding;
            binding.PlatformUserId = request.PsnOnlineId;
            binding.AccessToken = encryptedAccessToken;
            binding.RefreshToken = encryptedRefreshToken;
            binding.BindingStatus = true;
            binding.BindingTime = DateTime.UtcNow;
            binding.ExpireTime = DateTime.UtcNow.AddDays(30);

            _dbContext.UserPlatformBindings.Update(binding);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            binding = new UserPlatformBinding
        {
            UserId = userId,
            PlatformId = 6, // PSN
            PlatformUserId = request.PsnOnlineId,
            AccessToken = encryptedAccessToken,
            RefreshToken = encryptedRefreshToken,
            BindingStatus = true,
            BindingTime = DateTime.UtcNow,
            ExpireTime = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.UserPlatformBindings.Add(binding);
        await _dbContext.SaveChangesAsync();
        }

        return new PlatformBindResponseDto
        {
            BindingId = binding.BindingId,
            PlatformName = "PSN",
            PlatformUserId = request.PsnOnlineId,
            BindingTime = binding.BindingTime ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// 绑定GOG平台
    /// </summary>
    private async Task<PlatformBindResponseDto?> BindGogPlatform(int userId, PlatformBindRequestDto request)
    {
        // 验证必需参数
        if (string.IsNullOrEmpty(request.GogUserId))
        {
            throw new ArgumentException("GOG绑定需要提供GogUserId");
        }

        // 先创建或更新PlayerPlatform记录（外键约束要求）
        var playerPlatform = await _dbContext.PlayerPlatforms
            .FirstOrDefaultAsync(pp => pp.PlatformUserId == request.GogUserId && pp.PlatformId == 5);

        if (playerPlatform == null)
        {
            // 尝试获取GOG用户信息（如果有令牌）
            string? profileName = request.GogUserId;
            string? profileUrl = null;
            
            if (!string.IsNullOrEmpty(request.AccessToken))
            {
                try
                {
                    var gogUser = await _gogService.GetGogUser(request.GogUserId, userId);
                    if (gogUser != null)
                    {
                        profileName = gogUser.Username ?? request.GogUserId;
                        profileUrl = gogUser.ProfileUrl;
                    }
                }
                catch
                {
                    // 如果获取失败，使用默认值
                }
            }

            playerPlatform = new PlayerPlatform
            {
                PlatformUserId = request.GogUserId,
                PlatformId = 5, // GOG
                ProfileName = profileName ?? request.GogUserId,
                ProfileUrl = profileUrl
            };
            _dbContext.PlayerPlatforms.Add(playerPlatform);
        }
        await _dbContext.SaveChangesAsync();

        // 如果提供了令牌，加密存储
        string? encryptedAccessToken = null;
        string? encryptedRefreshToken = null;

        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            encryptedAccessToken = _encryptionService.EncryptToken(request.AccessToken);
        }
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            encryptedRefreshToken = _encryptionService.EncryptToken(request.RefreshToken);
        }

        // 创建绑定记录
        var existingBinding = await _dbContext.UserPlatformBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 5);

        UserPlatformBinding binding;
        if (existingBinding != null)
        {
            binding = existingBinding;
            binding.PlatformUserId = request.GogUserId;
            binding.AccessToken = encryptedAccessToken;
            binding.RefreshToken = encryptedRefreshToken;
            binding.BindingStatus = true;
            binding.BindingTime = DateTime.UtcNow;
            binding.ExpireTime = DateTime.UtcNow.AddDays(30);

            _dbContext.UserPlatformBindings.Update(binding);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            binding = new UserPlatformBinding
        {
            UserId = userId,
            PlatformId = 5, // GOG
            PlatformUserId = request.GogUserId,
            AccessToken = encryptedAccessToken,
            RefreshToken = encryptedRefreshToken,
            BindingStatus = true,
            BindingTime = DateTime.UtcNow,
            ExpireTime = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.UserPlatformBindings.Add(binding);
        await _dbContext.SaveChangesAsync();
        }

        return new PlatformBindResponseDto
        {
            BindingId = binding.BindingId,
            PlatformName = "GOG",
            PlatformUserId = request.GogUserId,
            BindingTime = binding.BindingTime ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// 获取绑定列表
    /// </summary>
    [SwaggerOperation(Summary = "获取绑定列表", Description = "返回当前用户的第三方平台绑定记录列表，包含平台名、平台用户ID、绑定时间等。需要JWT认证。")]
    [HttpGet("bindings")]
    [ProducesResponseType(typeof(ApiResponse<PlatformBindingsListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<PlatformBindingsListResponseDto>> GetBindings()
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var bindings = _dbContext.UserPlatformBindings
                .Where(b => b.UserId == userId && b.BindingStatus == true)
                .Include(b => b.Platform)
                .ToList();

            var bindingDtos = bindings.Select(b => new PlatformBindingDto
            {
                BindingId = b.BindingId,
                PlatformName = b.Platform?.PlatformName ?? "Unknown",
                PlatformUserId = b.PlatformUserId,
                ProfileName = b.Platform?.PlatformName,
                BindingTime = b.BindingTime ?? DateTime.UtcNow
            }).ToList();

            var response = new PlatformBindingsListResponseDto
            {
                Bindings = bindingDtos,
                TotalCount = bindingDtos.Count
            };

            _logger.LogInformation($"Bindings retrieved for user: {userId}");
            return Ok(ApiResponse<PlatformBindingsListResponseDto>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bindings");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 解绑平台
    /// </summary>
    /// <param name="id">绑定ID</param>
    [SwaggerOperation(Summary = "解绑平台", Description = "解绑指定平台的绑定记录，同时删除相关的游戏库和成就数据，并重新计算统计数据。路径参数：id=绑定ID。需要JWT认证。")]
    [HttpDelete("bindings/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UnbindPlatform(int id)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var binding = _dbContext.UserPlatformBindings
                .Include(b => b.Platform)
                .FirstOrDefault(b => b.BindingId == id && b.UserId == userId);

            if (binding == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "绑定记录不存在"));
            }

            var platformUserId = binding.PlatformUserId;
            var platformId = binding.PlatformId;

            // 1. 删除该平台账号相关的游戏库记录
            var platformLibraryRecords = _dbContext.UserPlatformLibraries
                .Where(upl => upl.PlatformUserId == platformUserId && upl.PlatformId == platformId)
                .ToList();

            if (platformLibraryRecords.Any())
            {
                _dbContext.UserPlatformLibraries.RemoveRange(platformLibraryRecords);
                _logger.LogInformation($"删除 {platformLibraryRecords.Count} 条游戏库记录: user {userId}, platform {platformId}, platformUserId {platformUserId}");
            }

            // 2. 删除该平台账号相关的成就记录（通过 PlatformId 和 UserId）
            var achievementsToDelete = _dbContext.UserAchievements
                .Where(ua => ua.UserId == userId && ua.PlatformId == platformId)
                .ToList();

            if (achievementsToDelete.Any())
            {
                _dbContext.UserAchievements.RemoveRange(achievementsToDelete);
                _logger.LogInformation($"删除 {achievementsToDelete.Count} 条成就记录: user {userId}, platform {platformId}");
            }

            // 3. 更新绑定状态
            binding.BindingStatus = false;
            _dbContext.UserPlatformBindings.Update(binding);

            await _dbContext.SaveChangesAsync();

            // 4. 重新计算用户统计数据
            await RecalculateUserLibraryStats(userId);

            _logger.LogInformation($"Platform unbound: user {userId}, binding {id}, platform {platformId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, $"{binding.Platform?.PlatformName}平台解绑成功，相关数据已清理"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbinding platform");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 重新计算用户游戏库统计数据
    /// </summary>
    private async Task RecalculateUserLibraryStats(int userId)
    {
        try
        {
            // 获取用户所有活跃绑定的平台账号
            var activeBindings = await _dbContext.UserPlatformBindings
                .Where(upb => upb.UserId == userId && upb.BindingStatus == true)
                .Select(upb => new { upb.PlatformUserId, upb.PlatformId })
                .ToListAsync();

            // 获取这些平台账号的所有游戏库记录
            var allLibraryRecords = new List<UserPlatformLibrary>();
            foreach (var binding in activeBindings)
            {
                var records = await _dbContext.UserPlatformLibraries
                    .Where(upl => upl.PlatformUserId == binding.PlatformUserId && upl.PlatformId == binding.PlatformId)
                    .ToListAsync();
                allLibraryRecords.AddRange(records);
            }

            // 计算统计数据
            var totalGamesOwned = allLibraryRecords.Select(upl => upl.GameId).Distinct().Count();
            var gamesPlayed = allLibraryRecords.Count(upl => upl.PlaytimeMinutes > 0);
            var totalPlaytimeMinutes = allLibraryRecords.Sum(upl => upl.PlaytimeMinutes);
            
            // 计算最近30天内的游戏数和游戏时长
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentGames = allLibraryRecords
                .Where(upl => upl.LastPlayed.HasValue && upl.LastPlayed.Value >= thirtyDaysAgo)
                .ToList();
            var recentlyPlayedCount = recentGames.Select(upl => upl.GameId).Distinct().Count();
            var recentPlaytimeMinutes = recentGames.Sum(upl => upl.PlaytimeMinutes);

            // 计算成就统计（统计所有活跃绑定平台的成就）
            var activePlatformIds = activeBindings.Select(b => b.PlatformId).Distinct().ToList();
            var totalAchievements = await _dbContext.UserAchievements
                .Where(ua => ua.UserId == userId && activePlatformIds.Contains(ua.PlatformId))
                .CountAsync();
            var unlockedAchievements = await _dbContext.UserAchievements
                .Where(ua => ua.UserId == userId && activePlatformIds.Contains(ua.PlatformId) && ua.UnlockTime != null)
                .CountAsync();

            // 更新或创建 UserGameLibrary 记录
            var library = await _dbContext.UserGameLibraries
                .FirstOrDefaultAsync(ugl => ugl.UserId == userId);

            if (library == null)
            {
                library = new UserGameLibrary
                {
                    UserId = userId,
                    TotalGamesOwned = totalGamesOwned,
                    GamesPlayed = gamesPlayed,
                    TotalPlaytimeMinutes = totalPlaytimeMinutes,
                    TotalAchievements = totalAchievements,
                    UnlockedAchievements = unlockedAchievements,
                    RecentlyPlayedCount = recentlyPlayedCount,
                    RecentPlaytimeMinutes = recentPlaytimeMinutes
                };
                _dbContext.UserGameLibraries.Add(library);
            }
            else
            {
                library.TotalGamesOwned = totalGamesOwned;
                library.GamesPlayed = gamesPlayed;
                library.TotalPlaytimeMinutes = totalPlaytimeMinutes;
                library.TotalAchievements = totalAchievements;
                library.UnlockedAchievements = unlockedAchievements;
                library.RecentlyPlayedCount = recentlyPlayedCount;
                library.RecentPlaytimeMinutes = recentPlaytimeMinutes;
                _dbContext.UserGameLibraries.Update(library);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"重新计算用户统计数据: user {userId}, games={totalGamesOwned}, playtime={totalPlaytimeMinutes}, achievements={totalAchievements}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"重新计算用户统计数据失败: user {userId}");
            throw;
        }
    }
}

