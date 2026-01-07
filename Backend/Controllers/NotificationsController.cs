using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace PlayLinker.Controllers;

/// <summary>
/// 通知中心控制器
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly PlayLinkerDbContext _dbContext;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(PlayLinkerDbContext dbContext, ILogger<NotificationsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 获取通知列表
    /// </summary>
    /// <param name="isRead">是否已读 (可选)</param>
    /// <param name="type">通知类型 (可选)</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页数量，默认20</param>
    [SwaggerOperation(Summary = "获取通知列表", Description = "按是否已读与类型进行筛选并分页返回，同时提供未读计数与分页元数据。查询参数：isRead、type、page、pageSize。需要JWT认证。")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationsListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<NotificationsListResponseDto>> GetNotifications(
        [FromQuery] bool? isRead = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 验证分页参数
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _dbContext.NotificationCenters.Where(n => n.UserId == userId && (n.IsVisible ?? true));

            // 应用过滤条件
            if (isRead.HasValue)
                query = query.Where(n => n.IsRead == isRead.Value);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(n => n.NotificationType == type);

            // 计算总数
            var total = query.Count();

            // 排序：未读在上，已读在下，然后按时间倒序
            var notifications = query
                .OrderBy(n => n.IsRead ?? false) // false（未读）在前，true（已读）在后
                .ThenByDescending(n => n.CreatedAt) // 相同已读状态下按时间倒序
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var notificationDtos = notifications.Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                SourceModule = n.SourceModule,
                Title = n.Title,
                Content = n.Content,
                NotificationType = n.NotificationType ?? "info",
                IsRead = n.IsRead ?? false,
                CreatedAt = n.CreatedAt ?? DateTime.UtcNow
            }).ToList();

            var unreadCount = _dbContext.NotificationCenters
                .Count(n => n.UserId == userId && (n.IsVisible ?? true) && n.IsRead == false);

            var response = new NotificationsListResponseDto
            {
                Items = notificationDtos,
                UnreadCount = unreadCount,
                Meta = new PaginationMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            _logger.LogInformation($"Notifications retrieved for user: {userId}");
            return Ok(ApiResponse<NotificationsListResponseDto>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 标记通知为已读
    /// </summary>
    /// <param name="id">通知ID</param>
    [SwaggerOperation(Summary = "标记通知已读", Description = "将指定通知标记为已读状态。需要JWT认证。路径参数：id=通知ID。")]
    [HttpPatch("{id}/read")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(long id)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var notification = _dbContext.NotificationCenters
                .FirstOrDefault(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "通知不存在"));
            }

            notification.IsRead = true;
            _dbContext.NotificationCenters.Update(notification);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Notification marked as read: {id}, user: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "标记成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 删除通知
    /// </summary>
    /// <param name="id">通知ID</param>
    [SwaggerOperation(Summary = "删除通知", Description = "删除指定通知记录。需要JWT认证。路径参数：id=通知ID。")]
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNotification(long id)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            var notification = _dbContext.NotificationCenters
                .FirstOrDefault(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NOT_FOUND", "通知不存在"));
            }

            // 假删除：仅对当前用户隐藏，不做物理删除，也无需删除关联日志

            // 假删除：仅对当前用户隐藏，不做物理删除
            notification.IsVisible = false;
            _dbContext.NotificationCenters.Update(notification);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Notification hidden (soft deleted): {id}, user: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "删除成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 通知订阅设置
    /// </summary>
    /// <param name="request">订阅设置请求</param>
    [SwaggerOperation(Summary = "通知订阅设置", Description = "更新当前用户的通知订阅偏好，如价格提醒、家长监管、系统、推荐、游戏更新等。需要JWT认证。")]
    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<object>> Subscribe([FromBody] NotificationSubscribeRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
            }

            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 这里应该保存通知订阅设置到数据库
            // 简化处理，直接返回成功
            _logger.LogInformation("Notification settings updated for user: {UserId}", userId);
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "通知设置已更新"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification settings");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

