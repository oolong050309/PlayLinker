using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using Swashbuckle.AspNetCore.Annotations;

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

    public PlatformsController(PlayLinkerDbContext dbContext, ILogger<PlatformsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 获取OAuth URL
    /// </summary>
    /// <param name="platform">平台名称 (steam|epic|origin|uplay|gog)</param>
    [SwaggerOperation(Summary = "获取OAuth URL", Description = "生成第三方平台OAuth认证URL以及state，用于后续绑定。平台支持：steam|epic|origin|uplay|gog。需要JWT认证。")]
    [HttpGet("oauth/{platform}")]
    [ProducesResponseType(typeof(ApiResponse<OAuthUrlResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<OAuthUrlResponseDto>> GetOAuthUrl(string platform)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 验证平台
            var validPlatforms = new[] { "steam", "epic", "origin", "uplay", "gog" };
            if (!validPlatforms.Contains(platform.ToLower()))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_PLATFORM", "无效的平台"));
            }

            // 生成OAuth URL和State
            var state = Guid.NewGuid().ToString();
            var authUrl = GenerateOAuthUrl(platform, state);

            var response = new OAuthUrlResponseDto
            {
                Platform = platform,
                AuthUrl = authUrl,
                State = state,
                ExpiresIn = 600
            };

            _logger.LogInformation($"OAuth URL generated for platform: {platform}, user: {userId}");
            return Ok(ApiResponse<OAuthUrlResponseDto>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating OAuth URL");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 绑定平台
    /// </summary>
    /// <param name="request">绑定请求</param>
    [SwaggerOperation(Summary = "绑定平台", Description = "使用OAuth授权码完成平台绑定，创建绑定记录并返回绑定信息。冲突错误：ERR_ALREADY_BOUND。")]
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

            // 验证平台是否存在
            var platform = _dbContext.Platforms.FirstOrDefault(p => p.PlatformId == request.PlatformId);
            if (platform == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_PLATFORM_NOT_FOUND", "平台不存在"));
            }

            // 检查是否已绑定
            var existingBinding = _dbContext.UserPlatformBindings
                .FirstOrDefault(b => b.UserId == userId && b.PlatformId == request.PlatformId && b.BindingStatus == true);

            if (existingBinding != null)
            {
                return Conflict(ApiResponse<object>.ErrorResponse("ERR_ALREADY_BOUND", "平台已绑定"));
            }

            // 这里应该调用第三方API验证授权码
            // 简化处理，直接创建绑定记录
            var platformUserId = $"platform_user_{Guid.NewGuid().ToString().Substring(0, 8)}";

            var binding = new UserPlatformBinding
            {
                UserId = userId,
                PlatformId = request.PlatformId,
                PlatformUserId = platformUserId,
                AccessToken = "encrypted_token",
                RefreshToken = "encrypted_refresh_token",
                BindingStatus = true,
                BindingTime = DateTime.UtcNow,
                ExpireTime = DateTime.UtcNow.AddDays(30)
            };

            _dbContext.UserPlatformBindings.Add(binding);
            await _dbContext.SaveChangesAsync();

            var response = new PlatformBindResponseDto
            {
                BindingId = binding.BindingId,
                PlatformName = platform.PlatformName ?? "Unknown",
                PlatformUserId = platformUserId,
                BindingTime = binding.BindingTime ?? DateTime.UtcNow
            };

            _logger.LogInformation($"Platform bound: user {userId}, platform {request.PlatformId}");
            return CreatedAtAction(nameof(BindPlatform), ApiResponse<PlatformBindResponseDto>.SuccessResponse(response, $"{platform.PlatformName}平台绑定成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binding platform");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
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
    [SwaggerOperation(Summary = "解绑平台", Description = "解绑指定平台的绑定记录。路径参数：id=绑定ID。需要JWT认证。")]
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

            binding.BindingStatus = false;
            _dbContext.UserPlatformBindings.Update(binding);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Platform unbound: user {userId}, binding {id}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, $"{binding.Platform?.PlatformName}平台解绑成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbinding platform");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    private string GenerateOAuthUrl(string platform, string state)
    {
        return platform.ToLower() switch
        {
            "steam" => $"https://steamcommunity.com/openid/login?openid.ns=http://specs.openid.net/auth/2.0&openid.mode=checkid_setup&openid.return_to=http://localhost:5000/api/v1/platforms/oauth/steam/callback&openid.realm=http://localhost:5000&openid.identity=http://specs.openid.net/auth/2.0/identifier_select&openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select&state={state}",
            "epic" => $"https://www.epicgames.com/id/oauth/authorize?client_id=test&response_type=code&scope=openid&redirect_uri=http://localhost:5000/api/v1/platforms/oauth/epic/callback&state={state}",
            "origin" => $"https://accounts.ea.com/connect/authorize?client_id=test&response_type=code&redirect_uri=http://localhost:5000/api/v1/platforms/oauth/origin/callback&state={state}",
            "uplay" => $"https://uplay.ubisoft.com/en-US/oauth/authorize?client_id=test&response_type=code&redirect_uri=http://localhost:5000/api/v1/platforms/oauth/uplay/callback&state={state}",
            "gog" => $"https://auth.gog.com/auth?client_id=test&response_type=code&redirect_uri=http://localhost:5000/api/v1/platforms/oauth/gog/callback&state={state}",
            _ => string.Empty
        };
    }
}

