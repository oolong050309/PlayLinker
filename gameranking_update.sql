-- ---------------------------------------------------------
-- 脚本目标：配合排行榜功能升级，修改数据库结构
-- 涉及表：game_ranking
-- ---------------------------------------------------------

-- 1. 检查是否存在 updated_at 列，如果不存在则添加
-- 注意：MySQL 5.7+ / 8.0 支持 IF NOT EXISTS 语法，或者直接运行 ALTER 语句
-- 如果您的 MySQL 版本较老，直接运行下面的 ALTER 语句即可，报错说明已存在

ALTER TABLE `game_ranking` 
ADD COLUMN `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '数据最后更新时间' AFTER `current_rank`;

-- 2. 为 updated_at 添加索引
-- 服务启动时会执行 OrderByDescending(r => r.UpdatedAt)，加索引可提高检查性能
CREATE INDEX `idx_game_ranking_updated_at` ON `game_ranking` (`updated_at`);

-- 3. (可选) 验证修改结果
DESCRIBE `game_ranking`;