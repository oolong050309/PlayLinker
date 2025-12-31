using Microsoft.AspNetCore.Http;

namespace PlayLinker.Services;

/// <summary>
/// 阿里云 OSS 存储服务（用于上传用户头像等）
/// </summary>
public interface IAliyunOssService
{
    /// <summary>
    /// 上传用户头像，返回可直接访问的 URL
    /// </summary>
    /// <param name="userId">用户ID，用于生成文件路径</param>
    /// <param name="file">上传的头像文件</param>
    /// <returns>头像完整 URL</returns>
    Task<string> UploadUserAvatarAsync(int userId, IFormFile file);

    /// <summary>
    /// 上传云存档文件到 OSS
    /// </summary>
    Task<string> UploadCloudSaveAsync(int userId, long gameId, Stream fileStream, string fileName);

    /// <summary>
    /// 从 OSS 下载云存档文件
    /// </summary>
    Task<Stream> DownloadCloudSaveAsync(string objectKey);

    /// <summary>
    /// 从 OSS 删除云存档文件
    /// </summary>
    Task<bool> DeleteCloudSaveAsync(string objectKey);

    /// <summary>
    /// 获取文件访问 URL
    /// </summary>
    string GetFileUrl(string objectKey);
}
