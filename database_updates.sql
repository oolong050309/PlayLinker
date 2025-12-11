-- PlayLinker 数据库更新脚本
-- 用于支持网页版功能
-- 执行日期: 2024-11-27

-- =============================================================================
-- 1. 更新 local_game_install 表，添加游戏详情字段
-- =============================================================================
ALTER TABLE local_game_install
ADD COLUMN size_gb DECIMAL(10, 2) DEFAULT 0.00 COMMENT '安装大小(GB)',
ADD COLUMN last_played DATETIME DEFAULT NULL COMMENT '最后游玩时间',
ADD COLUMN executable_path VARCHAR(750) DEFAULT NULL COMMENT '游戏主程序路径',
ADD COLUMN config_path VARCHAR(750) DEFAULT NULL COMMENT '配置文件目录路径';

-- =============================================================================
-- 说明：
-- 1. size_gb: 游戏安装大小，单位GB，保留两位小数
-- 2. last_played: 最后游玩时间，可为NULL
-- 3. executable_path: 游戏主程序完整路径，例如 D:\Games\CS2\cs2.exe
-- 4. config_path: 配置文件目录路径，例如 D:\Games\CS2\cfg
-- 
-- 注意：
-- - 这些字段在网页版中需要用户手动选择文件后由前端上传
-- - 本地客户端版本可以自动扫描并填充这些字段
-- =============================================================================
