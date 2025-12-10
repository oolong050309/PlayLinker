# PlayLinker Backend API - Developer C

PlayLinker 统一游戏管理平台后端 API - 本地文件管理、存档管理、Mod管理、报表系统

## 技术栈

- **框架**: ASP.NET Core 9.0
- **数据库**: MySQL 8.0
- **ORM**: Entity Framework Core 9.0 + Pomelo.EntityFrameworkCore.MySql
- **API文档**: Swagger/OpenAPI

## 项目结构

```
PlayLinker.API/
├── Controllers/          # API控制器
│   └── LocalGamesController.cs
├── Data/                # 数据库上下文
│   └── PlayLinkerDbContext.cs
├── Models/              # 数据模型
│   ├── Entities/        # 实体类
│   └── DTOs/            # 数据传输对象
├── appsettings.json     # 配置文件
└── Program.cs           # 程序入口
```

## 快速开始

### 1. 确认 .NET SDK

确保已安装 .NET 9.0 SDK：

```bash
dotnet --version
```

### 2. 配置数据库连接

编辑 `appsettings.json`，确认数据库连接字符串：

```json
{
  "ConnectionStrings": {
    "PlayLinkerDb": "Server=114.55.115.211;Port=3306;Database=playlinker_db;User=root;Password=123456;CharSet=utf8mb4;SslMode=None;"
  }
}
```

### 3. 还原NuGet包

```bash
cd Backend/PlayLinker.API
dotnet restore
```

### 4. 运行项目

```bash
dotnet run
```

项目将在以下地址启动：
- HTTP: `http://localhost:5000`
- Swagger UI: `http://localhost:5000`

## API 文档

启动项目后，访问 `http://localhost:5000` 即可查看完整的 Swagger API 文档。

### 已实现的接口（第1周 Day 1-2）

#### 本地游戏管理 API

- **POST** `/api/v1/local/scan` - 扫描本地游戏
  - 请求体：目录列表、是否深度扫描
  - 返回：扫描结果

- **GET** `/api/v1/local/games` - 获取本地游戏列表
  - 查询参数：`page`, `pageSize`, `sortBy`
  - 支持分页、排序

- **GET** `/api/v1/local/games/{id}` - 获取本地游戏详情
  - 返回：完整的游戏信息，包括存档和Mod列表

## 开发进度

### ✅ 第1周 Day 1-2: 项目初始化与本地文件

- [x] 项目结构搭建
- [x] Entity Framework配置
- [x] 扫描本地游戏 (POST /local/scan)
- [x] 本地游戏列表 (GET /local/games)
- [x] 本地游戏详情 (GET /local/games/{id})
- [x] Swagger文档配置

### 🔄 第1周 Day 3-4: 存档管理（待开发）

- [ ] 本地存档列表 (GET /saves/local)
- [ ] 备份存档 (POST /saves/backup)
- [ ] 恢复存档 (POST /saves/restore/{id})
- [ ] 云存档列表 (GET /cloud/saves)
- [ ] 上传到云端 (POST /cloud/upload)
- [ ] 从云端下载 (POST /cloud/download/{id})

### 🔄 第1周 Day 5: Mod管理（待开发）

- [ ] 游戏Mod列表 (GET /games/{gameId}/mods)
- [ ] 安装Mod (POST /mods/install)
- [ ] 启用/禁用Mod (PATCH /mods/{id}/toggle)
- [ ] 卸载Mod (DELETE /mods/{id})
- [ ] 检测Mod冲突 (GET /mods/conflicts)

## 数据库说明

项目使用单一数据库 `playlinker_db`，所有表都在同一个 schema 中。

### 主要数据表

- `local_game_install` - 本地游戏安装信息
- `local_save_file` - 本地存档文件
- `local_mod` - 本地Mod
- `cloud_save_backup` - 云端备份存档

## 测试

### 使用 Swagger UI 测试

1. 启动项目
2. 打开浏览器访问 `http://localhost:5000`
3. 在 Swagger UI 中测试各个接口

### 示例请求

#### 扫描本地游戏
```bash
curl -X POST "http://localhost:5000/api/v1/local/scan" \
  -H "Content-Type: application/json" \
  -d '{
    "directories": ["D:\\Games", "E:\\SteamLibrary"],
    "deepScan": true
  }'
```

#### 获取本地游戏列表
```bash
curl -X GET "http://localhost:5000/api/v1/local/games?page=1&pageSize=10"
```

#### 获取游戏详情
```bash
curl -X GET "http://localhost:5000/api/v1/local/games/1"
```

## 注意事项

1. **数据库连接**: 确保数据库服务器可访问
2. **数据准备**: 确保数据库中已有测试数据
3. **端口占用**: 如果5000端口被占用，可在配置中修改

## 下一步计划

- 实现存档管理接口
- 实现Mod管理接口
- 实现报表系统
- 添加JWT认证
- 编写单元测试

## 联系方式

如有问题，请联系开发者C。
