using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace PlayLinker.Controllers;

/// <summary>
/// 家长监管控制器
/// </summary>
[ApiController]
[Route("api/v1/parental")]
[Authorize]
public class ParentalController : ControllerBase
{
    private readonly PlayLinkerDbContext _dbContext;
    private readonly ILogger<ParentalController> _logger;

    public ParentalController(PlayLinkerDbContext dbContext, ILogger<ParentalController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// （旧）直接创建监管关系（使用子账户ID）
    /// </summary>
    /// <remarks>
    /// 建议改用基于邀请/令牌的流程：CreateInvitation 和 RespondInvitation。
    /// 该接口仍然保留用于兼容管理后台或测试。
    /// </remarks>
    /// <param name="request">创建监管关系请求</param>
    [SwaggerOperation(Summary = "直接创建监管关系（兼容）", Description = "家长用户与子账户建立一对一监管关系（直接使用子账户ID）。错误码：ERR_CHILD_ALREADY_SUPERVISED。需要parent角色。")]
    [HttpPost("relationships")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> CreateRelationship([FromBody] CreateParentalRelationshipRequestDto request)
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

            // 检查用户角色是否为parent或admin
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            var roleName = parentUser?.Role?.RoleName;
            if (roleName != "parent" && roleName != "admin")
            {
                return Forbid();
            }

            // 允许admin为指定的ParentUserId创建关系，否则使用当前登录用户
            var targetParentId = (roleName == "admin" && request.ParentUserId.HasValue)
                ? request.ParentUserId.Value
                : userId;

            // 若指定了不同的家长ID，校验该家长存在且为parent角色
            if (targetParentId != userId)
            {
                var targetParent = _dbContext.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == targetParentId);
                if (targetParent == null || targetParent.Role.RoleName != "parent")
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_PARENT", "指定的家长ID不存在或不是parent角色"));
                }
            }

            // 检查子账户是否存在
            var childUser = _dbContext.Users.FirstOrDefault(u => u.UserId == request.ChildUserId);
            if (childUser == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_CHILD_NOT_FOUND", "子账户不存在"));
            }

            // 检查是否已存在监管关系
            var existingRelationship = _dbContext.ParentalControlRelationships
                .FirstOrDefault(r => r.ChildUserId == request.ChildUserId);

            if (existingRelationship != null)
            {
                return Conflict(ApiResponse<object>.ErrorResponse("ERR_CHILD_ALREADY_SUPERVISED", "子账户已被监管"));
            }

            var relationship = new ParentalControlRelationship
            {
                ParentUserId = targetParentId,
                ChildUserId = request.ChildUserId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ParentalControlRelationships.Add(relationship);
            await _dbContext.SaveChangesAsync();

            var response = new
            {
                relationshipId = relationship.RelationshipId,
                parentUserId = relationship.ParentUserId,
                childUserId = relationship.ChildUserId,
                childUsername = childUser.Username,
                createdAt = relationship.CreatedAt
            };

            _logger.LogInformation($"Parental relationship created: parent {userId}, child {request.ChildUserId}");
            return CreatedAtAction(nameof(CreateRelationship), ApiResponse<object>.SuccessResponse(response, "监管关系建立成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating parental relationship");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
        /// 创建家长监管邀请（通过用户名 + 邀请令牌）
        /// </summary>
        /// <remarks>
        /// 流程说明：
        /// 1. 家长在前端输入子账户用户名，调用本接口创建邀请；
        /// 2. 系统为被邀请用户生成一条通知（SourceModule = parental_control），内容中包含唯一令牌token；
        /// 3. 子账户在通知中心中同意或拒绝邀请，调用 RespondInvitation 完成绑定。
        /// 
        /// 错误码：
        /// - ERR_CHILD_NOT_FOUND 子账户不存在
        /// - ERR_CHILD_ALREADY_SUPERVISED 子账户已被监管
        /// </remarks>
        [SwaggerOperation(Summary = "创建家长监管邀请", Description = "使用子账户用户名发起家长监管邀请，系统会给对方发送通知，待对子账户同意后正式建立监管关系。需要parent或admin角色。")]
        [HttpPost("invitations")]
        [ProducesResponseType(typeof(ApiResponse<CreateParentalInvitationResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiResponse<CreateParentalInvitationResponseDto>>> CreateInvitation([FromBody] CreateParentalInvitationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
                }

                var userIdClaim = User.FindFirst("user_id");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int parentUserId))
                {
                    return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
                }

                // 检查家长角色（允许 parent 或 admin）
                var parentUser = _dbContext.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(u => u.UserId == parentUserId);

                var roleName = parentUser?.Role?.RoleName;
                if (roleName != "parent" && roleName != "admin")
                {
                    return Forbid();
                }

                // 通过用户名查找子账户
                var childUser = _dbContext.Users.FirstOrDefault(u => u.Username == request.ChildUsername);
                if (childUser == null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_CHILD_NOT_FOUND", "子账户用户名不存在"));
                }

                if (childUser.UserId == parentUserId)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_CHILD", "不能对自己创建监管关系"));
                }

                // 检查是否已存在监管关系
                var existingRelationship = _dbContext.ParentalControlRelationships
                    .FirstOrDefault(r => r.ChildUserId == childUser.UserId);

                if (existingRelationship != null)
                {
                    return Conflict(ApiResponse<object>.ErrorResponse("ERR_CHILD_ALREADY_SUPERVISED", "子账户已被监管"));
                }

                // 生成邀请令牌
                var token = Guid.NewGuid().ToString("N");
                var expiresAt = DateTime.UtcNow.AddDays(3);

                var payload = new ParentalInvitationPayload
                {
                    Token = token,
                    ParentUserId = parentUserId,
                    ParentUsername = parentUser!.Username,
                    ChildUserId = childUser.UserId,
                    ChildUsername = childUser.Username,
                    Message = request.Message,
                    ExpiresAt = expiresAt
                };

                var contentJson = JsonSerializer.Serialize(payload);

                // 创建通知给子账户
                var notification = new NotificationCenter
                {
                    UserId = childUser.UserId,
                    SourceModule = "parental_control",
                    Title = "家长监管邀请",
                    Content = contentJson,
                    NotificationType = "info",
                    IsRead = false,
                    RelatedId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.NotificationCenters.Add(notification);
                await _dbContext.SaveChangesAsync();

                var response = new CreateParentalInvitationResponseDto
                {
                    Token = token,
                    ParentUserId = parentUserId,
                    ParentUsername = parentUser.Username,
                    ChildUserId = childUser.UserId,
                    ChildUsername = childUser.Username,
                    ExpiresAt = expiresAt
                };

                _logger.LogInformation("Parental invitation created: parent {ParentUserId}, child {ChildUserId}", parentUserId, childUser.UserId);
                return CreatedAtAction(nameof(CreateInvitation), ApiResponse<CreateParentalInvitationResponseDto>.SuccessResponse(response, "邀请已发送"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating parental invitation");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
            }
        }

        /// <summary>
        /// 子账户响应家长监管邀请（同意 / 拒绝）
        /// </summary>
        /// <remarks>
        /// - 子账户通过通知中的token调用本接口；
        /// - 同意时会正式创建 ParentalControlRelationship；
        /// - 无论同意或拒绝，都会给家长发送一条结果通知。
        /// 
        /// 错误码：
        /// - ERR_INVITE_NOT_FOUND 邀请不存在或已过期
        /// - ERR_INVITE_EXPIRED 邀请已过期
        /// - ERR_CHILD_ALREADY_SUPERVISED 子账户已被监管
        /// </remarks>
        [SwaggerOperation(Summary = "响应家长监管邀请", Description = "子账户通过token同意或拒绝家长监管邀请，同时给家长发送通知。")]
        [HttpPost("invitations/respond")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<object>>> RespondInvitation([FromBody] RespondParentalInvitationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_VALIDATION", string.Join(", ", errors)));
                }

                var userIdClaim = User.FindFirst("user_id");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int childUserId))
                {
                    return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
                }

                // 查找该用户收到的家长监管邀请通知
                var notifications = _dbContext.NotificationCenters
                    .Where(n => n.UserId == childUserId && n.SourceModule == "parental_control" && n.IsRead == false)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ParentalInvitationPayload? matchedPayload = null;
                NotificationCenter? matchedNotification = null;

                foreach (var n in notifications)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<ParentalInvitationPayload>(n.Content);
                        if (payload != null && payload.Token == request.Token)
                        {
                            matchedPayload = payload;
                            matchedNotification = n;
                            break;
                        }
                    }
                    catch
                    {
                        // 忽略解析失败的通知
                    }
                }

                if (matchedPayload == null || matchedNotification == null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVITE_NOT_FOUND", "邀请不存在或已失效"));
                }

                // 检查是否过期
                if (matchedPayload.ExpiresAt <= DateTime.UtcNow)
                {
                    matchedNotification.IsRead = true;
                    _dbContext.NotificationCenters.Update(matchedNotification);
                    await _dbContext.SaveChangesAsync();
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVITE_EXPIRED", "邀请已过期"));
                }

                // 标记原通知为已读
                matchedNotification.IsRead = true;
                _dbContext.NotificationCenters.Update(matchedNotification);

                // 如果拒绝，发送通知给家长后返回
                if (!request.Accept)
                {
                    var rejectNotification = new NotificationCenter
                    {
                        UserId = matchedPayload.ParentUserId,
                        SourceModule = "parental_control",
                        Title = "家长监管邀请被拒绝",
                        Content = $"{matchedPayload.ChildUsername} 拒绝了你的家长监管请求。",
                        NotificationType = "info",
                        IsRead = false,
                        RelatedId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.NotificationCenters.Add(rejectNotification);

                    await _dbContext.SaveChangesAsync();
                    return Ok(ApiResponse<object>.SuccessResponse(new { }, "已拒绝邀请"));
                }

                // 同意：检查是否已存在监管关系
                var existingRelationship = _dbContext.ParentalControlRelationships
                    .FirstOrDefault(r => r.ChildUserId == matchedPayload.ChildUserId);

                if (existingRelationship != null)
                {
                    await _dbContext.SaveChangesAsync();
                    return Conflict(ApiResponse<object>.ErrorResponse("ERR_CHILD_ALREADY_SUPERVISED", "子账户已被监管"));
                }

                // 创建监管关系
                var relationship = new ParentalControlRelationship
                {
                    ParentUserId = matchedPayload.ParentUserId,
                    ChildUserId = matchedPayload.ChildUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.ParentalControlRelationships.Add(relationship);

                // 给家长发送同意通知
                var acceptNotification = new NotificationCenter
                {
                    UserId = matchedPayload.ParentUserId,
                    SourceModule = "parental_control",
                    Title = "家长监管邀请已接受",
                    Content = $"{matchedPayload.ChildUsername} 已同意你的家长监管请求。",
                    NotificationType = "info",
                    IsRead = false,
                    RelatedId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.NotificationCenters.Add(acceptNotification);

                await _dbContext.SaveChangesAsync();

                var response = new
                {
                    relationshipId = relationship.RelationshipId,
                    parentUserId = relationship.ParentUserId,
                    childUserId = relationship.ChildUserId,
                    createdAt = relationship.CreatedAt
                };

                _logger.LogInformation("Parental invitation accepted: parent {ParentUserId}, child {ChildUserId}", matchedPayload.ParentUserId, matchedPayload.ChildUserId);
                return Ok(ApiResponse<object>.SuccessResponse(response, "已建立家长监管关系"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error responding to parental invitation");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取子账户列表
    /// </summary>
    [SwaggerOperation(Summary = "获取子账户列表", Description = "家长用户获取其名下所有被监管的子账户信息，包括活跃规则数、今日时长、近期违规次数等。需要parent角色。")]
    [HttpGet("children")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<object>> GetChildren()
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            if (parentUser?.Role?.RoleName != "parent")
            {
                return Forbid();
            }

            var children = _dbContext.ParentalControlRelationships
                .Where(r => r.ParentUserId == userId)
                .Include(r => r.ChildUser)
                .ToList();

            var childrenDtos = children.Select(r => new
            {
                parentUserId = userId,
                childUserId = r.ChildUser.UserId,
                childUsername = r.ChildUser.Username,
                activeRulesCount = _dbContext.ParentalControlRules
                    .Count(rule => rule.ChildUserId == r.ChildUser.UserId && rule.IsActive == true),
                todayPlaytime = 0, // 这里应该从游戏时间记录中计算
                recentAlerts = _dbContext.ParentalAlertLogs
                    .Count(alert => alert.ChildUserId == r.ChildUser.UserId && 
                                   alert.AlertTime >= DateTime.UtcNow.AddDays(-1))
            }).ToList();

            var response = new
            {
                parentUserId = userId,
                children = childrenDtos,
                totalCount = childrenDtos.Count
            };

            _logger.LogInformation($"Children list retrieved for parent: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving children");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 设置监管规则
    /// </summary>
    /// <param name="request">规则设置请求</param>
    [SwaggerOperation(Summary = "设置监管规则", Description = "为子账户设置家长监管规则，支持：playtime_daily_limit、playtime_curfew、spending_limit、game_restriction、age_restriction。需要parent角色。")]
    [HttpPost("rules")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> SetRule([FromBody] SetParentalRuleRequestDto request)
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

            // 检查用户角色（允许parent或admin）
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            var roleNameForRule = parentUser?.Role?.RoleName;
            if (roleNameForRule != "parent" && roleNameForRule != "admin")
            {
                return Forbid();
            }

            // 允许admin指定ParentUserId，否则默认当前用户
            var targetParentIdForRule = (roleNameForRule == "admin" && request.ParentUserId.HasValue)
                ? request.ParentUserId.Value
                : userId;

            if (targetParentIdForRule != userId && roleNameForRule == "admin")
            {
                var targetParent = _dbContext.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == targetParentIdForRule);
                if (targetParent == null || targetParent.Role.RoleName != "parent")
                {
                    return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_PARENT", "指定的家长ID不存在或不是parent角色"));
                }
            }

            // 检查监管关系
            var relationship = _dbContext.ParentalControlRelationships
                .FirstOrDefault(r => r.ParentUserId == targetParentIdForRule && r.ChildUserId == request.ChildUserId);

            if (relationship == null)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_NO_RELATIONSHIP", "没有监管关系"));
            }

            // 验证规则类型
            var validRuleTypes = new[] { "playtime_daily_limit", "playtime_curfew", "spending_limit", "game_restriction", "age_restriction" };
            if (!validRuleTypes.Contains(request.RuleType))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_RULE_TYPE", "无效的规则类型"));
            }

            var rule = new ParentalControlRule
            {
                ChildUserId = request.ChildUserId,
                RuleType = request.RuleType,
                RuleValue = JsonSerializer.Serialize(request.RuleValue),
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ParentalControlRules.Add(rule);
            await _dbContext.SaveChangesAsync();

            var response = new
            {
                ruleId = rule.RuleId,
                ruleType = rule.RuleType,
                createdAt = rule.CreatedAt
            };

            _logger.LogInformation($"Parental rule set: parent {userId}, child {request.ChildUserId}, rule {request.RuleType}");
            return CreatedAtAction(nameof(SetRule), ApiResponse<object>.SuccessResponse(response, "规则设置成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting parental rule");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 更新监管规则
    /// </summary>
    /// <param name="ruleId">规则ID</param>
    /// <param name="request">规则更新请求</param>
    [SwaggerOperation(Summary = "更新监管规则", Description = "更新指定规则的规则值和激活状态。需要parent角色。")]
    [HttpPut("rules/{ruleId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRule(int ruleId, [FromBody] UpdateParentalRuleRequestDto request)
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

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            var roleName = parentUser?.Role?.RoleName;
            if (roleName != "parent" && roleName != "admin")
            {
                return Forbid();
            }

            // 查找规则
            var rule = _dbContext.ParentalControlRules
                .FirstOrDefault(r => r.RuleId == ruleId);

            if (rule == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_RULE_NOT_FOUND", "规则不存在"));
            }

            // 检查监管关系（确保该规则属于当前家长监管的子账户）
            var relationship = _dbContext.ParentalControlRelationships
                .FirstOrDefault(r => r.ParentUserId == userId && r.ChildUserId == rule.ChildUserId);

            if (relationship == null && roleName != "admin")
            {
                return Forbid();
            }

            // 更新规则
            // 只有当 ruleValue 不为空且不是空对象时才更新规则值
            // 如果 ruleValue 是空对象或 null，则保持原有规则值不变（只更新 isActive）
            if (request.RuleValue != null)
            {
                var ruleValueJson = JsonSerializer.Serialize(request.RuleValue);
                // 检查是否是空对象 "{}" 或 "null"
                var isEmpty = ruleValueJson == "{}" || ruleValueJson == "null" || string.IsNullOrWhiteSpace(ruleValueJson);
                if (!isEmpty)
                {
                    rule.RuleValue = ruleValueJson;
                }
                // 如果 ruleValue 是空对象，说明前端只想更新 isActive，保持原有规则值不变
            }
            // 始终更新 isActive 状态
            rule.IsActive = request.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            var response = new
            {
                ruleId = rule.RuleId,
                ruleType = rule.RuleType,
                updatedAt = rule.UpdatedAt
            };

            _logger.LogInformation($"Parental rule updated: rule {ruleId}, parent {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "规则更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating parental rule");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 批量禁用用户的所有规则
    /// </summary>
    /// <param name="parentUserId">家长用户ID</param>
    [SwaggerOperation(Summary = "批量禁用用户的所有规则", Description = "禁用指定家长用户的所有监管规则。需要admin角色或用户本人。")]
    [HttpPatch("rules/disable-all/{parentUserId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> DisableAllRules(int parentUserId)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 检查用户角色
            var currentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            var roleName = currentUser?.Role?.RoleName;
            
            // 只允许admin或用户本人操作
            if (roleName != "admin" && userId != parentUserId)
            {
                return Forbid();
            }

            // 获取该用户作为家长的所有监管关系
            var relationships = _dbContext.ParentalControlRelationships
                .Where(r => r.ParentUserId == parentUserId)
                .Select(r => r.ChildUserId)
                .ToList();

            if (relationships.Count == 0)
            {
                return Ok(ApiResponse<object>.SuccessResponse(new { disabledCount = 0 }, "没有需要禁用的规则"));
            }

            // 批量禁用所有规则
            var rules = _dbContext.ParentalControlRules
                .Where(r => relationships.Contains(r.ChildUserId))
                .ToList();

            var disabledCount = 0;
            foreach (var rule in rules)
            {
                if (rule.IsActive == true)
                {
                    rule.IsActive = false;
                    rule.UpdatedAt = DateTime.UtcNow;
                    disabledCount++;
                }
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Disabled {disabledCount} rules for parent user: {parentUserId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { disabledCount }, $"已禁用 {disabledCount} 条规则"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling all rules");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 删除监管规则
    /// </summary>
    /// <param name="ruleId">规则ID</param>
    [SwaggerOperation(Summary = "删除监管规则", Description = "删除指定的监管规则。需要parent角色。")]
    [HttpDelete("rules/{ruleId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRule(int ruleId)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            var roleName = parentUser?.Role?.RoleName;
            if (roleName != "parent" && roleName != "admin")
            {
                return Forbid();
            }

            // 查找规则
            var rule = _dbContext.ParentalControlRules
                .FirstOrDefault(r => r.RuleId == ruleId);

            if (rule == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_RULE_NOT_FOUND", "规则不存在"));
            }

            // 检查监管关系（确保该规则属于当前家长监管的子账户）
            var relationship = _dbContext.ParentalControlRelationships
                .FirstOrDefault(r => r.ParentUserId == userId && r.ChildUserId == rule.ChildUserId);

            if (relationship == null && roleName != "admin")
            {
                return Forbid();
            }

            // 删除规则
            _dbContext.ParentalControlRules.Remove(rule);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Parental rule deleted: rule {ruleId}, parent {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(new { }, "规则删除成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting parental rule");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取规则列表
    /// </summary>
    /// <param name="childId">子账户ID</param>
    [SwaggerOperation(Summary = "获取规则列表", Description = "获取指定子账户的家长监管规则列表，并返回违规统计。需要parent角色与有效监管关系。路径参数：childId=子账户用户ID。")]
    [HttpGet("rules/{childId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<object>> GetRules(int childId)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            if (parentUser?.Role?.RoleName != "parent")
            {
                return Forbid();
            }

            // 检查监管关系
            var relationship = _dbContext.ParentalControlRelationships
                .FirstOrDefault(r => r.ParentUserId == userId && r.ChildUserId == childId);

            if (relationship == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NO_RELATIONSHIP", "没有监管关系"));
            }

            var childUser = _dbContext.Users.FirstOrDefault(u => u.UserId == childId);
            var rules = _dbContext.ParentalControlRules
                .Where(r => r.ChildUserId == childId)
                .ToList();

            var rulesDtos = rules.Select(r => new
            {
                ruleId = r.RuleId,
                ruleType = r.RuleType,
                ruleValue = JsonSerializer.Deserialize<object>(r.RuleValue),
                isActive = r.IsActive,
                statistics = new
                {
                    totalViolations = _dbContext.ParentalAlertLogs
                        .Count(alert => alert.RuleId == r.RuleId),
                    recentViolations = _dbContext.ParentalAlertLogs
                        .Count(alert => alert.RuleId == r.RuleId && 
                                       alert.AlertTime >= DateTime.UtcNow.AddDays(-7))
                }
            }).ToList();

            var response = new
            {
                parentUserId = userId,
                childUserId = childId,
                childUsername = childUser?.Username,
                rules = rulesDtos,
                totalCount = rulesDtos.Count
            };

            _logger.LogInformation($"Rules retrieved for child: {childId}, parent: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rules");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取违规记录
    /// </summary>
    /// <param name="childId">子账户ID (可选)</param>
    /// <param name="ruleType">规则类型 (可选)</param>
    /// <param name="startDate">开始日期 (可选)</param>
    /// <param name="endDate">结束日期 (可选)</param>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页数量，默认20</param>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<object>> GetAlerts(
        [FromQuery] int? childId = null,
        [FromQuery] string? ruleType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
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

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            if (parentUser?.Role?.RoleName != "parent")
            {
                return Forbid();
            }

            // 验证分页参数
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _dbContext.ParentalAlertLogs
                .Include(a => a.ChildUser)
                .Include(a => a.Rule)
                .AsQueryable();

            // 获取该家长的所有子账户
            var childrenIds = _dbContext.ParentalControlRelationships
                .Where(r => r.ParentUserId == userId)
                .Select(r => r.ChildUserId)
                .ToList();

            query = query.Where(a => childrenIds.Contains(a.ChildUserId));

            // 应用过滤条件
            if (childId.HasValue)
                query = query.Where(a => a.ChildUserId == childId.Value);

            if (!string.IsNullOrEmpty(ruleType))
                query = query.Where(a => a.Rule.RuleType == ruleType);

            if (startDate.HasValue)
                query = query.Where(a => a.AlertTime >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.AlertTime <= endDate.Value);

            // 计算总数
            var total = query.Count();

            // 分页
            var alerts = query
                .OrderByDescending(a => a.AlertTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var alertsDtos = alerts.Select(a => new
            {
                parentUserId = userId,
                alertId = a.AlertId,
                ruleType = a.Rule?.RuleType,
                childUserId = a.ChildUserId,
                childUsername = a.ChildUser?.Username,
                violationDetails = JsonSerializer.Deserialize<object>(a.ViolationDetails ?? "{}"),
                alertTime = a.AlertTime,
                severity = a.Severity ?? "warning"
            }).ToList();

            var response = new
            {
                items = alertsDtos,
                meta = new
                {
                    page = page,
                    pageSize = pageSize,
                    total = total
                }
            };

            _logger.LogInformation($"Alerts retrieved for parent: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取家长信息（供子账户使用）
    /// </summary>
    [SwaggerOperation(Summary = "获取家长信息", Description = "子账户获取其家长用户信息。需要已建立监管关系。")]
    [HttpGet("parent")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<object>> GetParent()
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 查找该用户作为子账户的监管关系
            var relationship = _dbContext.ParentalControlRelationships
                .Include(r => r.ParentUser)
                .FirstOrDefault(r => r.ChildUserId == userId);

            if (relationship == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NO_RELATIONSHIP", "未找到监管关系"));
            }

            var response = new
            {
                parentUserId = relationship.ParentUser.UserId,
                parentUsername = relationship.ParentUser.Username,
                relationshipId = relationship.RelationshipId,
                createdAt = relationship.CreatedAt
            };

            _logger.LogInformation($"Parent info retrieved for child: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parent info");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 删除监管关系（仅家长可以删除）
    /// </summary>
    /// <param name="childId">子账户ID</param>
    [SwaggerOperation(Summary = "删除监管关系", Description = "家长单方面解除与子账户的监管关系，并通知子账户。需要parent角色。")]
    [HttpDelete("relationships/{childId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRelationship(int childId)
    {
        try
        {
            var userIdClaim = User.FindFirst("user_id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.ErrorResponse("ERR_UNAUTHORIZED", "未认证"));
            }

            // 检查用户角色
            var parentUser = _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == userId);

            if (parentUser?.Role?.RoleName != "parent")
            {
                return Forbid();
            }

            // 查找监管关系
            var relationship = _dbContext.ParentalControlRelationships
                .Include(r => r.ChildUser)
                .FirstOrDefault(r => r.ParentUserId == userId && r.ChildUserId == childId);

            if (relationship == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_NO_RELATIONSHIP", "未找到监管关系"));
            }

            var childUserId = relationship.ChildUserId;
            var childUsername = relationship.ChildUser.Username;
            var parentUsername = parentUser.Username;

            // 删除监管关系
            _dbContext.ParentalControlRelationships.Remove(relationship);

            // 删除相关的规则
            var rules = _dbContext.ParentalControlRules
                .Where(r => r.ChildUserId == childId)
                .ToList();
            _dbContext.ParentalControlRules.RemoveRange(rules);

            // 创建通知给子账户
            var notification = new NotificationCenter
            {
                UserId = childUserId,
                SourceModule = "parental_control",
                NotificationType = "relationship_terminated",
                Title = "监管关系已解除",
                Content = JsonSerializer.Serialize(new
                {
                    parentUserId = userId,
                    parentUsername = parentUsername,
                    childUserId = childUserId,
                    childUsername = childUsername,
                    terminatedAt = DateTime.UtcNow,
                    message = $"家长 {parentUsername} 已解除与您的监管关系"
                }),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.NotificationCenters.Add(notification);

            await _dbContext.SaveChangesAsync();

            var response = new
            {
                relationshipId = relationship.RelationshipId,
                parentUserId = userId,
                childUserId = childId,
                deletedAt = DateTime.UtcNow
            };

            _logger.LogInformation($"Parental relationship deleted: parent {userId}, child {childId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "监管关系已解除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting parental relationship");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

/// <summary>
/// 创建监管关系请求DTO
/// </summary>
public class CreateParentalRelationshipRequestDto
{
    /// <summary>
    /// 子账户用户ID
    /// </summary>
    public int ChildUserId { get; set; }

    /// <summary>
    /// 家长用户ID（可选）。默认为当前登录用户；仅admin可指定他人。
    /// </summary>
    public int? ParentUserId { get; set; }
}

/// <summary>
/// 设置监管规则请求DTO
/// </summary>
public class SetParentalRuleRequestDto
{
    /// <summary>
    /// 子账户用户ID
    /// </summary>
    public int ChildUserId { get; set; }

    /// <summary>
    /// 家长用户ID（可选）。默认为当前登录用户；仅admin可指定他人。
    /// </summary>
    public int? ParentUserId { get; set; }

    /// <summary>
    /// 规则类型
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// 规则值
    /// </summary>
    public object RuleValue { get; set; } = new { };

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 更新监管规则请求DTO
/// </summary>
public class UpdateParentalRuleRequestDto
{
    /// <summary>
    /// 规则值
    /// </summary>
    public object RuleValue { get; set; } = new { };

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 创建家长监管邀请请求DTO（基于用户名）
/// </summary>
public class CreateParentalInvitationRequestDto
{
    /// <summary>
    /// 子账户用户名
    /// </summary>
    [Required(ErrorMessage = "子账户用户名不能为空")]
    [StringLength(128)]
    public string ChildUsername { get; set; } = string.Empty;

    /// <summary>
    /// 给对方的附加留言（可选）
    /// </summary>
    [StringLength(500)]
    public string? Message { get; set; }
}

/// <summary>
/// 创建家长监管邀请响应DTO
/// </summary>
public class CreateParentalInvitationResponseDto
{
    /// <summary>
    /// 邀请令牌（仅用于前端调试或日志，不建议在URL中长期暴露）
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public int ParentUserId { get; set; }
    public string ParentUsername { get; set; } = string.Empty;

    public int ChildUserId { get; set; }
    public string ChildUsername { get; set; } = string.Empty;

    /// <summary>
    /// 邀请过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// 子账户响应家长监管邀请请求DTO
/// </summary>
public class RespondParentalInvitationRequestDto
{
    /// <summary>
    /// 邀请令牌
    /// </summary>
    [Required(ErrorMessage = "邀请令牌不能为空")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 是否同意（true=同意，false=拒绝）
    /// </summary>
    public bool Accept { get; set; }
}

/// <summary>
/// 存储在通知Content中的家长邀请载荷
/// </summary>
public class ParentalInvitationPayload
{
    public string Token { get; set; } = string.Empty;
    public int ParentUserId { get; set; }
    public string ParentUsername { get; set; } = string.Empty;
    public int ChildUserId { get; set; }
    public string ChildUsername { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ExpiresAt { get; set; }
}

