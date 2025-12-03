# PlayLinker - 统一游戏管理平台

## 项目简介

PlayLinker是一个统一游戏管理平台,旨在整合多个游戏平台(Steam、Epic Games、Origin等)的游戏库,为玩家提供一站式的游戏管理体验。

**开发者**: 开发者B  
**负责模块**: 游戏数据、游戏元数据、游戏库管理、成就系统、Steam集成  
**开发周期**: 2周  
**最后更新**: 2024-11-27

---

## 技术栈

### 后端
- **框架**: C# ASP.NET Core 8.0
- **数据库**: MySQL 8.0
- **ORM**: Entity Framework Core 8.0
- **认证**: JWT Bearer Token
- **API文档**: Swagger UI

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
│   │   └── SteamController.cs           # Steam集成API
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
│   │   └── SteamService.cs
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

#### Steam集成
- ✅ Steam数据导入接口
- ✅ Steam用户信息查询
- ✅ Steam游戏信息查询
- ✅ 对接Steam Web API

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
- ✅ Swagger API文档
- ✅ JWT认证机制
- ✅ 统一响应格式
- ✅ 错误处理和日志记录
- ✅ CORS跨域支持
- ✅ Entity Framework Code First
- ✅ 数据库关系映射

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

### 第1周完成情况

✅ **Day 1-2**: 项目初始化与游戏数据
- 完成项目结构搭建
- 完成Entity Framework配置
- 实现游戏列表、详情、搜索接口
- 编写单元测试(控制器逻辑)

✅ **Day 3-4**: 游戏库与Steam集成
- 完成Steam API客户端开发
- 实现游戏库概览接口
- 实现用户游戏列表接口
- 实现同步平台数据接口
- 实现Steam数据导入接口
- 实现Steam用户信息查询

### 第2周完成情况

✅ **Day 5**: 成就系统
- 实现游戏成就列表接口
- 实现用户成就总览接口
- 实现用户游戏成就接口
- 实现成就同步接口

✅ **Day 6-7**: 元数据、新闻与前端
- 完成题材/分类/开发商/发行商接口
- 实现新闻列表和详情接口
- 实现游戏新闻接口
- 完成游戏排行榜接口
- 创建Vue.js前端项目
- 实现所有主要页面组件
- 集成前后端交互

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

- **项目负责人**: 开发者B
- **邮箱**: developer@playlinker.com
- **问题反馈**: 请在GitHub Issues中提交

---

**最后更新**: 2024-11-27  
**版本**: v1.0.0  
**状态**: 开发中 🚧

---

## 致谢

感谢以下开源项目和服务:
- ASP.NET Core团队
- Entity Framework Core团队
- Vue.js团队
- Steam Web API
- MySQL数据库

PlayLinker © 2024

