namespace PlayLinker.Services;

/// <summary>
/// 令牌加密服务接口
/// 用于加密/解密存储在数据库中的平台令牌
/// </summary>
public interface ITokenEncryptionService
{
    /// <summary>
    /// 加密令牌（AES-256）
    /// </summary>
    /// <param name="plainToken">明文令牌</param>
    /// <returns>Base64编码的加密令牌</returns>
    string EncryptToken(string plainToken);

    /// <summary>
    /// 解密令牌（AES-256）
    /// </summary>
    /// <param name="encryptedToken">Base64编码的加密令牌</param>
    /// <returns>明文令牌</returns>
    string DecryptToken(string encryptedToken);
}

