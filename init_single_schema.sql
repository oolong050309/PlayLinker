/*
 * PlayLinker 数据库初始化脚本 - 单Schema版本
 * 版本: 2.0.0
 * 说明: 所有表统一在 playlinker_db 数据库中
 */

-- =============================================================================
-- 1. 基础环境设置
-- =============================================================================
CREATE DATABASE IF NOT EXISTS playlinker_db DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE playlinker_db;

-- =============================================================================
-- 模块一：账号绑定与数据接入模块
-- =============================================================================

-- 表1.2: Role（角色表 - 登录权限控制）
CREATE TABLE role (
    role_id INT PRIMARY KEY AUTO_INCREMENT,
    role_name ENUM('user','parent','admin') UNIQUE NOT NULL,
    role_desc VARCHAR(300),
    INDEX idx_role_name (role_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='登录权限控制表';

-- 表1.1: User（用户表 - 核心登录与基础信息表）
CREATE TABLE user (
    user_id INT PRIMARY KEY AUTO_INCREMENT,
    username VARCHAR(128) UNIQUE NOT NULL,
    hashed_password VARCHAR(128) NOT NULL COMMENT 'AES-256加密',
    email VARCHAR(100) UNIQUE,
    gender INT DEFAULT 0 COMMENT '1男/2女/0未知',
    phone VARCHAR(100) UNIQUE,
    avatar_url VARCHAR(2048),
    role_id INT NOT NULL DEFAULT 1,
    status ENUM('active', 'disabled', 'inactive') DEFAULT 'inactive',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_login_time DATETIME,
    login_ip VARCHAR(45),
    FOREIGN KEY (role_id) REFERENCES role(role_id),
    INDEX idx_username (username),
    INDEX idx_email (email),
    INDEX idx_status (status)
) ENGINE=InnoDB AUTO_INCREMENT=1000 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='核心登录与基础信息表';

-- =============================================================================
-- 模块二：统一游戏库模块 & 模块三：游戏详情与扩展面板模块
-- =============================================================================

-- 表2.5: Platforms（游戏平台表）
CREATE TABLE platforms (
    platform_id INT PRIMARY KEY AUTO_INCREMENT,
    platform_name VARCHAR(128) UNIQUE NOT NULL,
    description TEXT,
    logo_url VARCHAR(2048),
    status TINYINT(1) NOT NULL DEFAULT 1 COMMENT '0不启用,1启用',
    INDEX idx_platform_name (platform_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏平台表';

-- 表3.1: Games（游戏主表）
CREATE TABLE games (
    game_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(128) UNIQUE NOT NULL,
    is_free TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1代表免费，0代表不免费',
    require_age TINYINT,
    short_description TEXT,
    detailed_description TEXT,
    header_image VARCHAR(2048) NOT NULL,
    capsile_image VARCHAR(2048) NOT NULL,
    background VARCHAR(2048) NOT NULL,
    pc_recommended TEXT,
    pc_minimum TEXT,
    mac_recommended TEXT,
    mac_minimum TEXT,
    linux_recommended TEXT,
    linux_minimum TEXT,
    windows TINYINT(1) NOT NULL DEFAULT 0,
    mac TINYINT(1) NOT NULL DEFAULT 0,
    linux TINYINT(1) NOT NULL DEFAULT 0,
    release_date DATE NOT NULL,
    review_score INT NOT NULL DEFAULT 0,
    review_score_desc VARCHAR(20) NOT NULL DEFAULT '',
    num_reviews INT NOT NULL DEFAULT 0,
    total_positive INT NOT NULL DEFAULT 0,
    INDEX idx_name (name),
    INDEX idx_release_date (release_date)
) ENGINE=InnoDB AUTO_INCREMENT=10000 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏主表';

-- 表3.2: GameRanking（游戏排行榜表）
CREATE TABLE game_ranking (
    rank_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT UNIQUE NOT NULL,
    pack_in_game INT COMMENT '峰值人数',
    last_week_rank INT COMMENT '上周排名',
    current_rank INT COMMENT '排名',
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    INDEX idx_current_rank (current_rank)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏排行榜表';

-- 表3.3: Genres（游戏题材/风格词表）
CREATE TABLE genres (
    genre_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(20) UNIQUE NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏题材/风格词表';

-- 表3.4: GameGenres（游戏与题材关联表）
CREATE TABLE game_genres (
    id INT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    genre_id INT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (genre_id) REFERENCES genres(genre_id),
    UNIQUE KEY uk_game_genre (game_id, genre_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与题材关联表';

-- 表3.5: Developers（游戏开发商）
CREATE TABLE developers (
    developers_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(20) UNIQUE NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏开发商';

-- 表3.6: GameDevelopers（游戏与开发商关联表）
CREATE TABLE game_developers (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    developers_id INT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (developers_id) REFERENCES developers(developers_id),
    UNIQUE KEY uk_game_developer (game_id, developers_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与开发商关联表';

-- 表3.7: Publishers（游戏发行商）
CREATE TABLE publishers (
    publishers_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(20) UNIQUE NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏发行商';

-- 表3.8: GamePublishers（游戏与发行商关联表）
CREATE TABLE game_publishers (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    publishers_id INT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (publishers_id) REFERENCES publishers(publishers_id),
    UNIQUE KEY uk_game_publisher (game_id, publishers_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与发行商关联表';

-- 表3.9: Categories（分类/标签词表）
CREATE TABLE categories (
    category_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(50) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='分类/标签词表';

-- 表3.10: GameCategories（游戏与分类关联表）
CREATE TABLE game_categories (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    category_id INT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (category_id) REFERENCES categories(category_id),
    UNIQUE KEY uk_game_category (game_id, category_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与分类关联表';

-- 表3.11: Languages（支持语言表）
CREATE TABLE languages (
    language_id INT PRIMARY KEY AUTO_INCREMENT,
    language_name VARCHAR(50) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='支持语言表';

-- 表3.12: GameLanguages（游戏语言关联表）
CREATE TABLE game_languages (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    language_id INT NOT NULL,
    notes VARCHAR(256) COMMENT '比如 是否有简体中文',
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (language_id) REFERENCES languages(language_id),
    UNIQUE KEY uk_game_language (game_id, language_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏语言关联表';

-- 表3.13: Achievements（成就表）
CREATE TABLE achievements (
    achievement_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    achievement_name VARCHAR(128) NOT NULL,
    displayName VARCHAR(128) NOT NULL,
    hidden TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0=不隐藏，1=隐藏',
    description TEXT,
    icon_unlocked VARCHAR(2048) NOT NULL COMMENT '解锁状态',
    icon_locked VARCHAR(2048) NOT NULL COMMENT '未解锁状态/灰色',
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    INDEX idx_game_id (game_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='成就表';

-- 表3.15: ExternalLinks（第三方攻略/外链源）
CREATE TABLE external_links (
    link_id INT PRIMARY KEY AUTO_INCREMENT,
    source VARCHAR(255) NOT NULL COMMENT 'bilibili,youtube等',
    link_title VARCHAR(255) NOT NULL,
    link_url VARCHAR(2048) NOT NULL,
    link_type ENUM('guide', 'video', 'review', 'wiki', 'community') NOT NULL,
    link_summary VARCHAR(255)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='第三方攻略/外链源';

-- 表3.16: GameExternalLinks（游戏与外链关联表）
CREATE TABLE game_external_links (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    link_id INT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (link_id) REFERENCES external_links(link_id),
    UNIQUE KEY uk_game_link (game_id, link_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与外链关联表';

-- 表3.17: News（新闻/公告源）
CREATE TABLE news (
    news_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    news_title VARCHAR(512) NOT NULL,
    news_url VARCHAR(2048),
    date BIGINT UNSIGNED NOT NULL COMMENT 'Unix时间戳(如1763596068)',
    author VARCHAR(128) NOT NULL,
    contents TEXT NOT NULL,
    INDEX idx_date (date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='新闻/公告源';

-- 表3.18: GameNews（游戏与新闻关联表）
CREATE TABLE game_news (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    news_id BIGINT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (news_id) REFERENCES news(news_id),
    UNIQUE KEY uk_game_news (game_id, news_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏与新闻关联表';

-- 表2.4: GamePlatform（游戏在平台的映射）
CREATE TABLE game_platform (
    game_id BIGINT NOT NULL,
    platform_id INT NOT NULL,
    platform_game_id VARCHAR(128) NOT NULL COMMENT '平台内部标识',
    game_platform_url VARCHAR(2048),
    PRIMARY KEY (game_id, platform_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id) ON DELETE CASCADE,
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏在平台的映射';

-- =============================================================================
-- 用户游戏库相关表
-- =============================================================================

-- 表2.2: PlayerPlatform（玩家在某一平台的账号资料）
CREATE TABLE player_platform (
    platform_user_id VARCHAR(128) NOT NULL COMMENT '平台侧用户标识（平台唯一）',
    platform_id INT NOT NULL COMMENT '平台ID（标识Steam/Epic等）',
    profile_name VARCHAR(128) NOT NULL,
    profile_url VARCHAR(2048),
    account_created DATETIME,
    country VARCHAR(50),
    PRIMARY KEY (platform_user_id, platform_id),
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='玩家在某一平台的账号资料';

-- 表1.3: UserPlatformBinding（账号绑定表 - 跨平台账号OAuth绑定记录）
CREATE TABLE user_platform_binding (
    binding_id INT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    platform_id INT NOT NULL,
    platform_user_id VARCHAR(128) NOT NULL COMMENT '第三方平台用户ID（如SteamID）',
    access_token VARCHAR(128) COMMENT 'AES-256加密存储',
    refresh_token VARCHAR(128) COMMENT 'AES-256加密存储',
    binding_status TINYINT(1) NOT NULL DEFAULT 1 COMMENT '1已绑定/0已解绑',
    binding_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_sync_time DATETIME,
    expire_time DATETIME NOT NULL COMMENT '按平台API规则设置',
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id),
    FOREIGN KEY (platform_user_id, platform_id) REFERENCES player_platform(platform_user_id, platform_id),
    UNIQUE KEY uk_user_platform (user_id, platform_id),
    INDEX idx_user_id (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='跨平台账号OAuth绑定记录';

-- 表2.1: UserGameLibrary（用户统一游戏库统计）
CREATE TABLE user_game_library (
    user_id INT PRIMARY KEY,
    total_games_owned INT NOT NULL DEFAULT 0,
    games_played INT NOT NULL DEFAULT 0,
    total_playtime_minutes INT NOT NULL DEFAULT 0,
    total_achievements INT,
    unlocked_achievements INT,
    recently_played_count INT NOT NULL DEFAULT 0,
    recent_playtime_minutes INT NOT NULL DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES user(user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户统一游戏库统计';

-- 表2.3: UserPlatformLibrary（用户在某平台的单款游戏记录）
CREATE TABLE user_platform_library (
    platform_user_id VARCHAR(128) NOT NULL COMMENT '平台侧用户标识（平台唯一）',
    platform_id INT NOT NULL,
    game_id BIGINT NOT NULL COMMENT '若为单款记录',
    playtime_minutes INT NOT NULL DEFAULT 0 COMMENT '累计游玩分钟数（该平台/该游戏）',
    last_played DATETIME,
    achievements_total INT COMMENT '成就总数（平台/该游戏）',
    achievements_unlocked INT COMMENT '已解锁成就数（平台/该游戏）',
    PRIMARY KEY (platform_user_id, platform_id, game_id),
    FOREIGN KEY (platform_user_id, platform_id) REFERENCES player_platform(platform_user_id, platform_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    INDEX idx_last_played (last_played)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户在某平台的单款游戏记录';

-- 表3.14: UserAchievements（用户成就解锁记录）
CREATE TABLE user_achievements (
    user_achievement_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    achievement_id BIGINT NOT NULL,
    unlocked TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0未解锁/1已解锁',
    unlock_time DATETIME COMMENT 'null表示未解锁',
    platform_id INT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id),
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id),
    UNIQUE KEY uk_user_achievement (user_id, achievement_id, platform_id),
    INDEX idx_user_id (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户成就解锁记录';

-- =============================================================================
-- 模块四：本地文件与Mod管理模块
-- =============================================================================

-- 表4.1: LocalGameInstall（本地安装信息）
CREATE TABLE local_game_install (
    install_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    platform_id INT,
    user_id INT NOT NULL,
    game_id BIGINT NOT NULL,
    install_path VARCHAR(750) UNIQUE NOT NULL,
    detected_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    version VARCHAR(100) NOT NULL,
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id),
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    INDEX idx_user_id (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='本地安装信息';

-- 表4.2: LocalSaveFile（本地存档）
CREATE TABLE local_save_file (
    save_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    file_path VARCHAR(750) UNIQUE NOT NULL,
    file_size INT NOT NULL COMMENT '文件大小KB',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    is_backup_local TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0代表不备份，1代表备份',
    install_id BIGINT NOT NULL,
    FOREIGN KEY (install_id) REFERENCES local_game_install(install_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='本地存档';

-- 表4.3: LocalMod（本地mod）
CREATE TABLE local_mod (
    mod_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    mod_name VARCHAR(128) NOT NULL,
    version INT NOT NULL,
    file_path VARCHAR(2048) NOT NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 1 COMMENT '0代表不启用，1代表启用',
    last_modified DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    install_id BIGINT NOT NULL,
    FOREIGN KEY (install_id) REFERENCES local_game_install(install_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='本地mod';

-- 表4.4: CloudSaveBackup（云端备份存档）
CREATE TABLE cloud_save_backup (
    cloud_backup_id VARCHAR(20) PRIMARY KEY,
    user_id INT NOT NULL,
    game_id BIGINT NOT NULL,
    upload_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    file_size INT NOT NULL COMMENT '大小MB',
    storage_url VARCHAR(750) UNIQUE NOT NULL,
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    INDEX idx_user_id (user_id),
    INDEX idx_upload_time (upload_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='云端备份存档';

-- =============================================================================
-- 模块五：数据可视化与报表模块
-- =============================================================================

-- 表5.1: ReportTemplate（报表模板）
CREATE TABLE report_template (
    template_id INT PRIMARY KEY AUTO_INCREMENT,
    template_name VARCHAR(128) NOT NULL,
    description TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='报表模板';

-- 表5.2: ReportGenerationRecord（报表生成历史）
CREATE TABLE report_generation_record (
    report_id VARCHAR(20) PRIMARY KEY,
    user_id INT NOT NULL,
    template_id INT NOT NULL,
    generated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0代表未生成，1代表生成',
    output_path VARCHAR(2048),
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (template_id) REFERENCES report_template(template_id),
    INDEX idx_user_id (user_id),
    INDEX idx_generated_at (generated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='报表生成历史';

-- =============================================================================
-- 模块六：个性化推荐与分析模块（AI）
-- =============================================================================

-- 表6.1: UserPreference（用户偏好表 - AI推荐算法支撑数据）
CREATE TABLE user_preference (
    preference_id INT PRIMARY KEY AUTO_INCREMENT,
    user_id INT UNIQUE NOT NULL,
    playtime_range VARCHAR(50) COMMENT '偏好游玩时长区间（如"1-3小时/天"）',
    price_sensitivity INT NOT NULL DEFAULT 2 COMMENT '价格敏感度（1高/2中/3低）',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    INDEX idx_updated_at (updated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户偏好表-AI推荐算法支撑数据';

-- 表6.2: PreferenceGenre（用户偏好与游戏题材关联表）
CREATE TABLE preference_genre (
    id INT PRIMARY KEY AUTO_INCREMENT,
    preference_id INT NOT NULL,
    genre_id INT NOT NULL,
    FOREIGN KEY (preference_id) REFERENCES user_preference(preference_id) ON DELETE CASCADE,
    FOREIGN KEY (genre_id) REFERENCES genres(genre_id),
    UNIQUE KEY uk_preference_genre (preference_id, genre_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户偏好与游戏题材关联表';

-- 表6.3: Recommendation（AI推荐结果表 - 个性化推荐记录）
CREATE TABLE recommendation (
    recommendation_id INT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    game_id BIGINT NOT NULL,
    recommendation_type ENUM('game', 'discount', 'similar', 'trending') NOT NULL COMMENT '推荐类型',
    recommendation_strategy ENUM('collaborative', 'content_based', 'hybrid', 'popular') NOT NULL,
    reason TEXT NOT NULL COMMENT 'AI生成解释短文',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expire_time DATETIME NOT NULL COMMENT '默认7天',
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    INDEX idx_user_id (user_id),
    INDEX idx_expire_time (expire_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='AI推荐结果表-个性化推荐记录';

-- 表6.4: RecommendationFeedback（推荐反馈表 - AI算法优化反馈记录）
CREATE TABLE recommendation_feedback (
    feedback_id INT PRIMARY KEY AUTO_INCREMENT,
    recommendation_id INT UNIQUE NOT NULL,
    user_id INT NOT NULL,
    feedback_result INT NOT NULL COMMENT '1喜欢/2不喜欢',
    feedback_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    remark TEXT,
    FOREIGN KEY (recommendation_id) REFERENCES recommendation(recommendation_id),
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    INDEX idx_feedback_time (feedback_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='推荐反馈表-AI算法优化反馈记录';

-- =============================================================================
-- 模块七：折扣/价格监控与提醒模块
-- =============================================================================

-- 表7.1: PriceHistory（游戏价格历史表）
CREATE TABLE price_history (
    price_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    platform_id INT NOT NULL,
    current_price DECIMAL(10, 2) NOT NULL,
    original_price DECIMAL(10, 2) NOT NULL,
    discount_rate INT NOT NULL COMMENT '0-100',
    is_discount TINYINT(1) NOT NULL DEFAULT 0,
    record_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id),
    INDEX idx_game_platform (game_id, platform_id),
    INDEX idx_record_date (record_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='游戏价格历史表';

-- 表7.2: PriceAlertSubscription（价格提醒订阅表，愿望单）
CREATE TABLE price_alert_subscription (
    subscription_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    game_id BIGINT NOT NULL,
    platform_id INT NOT NULL,
    target_price DECIMAL(10, 2) COMMENT 'NULL表示不启用',
    target_discount INT COMMENT 'NULL表示不启用',
    is_active TINYINT(1) DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    FOREIGN KEY (game_id) REFERENCES games(game_id),
    FOREIGN KEY (platform_id) REFERENCES platforms(platform_id),
    UNIQUE KEY uk_user_game_platform (user_id, game_id, platform_id),
    INDEX idx_is_active (is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='价格提醒订阅表(愿望单)';

-- =============================================================================
-- 模块八：家长监管与通知中心模块
-- =============================================================================

-- 表8.3: NotificationCenter（通知中心表）
CREATE TABLE notification_center (
    notification_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    source_module ENUM('price_alert', 'parental_control', 'system', 'recommendation', 'game_update') NOT NULL,
    title VARCHAR(255) NOT NULL,
    content TEXT NOT NULL,
    notification_type ENUM('info', 'warning', 'alert') DEFAULT 'info',
    is_read TINYINT(1) DEFAULT 0,
    related_id BIGINT NOT NULL UNIQUE,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES user(user_id),
    INDEX idx_user_id (user_id),
    INDEX idx_is_read (is_read)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='通知中心表';

-- 表7.3: PriceAlertLog（价格提醒日志表）
CREATE TABLE price_alert_log (
    alert_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    subscription_id BIGINT NOT NULL,
    price_id BIGINT NOT NULL,
    alert_type ENUM('target_price', 'target_discount') NOT NULL,
    alert_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    notification_id BIGINT,
    FOREIGN KEY (subscription_id) REFERENCES price_alert_subscription(subscription_id),
    FOREIGN KEY (price_id) REFERENCES price_history(price_id),
    FOREIGN KEY (notification_id) REFERENCES notification_center(notification_id),
    INDEX idx_alert_time (alert_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='价格提醒日志表';

-- 表8.1: ParentalControlRelationship（家长监管关系表）
CREATE TABLE parental_control_relationship (
    relationship_id INT PRIMARY KEY AUTO_INCREMENT,
    parent_user_id INT NOT NULL,
    child_user_id INT UNIQUE NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (parent_user_id) REFERENCES user(user_id),
    FOREIGN KEY (child_user_id) REFERENCES user(user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='家长监管关系表';

-- 表8.2: ParentalControlRule（家长监管规则表）
CREATE TABLE parental_control_rule (
    rule_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    child_user_id INT NOT NULL,
    rule_type ENUM('playtime_daily_limit','playtime_curfew','spending_limit','game_restriction','age_restriction') NOT NULL,
    rule_value JSON NOT NULL,
    is_active TINYINT(1) DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (child_user_id) REFERENCES user(user_id),
    INDEX idx_child_user (child_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='家长监管规则表';

-- 表8.4: ParentalAlertLog（家长监管报警日志表）
CREATE TABLE parental_alert_log (
    alert_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    rule_id BIGINT NOT NULL,
    child_user_id INT NOT NULL,
    violation_details JSON NOT NULL,
    alert_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    notification_id BIGINT,
    FOREIGN KEY (rule_id) REFERENCES parental_control_rule(rule_id),
    FOREIGN KEY (child_user_id) REFERENCES user(user_id),
    FOREIGN KEY (notification_id) REFERENCES notification_center(notification_id),
    INDEX idx_alert_time (alert_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='家长监管报警日志表';
