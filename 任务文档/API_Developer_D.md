# PlayLinker API 文档 - 开发者D

## 项目信息

**项目名称**: PlayLinker - 统一游戏管理平台  
**技术栈**: C# ASP.NET Core + MySQL + Vue 3  
**认证方式**: JWT Bearer Token  
**开发周期**: 2周  
**负责人**: 开发者D  
**负责模块**: 用户偏好、推荐系统、价格监控、愿望单、折扣提醒  
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
- 用户相关: `INT` (user_id, preference_id)
- 游戏相关: `BIGINT` (game_id)
- 推荐/价格: `INT/BIGINT` (recommendation_id, subscription_id)

### 时间格式
- 统一使用 **ISO 8601** 格式（UTC时间）
- 示例: `2024-11-27T10:00:00Z`

### 货币
- 人民币: `CNY`
- 美元: `USD`

### 枚举定义
- `recommendation_type`: game | discount | similar | trending
- `recommendation_strategy`: collaborative | content_based | hybrid | popular
- `alert_type`: target_price | target_discount

---

## 开发里程碑（2周计划）

### 第1周：基础功能开发

#### Day 1-2: 项目初始化与用户偏好
- [x] 项目结构搭建
- [ ] Redis缓存配置
- [ ] 获取用户偏好 (GET /preferences)
- [ ] 更新用户偏好 (PATCH /preferences)
- [ ] AI分析偏好 (POST /preferences/analyze)
- [ ] 单元测试编写

#### Day 3-4: 价格监控与愿望单
- [ ] 价格历史 (GET /prices/history/{gameId})
- [ ] 当前价格查询 (GET /prices/current)
- [ ] 开始跟踪价格 (POST /prices/track)
- [ ] 愿望单列表 (GET /wishlist)
- [ ] 添加到愿望单 (POST /wishlist)
- [ ] 价格爬虫开发

#### Day 5: 推荐系统
- [ ] 推荐列表 (GET /recommendations)
- [ ] 探索新游戏 (GET /recommendations/explore)
- [ ] 推荐反馈 (POST /recommendations/{id}/feedback)
- [ ] 相似游戏推荐 (GET /recommendations/similar/{gameId})

### 第2周：AI功能与测试

#### Day 6-7: 折扣提醒与促销
- [ ] 提醒订阅列表 (GET /alerts/subscriptions)
- [ ] 订阅价格提醒 (POST /alerts/subscribe)
- [ ] 提醒历史 (GET /alerts/history)
- [ ] 当前促销 (GET /sales/current)
- [ ] 即将到来的促销 (GET /sales/upcoming)
- [ ] AI价格预测 (GET /prices/predictions/{gameId})

#### Day 8-9: 联调与优化
- [ ] 与其他开发者API联调
- [ ] 推荐算法优化
- [ ] 价格预测模型训练
- [ ] 缓存策略优化

#### Day 10: 测试与文档
- [ ] 集成测试
- [ ] Swagger文档完善
- [ ] Postman测试集合
- [ ] 代码审查

---

## 数据表职责

### 拥有的数据表
- `business_features.user_preference` - 用户偏好
- `business_features.preference_genre` - 偏好题材关联
- `business_features.recommendation` - 推荐记录
- `business_features.recommendation_feedback` - 推荐反馈
- `business_features.price_history` - 价格历史
- `business_features.price_alert_subscription` - 价格提醒订阅
- `business_features.price_alert_log` - 价格提醒日志

### 依赖的其他表
- `user_management.user` - 用户信息（只读）
- `game_data.games` - 游戏信息（只读）
- `game_data.genres` - 游戏题材（只读）
- `game_data.platforms` - 平台信息（只读）
- `parental_notification.notification_center` - 通知中心（写入）

---

## 1. 用户偏好 API

### 1.1 GET `/api/v1/preferences` - 获取用户偏好
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "preferenceId": 1,
    "userId": 1001,
    "favoriteGenres": [
      {"genreId": 1, "name": "FPS", "weight": 0.35},
      {"genreId": 2, "name": "RPG", "weight": 0.25},
      {"genreId": 3, "name": "Strategy", "weight": 0.20}
    ],
    "playtimeRange": "3-5hours",
    "priceSensitivity": 2,
    "languagePreference": ["zh-CN", "en-US"],
    "avoidGenres": ["Horror", "Visual Novel"],
    "preferredPlatforms": [1, 2],
    "multiplayerPreference": "both",
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

**字段说明**:
- `priceSensitivity`: 1=高敏感度, 2=中等, 3=低敏感度
- `playtimeRange`: 偏好游玩时长区间
- `multiplayerPreference`: single | multi | both

---

### 1.2 PATCH `/api/v1/preferences` - 更新用户偏好
**认证**: 必需

**请求体**:
```json
{
  "favoriteGenres": [1, 2, 3],
  "playtimeRange": "3-5hours",
  "priceSensitivity": 2,
  "languagePreference": ["zh-CN", "en-US"],
  "avoidGenres": ["Horror"],
  "preferredPlatforms": [1, 2],
  "multiplayerPreference": "both"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "偏好设置已更新",
  "data": {
    "preferenceId": 1,
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 1.3 POST `/api/v1/preferences/analyze` - 分析用户行为生成偏好
**认证**: 必需

**请求体**:
```json
{
  "analyzePlaytime": true,
  "analyzePurchases": true,
  "analyzeAchievements": true,
  "timeRange": "last_6_months"
}
```

**字段说明**:
- `analyzePlaytime`: 分析游玩时间
- `analyzePurchases`: 分析购买历史
- `analyzeAchievements`: 分析成就解锁
- `timeRange`: last_month | last_3_months | last_6_months | last_year

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "偏好分析完成",
  "data": {
    "analyzedGames": 128,
    "analyzedPeriod": "2024-05-27 to 2024-11-27",
    "detectedPreferences": {
      "topGenres": [
        {"genre": "FPS", "confidence": 0.85},
        {"genre": "RPG", "confidence": 0.72}
      ],
      "averagePlaytime": "4.5hours",
      "priceRange": "50-200CNY",
      "multiplayerRatio": 0.65
    },
    "recommendations": [
      "建议增加FPS和RPG到偏好题材",
      "您似乎偏好中长时长游戏",
      "您对多人游戏有较高兴趣"
    ]
  }
}
```

---

## 2. 推荐系统 API

### 2.1 GET `/api/v1/recommendations` - 获取推荐列表
**认证**: 必需  
**查询参数**: `type`, `limit`, `page`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "recommendationId": 1,
        "gameId": 10003,
        "gameName": "Apex Legends",
        "headerImage": "url",
        "recommendationType": "similar",
        "recommendationStrategy": "collaborative",
        "score": 0.95,
        "reason": "基于您玩过的CS2和Valorant，您可能喜欢这款团队竞技射击游戏",
        "tags": ["FPS", "Battle Royale", "Free to Play"],
        "matchedPreferences": ["FPS", "Multiplayer", "Free"],
        "currentPrice": 0,
        "originalPrice": 0,
        "discount": 0,
        "reviewScore": 88,
        "media": {
          "headerImage": "url",
          "video": "trailer_url"
        },
        "createdAt": "2024-11-27T10:00:00Z",
        "expiresAt": "2024-12-04T10:00:00Z"
      },
      {
        "recommendationId": 2,
        "gameId": 10005,
        "gameName": "Elden Ring",
        "headerImage": "url",
        "recommendationType": "trending",
        "recommendationStrategy": "popular",
        "score": 0.88,
        "reason": "近期热门RPG游戏，与您的偏好高度匹配",
        "tags": ["RPG", "Action", "Open World"],
        "matchedPreferences": ["RPG", "Single-player"],
        "currentPrice": 298.00,
        "originalPrice": 298.00,
        "discount": 0,
        "reviewScore": 96,
        "media": {
          "headerImage": "url",
          "video": "trailer_url"
        },
        "createdAt": "2024-11-27T10:00:00Z",
        "expiresAt": "2024-12-04T10:00:00Z"
      }
    ],
    "meta": {
      "page": 1,
      "pageSize": 10,
      "total": 50,
      "generatedAt": "2024-11-27T10:00:00Z",
      "algorithmVersion": "2.1.0"
    }
  }
}
```

**推荐类型说明**:
- `game`: 游戏推荐
- `discount`: 折扣推荐
- `similar`: 相似游戏
- `trending`: 热门趋势

**推荐策略说明**:
- `collaborative`: 协同过滤
- `content_based`: 基于内容
- `hybrid`: 混合策略
- `popular`: 热门推荐

---

### 2.2 GET `/api/v1/recommendations/explore` - 探索新游戏
**认证**: 必需  
**查询参数**: `genre`, `limit`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "exploreCategory": "新兴独立游戏",
    "items": [
      {
        "gameId": 10010,
        "gameName": "Hades",
        "headerImage": "url",
        "genres": ["Roguelike", "Action"],
        "releaseDate": "2020-09-17",
        "reviewScore": 93,
        "currentPrice": 90.00,
        "whyExplore": "高评分独立游戏，与您喜欢的动作类游戏相似",
        "uniqueFeatures": ["Roguelike", "希腊神话", "高重玩性"]
      }
    ],
    "totalCount": 20
  }
}
```

---

### 2.3 POST `/api/v1/recommendations/{id}/feedback` - 推荐反馈
**认证**: 必需  
**路径参数**: id = recommendationId

**请求体**:
```json
{
  "feedbackResult": 1,
  "remark": "游戏不错，但是优化太差",
  "tags": ["performance_issue"],
  "purchased": false,
  "addedToWishlist": true
}
```

**字段说明**:
- `feedbackResult`: 1=喜欢, 2=不喜欢, 3=不感兴趣
- `remark`: 反馈备注（可选）
- `tags`: 反馈标签（可选）
- `purchased`: 是否购买
- `addedToWishlist`: 是否加入愿望单

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "感谢您的反馈",
  "data": {
    "feedbackId": 1,
    "recommendationId": 1,
    "feedbackResult": 1,
    "feedbackTime": "2024-11-27T10:00:00Z",
    "impact": "您的反馈将帮助我们改进推荐算法"
  }
}
```

---

### 2.4 GET `/api/v1/recommendations/similar/{gameId}` - 相似游戏推荐
**认证**: 不需要  
**路径参数**: gameId = 游戏ID  
**查询参数**: `limit`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "sourceGame": {
      "gameId": 10001,
      "gameName": "Counter-Strike 2"
    },
    "similarGames": [
      {
        "gameId": 10003,
        "gameName": "Valorant",
        "similarityScore": 0.92,
        "commonTags": ["FPS", "Tactical", "Competitive"],
        "headerImage": "url",
        "currentPrice": 0,
        "reviewScore": 85
      },
      {
        "gameId": 10006,
        "gameName": "Rainbow Six Siege",
        "similarityScore": 0.88,
        "commonTags": ["FPS", "Tactical", "Team-based"],
        "headerImage": "url",
        "currentPrice": 128.00,
        "reviewScore": 82
      }
    ],
    "totalCount": 10
  }
}
```

---

## 3. 价格监控 API

### 3.1 GET `/api/v1/prices/history/{gameId}` - 价格历史
**认证**: 不需要  
**路径参数**: gameId = 游戏ID  
**查询参数**: `platform`, `days`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "platform": "Steam",
    "currency": "CNY",
    "currentPrice": 298.00,
    "lowestPrice": 89.40,
    "lowestDate": "2024-06-15",
    "highestPrice": 298.00,
    "averagePrice": 223.50,
    "priceHistory": [
      {
        "priceId": 1,
        "date": "2024-11-27",
        "currentPrice": 298.00,
        "originalPrice": 298.00,
        "discount": 0,
        "isDiscount": false,
        "event": null
      },
      {
        "priceId": 2,
        "date": "2024-11-11",
        "currentPrice": 149.00,
        "originalPrice": 298.00,
        "discount": 50,
        "isDiscount": true,
        "event": "双11特惠"
      },
      {
        "priceId": 3,
        "date": "2024-06-15",
        "currentPrice": 89.40,
        "originalPrice": 298.00,
        "discount": 70,
        "isDiscount": true,
        "event": "夏季特卖"
      }
    ],
    "statistics": {
      "totalRecords": 50,
      "daysTracked": 90,
      "discountFrequency": 0.15,
      "averageDiscount": 45
    },
    "predictions": {
      "nextSaleProbability": 0.85,
      "estimatedDate": "2024-12-20",
      "estimatedDiscount": 50,
      "confidence": 0.75,
      "reasoning": "基于历史数据，该游戏通常在冬季特卖期间打5折"
    }
  }
}
```

---

### 3.2 GET `/api/v1/prices/current` - 当前价格查询
**认证**: 不需要  
**查询参数**: `game_ids`, `platform`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "prices": [
      {
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "platform": "Steam",
        "currentPrice": 298.00,
        "originalPrice": 298.00,
        "discount": 0,
        "isDiscount": false,
        "currency": "CNY",
        "lastUpdated": "2024-11-27T10:00:00Z"
      },
      {
        "gameId": 10005,
        "gameName": "Elden Ring",
        "platform": "Steam",
        "currentPrice": 268.00,
        "originalPrice": 298.00,
        "discount": 10,
        "isDiscount": true,
        "currency": "CNY",
        "lastUpdated": "2024-11-27T10:00:00Z"
      }
    ],
    "totalCount": 2
  }
}
```

---

### 3.3 POST `/api/v1/prices/track` - 开始跟踪价格
**认证**: 必需

**请求体**:
```json
{
  "gameId": 10004,
  "platformId": 1,
  "notifyOnDiscount": true,
  "targetDiscount": 50
}
```

**字段说明**:
- `gameId`: 游戏ID
- `platformId`: 平台ID
- `notifyOnDiscount`: 是否在打折时通知
- `targetDiscount`: 目标折扣（可选）

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "价格跟踪已启动",
  "data": {
    "trackId": "track_20241127_100000",
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "platformId": 1,
    "currentPrice": 298.00,
    "targetDiscount": 50,
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 3.4 GET `/api/v1/prices/predictions/{gameId}` - 价格预测（AI）
**认证**: 必需  
**路径参数**: gameId = 游戏ID  
**查询参数**: `platform`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "platform": "Steam",
    "currentPrice": 298.00,
    "predictions": [
      {
        "event": "冬季特卖",
        "estimatedDate": "2024-12-20",
        "estimatedStartDate": "2024-12-20",
        "estimatedEndDate": "2025-01-05",
        "predictedPrice": 149.00,
        "predictedDiscount": 50,
        "probability": 0.85,
        "confidence": "high"
      },
      {
        "event": "春节促销",
        "estimatedDate": "2025-02-10",
        "estimatedStartDate": "2025-02-10",
        "estimatedEndDate": "2025-02-17",
        "predictedPrice": 178.80,
        "predictedDiscount": 40,
        "probability": 0.70,
        "confidence": "medium"
      }
    ],
    "recommendation": {
      "shouldWait": true,
      "reason": "建议等待冬季特卖，预计可节省¥149",
      "bestTime": "2024-12-20",
      "potentialSavings": 149.00
    },
    "historicalPattern": {
      "averageDiscountCycle": 90,
      "lastDiscount": "2024-11-11",
      "daysSinceLastDiscount": 16,
      "typicalDiscountDepth": 50
    },
    "modelInfo": {
      "algorithm": "LSTM + Historical Pattern Analysis",
      "version": "2.1.0",
      "trainedOn": "5年历史数据",
      "accuracy": 0.82
    }
  }
}
```

---

## 4. 愿望单 API

### 4.1 GET `/api/v1/wishlist` - 愿望单列表
**认证**: 必需  
**查询参数**: `sort_by`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "subscriptionId": 1,
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "headerImage": "url",
        "platformId": 1,
        "platformName": "Steam",
        "currentPrice": 298.00,
        "originalPrice": 298.00,
        "lowestPrice": 89.40,
        "lowestDate": "2024-06-15",
        "targetPrice": 100.00,
        "targetDiscount": 70,
        "isOnSale": false,
        "addedAt": "2024-10-01T10:00:00Z",
        "priceHistory": [
          {"date": "2024-11-01", "price": 298.00, "discount": 0},
          {"date": "2024-06-15", "price": 89.40, "discount": 70}
        ],
        "priceAlert": {
          "enabled": true,
          "lastAlertTime": null,
          "alertCount": 0
        },
        "recommendation": {
          "shouldBuyNow": false,
          "reason": "建议等待冬季特卖",
          "estimatedBestPrice": 149.00,
          "estimatedDate": "2024-12-20"
        }
      },
      {
        "subscriptionId": 2,
        "gameId": 10005,
        "gameName": "Elden Ring",
        "headerImage": "url",
        "platformId": 1,
        "platformName": "Steam",
        "currentPrice": 268.00,
        "originalPrice": 298.00,
        "lowestPrice": 199.00,
        "lowestDate": "2024-08-20",
        "targetPrice": 200.00,
        "targetDiscount": 50,
        "isOnSale": true,
        "addedAt": "2024-09-15T10:00:00Z",
        "priceHistory": [
          {"date": "2024-11-27", "price": 268.00, "discount": 10},
          {"date": "2024-08-20", "price": 199.00, "discount": 33}
        ],
        "priceAlert": {
          "enabled": true,
          "lastAlertTime": "2024-11-27T09:00:00Z",
          "alertCount": 1
        },
        "recommendation": {
          "shouldBuyNow": false,
          "reason": "接近目标价格，但可能还会更低",
          "estimatedBestPrice": 199.00,
          "estimatedDate": "2024-12-25"
        }
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 25},
    "summary": {
      "totalGames": 25,
      "totalValue": 3500.00,
      "currentValue": 2800.00,
      "potentialSavings": 1200.00,
      "gamesOnSale": 5,
      "averageDiscount": 15
    }
  }
}
```

**排序选项**:
- `price`: 按价格排序
- `discount`: 按折扣排序
- `added_date`: 按添加时间排序
- `name`: 按名称排序

---

### 4.2 POST `/api/v1/wishlist` - 添加到愿望单
**认证**: 必需

**请求体**:
```json
{
  "gameId": 10005,
  "platformId": 1,
  "targetPrice": 150.00,
  "targetDiscount": 60,
  "notifyEmail": true,
  "notifyApp": true,
  "priority": "high"
}
```

**字段说明**:
- `gameId`: 游戏ID
- `platformId`: 平台ID
- `targetPrice`: 目标价格（可选）
- `targetDiscount`: 目标折扣（可选）
- `notifyEmail`: 是否邮件通知
- `notifyApp`: 是否应用内通知
- `priority`: 优先级（high | medium | low）

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "已添加到愿望单",
  "data": {
    "subscriptionId": 3,
    "gameId": 10005,
    "gameName": "Elden Ring",
    "platformId": 1,
    "currentPrice": 268.00,
    "targetPrice": 150.00,
    "targetDiscount": 60,
    "addedAt": "2024-11-27T10:00:00Z",
    "estimatedAlert": {
      "probability": 0.75,
      "estimatedDate": "2024-12-20",
      "reason": "根据历史数据，该游戏可能在冬季特卖达到目标价格"
    }
  }
}
```

---

### 4.3 DELETE `/api/v1/wishlist/{id}` - 从愿望单移除
**认证**: 必需  
**路径参数**: id = subscriptionId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "已从愿望单移除",
  "data": {
    "subscriptionId": 3,
    "gameName": "Elden Ring",
    "removedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 4.4 PATCH `/api/v1/wishlist/{id}` - 更新价格期望
**认证**: 必需  
**路径参数**: id = subscriptionId

**请求体**:
```json
{
  "targetPrice": 100.00,
  "targetDiscount": 70,
  "notifyEmail": true,
  "notifyApp": true,
  "priority": "high"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "愿望单设置已更新",
  "data": {
    "subscriptionId": 1,
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "targetPrice": 100.00,
    "targetDiscount": 70,
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 5. 折扣提醒 API

### 5.1 GET `/api/v1/alerts/subscriptions` - 提醒订阅列表
**认证**: 必需  
**查询参数**: `is_active`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "subscriptionId": 1,
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "platformId": 1,
        "platformName": "Steam",
        "targetPrice": 100.00,
        "targetDiscount": 70,
        "isActive": true,
        "createdAt": "2024-10-01T10:00:00Z",
        "lastCheckTime": "2024-11-27T09:00:00Z",
        "alertHistory": [
          {
            "alertTime": "2024-11-11T10:00:00Z",
            "price": 149.00,
            "discount": 50,
            "alertType": "target_discount"
          }
        ]
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 10},
    "summary": {
      "totalSubscriptions": 10,
      "activeSubscriptions": 8,
      "totalAlerts": 25
    }
  }
}
```

---

### 5.2 POST `/api/v1/alerts/subscribe` - 订阅价格提醒
**认证**: 必需

**请求体**:
```json
{
  "gameId": 10004,
  "platformId": 1,
  "targetPrice": 100.00,
  "targetDiscount": 70,
  "notifyEmail": true,
  "notifyApp": true,
  "notifyPush": false
}
```

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "价格提醒已设置",
  "data": {
    "subscriptionId": 11,
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "targetPrice": 100.00,
    "targetDiscount": 70,
    "isActive": true,
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 5.3 DELETE `/api/v1/alerts/subscriptions/{id}` - 取消订阅
**认证**: 必需  
**路径参数**: id = subscriptionId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "价格提醒已取消",
  "data": {
    "subscriptionId": 11,
    "cancelledAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 5.4 GET `/api/v1/alerts/history` - 提醒历史
**认证**: 必需  
**查询参数**: `game_id`, `start_date`, `end_date`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "alertId": 1,
        "subscriptionId": 1,
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "platformId": 1,
        "alertType": "target_discount",
        "alertTime": "2024-11-11T10:00:00Z",
        "priceSnapshot": {
          "currentPrice": 149.00,
          "originalPrice": 298.00,
          "discount": 50
        },
        "targetCondition": {
          "targetPrice": 100.00,
          "targetDiscount": 70
        },
        "notificationSent": true,
        "notificationId": 123,
        "userAction": {
          "viewed": true,
          "purchased": false,
          "dismissed": false
        }
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 25},
    "statistics": {
      "totalAlerts": 25,
      "viewedAlerts": 20,
      "purchasedAfterAlert": 5,
      "conversionRate": 0.25
    }
  }
}
```

---

## 6. 促销活动 API

### 6.1 GET `/api/v1/sales/current` - 当前促销
**认证**: 不需要  
**查询参数**: `platform`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "activeSales": [
      {
        "saleId": 1,
        "saleName": "Steam 秋季特卖",
        "platformId": 1,
        "platformName": "Steam",
        "startDate": "2024-11-20T00:00:00Z",
        "endDate": "2024-12-05T00:00:00Z",
        "description": "数千款游戏低至1折",
        "bannerImage": "url",
        "featuredGames": [
          {
            "gameId": 10004,
            "gameName": "Cyberpunk 2077",
            "currentPrice": 149.00,
            "originalPrice": 298.00,
            "discount": 50,
            "headerImage": "url"
          }
        ],
        "totalGames": 5000,
        "averageDiscount": 45,
        "maxDiscount": 90
      }
    ],
    "upcomingSales": [
      {
        "saleId": 2,
        "saleName": "Steam 冬季特卖",
        "platformId": 1,
        "platformName": "Steam",
        "startDate": "2024-12-20T00:00:00Z",
        "endDate": "2025-01-05T00:00:00Z",
        "description": "年度最大促销",
        "estimatedGames": 8000,
        "estimatedMaxDiscount": 95
      }
    ],
    "totalActive": 1,
    "totalUpcoming": 1
  }
}
```

---

### 6.2 GET `/api/v1/sales/upcoming` - 即将到来的促销
**认证**: 不需要  
**查询参数**: `platform`, `days`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "upcomingSales": [
      {
        "saleId": 2,
        "saleName": "Steam 冬季特卖",
        "platformId": 1,
        "platformName": "Steam",
        "startDate": "2024-12-20T00:00:00Z",
        "endDate": "2025-01-05T00:00:00Z",
        "daysUntilStart": 23,
        "description": "年度最大促销活动",
        "bannerImage": "url",
        "historicalData": {
          "lastYearMaxDiscount": 95,
          "lastYearParticipatingGames": 7500,
          "lastYearAverageDiscount": 50
        },
        "predictions": {
          "estimatedMaxDiscount": 95,
          "estimatedGames": 8000,
          "recommendedWishlistGames": [
            {
              "gameId": 10004,
              "gameName": "Cyberpunk 2077",
              "currentPrice": 298.00,
              "predictedPrice": 89.40,
              "predictedDiscount": 70,
              "inUserWishlist": true
            }
          ]
        }
      },
      {
        "saleId": 3,
        "saleName": "Epic 新年特卖",
        "platformId": 2,
        "platformName": "Epic Games",
        "startDate": "2024-12-28T00:00:00Z",
        "endDate": "2025-01-10T00:00:00Z",
        "daysUntilStart": 31,
        "description": "新年大促",
        "bannerImage": "url"
      }
    ],
    "totalCount": 2,
    "nextMajorSale": {
      "saleName": "Steam 冬季特卖",
      "startDate": "2024-12-20T00:00:00Z",
      "daysUntilStart": 23
    }
  }
}
```

---

### 6.3 GET `/api/v1/sales/history` - 历史促销活动
**认证**: 不需要  
**查询参数**: `platform`, `year`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "saleId": 100,
        "saleName": "Steam 夏季特卖 2024",
        "platformId": 1,
        "platformName": "Steam",
        "startDate": "2024-06-27T00:00:00Z",
        "endDate": "2024-07-11T00:00:00Z",
        "duration": 14,
        "participatingGames": 7200,
        "averageDiscount": 48,
        "maxDiscount": 95,
        "topDeals": [
          {
            "gameId": 10004,
            "gameName": "Cyberpunk 2077",
            "discount": 70,
            "price": 89.40
          }
        ],
        "statistics": {
          "totalRevenue": "估计数十亿",
          "peakConcurrentUsers": 32000000
        }
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 50},
    "yearlyStatistics": {
      "year": 2024,
      "totalSales": 12,
      "averageDuration": 12,
      "averageMaxDiscount": 92
    }
  }
}
```

---

## 附录：推荐算法说明

### 协同过滤（Collaborative Filtering）
基于用户行为相似度推荐：
- 找到与你游戏偏好相似的用户
- 推荐他们喜欢但你未玩过的游戏
- 适用于热门游戏推荐

### 基于内容（Content-Based）
基于游戏特征相似度推荐：
- 分析游戏的题材、标签、玩法
- 推荐与你已玩游戏相似的新游戏
- 适用于小众游戏发现

### 混合策略（Hybrid）
结合多种算法：
- 协同过滤 + 基于内容
- 加入热度、评分等因素
- 平衡准确性和多样性

### 热门推荐（Popular）
基于全局热度：
- 当前热门游戏
- 新发布的高分游戏
- 适用于新用户冷启动

---

## 价格预测模型说明

### 数据来源
- 5年历史价格数据
- 促销活动日历
- 游戏发布周期
- 市场趋势分析

### 预测因素
- 历史折扣模式
- 促销活动周期
- 游戏发布时间
- 竞品价格动态
- 季节性因素

### 准确度
- 短期预测（30天内）：82%
- 中期预测（90天内）：75%
- 长期预测（180天内）：65%

### 使用建议
- 高置信度（>0.8）：建议等待
- 中置信度（0.6-0.8）：可以考虑
- 低置信度（<0.6）：仅供参考

---

## 通知策略

### 价格提醒触发条件
1. 达到目标价格
2. 达到目标折扣
3. 创历史最低价
4. 即将结束的促销（最后24小时）

### 通知频率限制
- 同一游戏：24小时内最多1次
- 所有游戏：每天最多5次
- 促销活动：开始时1次，结束前1次

### 通知优先级
- **高优先级**：达到目标价格/折扣
- **中优先级**：历史最低价
- **低优先级**：一般折扣信息
