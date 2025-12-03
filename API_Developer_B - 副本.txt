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
- `platform`: steam | epic | origin | uplay | gog
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
  "platformId": 1,
  "gameId": 10001
}
```

**字段说明**:
- `platformId`: 平台ID（可选，不传则同步所有平台）
- `gameId`: 游戏ID（可选，不传则同步所有游戏）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "成就同步成功",
  "data": {
    "syncedGames": 1,
    "newUnlocks": 3,
    "totalUnlocked": 48,
    "syncTime": "2024-11-27T10:00:00Z"
  }
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

## 6. Steam API 集成

### 6.1 POST `/api/v1/steam/import` - 导入Steam数据
**认证**: 必需

**请求体**:
```json
{
  "steamId": "76561198000000000",
  "importGames": true,
  "importAchievements": true,
  "importFriends": false
}
```

**字段说明**:
- `steamId`: Steam用户ID
- `importGames`: 是否导入游戏库
- `importAchievements`: 是否导入成就数据
- `importFriends`: 是否导入好友列表

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
