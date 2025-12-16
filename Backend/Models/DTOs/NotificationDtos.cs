using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// 通知DTO
/// </summary>
public class NotificationDto
{
    /// <summary>
    /// 通知ID
    /// </summary>
    public long NotificationId { get; set; }

    /// <summary>
    /// 来源模块
    /// </summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 通知类型
    /// </summary>
    public string NotificationType { get; set; } = "info";

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 通知列表响应DTO
/// </summary>
public class NotificationsListResponseDto
{
    /// <summary>
    /// 通知列表
    /// </summary>
    public List<NotificationDto> Items { get; set; } = new();

    /// <summary>
    /// 未读数
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// 分页元数据
    /// </summary>
    public PaginationMetaDto Meta { get; set; } = new();
}

/// <summary>
/// 分页元数据DTO
/// </summary>
public class PaginationMetaDto
{
    /// <summary>
    /// 当前页
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总数
    /// </summary>
    public int Total { get; set; }
}

/// <summary>
/// 通知订阅设置请求DTO
/// </summary>
public class NotificationSubscribeRequestDto
{
    /// <summary>
    /// 价格提醒设置
    /// </summary>
    public NotificationSettingDto? PriceAlert { get; set; }

    /// <summary>
    /// 家长监管设置
    /// </summary>
    public NotificationSettingDto? ParentalControl { get; set; }

    /// <summary>
    /// 系统通知设置
    /// </summary>
    public NotificationSettingDto? System { get; set; }

    /// <summary>
    /// 推荐设置
    /// </summary>
    public NotificationSettingDto? Recommendation { get; set; }

    /// <summary>
    /// 游戏更新设置
    /// </summary>
    public NotificationSettingDto? GameUpdate { get; set; }
}

/// <summary>
/// 通知设置DTO
/// </summary>
public class NotificationSettingDto
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否邮件通知
    /// </summary>
    public bool Email { get; set; }

    /// <summary>
    /// 是否推送通知
    /// </summary>
    public bool Push { get; set; }
}

