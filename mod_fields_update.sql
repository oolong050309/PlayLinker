-- 为 local_mod 表添加网页版手动安装支持的新字段
-- 执行时间：2024-12-11

-- 添加安装状态字段
ALTER TABLE `local_mod` 
ADD COLUMN `install_status` VARCHAR(50) NOT NULL DEFAULT 'pending_manual_install' COMMENT '安装状态：pending_manual_install, installed, failed';

-- 添加目标安装路径字段
ALTER TABLE `local_mod` 
ADD COLUMN `target_path` VARCHAR(2048) NULL COMMENT '目标安装路径';

-- 添加Mod描述字段
ALTER TABLE `local_mod` 
ADD COLUMN `description` VARCHAR(1000) NULL COMMENT 'Mod描述';

-- 添加Mod作者字段
ALTER TABLE `local_mod` 
ADD COLUMN `author` VARCHAR(128) NULL COMMENT 'Mod作者';

-- 添加下载URL字段
ALTER TABLE `local_mod` 
ADD COLUMN `download_url` VARCHAR(2048) NULL COMMENT '下载URL';

-- 为现有记录设置默认值
UPDATE `local_mod` 
SET `install_status` = 'installed' 
WHERE `enabled` = 1;

-- 添加索引以提高查询性能
CREATE INDEX `idx_install_status` ON `local_mod` (`install_status`);
CREATE INDEX `idx_install_id_status` ON `local_mod` (`install_id`, `install_status`);

-- 验证更新
SELECT COUNT(*) as total_mods, 
       SUM(CASE WHEN install_status = 'pending_manual_install' THEN 1 ELSE 0 END) as pending_install,
       SUM(CASE WHEN install_status = 'installed' THEN 1 ELSE 0 END) as installed
FROM `local_mod`;
