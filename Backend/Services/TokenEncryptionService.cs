using System.Security.Cryptography;
using System.Text;

namespace PlayLinker.Services;

/// <summary>
/// 令牌加密服务实现
/// 使用AES-256-CBC加密算法
/// </summary>
public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<TokenEncryptionService> _logger;

    public TokenEncryptionService(IConfiguration configuration, ILogger<TokenEncryptionService> logger)
    {
        _logger = logger;
        
        // 从配置读取加密密钥（32字节=256位）
        var secretKey = configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("Encryption key not configured");
        
        // 使用SHA256生成固定长度的密钥
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
    }

    /// <summary>
    /// 加密令牌
    /// </summary>
    public string EncryptToken(string plainToken)
    {
        if (string.IsNullOrEmpty(plainToken))
        {
            throw new ArgumentNullException(nameof(plainToken));
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainToken);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // 将IV和加密数据组合在一起
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加密令牌失败");
            throw;
        }
    }

    /// <summary>
    /// 解密令牌
    /// </summary>
    public string DecryptToken(string encryptedToken)
    {
        if (string.IsNullOrEmpty(encryptedToken))
        {
            throw new ArgumentNullException(nameof(encryptedToken));
        }

        try
        {
            var fullCipher = Convert.FromBase64String(encryptedToken);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // 提取IV（前16字节）
            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - 16];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
            Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解密令牌失败");
            throw;
        }
    }
}

