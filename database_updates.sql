-- ============================================
-- PlayLinker 数据库更新记录
-- 日期: 2025-12-31
-- ============================================

-- 1. 添加游戏大小字段到 local_game_install 表
-- 用于存储本地游戏的安装大小（字节）
ALTER TABLE local_game_install ADD COLUMN size_bytes BIGINT NOT NULL DEFAULT 0 COMMENT '游戏大小（字节）';

-- 2. 添加游戏 Mod 平台映射表
-- 用于存储游戏在各 Mod 平台的 ID 映射
CREATE TABLE IF NOT EXISTS game_mod_source (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL COMMENT '本地游戏ID',
    source VARCHAR(50) NOT NULL COMMENT 'Mod来源: NexusMods, 3DM, GameBanana, Steam',
    external_game_id VARCHAR(100) NOT NULL COMMENT '第三方平台的游戏ID',
    external_domain VARCHAR(100) COMMENT '如 NexusMods 的 domain_name',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    UNIQUE KEY uk_game_source (game_id, source),
    INDEX idx_source (source)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏Mod平台映射表';

-- 注意事项:
-- 1. cloud_save_backup.file_size 现在存储的是字节（之前注释说是MB，但实际代码存的是字节）
-- 2. local_save_file.file_size 存储的是字节
-- 3. 前端显示时会自动转换为合适的单位（B/KB/MB/GB）
-- 4. game_mod_source 表用于 Mod 浏览功能，映射游戏到各 Mod 平台
