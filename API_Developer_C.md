# PlayLinker API 文档 - 开发者C

## 项目信息

**项目名称**: PlayLinker - 统一游戏管理平台  
**技术栈**: C# ASP.NET Core + MySQL + Vue 3  
**认证方式**: JWT Bearer Token  
**开发周期**: 2周  
**负责人**: 开发者C  
**负责模块**: 本地文件管理、存档管理、Mod管理、报表系统、数据分析  
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
- 用户相关: `INT` (user_id)
- 游戏相关: `BIGINT` (game_id, install_id)
- 存档/Mod: `BIGINT` (save_id, mod_id)
- 报表: `VARCHAR` (report_id)

### 时间格式
- 统一使用 **ISO 8601** 格式（UTC时间）
- 示例: `2024-11-27T10:00:00Z`

### 文件大小单位
- 小文件: KB
- 中文件: MB
- 大文件: GB

### 网页版功能限制
由于浏览器安全限制，网页版存在以下功能限制：

**✅ 支持的功能**:
- 查询和展示本地游戏/存档列表
- 数据库记录的增删改查
- 云存档上传下载（元数据）
- 统计和报表功能

**❌ 不支持的功能**:
- 自动扫描本地文件系统
- 删除本地游戏文件
- 解析存档文件内容（metadata）
- 自动备份/恢复本地文件
- Mod 文件的安装/卸载

**💡 解决方案**:
- 用户需手动选择文件/目录（通过浏览器文件选择器）
- 文件操作功能需要本地客户端版本（Electron/Tauri）
- 元数据字段在网页版中返回 `null`

---

## 开发里程碑（2周计划）

### 第1周：基础功能开发

#### Day 1-2: 项目初始化与本地文件
- [x] 项目结构搭建
- [ ] 文件操作基础类库
- [ ] 扫描本地游戏 (POST /local/scan)
- [ ] 本地游戏列表 (GET /local/games)
- [ ] 本地游戏详情 (GET /local/games/{id})
- [ ] 单元测试编写

#### Day 3-4: 存档管理
- [ ] 本地存档列表 (GET /saves/local)
- [ ] 备份存档 (POST /saves/backup)
- [ ] 恢复存档 (POST /saves/restore/{id})
- [ ] 云存档列表 (GET /cloud/saves)
- [ ] 上传到云端 (POST /cloud/upload)
- [ ] 从云端下载 (POST /cloud/download/{id})

#### Day 5: Mod管理
- [ ] 游戏Mod列表 (GET /games/{gameId}/mods)
- [ ] 安装Mod (POST /mods/install)
- [ ] 启用/禁用Mod (PATCH /mods/{id}/toggle)
- [ ] 卸载Mod (DELETE /mods/{id})
- [ ] 检测Mod冲突 (GET /mods/conflicts)

### 第2周：报表与测试

#### Day 6-7: 报表系统
- [ ] 报表模板列表 (GET /reports/templates)
- [ ] 生成报表 (POST /reports/generate)
- [ ] 报表历史 (GET /reports)
- [ ] 下载报表 (GET /reports/{id}/download)
- [ ] 数据分析接口 (analytics/*)

#### Day 8-9: 联调与优化
- [ ] 与其他开发者API联调
- [ ] 文件上传优化（分块、断点续传）
- [ ] 云存储集成（OSS）
- [ ] 报表生成性能优化

#### Day 10: 测试与文档
- [ ] 集成测试
- [ ] Swagger文档完善
- [ ] Postman测试集合
- [ ] 代码审查

---

## 数据表职责

### 拥有的数据表
- `user_library.local_game_install` - 本地安装信息
- `user_library.local_save_file` - 本地存档
- `user_library.local_mod` - 本地Mod
- `user_library.cloud_save_backup` - 云端备份存档
- `business_features.report_template` - 报表模板
- `business_features.report_generation_record` - 报表生成记录

### 依赖的其他表
- `user_management.user` - 用户信息（只读）
- `game_data.games` - 游戏信息（只读）
- `game_data.platforms` - 平台信息（只读）

---

## 1. 本地游戏管理 API

### 1.1 POST `/api/v1/local/scan` - 扫描本地游戏
**认证**: 必需

**请求体**:
```json
{
  "directories": [
    "D:\\Games",
    "E:\\SteamLibrary",
    "C:\\Program Files\\Epic Games"
  ],
  "deepScan": true
}
```

**字段说明**:
- `directories`: 要扫描的目录列表
- `deepScan`: 是否深度扫描（扫描子目录）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "扫描完成",
  "data": {
    "scanId": "scan_20241127_100000",
    "gamesFound": [
      {
        "installId": 1,
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "installPath": "D:\\Games\\CS2",
        "version": "1.0.2.3",
        "sizeGB": 35.5,
        "detectedTime": "2024-11-27T10:00:00Z",
        "lastPlayed": "2024-11-26T20:00:00Z"
      },
      {
        "installId": 2,
        "gameId": 10002,
        "gameName": "Cyberpunk 2077",
        "installPath": "E:\\SteamLibrary\\Cyberpunk2077",
        "version": "2.1.0",
        "sizeGB": 102.3,
        "detectedTime": "2024-11-27T10:00:00Z",
        "lastPlayed": "2024-11-25T18:00:00Z"
      }
    ],
    "totalFound": 15,
    "scanDuration": 5.2,
    "scannedDirectories": 3
  }
}
```

---

### 1.2 GET `/api/v1/local/games` - 本地游戏列表
**认证**: 必需  
**查询参数**: `page`, `page_size`, `sort_by`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "installId": 1,
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "platformId": 1,
        "platformName": "Steam",
        "installPath": "D:\\Games\\CS2",
        "version": "1.0.2.3",
        "sizeGB": 35.5,
        "detectedTime": "2024-11-27T10:00:00Z",
        "lastPlayed": "2024-11-26T20:00:00Z",
        "savesCount": 0,
        "modsCount": 5
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 15},
    "summary": {
      "totalGames": 15,
      "totalSizeGB": 450.5,
      "totalSaves": 45,
      "totalMods": 23
    }
  }
}
```

---

### 1.3 GET `/api/v1/local/games/{id}` - 本地游戏详情
**认证**: 必需  
**路径参数**: id = installId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "installId": 1,
    "gameId": 10001,
    "gameName": "Counter-Strike 2",
    "platformId": 1,
    "platformName": "Steam",
    "installPath": "D:\\Games\\CS2",
    "version": "1.0.2.3",
    "sizeGB": 35.5,
    "detectedTime": "2024-11-27T10:00:00Z",
    "lastPlayed": "2024-11-26T20:00:00Z",
    "executablePath": "D:\\Games\\CS2\\cs2.exe",
    "configPath": "D:\\Games\\CS2\\cfg",
    "saves": [
      {
        "saveId": 1,
        "filePath": "C:\\Users\\Player\\Saved Games\\CS2\\save001.dat",
        "fileSize": 5242880,
        "updatedAt": "2024-11-26T20:00:00Z"
      }
    ],
    "mods": [
      {
        "modId": 1,
        "modName": "HD Texture Pack",
        "version": 2,
        "enabled": true
      }
    ]
  }
}
```

---

### 1.4 DELETE `/api/v1/local/games/{id}` - 移除本地游戏
**认证**: 必需  
**路径参数**: id = installId

**网页版限制**:
- ⚠️ 网页版仅支持从数据库移除记录，不支持删除本地文件
- `deleteFiles` 参数在网页版中将被忽略，固定为 `false`
- 删除本地文件功能需要本地客户端版本

**请求体**:
```json
{
  "deleteFiles": false
}
```

**字段说明**:
- `deleteFiles`: 是否删除游戏文件（网页版固定为 false）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "游戏已移除",
  "data": {
    "installId": 1,
    "gameName": "Counter-Strike 2",
    "deletedFiles": false,
    "removedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 1.5 PATCH `/api/v1/local/games/{id}/path` - 更新安装路径
**认证**: 必需  
**路径参数**: id = installId

**请求体**:
```json
{
  "newPath": "E:\\NewLocation\\CS2"
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "路径更新成功",
  "data": {
    "installId": 1,
    "oldPath": "D:\\Games\\CS2",
    "newPath": "E:\\NewLocation\\CS2",
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 2. 存档管理 API

### 2.1 GET `/api/v1/saves/local` - 本地存档列表
**认证**: 必需  
**查询参数**: `game_id`, `page`, `page_size`

**网页版限制**: 
- ⚠️ `metadata` 字段在网页版中固定返回 `null`
- 存档元数据解析需要本地客户端版本

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "saveId": 1,
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "installId": 2,
        "filePath": "C:\\Users\\Player\\Saved Games\\CP2077\\save001.dat",
        "fileSize": 5242880,
        "fileSizeMB": 5.0,
        "updatedAt": "2024-11-26T18:00:00Z",
        "isBackupLocal": true,
        "metadata": null
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 45},
    "summary": {
      "totalSaves": 45,
      "totalSizeMB": 250.5,
      "backedUpCount": 30
    }
  }
}
```

---

### 2.2 POST `/api/v1/saves/backup` - 备份存档
**认证**: 必需

**网页版限制**:
- ❌ **网页版无法实现此功能**
- 原因：浏览器无法读取/写入用户本地文件系统
- 需要本地客户端版本才能真正备份存档文件
- 当前实现仅返回模拟数据，不执行实际文件操作

**请求体**:
```json
{
  "saveId": 1,
  "backupName": "关键任务前备份",
  "compress": true
}
```

**字段说明**:
- `saveId`: 存档ID
- `backupName`: 备份名称（可选）
- `compress`: 是否压缩（网页版无效）

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "存档备份成功",
  "data": {
    "backupId": "backup_20241127_100000",
    "saveId": 1,
    "backupName": "关键任务前备份",
    "backupPath": "C:\\Users\\Player\\PlayLinker\\Backups\\save001_20241127.bak",
    "originalSize": 5242880,
    "backupSize": 2621440,
    "compressed": true,
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 2.3 POST `/api/v1/saves/restore/{id}` - 恢复存档
**认证**: 必需  
**路径参数**: id = backupId

**网页版限制**:
- ❌ **网页版无法实现此功能**
- 原因：浏览器无法读取备份文件并写入到存档位置
- 需要本地客户端版本才能真正恢复存档文件
- 当前实现仅返回模拟数据，不执行实际文件操作

**请求体**:
```json
{
  "overwrite": true
}
```

**字段说明**:
- `overwrite`: 是否覆盖当前存档（网页版无效）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "存档恢复成功",
  "data": {
    "backupId": "backup_20241127_100000",
    "saveId": 1,
    "restoredPath": "C:\\Users\\Player\\Saved Games\\CP2077\\save001.dat",
    "restoredAt": "2024-11-27T10:05:00Z"
  }
}
```

---

### 2.4 DELETE `/api/v1/saves/{id}` - 删除存档
**认证**: 必需  
**路径参数**: id = saveId

**网页版限制**:
- ⚠️ 网页版仅支持从数据库删除记录，不支持删除物理文件
- `deleteFile` 和 `deleteBackups` 参数在网页版中将被忽略
- 删除本地文件功能需要本地客户端版本

**请求体**:
```json
{
  "deleteFile": false,
  "deleteBackups": false
}
```

**字段说明**:
- `deleteFile`: 是否删除物理文件（网页版固定为 false）
- `deleteBackups`: 是否同时删除备份（网页版固定为 false）

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "存档已删除",
  "data": {
    "saveId": 1,
    "deletedFile": false,
    "deletedBackups": false,
    "deletedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 3. 云存档 API

### 3.1 GET `/api/v1/cloud/saves` - 云存档列表
**认证**: 必需  
**查询参数**: `game_id`, `page`, `page_size`

**网页版限制**:
- ⚠️ `metadata` 字段在网页版中固定返回 `null`
- 存档元数据解析需要本地客户端版本
- ✅ `summary` 汇总信息已实现

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "cloudBackupId": "cloud_20241127_100000",
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "userId": 1001,
        "uploadTime": "2024-11-27T10:00:00Z",
        "fileSize": 5242880,
        "fileSizeMB": 5.0,
        "storageUrl": "https://114.55.115.211/storage/saves/user_1001/game_10004/cloud_xxx.dat",
        "metadata": null,
        "expiresAt": "2025-11-27T10:00:00Z"
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 10},
    "summary": {
      "totalCloudSaves": 10,
      "totalSizeMB": 50.5,
      "storageUsedMB": 50.5,
      "storageLimitMB": 1024
    }
  }
}
```

---

### 3.2 POST `/api/v1/cloud/upload` - 上传到云端
**认证**: 必需

**网页版实现**:
- ✅ **已实现**：用户手动选择存档文件上传到云服务器
- 文件存储位置：`D:\PlayLinker\Storage\Saves\user_{userId}\game_{gameId}\`
- 支持压缩（GZip）
- 最大文件大小：100MB

**请求体** (multipart/form-data):
- `file`: 存档文件（必需）
- `saveId`: 本地存档ID（必需）
- `compress`: 是否压缩（可选，默认false）
- `encrypt`: 是否加密（可选，默认false，暂未实现）
- `description`: 描述（可选）

**字段说明**:
- `file`: 用户选择的存档文件
- `saveId`: 本地存档ID
- `compress`: 是否压缩（true时使用GZip压缩）
- `encrypt`: 是否加密（暂未实现）
- `description`: 描述（可选）

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "存档上传成功",
  "data": {
    "cloudBackupId": "cloud_20241127_100000",
    "saveId": 1,
    "storageUrl": "https://storage.playlinker.com/saves/...",
    "originalSize": 5242880,
    "uploadedSize": 2621440,
    "compressed": true,
    "encrypted": true,
    "uploadTime": "2024-11-27T10:00:00Z",
    "expiresAt": "2025-11-27T10:00:00Z"
  }
}
```

---

### 3.3 GET `/api/v1/cloud/download/{id}` - 从云端下载
**认证**: 必需  
**路径参数**: id = cloudBackupId

**网页版实现**:
- ✅ **已实现**：直接返回文件流供用户下载
- 浏览器会自动弹出下载对话框
- 无需指定目标路径（由用户选择）

**无需请求体**

**成功响应** (200):
```
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="cloud_20241127_100000.dat"
Content-Length: 5242880

[文件的二进制内容]
```

**说明**:
- 此接口返回文件流，不是JSON格式
- 浏览器会自动识别为文件下载并弹出"另存为"对话框
- 用户选择保存位置后，文件下载到本地
- `Content-Disposition` 头指定了默认文件名

**前端调用示例**:
```javascript
const response = await axios.get(`/api/v1/cloud/download/${cloudBackupId}`, {
  responseType: 'blob'
})
const url = URL.createObjectURL(new Blob([response.data]))
const link = document.createElement('a')
link.href = url
link.download = `${cloudBackupId}.dat`
link.click()
URL.revokeObjectURL(url)
```

---

### 3.4 DELETE `/api/v1/cloud/saves/{id}` - 删除云存档
**认证**: 必需  
**路径参数**: id = cloudBackupId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "云存档已删除",
  "data": {
    "cloudBackupId": "cloud_20241127_100000",
    "freedSpaceMB": 5.0,
    "deletedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 3.5 GET `/api/v1/cloud/storage/usage` - 存储空间使用情况
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "userId": 1001,
    "storageUsedMB": 50.5,
    "storageLimitMB": 1024,
    "storageUsedPercent": 4.93,
    "totalFiles": 10,
    "largestFile": {
      "cloudBackupId": "cloud_20241120_100000",
      "gameName": "Elden Ring",
      "fileSizeMB": 15.2
    },
    "oldestFile": {
      "cloudBackupId": "cloud_20240101_100000",
      "gameName": "Dark Souls III",
      "uploadTime": "2024-01-01T10:00:00Z"
    },
    "recentUploads": [
      {
        "cloudBackupId": "cloud_20241127_100000",
        "gameName": "Cyberpunk 2077",
        "uploadTime": "2024-11-27T10:00:00Z",
        "fileSizeMB": 5.0
      }
    ]
  }
}
```

---

## 4. Mod 管理 API

### 4.1 GET `/api/v1/games/{gameId}/mods` - 游戏Mod列表
**认证**: 必需  
**路径参数**: gameId = 游戏ID  
**查询参数**: `install_id`, `enabled`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "gameId": 10004,
    "gameName": "Cyberpunk 2077",
    "mods": [
      {
        "modId": 1,
        "modName": "HD Texture Pack",
        "version": 2,
        "filePath": "D:\\Games\\CP2077\\mods\\hd_textures",
        "enabled": true,
        "lastModified": "2024-11-20T10:00:00Z",
        "sizeGB": 5.2,
        "installId": 2,
        "description": "高清材质包",
        "author": "ModAuthor123",
        "conflicts": []
      },
      {
        "modId": 2,
        "modName": "Better AI",
        "version": 1,
        "filePath": "D:\\Games\\CP2077\\mods\\better_ai",
        "enabled": false,
        "lastModified": "2024-11-15T10:00:00Z",
        "sizeGB": 0.5,
        "installId": 2,
        "description": "改进AI行为",
        "author": "AIModder",
        "conflicts": [3]
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 5},
    "summary": {
      "totalMods": 5,
      "enabledMods": 3,
      "totalSizeGB": 8.5,
      "conflictsCount": 1
    }
  }
}
```

---

### 4.2 POST `/api/v1/mods/install` - 安装Mod
**认证**: 必需

**请求体**:
```json
{
  "installId": 2,
  "modName": "New Weapons Pack",
  "version": 1,
  "filePath": "D:\\Downloads\\new_weapons.zip",
  "autoExtract": true,
  "enabled": true
}
```

**字段说明**:
- `installId`: 游戏安装ID
- `modName`: Mod名称
- `version`: Mod版本
- `filePath`: Mod文件路径
- `autoExtract`: 是否自动解压
- `enabled`: 安装后是否启用

**成功响应** (201):
```json
{
  "success": true,
  "code": "OK",
  "message": "Mod安装成功",
  "data": {
    "modId": 6,
    "modName": "New Weapons Pack",
    "version": 1,
    "installPath": "D:\\Games\\CP2077\\mods\\new_weapons",
    "enabled": true,
    "installedAt": "2024-11-27T10:00:00Z",
    "sizeGB": 1.2
  }
}
```

**错误响应**:
```json
// 409 Conflict - Mod冲突
{
  "success": false,
  "code": "ERR_MOD_CONFLICT",
  "message": "该Mod与已安装的Mod存在冲突",
  "data": {
    "conflictingMods": [
      {"modId": 3, "modName": "Old Weapons Pack"}
    ]
  }
}
```

---

### 4.3 PATCH `/api/v1/mods/{id}/toggle` - 启用/禁用Mod
**认证**: 必需  
**路径参数**: id = modId

**请求体**:
```json
{
  "enabled": true
}
```

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Mod已启用",
  "data": {
    "modId": 2,
    "modName": "Better AI",
    "enabled": true,
    "updatedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 4.4 DELETE `/api/v1/mods/{id}` - 卸载Mod
**认证**: 必需  
**路径参数**: id = modId

**请求体**:
```json
{
  "deleteFiles": true
}
```

**字段说明**:
- `deleteFiles`: 是否删除Mod文件

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "Mod已卸载",
  "data": {
    "modId": 2,
    "modName": "Better AI",
    "deletedFiles": true,
    "freedSpaceGB": 0.5,
    "uninstalledAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 4.5 GET `/api/v1/mods/conflicts` - 检测Mod冲突
**认证**: 必需  
**查询参数**: `install_id`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "installId": 2,
    "gameName": "Cyberpunk 2077",
    "conflicts": [
      {
        "conflictId": 1,
        "severity": "high",
        "mods": [
          {"modId": 2, "modName": "Better AI"},
          {"modId": 3, "modName": "Advanced AI"}
        ],
        "reason": "两个Mod修改了相同的AI文件",
        "recommendation": "只保留其中一个Mod"
      }
    ],
    "totalConflicts": 1,
    "hasBlockingConflicts": true
  }
}
```

---

## 5. 报表系统 API

### 5.1 GET `/api/v1/reports/templates` - 报表模板列表
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "templates": [
      {
        "templateId": 1,
        "templateName": "月度游戏报告",
        "description": "包含游戏时长、成就、消费等统计",
        "category": "gaming",
        "supportedFormats": ["pdf", "excel", "html"],
        "parameters": [
          {"name": "month", "type": "string", "required": true},
          {"name": "includePlatforms", "type": "array", "required": false}
        ]
      },
      {
        "templateId": 2,
        "templateName": "年度总结报告",
        "description": "年度游戏数据全面分析",
        "category": "gaming",
        "supportedFormats": ["pdf", "html"],
        "parameters": [
          {"name": "year", "type": "int", "required": true}
        ]
      }
    ],
    "totalCount": 2
  }
}
```

---

### 5.2 POST `/api/v1/reports/generate` - 生成报表
**认证**: 必需

**请求体**:
```json
{
  "templateId": 1,
  "reportType": "monthly_gaming",
  "parameters": {
    "startDate": "2024-11-01",
    "endDate": "2024-11-30",
    "includePlatforms": [1, 2],
    "includeGenres": ["FPS", "RPG"]
  },
  "format": "pdf"
}
```

**字段说明**:
- `templateId`: 模板ID
- `reportType`: 报表类型
- `parameters`: 报表参数
- `format`: 输出格式（pdf | excel | html）

**成功响应** (202):
```json
{
  "success": true,
  "code": "OK",
  "message": "报表生成任务已创建",
  "data": {
    "reportId": "rpt_20241127_100000",
    "templateId": 1,
    "status": "generating",
    "estimatedTime": 10,
    "queuePosition": 3,
    "createdAt": "2024-11-27T10:00:00Z"
  }
}
```

---

### 5.3 GET `/api/v1/reports` - 报表历史
**认证**: 必需  
**查询参数**: `status`, `page`, `page_size`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "reportId": "rpt_20241127_100000",
        "templateId": 1,
        "templateName": "月度游戏报告",
        "status": "completed",
        "format": "pdf",
        "generatedAt": "2024-11-27T10:00:00Z",
        "fileSizeMB": 2.5,
        "downloadUrl": "/api/v1/reports/rpt_20241127_100000/download"
      }
    ],
    "meta": {"page": 1, "pageSize": 20, "total": 10}
  }
}
```

**状态说明**:
- `generating`: 生成中
- `completed`: 已完成
- `failed`: 失败
- `expired`: 已过期

---

### 5.4 GET `/api/v1/reports/{id}` - 报表详情
**认证**: 必需  
**路径参数**: id = reportId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "reportId": "rpt_20241127_100000",
    "templateId": 1,
    "templateName": "月度游戏报告",
    "userId": 1001,
    "status": "completed",
    "format": "pdf",
    "parameters": {
      "startDate": "2024-11-01",
      "endDate": "2024-11-30"
    },
    "generatedAt": "2024-11-27T10:00:00Z",
    "fileSizeMB": 2.5,
    "outputPath": "/reports/rpt_20241127_100000.pdf",
    "downloadUrl": "/api/v1/reports/rpt_20241127_100000/download",
    "expiresAt": "2024-12-27T10:00:00Z"
  }
}
```

---

### 5.5 GET `/api/v1/reports/{id}/download` - 下载报表
**认证**: 必需  
**路径参数**: id = reportId

**成功响应** (200):
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="monthly_report_202411.pdf"

[PDF文件二进制数据]
```

---

### 5.6 DELETE `/api/v1/reports/{id}` - 删除报表
**认证**: 必需  
**路径参数**: id = reportId

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "message": "报表已删除",
  "data": {
    "reportId": "rpt_20241127_100000",
    "deletedAt": "2024-11-27T10:00:00Z"
  }
}
```

---

## 6. 数据分析 API

### 6.1 GET `/api/v1/analytics/playtime` - 游玩时间分析
**认证**: 必需  
**查询参数**: `period`, `year`, `month`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "period": "2024-11",
    "totalMinutes": 12000,
    "dailyAverage": 400,
    "peakDay": "2024-11-15",
    "peakMinutes": 780,
    "distribution": [
      {"date": "2024-11-01", "minutes": 360},
      {"date": "2024-11-02", "minutes": 420}
    ],
    "gameBreakdown": [
      {
        "gameId": 10001,
        "name": "Counter-Strike 2",
        "minutes": 5000,
        "percentage": 41.7,
        "sessions": 45
      },
      {
        "gameId": 10002,
        "name": "Dota 2",
        "minutes": 3000,
        "percentage": 25.0,
        "sessions": 30
      }
    ],
    "timeSlotDistribution": [
      {"slot": "00:00-06:00", "minutes": 500},
      {"slot": "06:00-12:00", "minutes": 1000},
      {"slot": "12:00-18:00", "minutes": 3000},
      {"slot": "18:00-24:00", "minutes": 7500}
    ],
    "weekdayDistribution": [
      {"day": "Monday", "minutes": 1500},
      {"day": "Tuesday", "minutes": 1600},
      {"day": "Wednesday", "minutes": 1700},
      {"day": "Thursday", "minutes": 1800},
      {"day": "Friday", "minutes": 2000},
      {"day": "Saturday", "minutes": 2200},
      {"day": "Sunday", "minutes": 2200}
    ]
  }
}
```

---

### 6.2 GET `/api/v1/analytics/genres` - 题材偏好分析
**认证**: 必需

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "genrePreferences": [
      {
        "genreId": 1,
        "genreName": "FPS",
        "gamesOwned": 45,
        "gamesPlayed": 30,
        "totalPlaytimeMinutes": 50000,
        "averagePlaytime": 1666,
        "preferenceScore": 0.85
      },
      {
        "genreId": 2,
        "genreName": "RPG",
        "gamesOwned": 30,
        "gamesPlayed": 25,
        "totalPlaytimeMinutes": 80000,
        "averagePlaytime": 3200,
        "preferenceScore": 0.92
      }
    ],
    "topGenre": {
      "genreId": 2,
      "genreName": "RPG",
      "reason": "最高平均游玩时长"
    },
    "emergingInterest": [
      {
        "genreId": 5,
        "genreName": "Strategy",
        "recentGrowth": 0.35
      }
    ]
  }
}
```

---

### 6.3 GET `/api/v1/analytics/achievements` - 成就统计分析
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
    "averageCompletionRate": 0.45,
    "recentTrend": {
      "last7Days": 12,
      "last30Days": 45,
      "trend": "increasing"
    },
    "difficultyDistribution": [
      {"difficulty": "common", "count": 1500, "unlocked": 1400},
      {"difficulty": "rare", "count": 1200, "unlocked": 600},
      {"difficulty": "epic", "count": 600, "unlocked": 90},
      {"difficulty": "legendary", "count": 200, "unlocked": 10}
    ],
    "topAchievementGames": [
      {
        "gameId": 10001,
        "gameName": "Counter-Strike 2",
        "totalAchievements": 100,
        "unlocked": 95,
        "completionRate": 0.95
      }
    ]
  }
}
```

---

### 6.4 GET `/api/v1/analytics/spending` - 消费分析
**认证**: 必需  
**查询参数**: `period`, `year`

**成功响应** (200):
```json
{
  "success": true,
  "code": "OK",
  "data": {
    "period": "2024",
    "totalSpending": 3500.00,
    "currency": "CNY",
    "gamesCount": 45,
    "averageGamePrice": 77.78,
    "monthlyBreakdown": [
      {"month": "2024-01", "spending": 298.00, "gamesCount": 3},
      {"month": "2024-02", "spending": 450.00, "gamesCount": 5}
    ],
    "platformBreakdown": [
      {"platform": "Steam", "spending": 2500.00, "gamesCount": 30},
      {"platform": "Epic Games", "spending": 1000.00, "gamesCount": 15}
    ],
    "genreBreakdown": [
      {"genre": "FPS", "spending": 1200.00, "gamesCount": 15},
      {"genre": "RPG", "spending": 1500.00, "gamesCount": 10}
    ],
    "discountSavings": {
      "totalSaved": 1200.00,
      "averageDiscount": 0.35,
      "bestDeal": {
        "gameId": 10004,
        "gameName": "Cyberpunk 2077",
        "originalPrice": 298.00,
        "paidPrice": 89.40,
        "discount": 0.70
      }
    }
  }
}
```

---

## 附录：文件管理最佳实践

### 存档备份策略
- **自动备份**: 游戏退出时自动备份
- **定期备份**: 每周自动备份一次
- **关键节点**: 重要任务前手动备份
- **保留策略**: 本地保留最近10个，云端保留最近30个

### Mod管理建议
- **安装前备份**: 安装Mod前自动备份原文件
- **冲突检测**: 安装时自动检测冲突
- **加载顺序**: 支持自定义Mod加载顺序
- **一键恢复**: 出问题时一键恢复原版

### 云存储优化
- **增量上传**: 只上传变化的部分
- **压缩传输**: 自动压缩减少流量
- **断点续传**: 支持大文件断点续传
- **版本控制**: 保留多个历史版本
