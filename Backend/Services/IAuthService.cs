using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Services;

public interface IAuthService
{
    Task<(bool success, string message, User? user)> RegisterAsync(RegisterRequestDto request);
    Task<(bool success, string message, User? user)> LoginAsync(LoginRequestDto request, string? clientIp = null);
    Task<(bool success, string message)> LogoutAsync(int userId, bool allDevices = false);

    // 发送重置验证码到邮箱
    Task<(bool success, string message)> ForgotPasswordAsync(string email);
    // 校验验证码
    Task<(bool success, string message)> VerifyResetCodeAsync(string email, string code);
    // 使用验证码重置密码
    Task<(bool success, string message)> ResetPasswordByCodeAsync(string email, string code, string newPassword);
}

