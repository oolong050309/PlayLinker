using Aliyun.OSS;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace PlayLinker.Services;

/// <summary>
/// 阿里云 OSS 存储服务实现
/// </summary>
public class AliyunOssService : IAliyunOssService
{
    private readonly AliyunOssOptions _options;
    private readonly ILogger<AliyunOssService> _logger;

    public AliyunOssService(IOptions<AliyunOssOptions> options, ILogger<AliyunOssService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadUserAvatarAsync(int userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("文件为空");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.AccessKeySecret) ||
            string.IsNullOrWhiteSpace(_options.BucketName) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("AliyunOss 配置不完整，请检查 appsettings 或环境变量。");
        }

        // 允许的图片类型
        var allowedContentTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        if (!allowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("仅支持上传 JPG/PNG/GIF/WEBP 格式的图片。");
        }

        // 建议限制头像大小（例如 <= 2MB）
        const long maxAvatarSizeBytes = 2 * 1024 * 1024;
        if (file.Length > maxAvatarSizeBytes)
        {
            throw new InvalidOperationException("头像文件大小不能超过 2MB。");
        }

        // 生成唯一文件名：avatars/{userId}/{yyyyMMddHHmmss}_{guid}.ext
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            // 默认使用 .jpg
            ext = ".jpg";
        }

        var objectKey = $"avatars/{userId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";

        try
        {
            var client = new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret);

            using (var stream = file.OpenReadStream())
            {
                var metadata = new ObjectMetadata
                {
                    ContentType = file.ContentType
                };

                // 上传
                await Task.Run(() => client.PutObject(_options.BucketName, objectKey, stream, metadata));
            }

            // 拼接外网访问地址
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/{objectKey}";

            _logger.LogInformation("用户 {UserId} 头像上传成功，对象键：{ObjectKey}", userId, objectKey);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传头像到阿里云 OSS 失败");
            throw;
        }
    }

    /// <summary>
    /// 上传云存档文件到 OSS
    /// </summary>
    public async Task<string> UploadCloudSaveAsync(int userId, long gameId, Stream fileStream, string fileName)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            throw new ArgumentException("文件流为空");
        }

        ValidateOssConfiguration();

        // 生成对象键：saves/{userId}/{gameId}/{yyyyMMddHHmmss}_{guid}.dat
        var objectKey = $"saves/{userId}/{gameId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.dat";

        try
        {
            var client = new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret);

            var metadata = new ObjectMetadata
            {
                ContentType = "application/octet-stream"
            };

            // 上传
            await Task.Run(() => client.PutObject(_options.BucketName, objectKey, fileStream, metadata));

            _logger.LogInformation("用户 {UserId} 游戏 {GameId} 云存档上传成功，对象键：{ObjectKey}", userId, gameId, objectKey);
            return objectKey; // 返回对象键，用于后续下载和删除
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传云存档到阿里云 OSS 失败");
            throw;
        }
    }

    /// <summary>
    /// 从 OSS 下载云存档文件
    /// </summary>
    public async Task<Stream> DownloadCloudSaveAsync(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("对象键为空");
        }

        ValidateOssConfiguration();

        try
        {
            var client = new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret);

            var ossObject = await Task.Run(() => client.GetObject(_options.BucketName, objectKey));

            // 将 OSS 流复制到内存流
            var memoryStream = new MemoryStream();
            await ossObject.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            _logger.LogInformation("云存档下载成功，对象键：{ObjectKey}，大小：{Size} bytes", objectKey, memoryStream.Length);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从阿里云 OSS 下载云存档失败，对象键：{ObjectKey}", objectKey);
            throw;
        }
    }

    /// <summary>
    /// 从 OSS 删除云存档文件
    /// </summary>
    public async Task<bool> DeleteCloudSaveAsync(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("对象键为空");
        }

        ValidateOssConfiguration();

        try
        {
            var client = new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret);

            await Task.Run(() => client.DeleteObject(_options.BucketName, objectKey));

            _logger.LogInformation("云存档删除成功，对象键：{ObjectKey}", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从阿里云 OSS 删除云存档失败，对象键：{ObjectKey}", objectKey);
            return false;
        }
    }

    /// <summary>
    /// 获取文件访问 URL
    /// </summary>
    public string GetFileUrl(string objectKey)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{objectKey}";
    }

    private void ValidateOssConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.AccessKeySecret) ||
            string.IsNullOrWhiteSpace(_options.BucketName) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("AliyunOss 配置不完整，请检查 appsettings 或环境变量。");
        }
    }
}
