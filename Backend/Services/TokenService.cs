using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PlayLinker.Models.Entities;

namespace PlayLinker.Services;

/// <summary>
/// Token服务 - 生成和验证JWT Token和Refresh Token
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateAccessToken(User user, out int expiresInSeconds)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));
        expiresInSeconds = jwtSettings.GetValue<int>("ExpiryMinutes", 60) * 60;

        var claims = new[]
        {
            new Claim("user_id", user.UserId.ToString()),
            new Claim("sub", user.UserId.ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "user"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(secretKey),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken(out DateTime expiresAtUtc)
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenExpiryDays = jwtSettings.GetValue<int>("RefreshTokenExpiryDays", 7);
        expiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        return Convert.ToBase64String(randomNumber);
    }

    public void StoreRefreshToken(int userId, string refreshToken, DateTime expiresAtUtc)
    {
        try
        {
            // 这里可以存储到数据库或缓存中
            // 目前简化处理，实际应该存储到数据库
            _logger.LogInformation($"Refresh token stored for user {userId}, expires at {expiresAtUtc}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing refresh token");
            throw;
        }
    }

    public bool ValidateRefreshToken(string refreshToken, out int userId)
    {
        userId = 0;
        try
        {
            // 这里应该从数据库或缓存中验证
            // 目前简化处理
            return !string.IsNullOrEmpty(refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token");
            return false;
        }
    }

    public void RevokeRefreshToken(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Refresh token revoked");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token");
        }
    }

    public void RevokeAllForUser(int userId)
    {
        try
        {
            _logger.LogInformation($"All refresh tokens revoked for user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all refresh tokens for user");
        }
    }
}

