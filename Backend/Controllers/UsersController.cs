using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace PlayLinker.Controllers;

/// <summary>
/// 用户管理控制器
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly PlayLinkerDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UsersController> _logger;

    public UsersController(PlayLinkerDbContext dbContext, IPasswordHasher passwordHasher, ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// 获取个人信息
    /// </summary>
    [SwaggerOperation(Summary = "获取个人信息", Description = "获取当前登录用户的个人资料与账号状态。需要JWT认证。")]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<UserProfileDto>> GetProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var user = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            var response = new UserProfileDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Phone = user.Phone,
                Gender = user.Gender ?? 0,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role?.RoleName ?? "user",
                Status = user.Status ?? "active",
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
            };

            _logger.LogInformation($"User profile retrieved: {userId}");
            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新个人信息
    /// </summary>
    /// <remarks>
    /// 可更新邮箱、手机号、性别与头像URL。会进行唯一性校验。
    /// </remarks>
    /// <param name="request">更新请求</param>
    [SwaggerOperation(Summary = "更新个人信息", Description = "可更新邮箱、手机号、性别与头像URL。需要JWT认证，带唯一性校验。")]
    [HttpPatch("profile")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateUserProfileRequestDto request)
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

            var user = _dbContext.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            // 检查邮箱是否已被其他用户使用
            if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
            {
                var existingEmail = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email && u.UserId != userId);
                if (existingEmail != null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_EMAIL_EXISTS", "邮箱已被使用"));
                }
            }

            // 检查手机号是否已被其他用户使用
            if (!string.IsNullOrEmpty(request.Phone) && request.Phone != user.Phone)
            {
                var existingPhone = _dbContext.Users.FirstOrDefault(u => u.Phone == request.Phone && u.UserId != userId);
                if (existingPhone != null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_PHONE_EXISTS", "手机号已被使用"));
                }
            }

            // 更新用户信息
            if (!string.IsNullOrEmpty(request.Email))
                user.Email = request.Email;

            if (!string.IsNullOrEmpty(request.Phone))
                user.Phone = request.Phone;

            if (request.Gender.HasValue)
                user.Gender = request.Gender.Value;

            if (!string.IsNullOrEmpty(request.AvatarUrl))
                user.AvatarUrl = request.AvatarUrl;

            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"User profile updated: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="request">修改密码请求</param>
    [SwaggerOperation(Summary = "修改密码", Description = "校验旧密码并设置新密码。新密码需满足强度要求：8+字符，含大小写、数字、特殊字符。成功后需重新登录。")]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequestDto request)
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

            var user = _dbContext.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            // 验证旧密码
            if (!_passwordHasher.Verify(request.OldPassword, user.HashedPassword))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_PASSWORD", "旧密码不正确"));
            }

            // 验证新密码强度
            if (!ValidatePasswordStrength(request.NewPassword))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_WEAK_PASSWORD", "新密码强度不足"));
            }

            // 更新密码
            user.HashedPassword = _passwordHasher.Hash(request.NewPassword);
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"User password changed: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "密码修改成功，请重新登录"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    private bool ValidatePasswordStrength(string password)
    {
        if (password.Length < 8)
            return false;

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}

