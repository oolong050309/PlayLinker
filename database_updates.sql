-- ============================================
-- PlayLinker 数据库更新记录
-- 日期: 2025-12-31
-- ============================================

-- 1. 添加游戏大小字段到 local_game_install 表
-- 用于存储本地游戏的安装大小（字节）
ALTER TABLE local_game_install ADD COLUMN size_bytes BIGINT NOT NULL DEFAULT 0 COMMENT '游戏大小（字节）';

-- 注意事项:
-- 1. cloud_save_backup.file_size 现在存储的是字节（之前注释说是MB，但实际代码存的是字节）
-- 2. local_save_file.file_size 存储的是字节
-- 3. 前端显示时会自动转换为合适的单位（B/KB/MB/GB）
