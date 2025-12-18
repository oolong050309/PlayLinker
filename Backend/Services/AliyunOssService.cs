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
}


