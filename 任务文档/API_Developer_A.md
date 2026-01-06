# PlayLinker API 文档 - 开发者A

## 项目信息

**项目名称**: PlayLinker - 统一游戏管理平台  
**技术栈**: C# ASP.NET Core + MySQL + Vue 3  
**认证方式**: JWT Bearer Token  
**开发周期**: 2周  
**负责人**: 开发者A  
**负责模块**: 用户认证、用户管理、平台绑定、通知中心、家长监管  
**Base URL**: `/api/v1`  
**最后更新**: 2024-11-27

---

## 统一约定

### 认证方式
- **JWT配置**: 密钥存储在 `appsettings.json` 或环境变量中，严禁硬编码
- **请求头**: `Authorization: Bearer <JWT_TOKEN>`
- **Token过期时间**: 3600秒（1小时）
- **RefreshToken过期时间**: 604800秒（7天）

### 响应格式
所有API统一返回格式：
```json
{
  "success": true,
  "code": "OK",
  "message": "操作成功",
  "data": {...},
  "meta": {
    "timestamp": "2024-11-27T10:00:00Z",
    "version": "1.0"
  }
}
```

### 分页参数
- `page`: 页码，从1开始
- `page_size`: 每页数量，默认20，最大100
- `sort_by`: 排序字段
- `order`: asc | desc

### ID格式规范
- 用户相关: `INT` (user_id, role_id)
- 游戏相关: `BIGINT` (game_id, achievement_id)
- 绑定记录: `INT` (binding_id)
- 通知记录: `BIGINT` (notification_id)

### 时间格式
- 统一使用 **ISO 8601** 格式（UTC时间）
- 示例: `2024-11-27T10:00:00Z`

### 枚举定义
- `status`: active | disabled | inactive
- `role_name`: user | parent | admin
- `platform`: steam | epic | origin | uplay | gog
- `notification_type`: info | warning | alert
- `rule_type`: playtime_daily_limit | playtime_curfew | spending_limit | game_restriction | age_restriction

### HTTP状态码
- `200 OK`: 请求成功
- `201 Created`: 资源创建成功
- `400 Bad Request`: 请求参数错误
- `401 Unauthorized`: 未认证或Token无效
- `403 Forbidden`: 无权限访问
- `404 Not Found`: 资源不存在
- `409 Conflict`: 资源冲突
- `422 Unprocessable Entity`: 参数验证失败
- `429 Too Many Requests`: 请求过于频繁
- `500 Internal Server Error`: 服务器错误

---

## 开发里程碑（2周计划）

### 第1周：基础功能开发

#### Day 1-2: 项目初始化与认证系统
- [x] 项目结构搭建
- [ ] JWT认证中间件开发
- [ ] 用户注册接口 (POST /auth/register)
- [ ] 用户登录接口 (POST /auth/login)
- [ ] Token刷新接口 (POST /auth/refresh)
- [ ] 单元测试编写

#### Day 3-4: 用户管理与平台绑定
- [ ] 用户信息管理接口 (GET/PATCH /users/profile)
- [ ] 修改密码接口 (POST /users/change-password)
- [ ] OAuth URL获取 (GET /platforms/oauth/{platform})
- [ ] 平台绑定接口 (POST /platforms/bind)
- [ ] 绑定列表查询 (GET /platforms/bindings)
- [ ] 解绑接口 (DELETE /platforms/bindings/{id})

#### Day 5: 通知中心
- [ ] 通知列表接口 (GET /notifications)
- [ ] 标记已读接口 (PATCH /notifications/{id}/read)
- [ ] 删除通知接口 (DELETE /notifications/{id})
- [ ] 通知订阅设置 (POST /notifications/subscribe)

### 第2周：高级功能与测试

#### Day 6-7: 家长监管系统
- [ ] 创建监管关系 (POST /parental/relationships)
- [ ] 获取子账户列表 (GET /parental/children)
- [ ] 设置监管规则 (POST /parental/rules)
- [ ] 获取规则列表 (GET /parental/rules/{childId})
- [ ] 获取违规记录 (GET /parental/alerts)

#### Day 8-9: 联调与优化
- [ ] 与其他开发者API联调
- [ ] 性能优化（缓存、索引）
- [ ] 错误处理完善
- [ ] 日志记录优化

#### Day 10: 测试与文档
- [ ] 集成测试
- [ ] Swagger文档完善
- [ ] Postman测试集合
- [ ] 代码审查

---

## 数据表职责

### 拥有的数据表
- `user_management.user` - 用户基础信息
- `user_management.role` - 角色权限
- `user_library.user_platform_binding` - 平台绑定记录
- `user_library.player_platform` - 平台账号信息
- `parental_notification.notification_center` - 通知中心
- `parental_notification.parental_control_relationship` - 监管关系
- `parental_notification.parental_control_rule` - 监管规则
- `parental_notification.parental_alert_log` - 违规记录

### 依赖的其他表
- `game_data.platforms` - 平台信息（只读）

---

## 1. 认证相关 API

### 1.1 POST `/api/v1/auth/register` - 用户注册
**认证**: 不需要

**请求体**:
```json
{
  "username": "player123",
  "password": "SecurePass123!",
  "email": "player@example.com",
  "phone": "13800138000"
}
```

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "注册成功",
  "data": {
    "userId": 1001,
    "username": "player123",
    "token": "eyJhbGci...",
    "refreshToken": "refresh_abc...",
    "expiresIn": 3600
  }
}
```

**错误码**: `ERR_USERNAME_EXISTS`, `ERR_EMAIL_EXISTS`, `ERR_WEAK_PASSWORD`

---

### 1.2 POST `/api/v1/auth/login` - 用户登录
**认证**: 不需要

**请求体**:
```json
{
  "username": "player123",
  "password": "SecurePass123!"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "user": {
      "userId": 1001,
      "username": "player123",
      "email": "player@example.com",
      "role": "user"
    },
    "token": "eyJhbGci...",
    "expiresIn": 3600
  }
}
```

**错误码**: `ERR_INVALID_CREDENTIALS`, `ERR_ACCOUNT_DISABLED`

---

### 1.3 POST `/api/v1/auth/refresh` - 刷新Token
**认证**: 不需要

**请求体**:
```json
{
  "refreshToken": "refresh_xyz..."
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "token": "eyJhbGci...",
    "refreshToken": "refresh_new...",
    "expiresIn": 3600
  }
}
```

---

### 1.4 POST `/api/v1/auth/logout` - 退出登录
**认证**: 必需

**请求体**:
```json
{
  "allDevices": false
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "退出登录成功"
}
```

---

### 1.5 POST `/api/v1/auth/forgot-password` - 忘记密码
**认证**: 不需要

**请求体**:
```json
{
  "email": "player@example.com"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "密码重置邮件已发送",
  "data": {
    "email": "pla***@example.com",
    "expiresIn": 1800
  }
}
```

---

## 2. 用户管理 API

### 2.1 GET `/api/v1/users/profile` - 获取个人信息
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "userId": 1001,
    "username": "player123",
    "email": "player@example.com",
    "phone": "13800138000",
    "gender": 1,
    "avatarUrl": "https://cdn.example.com/avatar.jpg",
    "role": "user",
    "status": "active",
    "createdAt": "2024-01-01T00:00:00Z"
  }
}
```

---

### 2.2 PATCH `/api/v1/users/profile` - 更新个人信息
**认证**: 必需

**请求体**:
```json
{
  "email": "new@example.com",
  "phone": "13900139000",
  "gender": 1,
  "avatarUrl": "https://new-avatar.jpg"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "更新成功"
}
```

---

### 2.3 POST `/api/v1/users/change-password` - 修改密码
**认证**: 必需

**请求体**:
```json
{
  "oldPassword": "OldPass123!",
  "newPassword": "NewPass456!"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "密码修改成功，请重新登录"
}
```

---

## 3. 平台绑定 API

### 3.1 GET `/api/v1/platforms/oauth/{platform}` - 获取OAuth URL
**认证**: 必需  
**路径参数**: platform = steam | epic | origin

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "platform": "steam",
    "authUrl": "https://steamcommunity.com/openid/login?...",
    "state": "random_state_abc123",
    "expiresIn": 600
  }
}
```

---

### 3.2 POST `/api/v1/platforms/bind` - 绑定平台
**认证**: 必需

**请求体**:
```json
{
  "platformId": 1,
  "authCode": "oauth_code_xyz",
  "state": "random_state_abc123"
}
```

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "Steam平台绑定成功",
  "data": {
    "bindingId": 1,
    "platformName": "Steam",
    "platformUserId": "76561198000000000",
    "bindingTime": "2024-11-27T10:00:00Z"
  }
}
```

---

### 3.3 GET `/api/v1/platforms/bindings` - 获取绑定列表
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "bindings": [
      {
        "bindingId": 1,
        "platformName": "Steam",
        "platformUserId": "76561198000000000",
        "profileName": "PlayerOne",
        "bindingTime": "2024-10-01T10:00:00Z"
      }
    ],
    "totalCount": 1
  }
}
```

---

### 3.4 DELETE `/api/v1/platforms/bindings/{id}` - 解绑平台
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Steam平台解绑成功"
}
```

---

## 4. 通知中心 API

### 4.1 GET `/api/v1/notifications` - 获取通知列表
**认证**: 必需  
**查询参数**: `is_read`, `type`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "notificationId": 1,
        "sourceModule": "price_alert",
        "title": "游戏降价了",
        "content": "《Cyberpunk 2077》现在仅售¥149",
        "notificationType": "info",
        "isRead": false,
        "createdAt": "2024-11-27T09:30:00Z"
      }
    ],
    "unreadCount": 5,
    "meta": {"page": 1, "pageSize": 20, "total": 25}
  }
}
```

---

### 4.2 PATCH `/api/v1/notifications/{id}/read` - 标记已读
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "标记成功"
}
```

---

### 4.3 DELETE `/api/v1/notifications/{id}` - 删除通知
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "删除成功"
}
```

---

### 4.4 POST `/api/v1/notifications/subscribe` - 通知订阅设置
**认证**: 必需

**请求体**:
```json
{
  "priceAlert": {"enabled": true, "email": true, "push": true},
  "parentalControl": {"enabled": true, "email": true, "push": true}
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "通知设置已更新"
}
```

---

## 5. 家长监管 API

### 5.1 POST `/api/v1/parental/relationships` - 创建监管关系
**认证**: 必需（需parent角色）

**请求体**:
```json
{
  "childUserId": 1002
}
```

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "监管关系建立成功",
  "data": {
    "relationshipId": 1,
    "parentUserId": 1001,
    "childUserId": 1002,
    "childUsername": "child123",
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

**错误码**: `ERR_NOT_PARENT_ROLE`, `ERR_CHILD_ALREADY_SUPERVISED`

---

### 5.2 GET `/api/v1/parental/children` - 获取子账户列表
**认证**: 必需（需parent角色）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "children": [
      {
        "childUserId": 1002,
        "childUsername": "child123",
        "activeRulesCount": 3,
        "todayPlaytime": 90,
        "recentAlerts": 2
      }
    ],
    "totalCount": 1
  }
}
```

---

### 5.3 POST `/api/v1/parental/rules` - 设置监管规则
**认证**: 必需（需parent角色）

**请求体**:
```json
{
  "childUserId": 1002,
  "ruleType": "playtime_daily_limit",
  "ruleValue": {
    "limitMinutes": 120,
    "warningMinutes": 100
  },
  "isActive": true
}
```

**规则类型**:
- `playtime_daily_limit`: 每日时长限制
- `playtime_curfew`: 宵禁时间
- `spending_limit`: 消费限制
- `game_restriction`: 游戏限制
- `age_restriction`: 年龄限制

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "规则设置成功",
  "data": {
    "ruleId": 1,
    "ruleType": "playtime_daily_limit",
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 5.4 GET `/api/v1/parental/rules/{childId}` - 获取规则列表
**认证**: 必需（需parent角色）  
**路径参数**: childId = 子账户用户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "childUserId": 1002,
    "childUsername": "child123",
    "rules": [
      {
        "ruleId": 1,
        "ruleType": "playtime_daily_limit",
        "ruleValue": {"limitMinutes": 120},
        "isActive": true,
        "statistics": {
          "totalViolations": 5,
          "recentViolations": 1
        }
      }
    ],
    "totalCount": 1
  }
}
```

---

### 5.5 GET `/api/v1/parental/alerts` - 获取违规记录
**认证**: 必需（需parent角色）  
**查询参数**: `child_id`, `rule_type`, `start_date`, `end_date`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "alertId": 1,
        "ruleType": "playtime_daily_limit",
        "childUserId": 1002,
        "childUsername": "child123",
        "violationDetails": {
          "limitMinutes": 120,
          "actualMinutes": 150,
          "exceededMinutes": 30,
          "currentGame": "Counter-Strike 2"
        },
        "alertTime": "2024-11-27T08:00:00Z",
        "severity": "warning"
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 15}
  }
}
```

---

## 附录：规则类型详细说明

### playtime_daily_limit（每日时长限制）
```json
{
  "limitMinutes": 120,
  "warningMinutes": 100,
  "resetTime": "00:00"
}
```

### playtime_curfew（宵禁时间）
```json
{
  "startTime": "22:00",
  "endTime": "07:00",
  "weekdaysOnly": false
}
```

### spending_limit（消费限制）
```json
{
  "dailyLimit": 50.00,
  "monthlyLimit": 300.00,
  "requireApproval": true,
  "approvalThreshold": 30.00
}
```

### game_restriction（游戏限制）
```json
{
  "allowedGames": [10001, 10002],
  "blockedGames": [10003],
  "allowAllExceptBlocked": false
}
```

### age_restriction（年龄限制）
```json
{
  "maxAgeRating": 12,
  "blockUnrated": true
}
```
