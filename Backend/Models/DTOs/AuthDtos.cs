using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// 用户注册请求DTO
/// </summary>
public class RegisterRequestDto
{
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(128, MinimumLength = 3, ErrorMessage = "用户名长度必须在3-128之间")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "密码长度必须在8-128之间")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? Phone { get; set; }
}

public class RegisterResponseDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class LoginRequestDto
{
    /// <summary>
    /// 用户名或邮箱地址（系统会自动识别输入格式）
    /// </summary>
    [Required(ErrorMessage = "用户名或邮箱不能为空")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public UserInfoDto User { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class UserInfoDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "user";
    public string Status { get; set; } = "active";
}

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "刷新Token不能为空")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class LogoutRequestDto
{
    public bool AllDevices { get; set; } = false;
}

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResponseDto
{
    public string Email { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

// 新增：验证码验证请求
public class VerifyResetCodeRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;
}

// 新增：验证码重置密码请求
public class ResetPasswordByCodeRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
