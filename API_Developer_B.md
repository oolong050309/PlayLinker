# PlayLinker API 文档 - 开发者B

## 项目信息

**项目名称**: PlayLinker - 统一游戏管理平台  
**技术栈**: C# ASP.NET Core + MySQL + Vue 3  
**认证方式**: JWT Bearer Token  
**开发周期**: 2周  
**负责人**: 开发者B  
**负责模块**: 游戏数据、游戏元数据、游戏库管理、成就系统、Steam集成  
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
- 平台相关: `INT` (platform_id)

### 时间格式
- 统一使用 **ISO 8601** 格式（UTC时间）
- 示例: `2024-11-27T10:00:00Z`

### 枚举定义
- `platform`: steam | epic | origin | uplay | gog | psn | xbox | nintendo
- `platformId`: 平台ID对应关系
  - `1` = Steam
  - `2` = Epic Games
  - `3` = Origin
  - `4` = Uplay
  - `5` = GOG
  - `6` = PSN
  - `7` = Xbox
  - `8` = Nintendo Switch
- `sort_by`: popularity | release_date | name | price

---

## 开发里程碑（2周计划）

### 第1周：基础功能开发

#### Day 1-2: 项目初始化与游戏数据
- [x] 项目结构搭建
- [ ] Entity Framework配置
- [ ] 游戏列表接口 (GET /games)
- [ ] 游戏详情接口 (GET /games/{id})
- [ ] 游戏搜索接口 (GET /games/search)
- [ ] 单元测试编写

#### Day 3-4: 游戏库与Steam集成
- [ ] Steam API客户端开发
- [ ] 游戏库概览 (GET /library/overview)
- [ ] 用户游戏列表 (GET /library/games)
- [ ] 同步平台数据 (POST /library/sync)
- [ ] Steam数据导入 (POST /steam/import)
- [ ] Steam用户信息 (GET /steam/user/{steamId})

### 第2周：扩展功能与测试

#### Day 5: 成就系统
- [ ] 游戏成就列表 (GET /games/{gameId}/achievements)
- [ ] 用户成就总览 (GET /library/achievements)
- [ ] 用户游戏成就 (GET /library/games/{id}/achievements)
- [ ] 成就同步 (POST /library/achievements/sync)

#### Day 6-7: 元数据与新闻
- [ ] 题材/分类/开发商/发行商接口
- [ ] 新闻列表 (GET /news)
- [ ] 游戏新闻 (GET /games/{id}/news)
- [ ] 游戏排行榜 (GET /games/ranking)
- [ ] 数据缓存优化

---

## 数据表职责

### 拥有的数据表
- `game_data.games` - 游戏主表
- `game_data.game_ranking` - 游戏排行榜
- `game_data.platforms` - 平台信息
- `game_data.game_platform` - 游戏平台映射
- `game_data.genres` - 游戏题材
- `game_data.game_genres` - 游戏题材关联
- `game_data.developers` - 开发商
- `game_data.game_developers` - 游戏开发商关联
- `game_data.publishers` - 发行商
- `game_data.game_publishers` - 游戏发行商关联
- `game_data.categories` - 分类
- `game_data.game_categories` - 游戏分类关联
- `game_data.languages` - 语言
- `game_data.game_languages` - 游戏语言关联
- `game_data.achievements` - 成就
- `game_data.news` - 新闻
- `game_data.game_news` - 游戏新闻关联
- `game_data.external_links` - 外链
- `game_data.game_external_links` - 游戏外链关联
- `user_library.user_game_library` - 用户游戏库统计
- `user_library.user_platform_library` - 用户平台游戏记录
- `user_library.user_achievements` - 用户成就记录

### 依赖的其他表
- `user_management.user` - 用户信息（只读）
- `user_library.player_platform` - 平台账号信息（只读）

---

## 1. 游戏数据 API

### 1.1 GET `/api/v1/games` - 游戏列表
**认证**: 不需要  
**查询参数**: `page`, `page_size`, `sort_by`, `platform`, `genre`, `is_free`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "gameId": 10001,
        "name": "Counter-Strike 2",
        "isFree": true,
        "releaseDate": "2023-09-27",
        "headerImage": "https://cdn.steam.com/cs2.jpg",
        "genres": ["FPS", "Multiplayer"],
        "platforms": {
          "windows": true,
          "mac": true,
          "linux": true
        },
        "reviewScore": 85,
        "totalPositive": 450000,
        "currentPlayers": 650000
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 1500}
  }
}
```

---

### 1.2 GET `/api/v1/games/{id}` - 游戏详情
**认证**: 不需要  
**路径参数**: id = gameId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10001,
    "name": "Counter-Strike 2",
    "isFree": true,
    "requireAge": 0,
    "shortDescription": "全球最受欢迎的竞技射击游戏",
    "detailedDescription": "详细描述...",
    "media": {
      "headerImage": "url",
      "capsuleImage": "url",
      "background": "url",
      "screenshots": ["url1", "url2"],
      "videos": ["video_url"]
    },
    "requirements": {
      "pcMinimum": "OS: Windows 10...",
      "pcRecommended": "OS: Windows 11...",
      "macMinimum": "OS: macOS 10.15...",
      "macRecommended": "OS: macOS 12...",
      "linuxMinimum": "OS: Ubuntu 20.04...",
      "linuxRecommended": "OS: Ubuntu 22.04..."
    },
    "genres": [
      {"genreId": 1, "name": "Action"},
      {"genreId": 2, "name": "FPS"}
    ],
    "developers": [
      {"developerId": 1, "name": "Valve"}
    ],
    "publishers": [
      {"publisherId": 1, "name": "Valve"}
    ],
    "categories": [
      {"categoryId": 1, "name": "Multiplayer"},
      {"categoryId": 2, "name": "PvP"}
    ],
    "languages": [
      {"languageId": 1, "name": "English"},
      {"languageId": 2, "name": "简体中文"}
    ],
    "platforms": {
      "windows": true,
      "mac": true,
      "linux": false
    },
    "releaseDate": "2023-09-27",
    "reviews": {
      "score": 85,
      "scoreDesc": "特别好评",
      "totalReviews": 500000,
      "totalPositive": 450000
    }
  }
}
```

---

### 1.3 GET `/api/v1/games/search` - 搜索游戏
**认证**: 不需要  
**查询参数**: `q`, `category`, `sort_by`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "gameId": 10001,
        "name": "Counter-Strike 2",
        "headerImage": "url",
        "genres": ["FPS"],
        "releaseDate": "2023-09-27",
        "reviewScore": 85
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 50}
  }
}
```

---

### 1.4 GET `/api/v1/games/ranking` - 游戏排行榜
**认证**: 不需要  
**查询参数**: `type`, `limit`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "rankId": 1,
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "currentRank": 1,
        "lastWeekRank": 1,
        "peakPlayers": 1500000,
        "headerImage": "url"
      }
    ],
    "totalCount": 100
  }
}
```

---

### 1.5 POST `/api/v1/games` - 添加游戏（管理员）
**认证**: 必需（需admin角色）

**请求体**:
```json
{
  "name": "New Game",
  "isFree": false,
  "releaseDate": "2024-12-01",
  "shortDescription": "描述",
  "detailedDescription": "详细描述",
  "headerImage": "url",
  "capsuleImage": "url",
  "background": "url",
  "requireAge": 18,
  "platforms": {
    "windows": true,
    "mac": false,
    "linux": false
  }
}
```

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "游戏添加成功",
  "data": {
    "gameId": 10100,
    "name": "New Game",
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 1.6 PUT `/api/v1/games/{id}` - 更新游戏（管理员）
**认证**: 必需（需admin角色）  
**路径参数**: id = gameId

**请求体**:
```json
{
  "name": "Updated Name",
  "shortDescription": "新描述",
  "headerImage": "new_url"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "游戏更新成功",
  "data": {
    "gameId": 10001,
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 2. 游戏元数据 API

### 2.1 GET `/api/v1/genres` - 获取所有题材
**认证**: 不需要

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {"genreId": 1, "name": "Action"},
      {"genreId": 2, "name": "FPS"},
      {"genreId": 3, "name": "RPG"},
      {"genreId": 4, "name": "Strategy"},
      {"genreId": 5, "name": "Adventure"}
    ],
    "totalCount": 5
  }
}
```

---

### 2.2 GET `/api/v1/categories` - 获取所有分类
**认证**: 不需要

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {"categoryId": 1, "name": "Multiplayer"},
      {"categoryId": 2, "name": "Single-player"},
      {"categoryId": 3, "name": "Co-op"},
      {"categoryId": 4, "name": "PvP"},
      {"categoryId": 5, "name": "Cross-Platform"}
    ],
    "totalCount": 5
  }
}
```

---

### 2.3 GET `/api/v1/developers` - 开发商列表
**认证**: 不需要  
**查询参数**: `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {"developerId": 1, "name": "Valve", "gamesCount": 15},
      {"developerId": 2, "name": "CD Projekt Red", "gamesCount": 5},
      {"developerId": 3, "name": "FromSoftware", "gamesCount": 8}
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 100}
  }
}
```

---

### 2.4 GET `/api/v1/publishers` - 发行商列表
**认证**: 不需要  
**查询参数**: `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {"publisherId": 1, "name": "Valve", "gamesCount": 15},
      {"publisherId": 2, "name": "CD Projekt", "gamesCount": 5},
      {"publisherId": 3, "name": "Bandai Namco", "gamesCount": 20}
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 80}
  }
}
```

---

## 3. 游戏库管理 API

### 3.1 GET `/api/v1/library/overview` - 游戏库概览
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "totalGamesOwned": 256,
    "gamesPlayed": 128,
    "totalPlaytimeMinutes": 150000,
    "totalAchievements": 3500,
    "unlockedAchievements": 2100,
    "recentlyPlayedCount": 5,
    "recentPlaytimeMinutes": 1200,
    "platformStats": [
      {
        "platformId": 1,
        "platformName": "Steam",
        "gamesOwned": 200,
        "lastSyncTime": "2024-11-27T09:00:00Z"
      },
      {
        "platformId": 2,
        "platformName": "Epic Games",
        "gamesOwned": 56,
        "lastSyncTime": "2024-11-26T10:00:00Z"
      }
    ],
    "genreDistribution": [
      {"genre": "FPS", "count": 45, "playtimeMinutes": 50000},
      {"genre": "RPG", "count": 30, "playtimeMinutes": 80000}
    ]
  }
}
```

---

### 3.2 GET `/api/v1/library/games` - 用户游戏列表
**认证**: 必需  
**查询参数**: `platform`, `sort_by`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "gameId": 10001,
        "name": "Counter-Strike 2",
        "headerImage": "url",
        "platforms": [1, 2],
        "playtimeMinutes": 3000,
        "lastPlayed": "2024-11-27T10:00:00Z",
        "achievementsUnlocked": 45,
        "achievementsTotal": 100,
        "ownedPlatforms": [
          {
            "platformId": 1,
            "platformName": "Steam",
            "playtimeMinutes": 2500
          },
          {
            "platformId": 2,
            "platformName": "Epic Games",
            "playtimeMinutes": 500
          }
        ]
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 150}
  }
}
```

---

### 3.3 POST `/api/v1/library/sync` - 同步平台数据
**认证**: 必需

**请求体**:
```json
{
  "platformId": 1,
  "fullSync": false
}
```

**字段说明**:
- `platformId`: 平台ID
  - `1` = Steam
  - `2` = Epic Games
  - `3` = Origin
  - `4` = Uplay
  - `5` = GOG
- `fullSync`: true=完全同步, false=增量同步

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "同步任务已启动",
  "data": {
    "taskId": "sync_20241127_100000",
    "status": "processing",
    "estimatedTime": 30,
    "gamesDetected": 256
  }
}
```

---

### 3.4 GET `/api/v1/library/stats` - 游戏统计数据
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "totalPlaytime": 150000,
    "averagePlaytime": 586,
    "mostPlayedGame": {
      "gameId": 10001,
      "name": "Counter-Strike 2",
      "playtime": 5000,
      "percentage": 3.33
    },
    "genreDistribution": [
      {"genre": "FPS", "count": 45, "playtime": 50000},
      {"genre": "RPG", "count": 30, "playtime": 80000}
    ],
    "platformDistribution": [
      {"platform": "Steam", "gamesCount": 200, "playtime": 120000},
      {"platform": "Epic Games", "gamesCount": 56, "playtime": 30000}
    ],
    "recentActivity": [
      {
        "date": "2024-11-27",
        "gamesPlayed": 3,
        "playtimeMinutes": 180
      },
      {
        "date": "2024-11-26",
        "gamesPlayed": 2,
        "playtimeMinutes": 120
      }
    ]
  }
}
```

---

### 3.5 PUT `/api/v1/library/games/{id}` - 更新游戏信息
**认证**: 必需  
**路径参数**: id = gameId

**请求体**:
```json
{
  "customTags": ["favorite", "multiplayer", "competitive"],
  "notes": "我的笔记内容",
  "isFavorite": true,
  "isHidden": false
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "更新成功",
  "data": {
    "gameId": 10001,
    "customTags": ["favorite", "multiplayer", "competitive"],
    "notes": "我的笔记内容",
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 4. 成就系统 API

### 4.1 GET `/api/v1/games/{gameId}/achievements` - 游戏成就列表
**认证**: 不需要  
**路径参数**: gameId = 游戏ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10001,
    "gameName": "Counter-Strike 2",
    "achievements": [
      {
        "achievementId": 1,
        "achievementName": "first_kill",
        "displayName": "首杀",
        "description": "获得第一次击杀",
        "hidden": false,
        "iconUnlocked": "https://cdn.steam.com/achievement_unlocked.jpg",
        "iconLocked": "https://cdn.steam.com/achievement_locked.jpg",
        "globalUnlockRate": 0.95
      },
      {
        "achievementId": 2,
        "achievementName": "ace",
        "displayName": "ACE",
        "description": "在一局中击杀所有敌人",
        "hidden": false,
        "iconUnlocked": "url",
        "iconLocked": "url",
        "globalUnlockRate": 0.15
      }
    ],
    "totalCount": 100
  }
}
```

---

### 4.2 GET `/api/v1/library/achievements` - 用户成就总览
**认证**: 必需

**查询参数**:
- `userId` (必需) - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）

**请求示例**:
```
GET /api/v1/library/achievements?userId=1001
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1001 不存在，请先创建用户"
}
```

- 当 `userId` 参数无效时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "userId 参数无效，必须提供有效的用户ID"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "totalAchievements": 3500,
    "unlockedAchievements": 2100,
    "unlockRate": 0.60,
    "perfectGames": 15,
    "recentUnlocks": [
      {
        "achievementId": 123,
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "achievementName": "Master",
        "displayName": "大师",
        "unlockTime": "2024-11-27T09:00:00Z",
        "iconUnlocked": "url"
      }
    ],
    "rareAchievements": [
      {
        "achievementId": 456,
        "gameId": 10002,
        "gameName": "Dark Souls III",
        "achievementName": "all_bosses",
        "displayName": "击败所有Boss",
        "globalUnlockRate": 0.05,
        "unlockTime": "2024-11-15T10:00:00Z"
      }
    ],
    "statistics": {
      "averageCompletionRate": 0.45,
      "totalPlaytime": 150000,
      "achievementsPerHour": 0.014
    }
  }
}
```

---

### 4.3 GET `/api/v1/library/games/{id}/achievements` - 用户游戏成就
**认证**: 必需  
**路径参数**: id = gameId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10001,
    "gameName": "Counter-Strike 2",
    "totalAchievements": 100,
    "unlockedAchievements": 45,
    "unlockRate": 0.45,
    "lastUnlockTime": "2024-11-27T09:00:00Z",
    "achievements": [
      {
        "achievementId": 1,
        "achievementName": "first_kill",
        "displayName": "首杀",
        "description": "获得第一次击杀",
        "unlocked": true,
        "unlockTime": "2024-10-01T10:00:00Z",
        "iconUnlocked": "url",
        "iconLocked": "url",
        "globalUnlockRate": 0.95
      },
      {
        "achievementId": 2,
        "achievementName": "ace",
        "displayName": "ACE",
        "description": "在一局中击杀所有敌人",
        "unlocked": false,
        "unlockTime": null,
        "iconUnlocked": "url",
        "iconLocked": "url",
        "globalUnlockRate": 0.15
      }
    ]
  }
}
```

---

### 4.4 POST `/api/v1/library/achievements/sync` - 同步成就数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "platformId": 1,
  "gameId": 10001
}
```

**字段说明**:
- `userId`: 需要同步的用户ID（必需）
  - 必须是有效的用户ID，如果用户不存在将返回错误
- `platformId`: 平台ID（可选）
  - `0` 或 `null` = 同步该用户已绑定平台的所有游戏
  - `1` = Steam
  - `2` = Epic Games
  - `3` = Origin
  - `4` = Uplay
  - `5` = GOG
  - `6` = PSN
  - `7` = Xbox
  - `8` = Nintendo Switch
- `gameId`: 游戏ID（可选）
  - `0` 或 `null` = 同步所有游戏
  - 其他值 = 同步指定游戏

**操作逻辑**:
1. 验证 `userId` 是否合法，若非法直接返回错误
2. 若 `platformId` 或 `gameId == 0`，则选择同步该用户已绑定平台的所有游戏
3. 根据用户绑定的平台账号（例如 Steam ID），调用平台 API 获取最新成就数据
4. 更新数据库中已有的该用户该平台该游戏的成就数据
5. 返回同步结果

**成功响应** (200):
返回本次同步的实际结果：
```json
{
  "success": true,
  "code": "OK",
  "message": "成就同步成功",
  "data": {
    "syncedGames": 5,
    "newUnlocks": 3,
    "totalUnlocked": 48,
    "syncTime": "2024-11-27T10:00:00Z"
  }
}
```

**响应字段说明**:
- `syncedGames`: 本次同步的游戏数量（实际值）
- `newUnlocks`: 本次新解锁的成就数量（实际值）
- `totalUnlocked`: 同步后用户总解锁成就数（实际值）
- `syncTime`: 同步完成时间（ISO 8601格式）

**错误响应** (400):
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "userId 参数无效，必须提供有效的用户ID",
  "data": null
}
```

或

```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户",
  "data": null
}
```

---

## 5. 新闻资讯 API

### 5.1 GET `/api/v1/news` - 新闻列表
**认证**: 不需要  
**查询参数**: `page`, `page_size`, `sort_by`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "newsId": 1,
        "title": "CS2 重大更新发布",
        "author": "Valve",
        "date": 1732694400,
        "contents": "更新内容包括新地图、武器平衡调整...",
        "newsUrl": "https://store.steampowered.com/news/...",
        "relatedGames": [
          {"gameId": 10001, "gameName": "Counter-Strike 2"}
        ]
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 100}
  }
}
```

---

### 5.2 GET `/api/v1/games/{id}/news` - 游戏相关新闻
**认证**: 不需要  
**路径参数**: id = gameId  
**查询参数**: `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10001,
    "gameName": "Counter-Strike 2",
    "news": [
      {
        "newsId": 1,
        "title": "CS2 重大更新发布",
        "author": "Valve",
        "date": 1732694400,
        "contents": "更新内容包括新地图、武器平衡调整...",
        "newsUrl": "https://store.steampowered.com/news/..."
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 50}
  }
}
```

---

### 5.3 GET `/api/v1/news/{id}` - 新闻详情
**认证**: 不需要  
**路径参数**: id = newsId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "newsId": 1,
    "title": "CS2 重大更新发布",
    "author": "Valve",
    "date": 1732694400,
    "contents": "完整的新闻内容...",
    "newsUrl": "https://store.steampowered.com/news/...",
    "relatedGames": [
      {
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "headerImage": "url"
      }
    ],
    "tags": ["update", "patch", "balance"],
    "views": 15000
  }
}
```

---

### 5.4 POST `/api/v1/news/steam/sync-all` - 同步所有游戏的Steam新闻
**认证**: 不需要

**请求体**:
```json
{
  "count": 20
}
```

**字段说明**:
- `count`: 每个游戏获取的新闻数量（可选，默认20）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Steam新闻同步完成",
  "data": {
    "processedGames": 150,
    "totalGames": 200,
    "totalNews": 3000,
    "errors": []
  }
}
```

**响应字段说明**:
- `processedGames`: 成功处理的游戏数量
- `totalGames`: 总游戏数量（有Steam平台映射的游戏）
- `totalNews`: 获取到的新闻总数
- `errors`: 处理过程中的错误列表

**错误响应** (500):
```json
{
  "success": false,
  "code": "ERR_INTERNAL",
  "message": "服务器内部错误"
}
```

---

### 5.5 POST `/api/v1/news/steam/sync` - 同步指定游戏的Steam新闻
**认证**: 不需要

**请求体**:
```json
{
  "gameId": 10001,
  "count": 20
}
```

**字段说明**:
- `gameId`: **必需** - 游戏ID（对应 `games` 表的 `game_id`）
- `count`: 获取的新闻数量（可选，默认20）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Steam新闻获取成功",
  "data": {
    "gameId": 10001,
    "gameName": "Counter-Strike 2",
    "appId": 730,
    "news": [
      {
        "gid": "12345678",
        "title": "CS2 重大更新发布",
        "url": "https://store.steampowered.com/news/...",
        "isExternalUrl": false,
        "author": "Valve",
        "contents": "更新内容包括新地图、武器平衡调整...",
        "feedLabel": "更新",
        "date": 1732694400,
        "feedName": "steam_community_announcements",
        "feedType": 0,
        "appId": 730
      }
    ],
    "total": 20
  }
}
```

**响应字段说明**:
- `gameId`: 游戏ID
- `gameName`: 游戏名称
- `appId`: Steam AppID
- `news`: 新闻列表
  - `gid`: 新闻全局ID
  - `title`: 新闻标题
  - `url`: 新闻链接
  - `isExternalUrl`: 是否为外部链接
  - `author`: 作者
  - `contents`: 新闻内容
  - `feedLabel`: 新闻源标签
  - `date`: 发布日期（Unix时间戳）
  - `feedName`: 新闻源名称
  - `feedType`: 新闻源类型
  - `appId`: Steam AppID
- `total`: 新闻总数

**错误响应** (404):
- 当游戏不存在时：
```json
{
  "success": false,
  "code": "ERR_GAME_NOT_FOUND",
  "message": "游戏不存在"
}
```

**错误响应** (400):
- 当游戏没有Steam平台映射时：
```json
{
  "success": false,
  "code": "ERR_NO_STEAM_MAPPING",
  "message": "游戏 10001 没有Steam平台映射"
}
```

- 当Steam API返回空数据时：
```json
{
  "success": false,
  "code": "ERR_STEAM_API_FAILED",
  "message": "Steam API返回空数据"
}
```

---

## 6. Steam API 集成

### 6.1 POST `/api/v1/steam/import` - 导入Steam数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "steamId": "76561198000000000",
  "importGames": true,
  "importAchievements": true,
  "importFriends": false
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `steamId`: Steam用户ID
- `importGames`: 是否导入游戏库
- `importAchievements`: 是否导入成就数据
- `importFriends`: 是否导入好友列表

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表：

1. **`platforms`** - 初始化平台数据（如果Steam平台不存在）
2. **`player_platform`** - 存储/更新Steam用户平台账号信息（profile_name, profile_url, account_created, country等）
3. **`user_platform_binding`** - **创建/更新用户与Steam平台的绑定关系**（user_id, platform_id, platform_user_id, binding_status, last_sync_time等）
4. **`games`** - 创建新游戏记录（如果游戏不存在）
5. **`developers`** - 创建开发商记录（如果不存在）
6. **`game_developers`** - 关联游戏和开发商
7. **`publishers`** - 创建发行商记录（如果不存在）
8. **`game_publishers`** - 关联游戏和发行商
9. **`game_platform`** - 关联游戏和Steam平台（存储Steam AppID和商店链接）
10. **`achievements`** - **自动创建游戏中不存在的成就记录**（如果Steam API返回的成就在数据库中不存在，会自动创建）
11. **`user_platform_library`** - 存储用户在Steam平台上的游戏记录（playtime_minutes, last_played等）
12. **`user_achievements`** - 存储用户的成就解锁记录（unlocked, unlock_time等）
13. **`user_game_library`** - **更新用户游戏库统计信息（统计该用户在所有8个平台的数据，而不仅仅是Steam）**

**重要说明**: 
- **`user_platform_binding` 表绑定**：该API会在 `user_platform_binding` 表中创建或更新 `userId` 和 `steamId` 的绑定关系，这是 `userId` 参数的主要用途
- **`user_game_library` 统计逻辑**：该表存储的是用户在所有平台（8个平台：Steam, Epic Games, Origin, Uplay, GOG, PSN, Xbox, Nintendo Switch）的游戏数据统计，而不仅仅是Steam平台。更新时会：
  - 从 `user_platform_binding` 表获取该用户绑定的所有平台账号
  - 从 `user_platform_library` 表统计所有平台的游戏数据
  - 从 `user_achievements` 表统计所有平台的成就数据
  - 计算去重后的游戏数量、总游戏时长、成就统计等
- **`achievements` 表自动创建**：该API会自动创建数据库中不存在的成就记录。当从Steam API获取成就数据时，如果某个成就在 `achievements` 表中不存在，会自动创建该成就记录（包括 `achievement_name`, `displayName`, `description` 等字段）
- 所有数据都会关联到指定的 `userId`，因此 `userId` 必须在 `user` 表中存在

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "导入任务已启动",
  "data": {
    "taskId": "import_20241127_100000",
    "status": "processing",
    "estimatedTime": 60,
    "items": {
      "games": 256,
      "achievements": 3500,
      "friends": 0
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

- 当 `userId` 参数无效时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "userId 参数无效，必须提供有效的用户ID"
}
```

---

### 6.2 GET `/api/v1/steam/user/{steamId}` - 获取Steam用户信息
**认证**: 必需  
**路径参数**: steamId = Steam用户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "steamId": "76561198000000000",
    "profileName": "PlayerOne",
    "profileUrl": "https://steamcommunity.com/id/playerone",
    "avatarUrl": "https://avatars.steamstatic.com/xxx.jpg",
    "accountCreated": "2015-06-15T00:00:00Z",
    "country": "CN",
    "level": 50,
    "gamesOwned": 256,
    "badges": 45,
    "isPublic": true
  }
}
```

---

### 6.3 GET `/api/v1/steam/games/{appId}` - 获取Steam游戏信息
**认证**: 必需  
**路径参数**: appId = Steam AppID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "appId": 730,
    "name": "Counter-Strike 2",
    "type": "game",
    "isFree": true,
    "shortDescription": "描述",
    "detailedDescription": "详细描述",
    "headerImage": "url",
    "developers": ["Valve"],
    "publishers": ["Valve"],
    "platforms": {
      "windows": true,
      "mac": true,
      "linux": true
    },
    "categories": ["Multi-player", "PvP"],
    "genres": ["Action", "Free to Play"],
    "releaseDate": "2023-09-27",
    "requiredAge": 0,
    "priceOverview": {
      "currency": "CNY",
      "initial": 0,
      "final": 0,
      "discountPercent": 0
    },
    "achievements": {
      "total": 100
    },
    "recommendations": {
      "total": 5000000
    }
  }
}
```

---

## 7. Epic Games API 集成

### 7.1 POST `/api/v1/epic/import` - 导入Epic Games数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "epicAccountId": "1234567890abcdef",
  "importGames": true,
  "importAchievements": true
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `epicAccountId`: Epic Games账户ID
- `importGames`: 是否导入游戏库
- `importAchievements`: 是否导入成就数据

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表（与Steam导入类似）：
- `platforms` - 初始化Epic Games平台数据（platformId = 2）
- `player_platform` - 存储Epic Games用户平台账号信息
- `games` - 创建新游戏记录
- `developers`, `publishers` - 创建开发商和发行商记录
- `game_platform` - 关联游戏和Epic Games平台
- `user_platform_library` - 存储用户在Epic Games平台上的游戏记录
- `user_achievements` - 存储用户的成就解锁记录（如果支持）
- `user_game_library` - 更新用户游戏库统计信息

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Epic Games数据导入任务已启动",
  "data": {
    "taskId": "epic_import_20241127_100000",
    "status": "processing",
    "estimatedTime": 60,
    "items": {
      "games": 58,
      "achievements": 0
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

---

### 7.2 GET `/api/v1/epic/user/{epicAccountId}` - 获取Epic Games用户信息
**认证**: 必需  
**路径参数**: epicAccountId = Epic Games账户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "epicAccountId": "1234567890abcdef",
    "displayName": "PlayerABC",
    "profileUrl": "https://www.epicgames.com/account/personal",
    "avatarUrl": "https://cdn.epicgames.com/avatar.jpg",
    "accountCreated": "2018-03-15T00:00:00Z",
    "country": "US",
    "gamesOwned": 58,
    "isPublic": true
  }
}
```

---

### 7.3 GET `/api/v1/epic/games/{epicGameId}` - 获取Epic Games游戏信息
**认证**: 必需  
**路径参数**: epicGameId = Epic Games游戏ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "epicGameId": "502001",
    "name": "Fortnite",
    "type": "game",
    "isFree": true,
    "shortDescription": "免费大逃杀游戏",
    "detailedDescription": "详细描述...",
    "headerImage": "https://cdn.epicgames.com/fortnite/header.jpg",
    "developers": ["Epic Games"],
    "publishers": ["Epic Games"],
    "platforms": {
      "windows": true,
      "mac": true,
      "linux": false
    },
    "categories": ["Multi-player", "Battle Royale"],
    "genres": ["Action", "Shooter"],
    "releaseDate": "2017-07-21",
    "requiredAge": 12,
    "priceOverview": {
      "currency": "USD",
      "initial": 0,
      "final": 0,
      "discountPercent": 0
    },
    "achievements": {
      "total": 0
    }
  }
}
```

---

### 7.4 GET `/api/v1/epic/user/{epicAccountId}/achievements` - 获取Epic Games用户成就
**认证**: 必需  
**路径参数**: epicAccountId = Epic Games账户ID  
**说明**: Epic Games部分游戏支持成就系统

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "achievementId": "achv123",
        "gameId": 502001,
        "gameName": "Fortnite",
        "achievementName": "first_win",
        "displayName": "First Win",
        "description": "Earn a Victory Royale",
        "unlocked": true,
        "unlockTime": "2024-11-27T10:00:00Z",
        "iconUnlocked": "url",
        "iconLocked": "url"
      }
    ],
    "total": 30
  }
}
```

---

## 8. GOG API 集成

### 8.1 POST `/api/v1/gog/import` - 导入GOG数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "gogUserId": "7654321",
  "importGames": true
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `gogUserId`: GOG用户ID
- `importGames`: 是否导入游戏库

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表（platformId = 5）：
- `platforms` - 初始化GOG平台数据
- `player_platform` - 存储GOG用户平台账号信息
- `games` - 创建新游戏记录
- `game_platform` - 关联游戏和GOG平台
- `user_platform_library` - 存储用户在GOG平台上的游戏记录
- `user_game_library` - 更新用户游戏库统计信息

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "GOG数据导入任务已启动",
  "data": {
    "taskId": "gog_import_20241127_100000",
    "status": "processing",
    "estimatedTime": 45,
    "items": {
      "games": 42,
      "achievements": 0
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

---

### 8.2 GET `/api/v1/gog/user/{gogUserId}` - 获取GOG用户信息
**认证**: 必需  
**路径参数**: gogUserId = GOG用户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gogUserId": "7654321",
    "username": "GOGPlayer",
    "profileUrl": "https://www.gog.com/u/gogplayer",
    "avatarUrl": "https://gog.com/avatar.jpg",
    "accountCreated": "2012-05-10T00:00:00Z",
    "country": "DE",
    "gamesOwned": 42,
    "isPublic": true
  }
}
```

---

### 8.3 GET `/api/v1/gog/games/{gogGameId}` - 获取GOG游戏信息
**认证**: 必需  
**路径参数**: gogGameId = GOG游戏ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gogGameId": "1207659045",
    "name": "The Witcher 3: Wild Hunt",
    "type": "game",
    "isFree": false,
    "shortDescription": "开放世界RPG杰作",
    "detailedDescription": "详细描述...",
    "headerImage": "https://images.gog.com/witcher3/header.jpg",
    "developers": ["CD Projekt RED"],
    "publishers": ["CD Projekt"],
    "platforms": {
      "windows": true,
      "mac": false,
      "linux": false
    },
    "categories": ["Single-player", "RPG"],
    "genres": ["RPG", "Open World"],
    "releaseDate": "2015-05-19",
    "requiredAge": 18,
    "priceOverview": {
      "currency": "USD",
      "initial": 3999,
      "final": 1999,
      "discountPercent": 50
    }
  }
}
```

---

## 9. Xbox API 集成

### 9.1 POST `/api/v1/xbox/import` - 导入Xbox数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "xboxUserId": "XUID1234567890",
  "importGames": true,
  "importAchievements": true
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `xboxUserId`: Xbox用户ID（XUID格式）
- `importGames`: 是否导入游戏库
- `importAchievements`: 是否导入成就数据

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表（platformId = 7）：
- `platforms` - 初始化Xbox平台数据
- `player_platform` - 存储Xbox用户平台账号信息
- `games` - 创建新游戏记录
- `game_platform` - 关联游戏和Xbox平台
- `user_platform_library` - 存储用户在Xbox平台上的游戏记录
- `user_achievements` - 存储用户的成就解锁记录
- `user_game_library` - 更新用户游戏库统计信息

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Xbox数据导入任务已启动",
  "data": {
    "taskId": "xbox_import_20241127_100000",
    "status": "processing",
    "estimatedTime": 90,
    "items": {
      "games": 134,
      "achievements": 2500
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

---

### 9.2 GET `/api/v1/xbox/user/{xuid}` - 获取Xbox用户信息
**认证**: 必需  
**路径参数**: xuid = Xbox用户ID（XUID格式）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "xuid": "XUID1234567890",
    "gamertag": "ProGamer007",
    "profileUrl": "https://account.xbox.com/Profile?Gamertag=ProGamer007",
    "avatarUrl": "https://avatar-ssl.xboxlive.com/avatar/ProGamer007/avatar-body.png",
    "accountCreated": "2014-11-22T00:00:00Z",
    "country": "US",
    "gamerscore": 15420,
    "tier": "Gold",
    "gamesOwned": 134,
    "isPublic": true
  }
}
```

---

### 9.3 GET `/api/v1/xbox/games/{titleId}` - 获取Xbox游戏信息
**认证**: 必需  
**路径参数**: titleId = Xbox游戏标题ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "titleId": "2194530",
    "name": "Halo Infinite",
    "type": "game",
    "isFree": false,
    "shortDescription": "标志性射击游戏系列的最新作品",
    "detailedDescription": "详细描述...",
    "headerImage": "https://storeedgefd.dsx.mp.microsoft.com/v9/images/halo-infinite-header.jpg",
    "developers": ["343 Industries"],
    "publishers": ["Xbox Game Studios"],
    "platforms": {
      "windows": true,
      "mac": false,
      "linux": false
    },
    "categories": ["Multi-player", "Online Co-op"],
    "genres": ["FPS", "Sci-Fi"],
    "releaseDate": "2021-12-08",
    "requiredAge": 17,
    "priceOverview": {
      "currency": "USD",
      "initial": 5999,
      "final": 5999,
      "discountPercent": 0
    },
    "achievements": {
      "total": 150
    }
  }
}
```

---

### 9.4 GET `/api/v1/xbox/user/{xuid}/achievements` - 获取Xbox用户成就
**认证**: 必需  
**路径参数**: xuid = Xbox用户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "achievementId": "123-abc-def",
        "gameId": 220055,
        "gameName": "Halo Infinite",
        "achievementName": "finish_tutorial",
        "displayName": "Finish the Tutorial",
        "description": "Complete the opening mission",
        "score": 10,
        "unlocked": true,
        "unlockTime": "2024-10-08T11:00:00Z",
        "iconUnlocked": "url",
        "iconLocked": "url"
      }
    ],
    "total": 2500
  }
}
```

---

## 10. PlayStation (PSN) API 集成

### 10.1 POST `/api/v1/psn/import` - 导入PSN数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "psnOnlineId": "Player_001",
  "importGames": true,
  "importTrophies": true
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `psnOnlineId`: PSN在线ID
- `importGames`: 是否导入游戏库
- `importTrophies`: 是否导入奖杯数据（PSN的成就系统）

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表（platformId = 6）：
- `platforms` - 初始化PSN平台数据
- `player_platform` - 存储PSN用户平台账号信息
- `games` - 创建新游戏记录
- `game_platform` - 关联游戏和PSN平台
- `user_platform_library` - 存储用户在PSN平台上的游戏记录
- `user_achievements` - 存储用户的奖杯解锁记录（PSN奖杯映射为成就）
- `user_game_library` - 更新用户游戏库统计信息

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "PSN数据导入任务已启动",
  "data": {
    "taskId": "psn_import_20241127_100000",
    "status": "processing",
    "estimatedTime": 75,
    "items": {
      "games": 90,
      "achievements": 1800
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

---

### 10.2 GET `/api/v1/psn/user/{onlineId}` - 获取PSN用户信息
**认证**: 必需  
**路径参数**: onlineId = PSN在线ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "onlineId": "Player_001",
    "profileUrl": "https://psnprofiles.com/Player_001",
    "avatarUrl": "https://psn-avatar.com/Player_001.png",
    "accountCreated": "2013-11-15T00:00:00Z",
    "country": "JP",
    "gamesOwned": 90,
    "trophySummary": {
      "bronze": 240,
      "silver": 80,
      "gold": 20,
      "platinum": 5,
      "total": 345
    },
    "level": 12,
    "isPublic": true
  }
}
```

---

### 10.3 GET `/api/v1/psn/games/{titleId}` - 获取PSN游戏信息
**认证**: 必需  
**路径参数**: titleId = PSN游戏标题ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "titleId": "CUSA12345",
    "name": "God of War",
    "type": "game",
    "isFree": false,
    "shortDescription": "史诗级动作冒险游戏",
    "detailedDescription": "详细描述...",
    "headerImage": "https://store.playstation.com/god-of-war-header.jpg",
    "developers": ["Santa Monica Studio"],
    "publishers": ["Sony Interactive Entertainment"],
    "platforms": {
      "windows": false,
      "mac": false,
      "linux": false
    },
    "categories": ["Single-player", "Action"],
    "genres": ["Action", "Adventure"],
    "releaseDate": "2018-04-20",
    "requiredAge": 18,
    "priceOverview": {
      "currency": "USD",
      "initial": 1999,
      "final": 1999,
      "discountPercent": 0
    },
    "achievements": {
      "total": 37
    }
  }
}
```

---

### 10.4 GET `/api/v1/psn/user/{onlineId}/trophies` - 获取PSN用户奖杯
**认证**: 必需  
**路径参数**: onlineId = PSN在线ID  
**说明**: PSN使用奖杯系统（Trophies），映射为成就系统

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "trophyId": "t-001",
        "gameId": 30001,
        "gameName": "God of War",
        "achievementName": "first_blood",
        "displayName": "First Blood",
        "description": "Defeat your first enemy",
        "type": "bronze",
        "score": 15,
        "unlocked": true,
        "unlockTime": "2024-11-20T08:00:00Z",
        "iconUnlocked": "url",
        "iconLocked": "url",
        "rarity": "common"
      }
    ],
    "total": 1800
  }
}
```

---

## 11. Nintendo Switch API 集成

### 11.1 POST `/api/v1/nintendo/import` - 导入Nintendo Switch数据
**认证**: 必需

**请求体**:
```json
{
  "userId": 1000,
  "nintendoAccountId": "abcd1234",
  "sessionToken": "xxxxxxx",
  "importGames": true
}
```

**字段说明**:
- `userId`: **必需** - 用户ID，必须是数据库中已存在的有效用户ID（对应 `user` 表的 `user_id`）
- `nintendoAccountId`: Nintendo账户ID
- `sessionToken`: Nintendo会话令牌（用于访问NSO API）
- `importGames`: 是否导入游戏库

**数据插入表说明**:
该API会将数据插入/更新到以下数据库表（platformId = 8）：
- `platforms` - 初始化Nintendo Switch平台数据
- `player_platform` - 存储Nintendo用户平台账号信息
- `games` - 创建新游戏记录
- `game_platform` - 关联游戏和Nintendo Switch平台
- `user_platform_library` - 存储用户在Nintendo Switch平台上的游戏记录（包含游戏时长）
- `user_game_library` - 更新用户游戏库统计信息

**注意**: Nintendo Switch没有官方公共API，需要通过NSO（Nintendo Switch Online）会话令牌或Web代理方式获取数据

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Nintendo Switch数据导入任务已启动",
  "data": {
    "taskId": "nintendo_import_20241127_100000",
    "status": "processing",
    "estimatedTime": 60,
    "items": {
      "games": 67,
      "achievements": 0
    }
  }
}
```

**错误响应** (400):
- 当 `userId` 无效或不存在时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "用户ID 1000 不存在，请先创建用户"
}
```

- 当 `sessionToken` 无效时：
```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "Nintendo会话令牌无效或已过期"
}
```

---

### 11.2 GET `/api/v1/nintendo/user/{accountId}` - 获取Nintendo用户信息
**认证**: 必需  
**路径参数**: accountId = Nintendo账户ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "nintendoAccountId": "abcd1234",
    "nickname": "SwitchUser",
    "profileUrl": "https://accounts.nintendo.com/profile",
    "avatarUrl": "https://switch/avatar.png",
    "accountCreated": "2017-03-03T00:00:00Z",
    "country": "TW",
    "gamesOwned": 67,
    "isPublic": true
  }
}
```

---

### 11.3 GET `/api/v1/nintendo/games/{id}` - 获取Nintendo游戏信息
**认证**: 必需  
**路径参数**: id = Nintendo游戏ID

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "nintendoGameId": "70010000012345",
    "name": "The Legend of Zelda: Tears of the Kingdom",
    "type": "game",
    "isFree": false,
    "shortDescription": "开放世界动作冒险游戏",
    "detailedDescription": "详细描述...",
    "headerImage": "https://cdn.nintendo.com/zelda-totk-header.jpg",
    "developers": ["Nintendo EPD"],
    "publishers": ["Nintendo"],
    "platforms": {
      "windows": false,
      "mac": false,
      "linux": false
    },
    "categories": ["Single-player", "Adventure"],
    "genres": ["Action", "Adventure"],
    "releaseDate": "2023-05-12",
    "requiredAge": 10,
    "priceOverview": {
      "currency": "USD",
      "initial": 6999,
      "final": 6999,
      "discountPercent": 0
    }
  }
}
```

---

### 11.4 GET `/api/v1/nintendo/user/{accountId}/playtime` - 获取Nintendo用户游戏时长
**认证**: 必需  
**路径参数**: accountId = Nintendo账户ID  
**说明**: Nintendo Switch没有成就系统，因此只提供游戏时长数据

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "gameId": 33001,
        "gameName": "The Legend of Zelda: Tears of the Kingdom",
        "playtimeMinutes": 1820,
        "firstPlayed": "2023-05-12T00:00:00Z",
        "lastPlayed": "2024-11-20T10:00:00Z",
        "daysSinceFirstPlayed": 558,
        "averagePlaytimePerDay": 3.26
      }
    ],
    "total": 67
  }
}
```

---

## 附录：数据同步说明

### 同步频率建议
- **游戏库同步**: 每天1次或手动触发
- **成就同步**: 每周1次或游戏结束后触发
- **新闻同步**: 每小时1次（后台定时任务）
- **价格同步**: 每6小时1次（后台定时任务）

### 同步状态码
- `pending`: 等待中
- `processing`: 处理中
- `completed`: 已完成
- `failed`: 失败
- `partial`: 部分成功

### Steam API 限制
- **请求频率**: 100,000次/天
- **并发限制**: 10个请求/秒
- **数据缓存**: 建议缓存24小时

### 错误处理
```json
// Steam API 不可用
{
  "success": false,
  "code": "ERR_STEAM_API_UNAVAILABLE",
  "message": "Steam API暂时不可用，请稍后重试",
  "data": {
    "retryAfter": 300
  }
}

// Steam用户资料私密
{
  "success": false,
  "code": "ERR_STEAM_PROFILE_PRIVATE",
  "message": "该Steam用户资料为私密状态",
  "data": null
}

// 超过API限制
{
  "success": false,
  "code": "ERR_RATE_LIMIT_EXCEEDED",
  "message": "已超过API请求限制",
  "data": {
    "resetTime": "2024-11-28T00:00:00Z"
  }
}
```
