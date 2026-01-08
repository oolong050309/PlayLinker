# PlayLinker - 统一游戏管理平台

## 项目简介

PlayLinker是一个统一游戏管理平台,旨在整合多个游戏平台(Steam、Epic Games、Origin等)的游戏库,为玩家提供一站式的游戏管理体验。

**开发团队**: 
- **郑耀辉**: 用户认证、用户管理、通知中心、家长监管
- **刘逸飞**: 游戏数据、游戏库管理、成就系统、Steam集成
- **杨景翔**: 本地文件管理、存档管理、Mod管理、报表系统、数据分析
- **杨家悦**: 用户偏好、推荐系统、价格监控、愿望单

**开发方法**: 增量开发模型（Incremental Development）  
**开发周期**: 10周（2024年11月1日 - 2025年1月8日）  
**最后更新**: 2025-01-08  
**当前版本**: v2.0.0  
**状态**: ✅ 核心功能已完成

---

## 技术栈

### 后端
- **框架**: C# ASP.NET Core 8.0
- **数据库**: MySQL 8.0
- **ORM**: Entity Framework Core 8.0
- **认证**: JWT Bearer Token
- **API文档**: Swagger UI
- **PDF生成**: QuestPDF
- **日志**: ILogger (ASP.NET Core)

### 前端
- **框架**: Vue 3
- **构建工具**: Vite
- **状态管理**: Pinia
- **HTTP客户端**: Axios
- **路由**: Vue Router

---

## 项目结构

```
PlayLinker/
├── Backend/                       # C# ASP.NET Core后端
│   ├── Controllers/              # API控制器
│   │   ├── GamesController.cs           # 游戏数据API
│   │   ├── MetadataController.cs        # 游戏元数据API
│   │   ├── LibraryController.cs         # 游戏库管理API
│   │   ├── AchievementsController.cs    # 成就系统API
│   │   ├── NewsController.cs            # 新闻资讯API
│   │   ├── SteamController.cs           # Steam集成API
│   │   ├── XboxController.cs            # Xbox集成API
│   │   ├── PsnController.cs             # PSN集成API
│   │   ├── GogController.cs             # GOG集成API
│   │   ├── LocalGamesController.cs      # 本地游戏管理API
│   │   ├── SavesController.cs           # 存档管理API
│   │   ├── CloudController.cs           # 云存档管理API
│   │   ├── ModsController.cs            # Mod管理API
│   │   ├── ReportsController.cs         # 报表系统API
│   │   ├── AnalyticsController.cs       # 数据分析API
│   │   ├── WishlistController.cs        # 愿望单API
│   │   └── PreferencesController.cs     # 用户偏好API
│   ├── Models/                   # 数据模型
│   │   ├── Entities/             # 数据库实体类
│   │   │   ├── Game.cs
│   │   │   ├── Achievement.cs
│   │   │   ├── News.cs
│   │   │   └── UserLibrary.cs
│   │   ├── DTOs/                 # 数据传输对象
│   │   │   ├── GameDtos.cs
│   │   │   ├── LibraryDtos.cs
│   │   │   ├── AchievementDtos.cs
│   │   │   ├── NewsDtos.cs
│   │   │   └── SteamDtos.cs
│   │   └── ApiResponse.cs        # 统一响应格式
│   ├── Services/                 # 业务逻辑服务
│   │   ├── ISteamService.cs
│   │   ├── SteamService.cs
│   │   └── ReportGenerationService.cs
│   ├── Data/                     # 数据访问层
│   │   └── PlayLinkerDbContext.cs
│   ├── Program.cs                # 程序入口
│   ├── PlayLinker.csproj         # 项目配置
│   └── appsettings.json          # 应用配置
├── Frontend/                      # Vue.js前端
│   ├── src/
│   │   ├── views/                # 页面组件
│   │   │   ├── GameList.vue             # 游戏列表
│   │   │   ├── GameDetail.vue           # 游戏详情
│   │   │   ├── GameRanking.vue          # 游戏排行榜
│   │   │   ├── Library.vue              # 游戏库
│   │   │   ├── Achievements.vue         # 成就
│   │   │   └── News.vue                 # 新闻
│   │   ├── api/                  # API接口封装
│   │   │   └── index.js
│   │   ├── router/               # 路由配置
│   │   │   └── index.js
│   │   ├── App.vue               # 根组件
│   │   ├── main.js               # 入口文件
│   │   └── style.css             # 全局样式
│   ├── index.html
│   ├── vite.config.js
│   └── package.json
├── init.sql                       # 数据库初始化脚本
├── API_Developer_B.md            # API文档
└── README.md                     # 项目说明文档(本文件)
```

---

## 环境要求

### 后端环境
- .NET 8.0 SDK 或更高版本
- MySQL 8.0 或更高版本
- Visual Studio 2022 / VS Code / JetBrains Rider

### 前端环境
- Node.js 18+ 
- npm 或 yarn

---

## 安装步骤

### 1. 克隆项目

```bash
git clone <repository-url>
cd PlayLinker
```

### 2. 数据库配置

#### 2.1 创建数据库

```bash
# 登录MySQL
mysql -u root -p

# 执行初始化脚本
source init.sql
```

或者直接导入SQL文件:

```bash
mysql -u root -p < init.sql
```

#### 2.2 验证数据库

```sql
USE playlinker_db;
SHOW TABLES;
```

你应该能看到以下schema:
- `user_management` - 用户管理
- `game_data` - 游戏数据
- `user_library` - 用户游戏库
- `business_features` - 业务功能
- `parental_notification` - 家长监管与通知

### 3. 后端配置

#### 3.1 配置数据库连接

编辑 `Backend/appsettings.json`,配置云服务器数据库连接:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=你的云服务器IP或域名;Port=3306;Database=playlinker_db;User=数据库用户名;Password=数据库密码;CharSet=utf8mb4;AllowPublicKeyRetrieval=True;SslMode=Required"
  }
}
```

**配置说明**:
- `Server`: 云服务器的IP地址或域名(例如: `123.456.789.0` 或 `mysql.example.com`)
- `Port`: MySQL端口,默认3306
- `Database`: 数据库名称,默认 `playlinker_db`
- `User`: 云服务器MySQL的用户名
- `Password`: 云服务器MySQL的密码
- `SslMode=Required`: 启用SSL加密连接(云服务器必需)

**注意事项**: 确保云服务器的MySQL允许远程连接,防火墙已开放3306端口

#### 3.2 配置Steam API密钥

在 `appsettings.json` 中配置Steam API密钥:

```json
{
  "SteamAPI": {
    "ApiKey": "你的Steam API密钥",
    "BaseUrl": "https://api.steampowered.com"
  }
}
```

> **获取Steam API密钥**: 访问 https://steamcommunity.com/dev/apikey

#### 3.3 恢复依赖并运行

```bash
cd Backend
dotnet restore
dotnet run
```

后端将在 `http://localhost:5000` 启动

**访问Swagger UI**:
- 启动后自动打开浏览器访问 Swagger UI
- 或手动访问: `http://localhost:5000/swagger`
- Swagger UI 在所有环境下都可用,无需特殊配置

### 4. 前端配置

#### 4.1 安装依赖

```bash
cd Frontend
npm install
```

#### 4.2 运行开发服务器

```bash
npm run dev
```

前端将在 `http://localhost:3000` 启动

---

## API文档

### Swagger UI访问

启动后端后,访问以下地址查看完整的API文档:

```
http://localhost:5000/swagger
```

**使用JWT认证测试需要认证的API**:

1. **获取Token**: 在Swagger UI中调用 `POST /api/v1/auth/token` 接口
   - 可以传入可选的参数: `userId`, `username`, `role`
   - 不传参数则使用默认值生成Token
   - 响应中会返回 `token` 字段

2. **设置Token**: 点击Swagger UI右上角的 **"Authorize"** 按钮
   - 在弹出的对话框中输入: `Bearer {你的token}`
   - 例如: `Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
   - 点击 "Authorize" 按钮确认

3. **测试API**: 现在可以测试所有需要认证的API接口了
   - 所有请求会自动携带Token
   - Token有效期为1小时(可在appsettings.json中配置)

### API Base URL

```
http://localhost:5000/api/v1
```

### 主要API端点

#### 认证API(测试用)
- `POST /api/v1/auth/token` - 生成测试用JWT Token
- `POST /api/v1/auth/validate` - 验证Token是否有效

#### 游戏数据API
- `GET /api/v1/games` - 获取游戏列表
- `GET /api/v1/games/{id}` - 获取游戏详情
- `GET /api/v1/games/search` - 搜索游戏
- `GET /api/v1/games/ranking` - 游戏排行榜
- `POST /api/v1/games` - 添加游戏(需admin权限)
- `PUT /api/v1/games/{id}` - 更新游戏(需admin权限)

#### 游戏元数据API
- `GET /api/v1/genres` - 获取所有游戏题材
- `GET /api/v1/categories` - 获取所有游戏分类
- `GET /api/v1/developers` - 获取开发商列表
- `GET /api/v1/publishers` - 获取发行商列表

#### 游戏库管理API(需认证)
- `GET /api/v1/library/overview` - 游戏库概览
- `GET /api/v1/library/games` - 用户游戏列表
- `POST /api/v1/library/sync` - 同步平台数据
- `GET /api/v1/library/stats` - 游戏统计数据

#### 成就系统API
- `GET /api/v1/games/{gameId}/achievements` - 游戏成就列表(公开)
- `GET /api/v1/library/achievements` - 用户成就总览(需认证)
- `GET /api/v1/library/games/{id}/achievements` - 用户游戏成就(需认证)
- `POST /api/v1/library/achievements/sync` - 同步成就(需认证)

#### 新闻资讯API
- `GET /api/v1/news` - 新闻列表
- `GET /api/v1/news/{id}` - 新闻详情
- `GET /api/v1/games/{id}/news` - 游戏相关新闻

#### Steam集成API(需认证)
- `POST /api/v1/steam/import` - 导入Steam数据
- `GET /api/v1/steam/user/{steamId}` - 获取Steam用户信息
- `GET /api/v1/steam/games/{appId}` - 获取Steam游戏信息

#### Xbox集成API(需认证)
- `GET /api/v1/xbox/token-status` - 检查Xbox令牌状态
- `POST /api/v1/xbox/authenticate` - Xbox认证
- `POST /api/v1/xbox/import` - 导入Xbox数据
- `GET /api/v1/xbox/user/{xuid}` - 获取Xbox用户信息
- `GET /api/v1/xbox/games/{titleId}` - 获取Xbox游戏信息
- `GET /api/v1/xbox/user/{xuid}/achievements` - 获取Xbox用户成就

#### PSN集成API(需认证)
- `GET /api/v1/psn/token-status` - 检查PSN令牌状态
- `POST /api/v1/psn/authenticate` - PSN认证
- `POST /api/v1/psn/import` - 导入PSN数据
- `GET /api/v1/psn/user/{onlineId}` - 获取PSN用户信息
- `GET /api/v1/psn/games/{titleId}` - 获取PSN游戏信息
- `GET /api/v1/psn/user/{onlineId}/trophies` - 获取PSN用户奖杯

#### GOG集成API(需认证)
- `GET /api/v1/gog/token-status` - 检查GOG令牌状态
- `POST /api/v1/gog/authenticate` - GOG认证
- `POST /api/v1/gog/import` - 导入GOG数据
- `GET /api/v1/gog/user/{gogUserId}` - 获取GOG用户信息
- `GET /api/v1/gog/games/{gogGameId}` - 获取GOG游戏信息

#### 本地游戏管理API(需认证)
- `POST /api/v1/local-games/scan` - 扫描本地游戏
- `GET /api/v1/local-games` - 获取本地游戏列表
- `GET /api/v1/local-games/{id}` - 获取本地游戏详情
- `DELETE /api/v1/local-games/{id}` - 删除本地游戏
- `PUT /api/v1/local-games/{id}/path` - 更新游戏路径

#### 存档管理API(需认证)
- `GET /api/v1/saves/local` - 获取本地存档列表
- `POST /api/v1/saves/backup` - 备份存档
- `POST /api/v1/saves/restore/{id}` - 恢复存档
- `DELETE /api/v1/saves/{id}` - 删除存档

#### 云存档管理API(需认证)
- `GET /api/v1/cloud` - 获取云存档列表
- `POST /api/v1/cloud/upload` - 上传存档到云端
- `POST /api/v1/cloud/download` - 从云端下载存档
- `DELETE /api/v1/cloud/{id}` - 删除云存档

#### Mod管理API(需认证)
- `POST /api/v1/mods/install` - 安装Mod(网页版手动安装指导)
- `PUT /api/v1/mods/{id}/toggle` - 启用/禁用Mod
- `POST /api/v1/mods/{id}/confirm-install` - 确认手动安装完成
- `DELETE /api/v1/mods/{id}` - 卸载Mod
- `GET /api/v1/mods/conflicts` - 检测Mod冲突

#### 报表系统API(需认证)
- `GET /api/v1/reports/templates` - 获取报表模板列表
- `POST /api/v1/reports/generate` - 生成报表
- `GET /api/v1/reports` - 获取报表历史
- `GET /api/v1/reports/{id}` - 获取报表详情
- `GET /api/v1/reports/{id}/download` - 下载报表(支持HTML/CSV/PDF)
- `DELETE /api/v1/reports/{id}` - 删除报表

#### 数据分析API(需认证)
- `GET /api/v1/analytics/playtime` - 游玩时间分析
- `GET /api/v1/analytics/genres` - 题材偏好分析
- `GET /api/v1/analytics/platforms` - 平台分布分析
- `GET /api/v1/analytics/achievements` - 成就统计分析
- `GET /api/v1/analytics/spending` - 消费分析(无数据提示)

#### 愿望单API(需认证)
- `GET /api/v1/wishlist` - 获取愿望单列表
- `POST /api/v1/wishlist` - 添加到愿望单
- `DELETE /api/v1/wishlist/{id}` - 从愿望单移除

**详细文档**:
- [开发者B API文档](API_Developer_B.md)
- [开发者C API文档](API_Developer_C.md)
- [Steam集成文档](Backend/README.md)
- [Xbox集成文档](Backend/XBOX_INTEGRATION.md)
- [PSN集成文档](Backend/PSN_INTEGRATION.md)
- [GOG集成文档](Backend/GOG_INTEGRATION.md)

---

## 测试方式

### 1. 测试环境准备

#### 准备测试数据

在MySQL中插入一些测试数据:

```sql
USE playlinker_db;

-- 插入平台数据
INSERT INTO game_data.platforms (platform_name, description, status) VALUES
('Steam', 'Valve旗下游戏平台', 1),
('Epic Games', 'Epic Games商店', 1),
('Origin', 'EA游戏平台', 1);

-- 插入游戏题材
INSERT INTO game_data.genres (name) VALUES
('Action'), ('FPS'), ('RPG'), ('Strategy'), ('Adventure');

-- 插入游戏分类
INSERT INTO game_data.categories (name) VALUES
('Multiplayer'), ('Single-player'), ('Co-op'), ('PvP'), ('Cross-Platform');

-- 插入语言
INSERT INTO game_data.languages (language_name) VALUES
('English'), ('简体中文'), ('繁體中文'), ('日本語');

-- 插入开发商
INSERT INTO game_data.developers (name) VALUES
('Valve'), ('CD Projekt Red'), ('FromSoftware');

-- 插入发行商
INSERT INTO game_data.publishers (name) VALUES
('Valve'), ('CD Projekt'), ('Bandai Namco');

-- 插入测试游戏
INSERT INTO game_data.games (
    name, is_free, require_age, short_description, detailed_description,
    header_image, capsile_image, background,
    windows, mac, linux, release_date, review_score, review_score_desc,
    num_reviews, total_positive
) VALUES
(
    'Counter-Strike 2', 1, 0, 
    '全球最受欢迎的竞技射击游戏',
    'Counter-Strike 2是一款免费的多人射击游戏',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/730/capsule_616x353.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/730/page_bg_generated_v6b.jpg',
    1, 1, 1, '2023-09-27', 85, '特别好评', 500000, 450000
),
(
    'Cyberpunk 2077', 0, 18,
    '开放世界动作冒险游戏',
    'Cyberpunk 2077是一款发生在未来都市夜之城的开放世界动作冒险游戏',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1091500/header.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1091500/capsule_616x353.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1091500/page_bg_generated_v6b.jpg',
    1, 0, 0, '2020-12-10', 78, '多半好评', 600000, 480000
),
(
    'Elden Ring', 0, 16,
    '魂系列最新作品',
    'Elden Ring是FromSoftware开发的动作角色扮演游戏',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/header.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/capsule_616x353.jpg',
    'https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/page_bg_generated_v6b.jpg',
    1, 0, 0, '2022-02-25', 92, '好评如潮', 700000, 670000
);

-- 插入游戏题材关联
INSERT INTO game_data.game_genres (game_id, genre_id) VALUES
(10000, 1), (10000, 2),  -- CS2: Action, FPS
(10001, 1), (10001, 3),  -- Cyberpunk: Action, RPG
(10002, 1), (10002, 3);  -- Elden Ring: Action, RPG

-- 插入游戏开发商关联
INSERT INTO game_data.game_developers (game_id, developers_id) VALUES
(10000, 1),  -- CS2: Valve
(10001, 2),  -- Cyberpunk: CD Projekt Red
(10002, 3);  -- Elden Ring: FromSoftware

-- 插入游戏发行商关联
INSERT INTO game_data.game_publishers (game_id, publishers_id) VALUES
(10000, 1),  -- CS2: Valve
(10001, 2),  -- Cyberpunk: CD Projekt
(10002, 3);  -- Elden Ring: Bandai Namco

-- 插入游戏排行榜
INSERT INTO game_data.game_ranking (game_id, pack_in_game, last_week_rank, current_rank) VALUES
(10000, 1500000, 1, 1),
(10002, 800000, 3, 2),
(10001, 500000, 2, 3);

-- 插入新闻
INSERT INTO game_data.news (news_title, news_url, date, author, contents) VALUES
(
    'CS2重大更新发布',
    'https://store.steampowered.com/news/',
    UNIX_TIMESTAMP('2024-11-27 10:00:00'),
    'Valve',
    '更新内容包括新地图、武器平衡调整、性能优化等...'
),
(
    'Cyberpunk 2077发布2.0更新',
    'https://www.cyberpunk.net/news',
    UNIX_TIMESTAMP('2024-11-25 15:00:00'),
    'CD Projekt Red',
    '全面更新游戏系统,包括警察系统、车辆驾驶等...'
);

-- 插入游戏新闻关联
INSERT INTO game_data.game_news (game_id, news_id) VALUES
(10000, 1),
(10001, 2);

-- 插入成就
INSERT INTO game_data.achievements (
    game_id, achievement_name, displayName, hidden, description,
    icon_unlocked, icon_locked
) VALUES
(
    10000, 'first_kill', '首杀', 0, '获得第一次击杀',
    'https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/first_kill.jpg',
    'https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/first_kill_gray.jpg'
),
(
    10000, 'ace', 'ACE', 0, '在一局中击杀所有敌人',
    'https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/ace.jpg',
    'https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/ace_gray.jpg'
);
```

### 2. 后端API测试

#### 方法1: 使用Swagger UI(推荐)

1. 启动后端: `dotnet run`
2. 访问 `http://localhost:5000/swagger`
3. **获取Token** (测试需要认证的API时):
   - 调用 `POST /api/v1/auth/token` 接口
   - 可以传入参数: `{"userId": 1, "username": "testuser", "role": "user"}`
   - 或者不传参数使用默认值
   - 复制响应中的 `token` 字段
4. **设置Token**:
   - 点击Swagger UI右上角的 **"Authorize"** 按钮
   - 输入: `Bearer {你的token}` (注意Bearer后面有空格)
   - 点击 "Authorize" 确认
5. 现在可以测试所有API端点了,需要认证的接口会自动携带Token

#### 方法2: 使用curl命令

```bash
# 测试获取游戏列表
curl http://localhost:5000/api/v1/games

# 测试获取游戏详情
curl http://localhost:5000/api/v1/games/10000

# 测试搜索游戏
curl "http://localhost:5000/api/v1/games/search?q=Counter"

# 测试获取游戏排行榜
curl http://localhost:5000/api/v1/games/ranking

# 测试获取游戏题材
curl http://localhost:5000/api/v1/genres

# 测试获取新闻列表
curl http://localhost:5000/api/v1/news

# 测试获取游戏成就
curl http://localhost:5000/api/v1/games/10000/achievements
```

#### 方法3: 使用Postman

1. 导入API端点到Postman
2. 设置Base URL为 `http://localhost:5000/api/v1`
3. 测试各个API

### 3. 前端功能测试

#### 启动前端

```bash
cd Frontend
npm run dev
```

访问 `http://localhost:3000`

#### 测试功能清单

- [ ] **游戏列表页**
  - 查看游戏列表
  - 搜索游戏
  - 排序功能
  - 分页功能
  - 点击游戏跳转详情

- [ ] **游戏详情页**
  - 查看游戏基本信息
  - 查看游戏介绍
  - 查看系统需求
  - 查看开发商/发行商

- [ ] **游戏排行榜**
  - 查看TOP榜单
  - 排名展示
  - 点击跳转详情

- [ ] **游戏库**
  - 查看游戏库概览统计

- [ ] **新闻页**
  - 查看新闻列表
  - 分页功能

### 4. Steam API集成测试

#### 测试Steam API(需要有效的Steam API密钥)

```bash
# 测试获取Steam游戏信息
curl http://localhost:5000/api/v1/steam/games/730

# 测试获取Steam用户信息(需要认证)
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost:5000/api/v1/steam/user/76561198000000000
```

### 5. 数据库测试

```sql
-- 验证游戏数据
SELECT COUNT(*) FROM game_data.games;

-- 验证关联数据
SELECT g.name, gr.name as genre 
FROM game_data.games g
JOIN game_data.game_genres gg ON g.game_id = gg.game_id
JOIN game_data.genres gr ON gg.genre_id = gr.genre_id;

-- 验证排行榜
SELECT g.name, gr.current_rank, gr.pack_in_game
FROM game_data.games g
JOIN game_data.game_ranking gr ON g.game_id = gr.game_id
ORDER BY gr.current_rank;

-- 验证新闻
SELECT n.news_title, g.name as game_name
FROM game_data.news n
JOIN game_data.game_news gn ON n.news_id = gn.news_id
JOIN game_data.games g ON gn.game_id = g.game_id;
```

### 6. 性能测试

使用Apache Bench进行简单的性能测试:

```bash
# 测试游戏列表接口
ab -n 1000 -c 10 http://localhost:5000/api/v1/games

# 测试游戏详情接口
ab -n 1000 -c 10 http://localhost:5000/api/v1/games/10000
```

### 7. 错误处理测试

```bash
# 测试不存在的游戏
curl http://localhost:5000/api/v1/games/999999

# 测试无效的参数
curl "http://localhost:5000/api/v1/games?page=-1"

# 测试需要认证的接口(不带Token)
curl http://localhost:5000/api/v1/library/overview
```

---

## 功能特性

### 已实现功能 ✅

#### 游戏数据管理
- ✅ 游戏列表查询(支持分页、排序、筛选)
- ✅ 游戏详情查询(包含完整元数据)
- ✅ 游戏搜索功能
- ✅ 游戏排行榜
- ✅ 游戏CRUD操作(管理员)
- ✅ 游戏Mod列表查询

#### 游戏元数据
- ✅ 题材(Genre)管理
- ✅ 分类(Category)管理
- ✅ 开发商(Developer)管理
- ✅ 发行商(Publisher)管理
- ✅ 语言(Language)支持

#### 游戏库管理
- ✅ 游戏库概览
- ✅ 用户游戏列表
- ✅ 平台数据同步接口
- ✅ 游戏统计数据

#### 成就系统
- ✅ 游戏成就列表
- ✅ 用户成就总览
- ✅ 用户游戏成就查询
- ✅ 成就同步接口

#### 新闻资讯
- ✅ 新闻列表查询
- ✅ 新闻详情
- ✅ 游戏相关新闻

#### 平台集成
- ✅ Steam数据导入接口
- ✅ Steam用户信息查询
- ✅ Steam游戏信息查询
- ✅ Xbox认证与数据导入
- ✅ PSN认证与数据导入
- ✅ GOG认证与数据导入
- ✅ 对接多平台Web API

#### 本地游戏管理(开发者C)
- ✅ 本地游戏扫描(网页版文件上传方式)
- ✅ 本地游戏列表(分页、排序、筛选)
- ✅ 本地游戏详情(含存档和Mod信息)
- ✅ 游戏路径更新
- ✅ 游戏删除(仅删除记录)

#### 存档管理(开发者C)
- ✅ 本地存档列表查询
- ✅ 存档备份(模拟实现)
- ✅ 存档恢复(模拟实现)
- ✅ 存档删除
- ✅ 存档汇总统计

#### 云存档管理(开发者C)
- ✅ 云存档列表(分页、汇总)
- ✅ 上传存档到云端(模拟实现)
- ✅ 从云端下载存档(模拟实现)
- ✅ 云存档删除
- ✅ 存储空间统计

#### Mod管理(开发者C)
- ✅ Mod安装(网页版手动安装指导)
- ✅ Mod启用/禁用
- ✅ 确认手动安装完成
- ✅ Mod卸载
- ✅ Mod冲突检测(简化实现)

#### 报表系统(开发者C)
- ✅ 报表模板管理
- ✅ 报表生成(异步任务)
- ✅ 报表历史记录
- ✅ 报表详情查询
- ✅ 报表下载(HTML/CSV/PDF三种格式)
- ✅ 报表删除
- ✅ QuestPDF专业PDF生成

#### 数据分析(开发者C)
- ✅ 游玩时间分析(真实数据)
- ✅ 题材偏好分析(真实数据)
- ✅ 平台分布分析(真实数据)
- ✅ 成就统计分析(真实数据)
- ✅ 消费分析(无数据提示)

#### 愿望单与价格监控(开发者D)
- ✅ 愿望单列表查询
- ✅ 添加到愿望单
- ✅ 从愿望单移除
- ✅ 价格历史记录
- ✅ 价格提醒订阅

#### 前端界面
- ✅ 游戏列表页面
- ✅ 游戏详情页面
- ✅ 游戏排行榜页面
- ✅ 游戏库概览页面
- ✅ 成就页面
- ✅ 新闻列表页面
- ✅ 响应式布局
- ✅ 优雅的UI设计

#### 技术特性
- ✅ RESTful API设计
- ✅ Swagger API文档(完整标注)
- ✅ JWT认证机制
- ✅ 统一响应格式
- ✅ 完善的错误处理和日志记录
- ✅ CORS跨域支持
- ✅ Entity Framework Code First
- ✅ 复杂数据库关系映射
- ✅ 网页版功能限制的优雅处理
- ✅ 分页、排序、筛选通用实现

### 待实现功能 📋

#### 用户认证与授权
- ⏳ 用户注册/登录
- ⏳ JWT Token生成和刷新
- ⏳ 角色权限管理
- ⏳ OAuth第三方登录

#### 游戏库功能增强
- ⏳ 实际的平台账号绑定
- ⏳ 真实的数据同步逻辑
- ⏳ 游戏时长统计
- ⏳ 最近游玩记录

#### 成就系统增强
- ⏳ 成就解锁率统计
- ⏳ 稀有成就展示
- ⏳ 成就进度追踪

#### Steam API完整集成
- ⏳ 用户游戏库导入
- ⏳ 游戏时长同步
- ⏳ 好友列表获取
- ⏳ 愿望单同步

#### 数据可视化
- ⏳ 游戏时长图表
- ⏳ 题材分布图
- ⏳ 成就完成度统计
- ⏳ 游玩趋势分析

#### 个性化推荐
- ⏳ 基于游戏历史的推荐
- ⏳ 相似游戏推荐
- ⏳ 折扣提醒

---

## 开发进度

### 增量开发过程

本项目采用**增量开发模型**，分为5个增量阶段：

| 增量 | 阶段 | 时间 | 交付物 |
|------|------|------|--------|
| 增量 1 | 需求分析与技术调研 | 11.1 - 11.9 | 需求文档、技术方案 |
| 增量 2 | 数据库设计 | 11.9 - 11.20 | E-R 图、数据库表结构 |
| 增量 3 | 后端 API 开发 | 11.20 - 12.18 | 后端服务、API 接口 |
| 增量 4 | 前端开发 | 12.18 - 1.1 | 前端页面、交互功能 |
| 增量 5 | 集成测试与部署 | 1.1 - 1.8 | 完整系统、部署上线 |

详细会议纪要请参考：[会议纪要](任务文档/会议纪要.md)

### 后端开发任务分配

| 成员 | 负责模块 | 详细文档 |
|------|----------|----------|
| 郑耀辉 | 用户认证、用户管理、通知中心、家长监管 | [API_Developer_A.md](任务文档/API_Developer_A.md) |
| 刘逸飞 | 游戏数据、游戏库管理、成就系统、Steam集成 | [API_Developer_B.md](任务文档/API_Developer_B.md) |
| 杨景翔 | 本地文件管理、存档管理、Mod管理、报表系统 | [API_Developer_C.md](任务文档/API_Developer_C.md) |
| 杨家悦 | 用户偏好、推荐系统、价格监控、愿望单 | [API_Developer_D.md](任务文档/API_Developer_D.md) |

### 前端开发任务分配

详见：[前端开发指南](任务文档/FRONTEND_DEVELOPMENT_GUIDE.md)

| 成员 | 前端模块 |
|------|----------|
| 郑耀辉 | 登录注册、通知中心、家长控制页面 |
| 刘逸飞 | 游戏库、游戏详情、商店详情页面 |
| 杨景翔 | Mod存档管理、数据分析、设置页面 |
| 杨家悦 | 游戏发现、平台绑定、价格监控页面 |

---

## 已知问题

1. **认证功能未完全实现**: 当前JWT认证配置已完成,但实际的登录注册接口尚未实现,需要认证的接口会使用模拟的用户ID
2. **Steam API密钥**: 需要自行申请Steam API密钥并配置
3. **数据同步为模拟实现**: 游戏库和成就的同步功能目前返回模拟数据,实际的Steam数据同步逻辑需要进一步开发
4. **图片资源**: 测试数据中的图片URL可能需要替换为有效的CDN地址
5. **前端样式**: 当前使用基础CSS,未引入UI组件库,样式较为简单

---

## 后续优化计划

### 短期(1-2周)
1. 实现完整的用户认证系统
2. 完善Steam数据同步逻辑
3. 添加数据缓存机制(Redis)
4. 优化数据库查询性能
5. 添加更多的单元测试和集成测试

### 中期(1个月)
1. 引入前端UI组件库(如Element Plus)
2. 实现数据可视化功能
3. 添加个性化推荐系统
4. 支持更多游戏平台(Epic、Origin等)
5. 实现愿望单和折扣提醒功能

### 长期(3个月+)
1. 移动端适配和PWA支持
2. 实时通知系统(SignalR)
3. 社交功能(好友、评论、分享)
4. 游戏Mod管理
5. 云存档备份功能
6. 家长监控功能
7. AI游戏推荐

---

## 常见问题(FAQ)

### Q1: 如何获取Steam API密钥?
**A**: 访问 https://steamcommunity.com/dev/apikey ,使用你的Steam账号登录,填写域名信息即可获取API密钥。

### Q2: 为什么访问需要认证的接口返回401错误?
**A**: 当前版本的用户认证系统尚未完全实现,需要认证的接口会检查JWT Token。你可以:
1. 暂时移除Controller上的`[Authorize]`特性进行测试
2. 或者等待后续版本实现完整的登录功能

### Q3: 数据库连接失败怎么办?
**A**: 请检查:
1. MySQL服务是否正常运行
2. `appsettings.json`中的连接字符串是否正确
3. 数据库用户名和密码是否正确
4. 防火墙是否允许3306端口访问

### Q4: 前端无法访问后端API?
**A**: 请确保:
1. 后端已启动在`http://localhost:5000`
2. 前端的API代理配置正确(`vite.config.js`)
3. CORS配置已启用
4. 浏览器开发者工具中查看具体错误信息

### Q5: 如何添加测试数据?
**A**: 执行上面"测试方式"章节中的SQL插入语句,或者使用Postman/Swagger UI调用添加游戏的API接口(需要admin权限)。

---

## 参考资料

### Steam Web API文档
- Steam API文档: https://steamcommunity.com/dev
- Steam Web API参考: https://developer.valvesoftware.com/wiki/Steam_Web_API

### 技术文档
- ASP.NET Core文档: https://learn.microsoft.com/aspnet/core
- Entity Framework Core: https://learn.microsoft.com/ef/core
- Vue 3文档: https://vuejs.org
- MySQL文档: https://dev.mysql.com/doc

---

## 贡献指南

欢迎提交Issue和Pull Request来帮助改进这个项目!

### 开发规范
1. 遵循C#编码规范(.NET Coding Conventions)
2. 使用有意义的变量和方法命名
3. 为公共API添加XML文档注释
4. 编写单元测试覆盖核心逻辑
5. 提交前运行代码格式化工具

### 提交规范
- feat: 新功能
- fix: 修复bug
- docs: 文档更新
- style: 代码格式调整
- refactor: 重构
- test: 测试相关
- chore: 构建/工具链更新

---

## 许可证

本项目仅用于学习和研究目的。

---

## 联系方式

- **项目负责人**: 郑耀辉、刘逸飞、杨景翔、杨家悦
- **问题反馈**: 请在GitHub Issues中提交

---

**最后更新**: 2025-01-08  
**版本**: v2.0.0  
**状态**: ✅ 核心功能已完成

---

## 项目统计

### 代码规模
- **API端点**: 60+ 个
- **数据库表**: 40+ 张
- **实体类**: 35+ 个
- **DTO类**: 50+ 个
- **控制器**: 20+ 个

### 功能模块
- ✅ **模块A (郑耀辉)**: 用户认证、通知中心、家长监管
- ✅ **模块B (刘逸飞)**: 游戏数据与平台集成
- ✅ **模块C (杨景翔)**: 本地文件与数据分析
- ✅ **模块D (杨家悦)**: 用户偏好与推荐

### 开发进度
- **增量1**: 需求分析与技术调研 ✅
- **增量2**: 数据库设计 ✅
- **增量3**: 后端API开发 ✅
- **增量4**: 前端开发 ✅
- **增量5**: 集成测试与部署 ✅

---

## 致谢

感谢以下开源项目和服务:
- ASP.NET Core团队
- Entity Framework Core团队
- Vue.js团队
- Steam Web API
- Xbox Web API (xbox-webapi-python)
- PSN API (psn-api)
- GOG API
- MySQL数据库

PlayLinker © 2024

