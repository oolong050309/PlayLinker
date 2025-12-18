namespace PlayLinker.Services;

/// <summary>
/// 阿里云 OSS 配置
/// </summary>
public class AliyunOssOptions
{
    /// <summary>
    /// 访问域名，如：https://oss-cn-hangzhou.aliyuncs.com
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// AccessKeyId（建议通过安全配置注入，而不是写死在 appsettings.json 中）
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// AccessKeySecret（建议通过安全配置注入，而不是写死在 appsettings.json 中）
    /// </summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// Bucket 名称
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// 外网访问前缀，如：https://bucket-name.oss-cn-hangzhou.aliyuncs.com
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}


