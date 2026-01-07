using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
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
  private readonly IAliyunOssService _ossService;

  public UsersController(
      PlayLinkerDbContext dbContext,
      IPasswordHasher passwordHasher,
      ILogger<UsersController> logger,
      IAliyunOssService ossService)
  {
      _dbContext = dbContext;
      _passwordHasher = passwordHasher;
      _logger = logger;
      _ossService = ossService;
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

    /// <summary>
    /// 上传头像（上传到阿里云 OSS，并更新用户 AvatarUrl）
    /// </summary>
    [SwaggerOperation(Summary = "上传头像", Description = "上传用户头像文件到阿里云 OSS，返回头像访问地址并更新用户头像URL。")]
    [HttpPost("avatar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> UploadAvatar([FromForm] IFormFile file)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_FILE_EMPTY", "文件不能为空"));
            }

            // 调用 OSS 服务上传
            string avatarUrl;
            try
            {
                avatarUrl = await _ossService.UploadUserAvatarAsync(userId, file);
            }
            catch (InvalidOperationException ex)
            {
                // 配置或业务校验错误
                _logger.LogWarning(ex, "Upload avatar validation/config error for user {UserId}", userId);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_UPLOAD_FAILED", ex.Message));
            }

            // 更新数据库中的 AvatarUrl
            var user = _dbContext.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            user.AvatarUrl = avatarUrl;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} avatar updated: {AvatarUrl}", userId, avatarUrl);

            return Ok(ApiResponse<object>.SuccessResponse(new { avatarUrl }, "上传成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading avatar");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新用户角色
    /// </summary>
    /// <param name="request">更新角色请求</param>
    [SwaggerOperation(Summary = "更新用户角色", Description = "更新当前登录用户的角色（user 或 parent）。需要JWT认证。")]
    [HttpPatch("role")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRole([FromBody] UpdateUserRoleRequestDto request)
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

            // 验证角色名称
            var validRoles = new[] { "user", "parent", "admin" };
            if (!validRoles.Contains(request.Role.ToLower()))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_ROLE", "无效的角色名称。允许的角色：user, parent, admin"));
            }

            // 查找角色
            var role = _dbContext.Roles.FirstOrDefault(r => r.RoleName.ToLower() == request.Role.ToLower());
            if (role == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_ROLE_NOT_FOUND", "角色不存在"));
            }

            // 查找用户
            var user = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            // 如果角色没有变化，直接返回成功
            if (user.RoleId == role.RoleId)
            {
                return Ok(ApiResponse<object>.SuccessResponse(new { role = role.RoleName }, "角色未变化"));
            }

            var oldRoleName = user.Role?.RoleName;
            var newRoleName = role.RoleName;

            // 更新角色
            user.RoleId = role.RoleId;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            // 如果从家长角色变为普通用户，禁用所有规则
            if (oldRoleName == "parent" && newRoleName.ToLower() == "user")
            {
                try
                {
                    // 获取该用户作为家长的所有子账户ID
                    var childUserIds = _dbContext.ParentalControlRelationships
                        .Where(r => r.ParentUserId == userId)
                        .Select(r => r.ChildUserId)
                        .ToList();

                    if (childUserIds.Count > 0)
                    {
                        // 批量禁用所有相关规则
                        var rules = _dbContext.ParentalControlRules
                            .Where(rule => childUserIds.Contains(rule.ChildUserId) && rule.IsActive == true)
                            .ToList();

                        var disabledCount = 0;
                        foreach (var rule in rules)
                        {
                            rule.IsActive = false;
                            rule.UpdatedAt = DateTime.UtcNow;
                            disabledCount++;
                        }

                        if (disabledCount > 0)
                        {
                            await _dbContext.SaveChangesAsync();
                            _logger.LogInformation($"Disabled {disabledCount} rules when user {userId} changed from parent to user");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to disable rules when user {userId} changed role, but role update succeeded");
                    // 不阻止角色更新，只记录警告
                }
            }

            _logger.LogInformation($"User role updated: {userId}, new role: {role.RoleName}");
            return Ok(ApiResponse<object>.SuccessResponse(new { role = role.RoleName }, "角色更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 删除账户（将状态标记为 disabled）
    /// </summary>
    [SwaggerOperation(Summary = "删除账户", Description = "将当前登录用户状态标记为 disabled，实现软删除。需要 JWT 认证。")]
    [HttpDelete("account")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAccount()
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "用户不存在"));
            }

            // 如果已是 disabled，直接返回
            if (string.Equals(user.Status, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse<object>.SuccessResponse(new { status = user.Status }, "账户已处于删除状态"));
            }

            user.Status = "disabled";
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User {UserId} set to disabled (account deleted)", userId);
            return Ok(ApiResponse<object>.SuccessResponse(new { status = user.Status }, "账户已删除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

