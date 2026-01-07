using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;
using PlayLinker.Services;

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
    private readonly IServiceProvider _serviceProvider;

    public ParentalController(PlayLinkerDbContext dbContext, ILogger<ParentalController> logger, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
    /// 获取子账户过去一周的游玩时间统计
    /// </summary>
    /// <param name="childId">子账户ID</param>
    [SwaggerOperation(Summary = "获取子账户过去一周游玩时间", Description = "获取指定子账户过去7天每天的累计游玩时间（分钟）。需要parent角色与有效监管关系。")]
    [HttpGet("children/{childId}/weekly-playtime")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetChildWeeklyPlaytime(int childId)
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

            // 获取过去7天的日期范围（注意：由于数据在每天2点更新，今天的数据实际上是昨天的）
            // 所以我们要显示的是：昨天、前天、...、7天前（不包含今天）
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1); // 昨天（这是最新有数据的日期）
            var sevenDaysAgo = yesterday.AddDays(-6); // 7天前

            // 查询过去7天的游玩时间历史记录（从7天前到今天）
            // 注意：由于数据在每天凌晨2点更新，RecordDate = today 的数据实际上是 yesterday 的累计数据
            // 所以要计算 yesterday 的增量，需要 RecordDate = today 的数据 - RecordDate = yesterday 的数据
            // 因此需要包含 today 的数据
            var historyRecords = await _dbContext.UserPlaytimeHistories
                .Where(h => h.UserId == childId 
                    && h.RecordDate >= sevenDaysAgo 
                    && h.RecordDate <= today) // 包含今天，因为今天的数据是昨天的累计数据
                .ToListAsync();

            // 按日期分组，计算每天的总游玩时间（分钟）
            var dailyPlaytime = new Dictionary<DateTime, int>();

            // 初始化过去7天的日期（从7天前到昨天，不包含今天）
            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                dailyPlaytime[date] = 0;
            }

            // 按日期和游戏分组
            var groupedByDate = historyRecords
                .GroupBy(h => h.RecordDate.Date)
                .ToList();

            foreach (var dateGroup in groupedByDate)
            {
                var recordDate = dateGroup.Key; // 数据库中的记录日期
                // 注意：由于数据在每天凌晨2点更新，RecordDate 存储的是更新当天的日期
                // 例如：周三凌晨2点更新时，记录的是周二的累计数据，RecordDate = 周三
                // 所以要显示周二的游玩时间，应该用 RecordDate = 周三 的数据 - RecordDate = 周二 的数据
                // 因此，actualDate = recordDate - 1（因为 recordDate 的数据是 actualDate 的累计）
                var actualDate = recordDate.AddDays(-1); // 实际对应的日期（因为数据延迟1天）
                
                // 如果实际日期不在我们要显示的范围内，跳过（只显示到昨天，不包含今天）
                // 如果 recordDate = today，那么 actualDate = yesterday，这是我们要显示的
                // 如果 recordDate = yesterday，那么 actualDate = yesterday - 1，这也是我们要显示的
                // 但如果 recordDate < sevenDaysAgo，那么 actualDate < sevenDaysAgo - 1，应该跳过
                if (actualDate < sevenDaysAgo || actualDate > yesterday)
                {
                    continue;
                }

                // 获取前一天的记录（用于计算增量）
                // 要计算 actualDate 的增量，需要 recordDate 的数据 - (recordDate - 1) 的数据
                var previousRecordDate = recordDate.AddDays(-1); // 前一天的记录日期
                var previousRecords = await _dbContext.UserPlaytimeHistories
                    .Where(h => h.UserId == childId && h.RecordDate == previousRecordDate)
                    .ToDictionaryAsync(h => new { h.GameId, h.PlatformId }, h => h.PlaytimeForever);

                int dailyTotal = 0;

                // 按游戏和平台分组，计算增量
                var gameGroups = dateGroup.GroupBy(h => new { h.GameId, h.PlatformId });
                foreach (var gameGroup in gameGroups)
                {
                    var currentMax = gameGroup.Max(h => h.PlaytimeForever);
                    var key = new { gameGroup.Key.GameId, gameGroup.Key.PlatformId };
                    
                    if (previousRecords.TryGetValue(key, out var previousPlaytime))
                    {
                        // 计算增量：当前记录的总时长 - 前一天记录的总时长
                        var increment = currentMax - previousPlaytime;
                        if (increment > 0)
                        {
                            dailyTotal += increment;
                        }
                    }
                    else
                    {
                        // 新游戏：如果总时长较小（小于8小时），可能是当天开始玩的
                        if (currentMax > 0 && currentMax < 480)
                        {
                            dailyTotal += currentMax;
                        }
                    }
                }

                dailyPlaytime[actualDate] = dailyTotal;
            }

            // 转换为数组格式，按日期排序
            var weeklyData = dailyPlaytime
                .Select(kvp => new
                {
                    date = kvp.Key.ToString("yyyy-MM-dd"),
                    playtimeMinutes = kvp.Value,
                    dayOfWeek = GetDayOfWeekChinese(kvp.Key.DayOfWeek)
                })
                .OrderBy(d => d.date)
                .ToList();

            var response = new
            {
                childId = childId,
                weeklyData = weeklyData,
                totalMinutes = weeklyData.Sum(d => d.playtimeMinutes)
            };

            _logger.LogInformation($"Weekly playtime retrieved for child: {childId}, parent: {userId}");
            return Ok(ApiResponse<object>.SuccessResponse(response, "获取成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weekly playtime for child {ChildId}", childId);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取星期几的中文名称
    /// </summary>
    private string GetDayOfWeekChinese(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            DayOfWeek.Sunday => "周日",
            _ => ""
        };
    }

    /// <summary>
    /// 获取子账户列表
    /// </summary>
    [SwaggerOperation(Summary = "获取子账户列表", Description = "家长用户获取其名下所有被监管的子账户信息，包括活跃规则数、今日时长、近期违规次数等。需要parent角色。")]
    [HttpGet("children")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> GetChildren()
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

            var childrenDtos = new List<object>();
            foreach (var r in children)
            {
                var todayPlaytime = await CalculateTodayPlaytimeAsync(r.ChildUser.UserId);
                childrenDtos.Add(new
                {
                    parentUserId = userId,
                    childUserId = r.ChildUser.UserId,
                    childUsername = r.ChildUser.Username,
                    activeRulesCount = _dbContext.ParentalControlRules
                        .Count(rule => rule.ChildUserId == r.ChildUser.UserId && rule.IsActive == true),
                    todayPlaytime = todayPlaytime,
                    recentAlerts = _dbContext.ParentalAlertLogs
                        .Count(alert => alert.ChildUserId == r.ChildUser.UserId && 
                                       alert.AlertTime >= DateTime.UtcNow.AddDays(-1))
                });
            }

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
    [SwaggerOperation(Summary = "设置监管规则", Description = "为子账户设置家长监管规则，支持：playtime_daily_limit、playtime_curfew、game_restriction、age_restriction。需要parent角色。")]
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
            var validRuleTypes = new[] { "playtime_daily_limit", "playtime_curfew", "game_restriction", "age_restriction" };
            if (!validRuleTypes.Contains(request.RuleType))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_RULE_TYPE", "无效的规则类型"));
            }
            
            // 验证游戏限制规则的数据格式（应使用游戏名而非游戏ID）
            if (request.RuleType == "game_restriction" && request.RuleValue != null)
            {
                try
                {
                    var ruleValueJson = JsonSerializer.Serialize(request.RuleValue);
                    var ruleValueObj = JsonSerializer.Deserialize<Dictionary<string, object>>(ruleValueJson);
                    
                    // 检查是否包含 blockedGameNames（新格式）或 blockedGameIds（旧格式，已废弃）
                    if (ruleValueObj != null)
                    {
                        // 如果只有 blockedGameIds 而没有 blockedGameNames，提示用户使用新格式
                        if (ruleValueObj.ContainsKey("blockedGameIds") && !ruleValueObj.ContainsKey("blockedGameNames"))
                        {
                            return BadRequest(ApiResponse<object>.ErrorResponse("ERR_DEPRECATED_FORMAT", "游戏限制规则已改为使用游戏名称（blockedGameNames），请使用游戏名称而非游戏ID"));
                        }
                        
                        // 验证 blockedGameNames 是否为字符串数组
                        if (ruleValueObj.ContainsKey("blockedGameNames"))
                        {
                            var gameNames = JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(ruleValueObj["blockedGameNames"]));
                            if (gameNames == null || gameNames.Any(name => string.IsNullOrWhiteSpace(name)))
                            {
                                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_RULE_VALUE", "游戏名称列表不能为空或包含空字符串"));
                            }
                        }
                    }
                }
                catch
                {
                    // JSON 解析失败，让后续代码处理
                }
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

            // 如果规则已启用，立即检查一次并通知家长
            if (request.IsActive)
            {
                try
                {
                    await CheckRuleAndNotifyAsync(rule);
                    _logger.LogInformation("规则 {RuleId} 已创建并立即检查，如有违规已通知家长", rule.RuleId);
                }
                catch (Exception ex)
                {
                    // 检测失败不影响规则创建
                    _logger.LogWarning(ex, "规则 {RuleId} 创建后立即检查失败，将在下次定时检查时生效", rule.RuleId);
                }
            }
            else
            {
                _logger.LogInformation("规则 {RuleId} 已创建（未启用），将在启用后检查", rule.RuleId);
            }

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

            // 如果规则已启用，立即检查一次并通知家长
            if (rule.IsActive == true)
            {
                try
                {
                    await CheckRuleAndNotifyAsync(rule);
                    _logger.LogInformation("规则 {RuleId} 已更新并立即检查，如有违规已通知家长", rule.RuleId);
                }
                catch (Exception ex)
                {
                    // 检测失败不影响规则更新
                    _logger.LogWarning(ex, "规则 {RuleId} 更新后立即检查失败，将在下次定时检查时生效", rule.RuleId);
                }
            }
            else
            {
                _logger.LogInformation("规则 {RuleId} 已更新（未启用）", rule.RuleId);
            }

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

             // 先删除相关的报警日志（避免外键约束错误）
            var alertLogs = await _dbContext.ParentalAlertLogs
                .Where(l => l.RuleId == ruleId)
                .ToListAsync();

            if (alertLogs.Any())
            {
                _dbContext.ParentalAlertLogs.RemoveRange(alertLogs);
                _logger.LogInformation($"删除规则 {ruleId} 相关的 {alertLogs.Count} 条报警日志");
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
                NotificationType = "warning", // 使用合法的通知类型
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
                RelatedId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // 生成唯一ID
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

    /// <summary>
    /// 计算用户昨日游戏时长（基于PlaytimeForever字段：今天的值 - 昨天的值）
    /// 注意：由于数据在每天凌晨2点更新，今天的数据实际上是昨天的累计数据
    /// 所以"今日时长"实际显示的是"昨日"的时长
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>昨日游戏时长（分钟）</returns>
    private async Task<int> CalculateTodayPlaytimeAsync(int userId)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var dayBeforeYesterday = yesterday.AddDays(-1);

            // 获取昨天的PlaytimeForever快照数据（实际上是前天的累计数据，用于计算昨天的增量）
            var yesterdayRecords = await _dbContext.UserPlaytimeHistories
                .Where(h => h.UserId == userId && h.RecordDate == yesterday)
                .ToDictionaryAsync(h => h.GameId, h => h.PlaytimeForever);

            int currentMinutes = 0;

            // 方法1：如果今天已有快照数据（实际上是昨天的累计数据），使用它来计算昨天的增量
            var todayRecords = await _dbContext.UserPlaytimeHistories
                .Where(h => h.UserId == userId && h.RecordDate == today)
                .ToListAsync();

            if (todayRecords.Any())
            {
                // 今天的记录实际上是昨天的累计数据
                // 使用今天的PlaytimeForever - 昨天的PlaytimeForever（前天的累计）计算昨天的增量
                foreach (var todayRecord in todayRecords)
                {
                    if (yesterdayRecords.TryGetValue(todayRecord.GameId, out var yesterdayPlaytime))
                    {
                        // 计算增量：今天的总时长（实际是昨天的累计）- 昨天的总时长（实际是前天的累计）= 昨天的新增时长
                        var dailyIncrement = todayRecord.PlaytimeForever - yesterdayPlaytime;
                        if (dailyIncrement > 0)
                        {
                            currentMinutes += dailyIncrement;
                        }
                    }
                    else
                    {
                        // 新游戏：如果今天有记录但昨天没有，且PlaytimeForever较小，可能是昨天开始玩的
                        if (todayRecord.PlaytimeForever > 0 && todayRecord.PlaytimeForever < 480) // 8小时内认为是昨天玩的
                        {
                            currentMinutes += todayRecord.PlaytimeForever;
                        }
                    }
                }
            }
            // 方法2：如果今天还没有快照，使用昨天的记录计算（昨天的记录 - 前天的记录）
            // 注意：这种情况下，昨天的记录实际上是前天的累计，前天的记录是大前天的累计
            // 所以计算出来的是前天的增量，不是昨天的增量
            // 但为了保持一致性，我们仍然返回这个值，或者返回0表示数据尚未更新
            else
            {
                // 获取前天的PlaytimeForever快照数据（实际上是大前天的累计数据）
                var dayBeforeYesterdayRecords = await _dbContext.UserPlaytimeHistories
                    .Where(h => h.UserId == userId && h.RecordDate == dayBeforeYesterday)
                    .ToDictionaryAsync(h => h.GameId, h => h.PlaytimeForever);

                if (yesterdayRecords.Any())
                {
                    // 使用昨天的记录（前天的累计）- 前天的记录（大前天的累计）计算前天的增量
                    // 注意：这不是昨天的增量，而是前天的增量
                    // 但由于今天的数据还没更新，我们暂时返回0，表示数据尚未更新
                    // 或者可以选择返回前天的增量作为参考
                    // 这里我们返回0，表示等待今天的数据更新
                    return 0;
                }
                else
                {
                    // 如果昨天也没有记录，尝试实时从Steam API获取
                    // 获取用户的Steam绑定信息
                    var steamBinding = await _dbContext.UserPlatformBindings
                        .Include(b => b.PlayerPlatform)
                        .FirstOrDefaultAsync(b => b.UserId == userId && b.PlatformId == 1 && b.BindingStatus == true);

                    if (steamBinding != null && !string.IsNullOrEmpty(steamBinding.AccessToken))
                    {
                        try
                        {
                            // 实时调用Steam API获取最新的PlaytimeForever
                            var tokenService = _serviceProvider.GetRequiredService<ITokenEncryptionService>();
                            var apiKey = tokenService.DecryptToken(steamBinding.AccessToken);
                            var steamId = steamBinding.PlatformUserId;

                            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(steamId))
                            {
                                var httpClient = new HttpClient();
                                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId)}&include_appinfo=false&include_played_free_games=true";
                                var response = await httpClient.GetAsync(url);

                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                                    if (doc.RootElement.TryGetProperty("response", out var responseEl) &&
                                        responseEl.TryGetProperty("games", out var gamesEl))
                                    {
                                        var knownGamesMap = await _dbContext.GamePlatforms
                                            .Where(gp => gp.PlatformId == 1)
                                            .ToDictionaryAsync(gp => gp.PlatformGameId, gp => gp.GameId);

                                        foreach (var gameItem in gamesEl.EnumerateArray())
                                        {
                                            var appId = gameItem.GetProperty("appid").GetInt32();
                                            var playtimeForever = gameItem.GetProperty("playtime_forever").GetInt32();

                                            if (playtimeForever > 0 && knownGamesMap.TryGetValue(appId.ToString(), out var gameId))
                                            {
                                                // 使用昨天的记录（实际上是前天的累计）来计算昨天的增量
                                                if (yesterdayRecords.TryGetValue(gameId, out var yesterdayPlaytime))
                                                {
                                                    // 使用PlaytimeForever计算增量：当前值（实际是昨天的累计）- 昨天的值（实际是前天的累计）= 昨天的增量
                                                    var dailyIncrement = playtimeForever - yesterdayPlaytime;
                                                    if (dailyIncrement > 0)
                                                    {
                                                        currentMinutes += dailyIncrement;
                                                    }
                                                }
                                                else
                                                {
                                                    // 新游戏：如果总时长较小，可能是昨天开始玩的
                                                    if (playtimeForever < 480) // 8小时内认为是昨天玩的
                                                    {
                                                        currentMinutes += playtimeForever;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "实时获取Steam PlaytimeForever数据失败: userId={UserId}", userId);
                        }
                    }
                }
            }

            return currentMinutes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算用户 {UserId} 今日游戏时长时发生错误", userId);
            return 0;
        }
    }

    /// <summary>
    /// 检查规则并通知家长（立即检测）
    /// </summary>
    private async Task CheckRuleAndNotifyAsync(ParentalControlRule rule)
    {
        try
        {
            // 重新加载规则及其关联数据
            var ruleWithChild = await _dbContext.ParentalControlRules
                .Include(r => r.ChildUser)
                .FirstOrDefaultAsync(r => r.RuleId == rule.RuleId);

            // IsActive 在实体中是 bool?，这里明确按“未启用/空值”处理为 false
            if (ruleWithChild == null || ruleWithChild.IsActive != true)
            {
                return;
            }

            // 解析规则值
            var ruleValue = JsonSerializer.Deserialize<JsonElement>(ruleWithChild.RuleValue);
            var now = DateTime.UtcNow;
            var today = now.Date;

            // 检查今天是否已经发送过该规则的提醒（避免重复通知）
            var todayAlert = await _dbContext.ParentalAlertLogs
                .Where(l => l.RuleId == ruleWithChild.RuleId
                    && l.AlertTime.HasValue
                    && l.AlertTime.Value.Date == today)
                .FirstOrDefaultAsync();

            if (todayAlert != null)
            {
                _logger.LogDebug("规则 {RuleId} 今天已发送过提醒，跳过立即检测", ruleWithChild.RuleId);
                return;
            }

            bool hasViolation = false;
            string violationType = "";
            Dictionary<string, object> violationDetails = new();

            // 根据规则类型进行检查
            switch (ruleWithChild.RuleType)
            {
                case "playtime_daily_limit":
                    hasViolation = await CheckPlaytimeDailyLimitAsync(ruleWithChild, ruleValue, violationDetails);
                    violationType = "playtime_daily_limit";
                    break;

                case "game_restriction":
                    hasViolation = await CheckGameRestrictionAsync(ruleWithChild, ruleValue, violationDetails);
                    violationType = "game_restriction";
                    break;

                case "age_restriction":
                    hasViolation = await CheckAgeRestrictionAsync(ruleWithChild, ruleValue, violationDetails);
                    violationType = "age_restriction";
                    break;

                default:
                    _logger.LogWarning("未知的规则类型: {RuleType}, 规则ID: {RuleId}", ruleWithChild.RuleType, ruleWithChild.RuleId);
                    return;
            }

            if (hasViolation)
            {
                // 获取家长用户信息
                var relationship = await _dbContext.ParentalControlRelationships
                    .Include(r => r.ParentUser)
                    .FirstOrDefaultAsync(r => r.ChildUserId == ruleWithChild.ChildUserId);

                if (relationship == null)
                {
                    _logger.LogWarning("未找到子账户 {ChildUserId} 的监管关系", ruleWithChild.ChildUserId);
                    return;
                }

                var parentUser = relationship.ParentUser;
                var childUser = ruleWithChild.ChildUser;

                // 创建通知
                string notificationTitle = "";
                string notificationContent = "";

                if (violationType == "playtime_daily_limit")
                {
                    notificationTitle = $"游戏时长提醒：{childUser.Username}";
                    var limitMinutes = ruleValue.TryGetProperty("limitMinutes", out var limit) ? limit.GetInt32() : 0;
                    var currentMinutes = violationDetails.ContainsKey("currentMinutes") 
                        ? (int)violationDetails["currentMinutes"] 
                        : 0;
                    notificationContent = $"您的孩子 {childUser.Username} 今日游戏时长已达到 {currentMinutes} 分钟，超过设定的限制 {limitMinutes} 分钟。";
                }
                else if (violationType == "game_restriction")
                {
                    notificationTitle = $"游戏限制提醒：{childUser.Username}";
                    var blockedGameNames = violationDetails.ContainsKey("blockedGameNames") 
                        ? (List<string>)violationDetails["blockedGameNames"] 
                        : new List<string>();
                    notificationContent = $"您的孩子 {childUser.Username} 的游戏库中包含被限制的游戏：{string.Join("、", blockedGameNames)}。";
                }
                else if (violationType == "age_restriction")
                {
                    notificationTitle = $"年龄限制提醒：{childUser.Username}";
                    var maxAgeRating = ruleValue.TryGetProperty("maxAgeRating", out var maxAge) ? maxAge.GetInt32() : 0;
                    var violatingGameNames = violationDetails.ContainsKey("violatingGameNames") 
                        ? (List<string>)violationDetails["violatingGameNames"] 
                        : new List<string>();
                    notificationContent = $"您的孩子 {childUser.Username} 的游戏库中包含超出年龄分级（{maxAgeRating}+）的游戏：{string.Join("、", violatingGameNames)}。";
                }

                var notification = new NotificationCenter
                {
                    UserId = parentUser.UserId,
                    SourceModule = "parental_control",
                    Title = notificationTitle,
                    Content = notificationContent,
                    NotificationType = "warning",
                    IsRead = false,
                    RelatedId = ruleWithChild.RuleId,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.NotificationCenters.Add(notification);
                await _dbContext.SaveChangesAsync();

                // 创建违规日志
                var alertLog = new ParentalAlertLog
                {
                    RuleId = ruleWithChild.RuleId,
                    ChildUserId = ruleWithChild.ChildUserId,
                    ViolationDetails = JsonSerializer.Serialize(violationDetails),
                    AlertTime = DateTime.UtcNow,
                    NotificationId = notification.NotificationId,
                    Severity = "warning"
                };

                _dbContext.ParentalAlertLogs.Add(alertLog);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("已为家长 {ParentUserId} 创建家长监管提醒: 规则ID={RuleId}, 子账户={ChildUserId}, 违规类型={ViolationType}",
                    parentUser.UserId, ruleWithChild.RuleId, ruleWithChild.ChildUserId, violationType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查规则违规时发生错误: 规则ID={RuleId}", rule.RuleId);
            throw;
        }
    }

    /// <summary>
    /// 检查每日游戏时长限制
    /// </summary>
    private async Task<bool> CheckPlaytimeDailyLimitAsync(
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails)
    {
        if (!ruleValue.TryGetProperty("limitMinutes", out var limitProp))
        {
            return false;
        }

        var limitMinutes = limitProp.GetInt32();
        if (limitMinutes <= 0)
        {
            return false;
        }

        // 使用CalculateTodayPlaytimeAsync计算今日游戏时长
        var currentMinutes = await CalculateTodayPlaytimeAsync(rule.ChildUserId);
        violationDetails["currentMinutes"] = currentMinutes;
        violationDetails["limitMinutes"] = limitMinutes;

        // 检查是否超过限制
        if (currentMinutes >= limitMinutes)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查游戏限制
    /// </summary>
    private async Task<bool> CheckGameRestrictionAsync(
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails)
    {
        if (!ruleValue.TryGetProperty("blockedGameNames", out var blockedGamesProp))
        {
            return false;
        }

        var blockedGameNames = JsonSerializer.Deserialize<List<string>>(blockedGamesProp.GetRawText());
        if (blockedGameNames == null || !blockedGameNames.Any())
        {
            return false;
        }

        // 获取子账户的游戏库
        // 说明：当前数据模型中没有 UserGames 表，统一游戏库请使用 user_platform_library
        // 这里按“该子账户绑定的所有平台账号”汇总其游戏库。
        var userGames = await _dbContext.UserPlatformBindings
            .Where(b => b.UserId == rule.ChildUserId && b.BindingStatus == true)
            .Join(_dbContext.UserPlatformLibraries,
                b => new { b.PlatformUserId, b.PlatformId },
                upl => new { upl.PlatformUserId, upl.PlatformId },
                (b, upl) => upl)
            .Include(upl => upl.Game)
            .ToListAsync();

        var violatingGameNames = userGames
            .Where(ug => blockedGameNames.Contains(ug.Game.Name, StringComparer.OrdinalIgnoreCase))
            .Select(ug => ug.Game.Name)
            .ToList();

        if (violatingGameNames.Any())
        {
            violationDetails["blockedGameNames"] = violatingGameNames;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查年龄限制
    /// </summary>
    private async Task<bool> CheckAgeRestrictionAsync(
        ParentalControlRule rule,
        JsonElement ruleValue,
        Dictionary<string, object> violationDetails)
    {
        if (!ruleValue.TryGetProperty("maxAgeRating", out var maxAgeProp))
        {
            return false;
        }

        var maxAgeRating = maxAgeProp.GetInt32();
        if (maxAgeRating <= 0)
        {
            return false;
        }

        // 获取子账户的游戏库
        // 说明：当前数据模型中没有 UserGames 表，统一游戏库请使用 user_platform_library
        // 这里按“该子账户绑定的所有平台账号”汇总其游戏库。
        var userGames = await _dbContext.UserPlatformBindings
            .Where(b => b.UserId == rule.ChildUserId && b.BindingStatus == true)
            .Join(_dbContext.UserPlatformLibraries,
                b => new { b.PlatformUserId, b.PlatformId },
                upl => new { upl.PlatformUserId, upl.PlatformId },
                (b, upl) => upl)
            .Include(upl => upl.Game)
            .ToListAsync();

        var violatingGameNames = userGames
            .Where(ug => ug.Game.RequireAge.HasValue && ug.Game.RequireAge.Value > maxAgeRating)
            .Select(ug => ug.Game.Name)
            .ToList();

        if (violatingGameNames.Any())
        {
            violationDetails["violatingGameNames"] = violatingGameNames;
            violationDetails["maxAgeRating"] = maxAgeRating;
            return true;
        }

        return false;
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

