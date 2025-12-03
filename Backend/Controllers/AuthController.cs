using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlayLinker.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlayLinker.Controllers;

/// <summary>
/// 认证控制器 - 用于测试和开发环境生成JWT Token
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 生成测试用JWT Token (仅用于开发和测试)
    /// </summary>
    /// <param name="request">Token生成请求</param>
    [HttpPost("token")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> GenerateToken([FromBody] TokenRequestDto? request = null)
    {
        try
        {
            _logger.LogInformation("生成测试Token");

            // 默认使用测试用户ID
            var userId = request?.UserId ?? 1;
            var username = request?.Username ?? "testuser";
            var role = request?.Role ?? "user";

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));

            var claims = new[]
            {
                new Claim("user_id", userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim("username", username),
                new Claim(ClaimTypes.Role, role),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(jwtSettings.GetValue<int>("ExpiryMinutes", 60)),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(secretKey),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            var result = new
            {
                token = tokenString,
                tokenType = "Bearer",
                expiresIn = jwtSettings.GetValue<int>("ExpiryMinutes", 60) * 60,
                expiresAt = tokenDescriptor.Expires?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                user = new
                {
                    userId = userId,
                    username = username,
                    role = role
                }
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "Token生成成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成Token时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 验证Token是否有效
    /// </summary>
    /// <param name="token">JWT Token</param>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> ValidateToken([FromBody] ValidateTokenRequestDto request)
    {
        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out SecurityToken validatedToken);
                var jwtToken = (JwtSecurityToken)validatedToken;

                var result = new
                {
                    valid = true,
                    claims = jwtToken.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                    expiresAt = jwtToken.ValidTo.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return Ok(ApiResponse<object>.SuccessResponse(result, "Token有效"));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<object>.ErrorResponse("ERR_TOKEN_INVALID", $"Token无效: {ex.Message}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证Token时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

/// <summary>
/// Token生成请求DTO
/// </summary>
public class TokenRequestDto
{
    /// <summary>
    /// 用户ID (可选,默认1)
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// 用户名 (可选,默认testuser)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 角色 (可选,默认user,可选值: user, admin, parent)
    /// </summary>
    public string? Role { get; set; }
}

/// <summary>
/// Token验证请求DTO
/// </summary>
public class ValidateTokenRequestDto
{
    /// <summary>
    /// JWT Token
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

