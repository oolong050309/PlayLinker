using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using Microsoft.Extensions.Caching.Memory;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Services;

/// <summary>
/// 认证服务
/// </summary>
public class AuthService : IAuthService
{
    private readonly PlayLinkerDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;

    public AuthService(PlayLinkerDbContext dbContext, IPasswordHasher passwordHasher, ILogger<AuthService> logger, IMemoryCache cache, IEmailService emailService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _cache = cache;
        _emailService = emailService;
    }

    public async Task<(bool success, string message, User? user)> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            // 检查用户名是否已存在
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (existingUser != null)
            {
                return (false, "ERR_USERNAME_EXISTS", null);
            }

            // 检查邮箱是否已存在
            var existingEmail = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingEmail != null)
            {
                return (false, "ERR_EMAIL_EXISTS", null);
            }

            // 检查手机号是否已存在
            if (!string.IsNullOrEmpty(request.Phone))
            {
                var existingPhone = await _dbContext.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);
                if (existingPhone != null)
                {
                    return (false, "ERR_PHONE_EXISTS", null);
                }
            }

            // 验证密码强度
            if (!ValidatePasswordStrength(request.Password))
            {
                return (false, "ERR_WEAK_PASSWORD", null);
            }

            // 获取默认角色（user）
            var defaultRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.RoleName == "user");
            if (defaultRole == null)
            {
                return (false, "ERR_DEFAULT_ROLE_NOT_FOUND", null);
            }

            // 创建新用户
            var newUser = new User
            {
                Username = request.Username,
                HashedPassword = _passwordHasher.Hash(request.Password),
                Email = request.Email,
                Phone = request.Phone,
                RoleId = defaultRole.RoleId,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"User registered successfully: {newUser.UserId}");
            return (true, "注册成功", newUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return (false, "ERR_INTERNAL", null);
        }
    }

    public async Task<(bool success, string message, User? user)> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                return (false, "ERR_INVALID_CREDENTIALS", null);
            }

            // 检查账户状态
            if (user.Status == "disabled")
            {
                return (false, "ERR_ACCOUNT_DISABLED", null);
            }

            // 验证密码
            if (!_passwordHasher.Verify(request.Password, user.HashedPassword))
            {
                return (false, "ERR_INVALID_CREDENTIALS", null);
            }

            // 更新最后登录时间
            user.LastLoginTime = DateTime.UtcNow;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"User logged in successfully: {user.UserId}");
            return (true, "登录成功", user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return (false, "ERR_INTERNAL", null);
        }
    }

    public Task<(bool success, string message)> LogoutAsync(int userId, bool allDevices = false)
    {
        try
        {
            // 这里可以实现Token黑名单或其他登出逻辑
            if (allDevices)
            {
                _logger.LogInformation("User {UserId} logged out from all devices", userId);
            }
            else
            {
                _logger.LogInformation("User {UserId} logged out", userId);
            }

            return Task.FromResult((true, "退出登录成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return Task.FromResult((false, "ERR_INTERNAL"));
        }
    }

    public async Task<(bool success, string message)> ForgotPasswordAsync(string email)
    {
        try
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Email == email);
            // 生成6位数字验证码（即使用户不存在也生成并返回成功，避免暴露账户存在性）
            var code = GenerateNumericCode(6);
            var cacheKey = GetResetCacheKey(email);
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(30));

            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailService.SendPasswordResetCodeAsync(user.Email!, user.Username, code, 30);
            }

            _logger.LogInformation("Password reset code generated and sent to: {Email}", email);
            return (true, "验证码已发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password");
            return (false, "ERR_INTERNAL");
        }
    }

    public Task<(bool success, string message)> VerifyResetCodeAsync(string email, string code)
    {
        var cacheKey = GetResetCacheKey(email);
        if (!_cache.TryGetValue<string>(cacheKey, out var cachedCode) || string.IsNullOrWhiteSpace(cachedCode))
        {
            return Task.FromResult((false, "ERR_CODE_EXPIRED"));
        }

        if (!string.Equals(cachedCode, code, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult((false, "ERR_INVALID_CODE"));
        }

        return Task.FromResult((true, "OK"));
    }

    public async Task<(bool success, string message)> ResetPasswordByCodeAsync(string email, string code, string newPassword)
    {
        try
        {
            var (ok, msg) = await VerifyResetCodeAsync(email, code);
            if (!ok)
            {
                return (false, msg);
            }

            if (!ValidatePasswordStrength(newPassword))
            {
                return (false, "ERR_WEAK_PASSWORD");
            }

            var user = _dbContext.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                return (false, "ERR_NOT_FOUND");
            }

            user.HashedPassword = _passwordHasher.Hash(newPassword);
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            // 清除验证码
            _cache.Remove(GetResetCacheKey(email));

            _logger.LogInformation("Password reset by code for user {UserId}", user.UserId);
            return (true, "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password by code");
            return (false, "ERR_INTERNAL");
        }
    }

    private static string GenerateNumericCode(int length)
    {
        var rnd = new Random();
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + rnd.Next(0, 10));
        }
        return new string(chars);
    }

    private static string GetResetCacheKey(string email) => $"pwd_reset_code:{email.Trim().ToLower()}";

    private bool ValidatePasswordStrength(string password)
    {
        // 至少8个字符，包含大小写字母、数字和特殊字符
        if (password.Length < 8)
            return false;

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}

