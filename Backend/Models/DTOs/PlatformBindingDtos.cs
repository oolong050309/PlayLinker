using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// OAuth URL响应DTO
/// </summary>
public class OAuthUrlResponseDto
{
    /// <summary>
    /// 平台名称
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// OAuth认证URL
    /// </summary>
    public string AuthUrl { get; set; } = string.Empty;

    /// <summary>
    /// 状态值（用于验证）
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; set; }
}

/// <summary>
/// 平台绑定请求DTO
/// </summary>
public class PlatformBindRequestDto
{
    /// <summary>
    /// 平台ID
    /// </summary>
    [Required(ErrorMessage = "平台ID不能为空")]
    public int PlatformId { get; set; }

    /// <summary>
    /// OAuth授权码
    /// </summary>
    [Required(ErrorMessage = "授权码不能为空")]
    public string AuthCode { get; set; } = string.Empty;

    /// <summary>
    /// 状态值（用于验证）
    /// </summary>
    [Required(ErrorMessage = "状态值不能为空")]
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// 平台绑定响应DTO
/// </summary>
public class PlatformBindResponseDto
{
    /// <summary>
    /// 绑定ID
    /// </summary>
    public int BindingId { get; set; }

    /// <summary>
    /// 平台名称
    /// </summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// 平台用户ID
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// 绑定时间
    /// </summary>
    public DateTime BindingTime { get; set; }
}

/// <summary>
/// 平台绑定记录DTO
/// </summary>
public class PlatformBindingDto
{
    /// <summary>
    /// 绑定ID
    /// </summary>
    public int BindingId { get; set; }

    /// <summary>
    /// 平台名称
    /// </summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// 平台用户ID
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// 个人资料名称
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// 绑定时间
    /// </summary>
    public DateTime BindingTime { get; set; }
}

/// <summary>
/// 平台绑定列表响应DTO
/// </summary>
public class PlatformBindingsListResponseDto
{
    /// <summary>
    /// 绑定列表
    /// </summary>
    public List<PlatformBindingDto> Bindings { get; set; } = new();

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; set; }
}

