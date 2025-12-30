using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;

namespace PlayLinker.Controllers;

/// <summary>
/// 认证控制器 - 用户注册、登录、Token管理
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthControllerV1 : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthControllerV1> _logger;

    public AuthControllerV1(IAuthService authService, ITokenService tokenService, ILogger<AuthControllerV1> logger)
    {
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <remarks>
    /// 创建新用户账户。密码必须包含大小写字母、数字和特殊字符，最少8个字符。
    /// 
    /// 示例请求:
    /// ```
    /// POST /api/v1/auth/register
    /// {
    ///   "username": "player123",
    ///   "password": "SecurePass123!",
    ///   "email": "player@example.com",
    ///   "phone": "13800138000"
    /// }
    /// ```
    /// 
    /// 成功后返回用户ID、用户名、JWT Token和刷新Token。
    /// </remarks>
    /// <param name="request">注册请求</param>
    [SwaggerOperation(Summary = "用户注册", Description = "创建新用户账户，返回用户ID、用户名、JWT Token和刷新Token。")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<RegisterResponseDto>>> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var (success, message, user) = await _authService.RegisterAsync(request);

            if (!success)
            {
                if (message == "ERR_USERNAME_EXISTS" || message == "ERR_EMAIL_EXISTS" || message == "ERR_PHONE_EXISTS")
                {
                    return Conflict(ApiResponse<object>.ErrorResponse(message, "用户已存在"));
                }

                if (message == "ERR_WEAK_PASSWORD")
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse(message, "密码强度不足"));
                }

                return StatusCode(500, ApiResponse<object>.ErrorResponse(message, "注册失败"));
            }

            // 生成Token
            var token = _tokenService.GenerateAccessToken(user!, out int expiresInSeconds);
            var refreshToken = _tokenService.GenerateRefreshToken(out DateTime refreshExpiresAt);
            _tokenService.StoreRefreshToken(user!.UserId, refreshToken, refreshExpiresAt);

            var response = new RegisterResponseDto
            {
                UserId = user!.UserId,
                Username = user.Username,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresIn = expiresInSeconds
            };

            _logger.LogInformation($"User registered: {user.UserId}");
            return CreatedAtAction(nameof(Register), ApiResponse<RegisterResponseDto>.SuccessResponse(response, "注册成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <remarks>
    /// 使用用户名或邮箱和密码登录，系统会自动识别输入格式。成功后返回用户信息与JWT Token。
    /// 
    /// 示例请求（用户名登录）:
    /// ```
    /// POST /api/v1/auth/login
    /// {
    ///   "username": "player123",
    ///   "password": "SecurePass123!"
    /// }
    /// ```
    /// 
    /// 示例请求（邮箱登录）:
    /// ```
    /// POST /api/v1/auth/login
    /// {
    ///   "username": "player@example.com",
    ///   "password": "SecurePass123!"
    /// }
    /// ```
    /// 
    /// 可能的错误码：
    /// - ERR_INVALID_CREDENTIALS 用户名或邮箱错误，或密码错误
    /// - ERR_ACCOUNT_DISABLED 账户已被禁用
    /// </remarks>
    /// <param name="request">登录请求（username字段支持用户名或邮箱）</param>
    [SwaggerOperation(Summary = "用户登录", Description = "使用用户名或邮箱和密码登录，系统会自动识别输入格式。成功后返回用户信息与JWT Token。可能错误码：ERR_INVALID_CREDENTIALS, ERR_ACCOUNT_DISABLED。")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            // 获取客户端IP地址
            var clientIp = GetClientIpAddress();

            var (success, message, user) = await _authService.LoginAsync(request, clientIp);

            if (!success)
            {
                if (message == "ERR_ACCOUNT_DISABLED")
                {
                    return Unauthorized(ApiResponse<object>.ErrorResponse(message, "账户已被禁用"));
                }

                return Unauthorized(ApiResponse<object>.ErrorResponse(message, "用户名/邮箱或密码错误"));
            }

            // 生成Token
            var token = _tokenService.GenerateAccessToken(user!, out int expiresInSeconds);
            var refreshToken = _tokenService.GenerateRefreshToken(out DateTime refreshExpiresAt);
            _tokenService.StoreRefreshToken(user!.UserId, refreshToken, refreshExpiresAt);

            var userInfo = new UserInfoDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role?.RoleName ?? "user",
                Status = user.Status ?? "active"
            };

            var response = new LoginResponseDto
            {
                User = userInfo,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresIn = expiresInSeconds
            };

            _logger.LogInformation($"User logged in: {user.UserId}");
            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "登录成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 刷新Token
    /// </summary>
    /// <param name="request">刷新Token请求</param>
    [SwaggerOperation(Summary = "刷新Token", Description = "使用Refresh Token获取新的JWT Token和Refresh Token。错误码：ERR_TOKEN_INVALID。")]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<RefreshTokenResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            if (!_tokenService.ValidateRefreshToken(request.RefreshToken, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_TOKEN_INVALID", "刷新Token无效或已过期"));
            }

            // 这里应该从数据库获取用户信息
            // 简化处理，直接生成新Token
            var newToken = _tokenService.GenerateAccessToken(new Models.Entities.User { UserId = userId, Username = "user" }, out int expiresInSeconds);
            var newRefreshToken = _tokenService.GenerateRefreshToken(out DateTime refreshExpiresAt);
            _tokenService.StoreRefreshToken(userId, newRefreshToken, refreshExpiresAt);

            var response = new RefreshTokenResponseDto
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = expiresInSeconds
            };

            _logger.LogInformation($"Token refreshed for user: {userId}");
            return Ok(ApiResponse<RefreshTokenResponseDto>.SuccessResponse(response, "Token刷新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    /// <param name="request">退出登录请求</param>
    [SwaggerOperation(Summary = "退出登录", Description = "退出当前会话，可选退出所有设备。请求体: { allDevices: false }。")]
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] LogoutRequestDto request)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var (success, message) = await _authService.LogoutAsync(userId, request.AllDevices);

            if (!success)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "退出登录失败"));
            }

            _logger.LogInformation($"User logged out: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "退出登录成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 忘记密码
    /// </summary>
    /// <param name="request">忘记密码请求</param>
    [SwaggerOperation(Summary = "忘记密码(发送验证码)", Description = "向邮箱发送6位验证码，有效期30分钟。出于安全考虑，即使邮箱不存在也返回成功。")]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ForgotPasswordResponseDto>>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var (success, message) = await _authService.ForgotPasswordAsync(request.Email);

            if (!success)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "发送邮件失败"));
            }

            var maskedEmail = MaskEmail(request.Email);
            var response = new ForgotPasswordResponseDto
            {
                Email = maskedEmail,
                ExpiresIn = 1800
            };

            _logger.LogInformation($"Password reset code sent to: {request.Email}");
            return Ok(ApiResponse<ForgotPasswordResponseDto>.SuccessResponse(response, "验证码已发送"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 校验邮箱验证码
    /// </summary>
    [SwaggerOperation(Summary = "校验邮箱验证码", Description = "校验通过返回OK，过期返回ERR_CODE_EXPIRED，错误返回ERR_INVALID_CODE。")]
    [HttpPost("verify-reset-code")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> VerifyResetCode([FromBody] VerifyResetCodeRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var (ok, msg) = await _authService.VerifyResetCodeAsync(request.Email, request.Code);
            if (!ok)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(msg, "验证码无效或已过期"));
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { }, "OK"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reset code");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 使用验证码重置密码
    /// </summary>
    [SwaggerOperation(Summary = "验证码重置密码", Description = "校验验证码后设置新密码。弱密码返回ERR_WEAK_PASSWORD，未找到用户返回ERR_NOT_FOUND。")]
    [HttpPost("reset-password-by-code")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPasswordByCode([FromBody] ResetPasswordByCodeRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var (ok, msg) = await _authService.ResetPasswordByCodeAsync(request.Email, request.Code, request.NewPassword);
            if (!ok)
            {
                // 将业务错误码直接返回
                return BadRequest(ApiResponse<object>.ErrorResponse(msg, "重置失败"));
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { }, "密码已重置，请使用新密码登录"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password by code");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    private string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2)
            return email;

        var localPart = parts[0];
        var domain = parts[1];

        if (localPart.Length <= 2)
            return $"{localPart[0]}***@{domain}";

        return $"{localPart[0]}{new string('*', localPart.Length - 2)}{localPart[^1]}@{domain}";
    }

    /// <summary>
    /// 获取客户端IP地址
    /// </summary>
    private string? GetClientIpAddress()
    {
        // 优先从X-Forwarded-For头获取（适用于反向代理场景）
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For可能包含多个IP，取第一个
            var ips = forwardedFor.Split(',');
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // 其次从X-Real-IP头获取
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        // 最后从HttpContext.Connection.RemoteIpAddress获取
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            // 如果是IPv6映射的IPv4地址，转换为IPv4格式
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                return remoteIp.MapToIPv4().ToString();
            }
            // 本地回环地址统一显示为127.0.0.1
            if (System.Net.IPAddress.IsLoopback(remoteIp))
            {
                return "127.0.0.1";
            }
            return remoteIp.ToString();
        }

        return null;
    }
}

