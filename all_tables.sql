-- we don't know how to generate root <with-no-name> (class Root) :(

create table __EFMigrationsHistory
(
    MigrationId    varchar(150) not null
        primary key,
    ProductVersion varchar(32)  not null
);

create table categories
(
    category_id int auto_increment
        primary key,
    name        varchar(50) not null
)
    comment '分类/标签词表';

create table developers
(
    developer_id int auto_increment
        primary key,
    name         varchar(20)                        not null,
    created_at   datetime default CURRENT_TIMESTAMP null,
    constraint name
        unique (name)
)
    comment '游戏开发商';

create table external_links
(
    link_id      int auto_increment
        primary key,
    source       varchar(255)                                           not null comment 'bilibili,youtube等',
    link_title   varchar(255)                                           not null,
    link_url     varchar(2048)                                          not null,
    link_type    enum ('guide', 'video', 'review', 'wiki', 'community') not null,
    link_summary varchar(255)                                           null
)
    comment '第三方攻略/外链源';

create table games
(
    game_id              bigint auto_increment
        primary key,
    name                 varchar(128)         not null,
    is_free              tinyint(1) default 0 not null comment '1代表免费，0代表不免费',
    require_age          tinyint              null,
    short_description    text                 null,
    detailed_description text                 null,
    header_image         varchar(2048)        not null,
    capsile_image        varchar(2048)        not null,
    background           varchar(2048)        not null,
    pc_recommended       text                 null,
    pc_minimum           text                 null,
    mac_recommended      text                 null,
    mac_minimum          text                 null,
    linux_recommended    text                 null,
    linux_minimum        text                 null,
    windows              tinyint(1) default 0 not null,
    mac                  tinyint(1) default 0 not null,
    linux                tinyint(1) default 0 not null,
    release_date         date                 not null,
    review_score         int        default 0 not null,
    review_score_desc    text                 not null,
    num_reviews          int        default 0 not null,
    total_positive       int        default 0 not null,
    constraint name
        unique (name)
)
    comment '游戏主表';

create table game_categories
(
    id          bigint auto_increment
        primary key,
    game_id     bigint not null,
    category_id int    not null,
    constraint uk_game_category
        unique (game_id, category_id),
    constraint game_categories_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_categories_ibfk_2
        foreign key (category_id) references categories (category_id)
)
    comment '游戏与分类关联表';

create index category_id
    on game_categories (category_id);

create table game_developers
(
    id           bigint auto_increment
        primary key,
    game_id      bigint                             not null,
    developer_id int                                not null,
    created_at   datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_developer
        unique (game_id, developer_id),
    constraint game_developers_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_developers_ibfk_2
        foreign key (developer_id) references developers (developer_id)
)
    comment '游戏与开发商关联表';

create index developer_id
    on game_developers (developer_id);

create table game_external_links
(
    id         bigint auto_increment
        primary key,
    game_id    bigint                             not null,
    link_id    int                                not null,
    created_at datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_link
        unique (game_id, link_id),
    constraint game_external_links_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_external_links_ibfk_2
        foreign key (link_id) references external_links (link_id)
)
    comment '游戏与外链关联表';

create index link_id
    on game_external_links (link_id);

create table game_mod_source
(
    id               bigint auto_increment
        primary key,
    game_id          bigint                             not null comment '本地游戏ID',
    source           varchar(50)                        not null comment 'Mod来源: NexusMods, 3DM, GameBanana, Steam',
    external_game_id varchar(100)                       not null comment '第三方平台的游戏ID',
    external_domain  varchar(100)                       null comment '如 NexusMods 的 domain_name',
    created_at       datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_source
        unique (game_id, source),
    constraint game_mod_source_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade
)
    comment '游戏Mod平台映射表';

create index idx_source
    on game_mod_source (source);

create table game_ranking
(
    rank_id        bigint auto_increment
        primary key,
    game_id        bigint                             not null,
    pack_in_game   int                                null comment '峰值人数',
    last_week_rank int                                null comment '上周排名',
    current_rank   int                                null comment '排名',
    updated_at     datetime default CURRENT_TIMESTAMP not null comment '数据最后更新时间',
    constraint game_id
        unique (game_id),
    constraint game_ranking_ibfk_1
        foreign key (game_id) references games (game_id)
)
    comment '游戏排行榜表';

create index idx_current_rank
    on game_ranking (current_rank);

create index idx_name
    on games (name);

create index idx_release_date
    on games (release_date);

create table genres
(
    genre_id   int auto_increment
        primary key,
    name       varchar(20)                        not null,
    created_at datetime default CURRENT_TIMESTAMP null,
    constraint name
        unique (name)
)
    comment '游戏题材/风格词表';

create table game_genres
(
    id         int auto_increment
        primary key,
    game_id    bigint                             not null,
    genre_id   int                                not null,
    created_at datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_genre
        unique (game_id, genre_id),
    constraint game_genres_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_genres_ibfk_2
        foreign key (genre_id) references genres (genre_id)
)
    comment '游戏与题材关联表';

create index genre_id
    on game_genres (genre_id);

create table languages
(
    language_id   int auto_increment
        primary key,
    language_name varchar(50) not null
)
    comment '支持语言表';

create table game_languages
(
    id          bigint auto_increment
        primary key,
    game_id     bigint       not null,
    language_id int          not null,
    notes       varchar(256) null comment '比如 是否有简体中文',
    constraint uk_game_language
        unique (game_id, language_id),
    constraint game_languages_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_languages_ibfk_2
        foreign key (language_id) references languages (language_id)
)
    comment '游戏语言关联表';

create index language_id
    on game_languages (language_id);

create table news
(
    news_id    bigint auto_increment
        primary key,
    news_title varchar(512)    not null,
    news_url   varchar(2048)   null,
    date       bigint unsigned not null comment 'Unix时间戳(如1763596068)',
    author     varchar(128)    not null,
    contents   text            not null
)
    comment '新闻/公告源';

create table game_news
(
    id         bigint auto_increment
        primary key,
    game_id    bigint                             not null,
    news_id    bigint                             not null,
    created_at datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_news
        unique (game_id, news_id),
    constraint game_news_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_news_ibfk_2
        foreign key (news_id) references news (news_id)
)
    comment '游戏与新闻关联表';

create index news_id
    on game_news (news_id);

create index idx_date
    on news (date);

create table platforms
(
    platform_id   int auto_increment
        primary key,
    platform_name varchar(128)         not null,
    description   text                 null,
    logo_url      varchar(2048)        null,
    status        tinyint(1) default 1 not null comment '0不启用,1启用',
    constraint platform_name
        unique (platform_name)
)
    comment '游戏平台表';

create table achievements
(
    achievement_id   bigint auto_increment
        primary key,
    game_id          bigint               not null,
    platform_id      int        default 1 not null comment '游戏平台ID，1=Steam',
    achievement_name varchar(128)         not null,
    displayName      varchar(128)         not null,
    hidden           tinyint(1) default 0 not null comment '0=不隐藏，1=隐藏',
    description      text                 null,
    icon_unlocked    varchar(2048)        not null comment '解锁状态',
    icon_locked      varchar(2048)        not null comment '未解锁状态/灰色',
    constraint achievements_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint achievements_ibfk_2
        foreign key (platform_id) references platforms (platform_id)
)
    comment '成就表';

create index fk_achievements_platform
    on achievements (platform_id);

create index idx_game_id
    on achievements (game_id);

create table game_platform
(
    game_id           bigint        not null,
    platform_id       int           not null,
    platform_game_id  varchar(128)  not null comment '平台内部标识',
    game_platform_url varchar(2048) null,
    primary key (game_id, platform_id),
    constraint game_platform_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_platform_ibfk_2
        foreign key (platform_id) references platforms (platform_id)
)
    comment '游戏在平台的映射';

create index platform_id
    on game_platform (platform_id);

create index idx_platform_name
    on platforms (platform_name);

create table player_platform
(
    platform_user_id varchar(128)  not null comment '平台侧用户标识（平台唯一）',
    platform_id      int           not null comment '平台ID（标识Steam/Epic等）',
    profile_name     varchar(128)  not null,
    profile_url      varchar(2048) null,
    account_created  datetime      null,
    country          varchar(50)   null,
    primary key (platform_user_id, platform_id),
    constraint player_platform_ibfk_1
        foreign key (platform_id) references platforms (platform_id)
)
    comment '玩家在某一平台的账号资料';

create index platform_id
    on player_platform (platform_id);

create table price_history
(
    price_id       bigint auto_increment
        primary key,
    game_id        bigint                               not null,
    platform_id    int                                  not null,
    current_price  decimal(10, 2)                       not null,
    original_price decimal(10, 2)                       not null,
    discount_rate  int                                  not null comment '0-100',
    is_discount    tinyint(1) default 0                 not null,
    record_date    datetime   default CURRENT_TIMESTAMP not null,
    constraint price_history_ibfk_1
        foreign key (game_id) references games (game_id),
    constraint price_history_ibfk_2
        foreign key (platform_id) references platforms (platform_id)
)
    comment '游戏价格历史表';

create index idx_game_platform
    on price_history (game_id, platform_id);

create index idx_record_date
    on price_history (record_date);

create index platform_id
    on price_history (platform_id);

create table publishers
(
    publisher_id int auto_increment
        primary key,
    name         varchar(20)                        not null,
    created_at   datetime default CURRENT_TIMESTAMP null,
    constraint name
        unique (name)
)
    comment '游戏发行商';

create table game_publishers
(
    id           bigint auto_increment
        primary key,
    game_id      bigint                             not null,
    publisher_id int                                not null,
    created_at   datetime default CURRENT_TIMESTAMP null,
    constraint uk_game_publisher
        unique (game_id, publisher_id),
    constraint game_publishers_ibfk_1
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint game_publishers_ibfk_2
        foreign key (publisher_id) references publishers (publisher_id)
)
    comment '游戏与发行商关联表';

create index publisher_id
    on game_publishers (publisher_id);

create table report_template
(
    template_id   int auto_increment
        primary key,
    template_name varchar(128)                       not null,
    description   text                               not null,
    created_at    datetime default CURRENT_TIMESTAMP not null
)
    comment '报表模板';

create table role
(
    role_id   int auto_increment
        primary key,
    role_name enum ('user', 'parent', 'admin') not null,
    role_desc varchar(300)                     null,
    constraint role_name
        unique (role_name)
)
    comment '登录权限控制表';

create index idx_role_name
    on role (role_name);

create table user
(
    user_id         int auto_increment
        primary key,
    username        varchar(128)                                                      not null,
    hashed_password varchar(128)                                                      not null comment 'AES-256加密',
    email           varchar(100)                                                      null,
    gender          int                                     default 0                 null comment '1男/2女/0未知',
    phone           varchar(100)                                                      null,
    avatar_url      varchar(2048)                                                     null,
    role_id         int                                     default 1                 not null,
    status          enum ('active', 'disabled', 'inactive') default 'inactive'        null,
    created_at      datetime                                default CURRENT_TIMESTAMP null,
    last_login_time datetime                                                          null,
    login_ip        varchar(45)                                                       null,
    constraint email
        unique (email),
    constraint phone
        unique (phone),
    constraint username
        unique (username),
    constraint user_ibfk_1
        foreign key (role_id) references role (role_id)
)
    comment '核心登录与基础信息表';

create table cloud_save_backup
(
    cloud_backup_id varchar(20)                        not null
        primary key,
    user_id         int                                not null,
    game_id         bigint                             not null,
    upload_time     datetime default CURRENT_TIMESTAMP not null,
    file_size       int                                not null comment '大小MB',
    storage_url     varchar(750)                       not null,
    constraint storage_url
        unique (storage_url),
    constraint cloud_save_backup_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint cloud_save_backup_ibfk_2
        foreign key (game_id) references games (game_id)
)
    comment '云端备份存档';

create index game_id
    on cloud_save_backup (game_id);

create index idx_upload_time
    on cloud_save_backup (upload_time);

create index idx_user_id
    on cloud_save_backup (user_id);

create table local_game_install
(
    install_id    bigint auto_increment
        primary key,
    platform_id   int                                null,
    user_id       int                                not null,
    game_id       bigint                             not null,
    install_path  varchar(750)                       not null,
    detected_time datetime default CURRENT_TIMESTAMP not null,
    version       varchar(100)                       not null,
    size_bytes    bigint   default 0                 not null comment '游戏大小（字节）',
    constraint install_path
        unique (install_path),
    constraint local_game_install_ibfk_1
        foreign key (platform_id) references platforms (platform_id),
    constraint local_game_install_ibfk_2
        foreign key (user_id) references user (user_id),
    constraint local_game_install_ibfk_3
        foreign key (game_id) references games (game_id)
)
    comment '本地安装信息';

create index game_id
    on local_game_install (game_id);

create index idx_user_id
    on local_game_install (user_id);

create index platform_id
    on local_game_install (platform_id);

create table local_mod
(
    mod_id        bigint auto_increment
        primary key,
    mod_name      varchar(128)                         not null,
    version       int                                  not null,
    file_path     varchar(2048)                        not null,
    enabled       tinyint(1) default 1                 not null comment '0代表不启用，1代表启用',
    last_modified datetime   default CURRENT_TIMESTAMP not null on update CURRENT_TIMESTAMP,
    install_id    bigint                               not null,
    constraint local_mod_ibfk_1
        foreign key (install_id) references local_game_install (install_id)
            on delete cascade
)
    comment '本地mod';

create index install_id
    on local_mod (install_id);

create table local_save_file
(
    save_id         bigint auto_increment
        primary key,
    file_path       varchar(750)                         not null,
    file_size       int                                  not null comment '文件大小KB',
    updated_at      datetime   default CURRENT_TIMESTAMP not null on update CURRENT_TIMESTAMP,
    is_backup_local tinyint(1) default 0                 not null comment '0代表不备份，1代表备份',
    install_id      bigint                               not null,
    constraint file_path
        unique (file_path),
    constraint local_save_file_ibfk_1
        foreign key (install_id) references local_game_install (install_id)
            on delete cascade
)
    comment '本地存档';

create index install_id
    on local_save_file (install_id);

create table notification_center
(
    notification_id   bigint auto_increment
        primary key,
    user_id           int                                                                                 not null,
    source_module     enum ('price_alert', 'parental_control', 'system', 'recommendation', 'game_update') not null,
    title             varchar(255)                                                                        not null,
    content           text                                                                                not null,
    notification_type enum ('info', 'warning', 'alert') default 'info'                                    null,
    is_read           tinyint(1)                        default 0                                         null,
    related_id        bigint                                                                              not null,
    created_at        datetime                          default CURRENT_TIMESTAMP                         null,
    constraint related_id
        unique (related_id),
    constraint notification_center_ibfk_1
        foreign key (user_id) references user (user_id)
)
    comment '通知中心表';

create index idx_is_read
    on notification_center (is_read);

create index idx_user_id
    on notification_center (user_id);

create table parental_control_relationship
(
    relationship_id int auto_increment
        primary key,
    parent_user_id  int                                not null,
    child_user_id   int                                not null,
    created_at      datetime default CURRENT_TIMESTAMP null,
    constraint child_user_id
        unique (child_user_id),
    constraint parental_control_relationship_ibfk_1
        foreign key (parent_user_id) references user (user_id),
    constraint parental_control_relationship_ibfk_2
        foreign key (child_user_id) references user (user_id)
)
    comment '家长监管关系表';

create index parent_user_id
    on parental_control_relationship (parent_user_id);

create table parental_control_rule
(
    rule_id       bigint auto_increment
        primary key,
    child_user_id int                                                                                                       not null,
    rule_type     enum ('playtime_daily_limit', 'playtime_curfew', 'spending_limit', 'game_restriction', 'age_restriction') not null,
    rule_value    json                                                                                                      not null,
    is_active     tinyint(1) default 1                                                                                      null,
    created_at    datetime   default CURRENT_TIMESTAMP                                                                      null,
    updated_at    datetime   default CURRENT_TIMESTAMP                                                                      null on update CURRENT_TIMESTAMP,
    constraint parental_control_rule_ibfk_1
        foreign key (child_user_id) references user (user_id)
)
    comment '家长监管规则表';

create table parental_alert_log
(
    alert_id          bigint auto_increment
        primary key,
    rule_id           bigint                             not null,
    child_user_id     int                                not null,
    violation_details json                               not null,
    alert_time        datetime default CURRENT_TIMESTAMP null,
    notification_id   bigint                             null,
    constraint parental_alert_log_ibfk_1
        foreign key (rule_id) references parental_control_rule (rule_id),
    constraint parental_alert_log_ibfk_2
        foreign key (child_user_id) references user (user_id),
    constraint parental_alert_log_ibfk_3
        foreign key (notification_id) references notification_center (notification_id)
)
    comment '家长监管报警日志表';

create index child_user_id
    on parental_alert_log (child_user_id);

create index idx_alert_time
    on parental_alert_log (alert_time);

create index notification_id
    on parental_alert_log (notification_id);

create index rule_id
    on parental_alert_log (rule_id);

create index idx_child_user
    on parental_control_rule (child_user_id);

create table price_alert_subscription
(
    subscription_id bigint auto_increment
        primary key,
    user_id         int                                  not null,
    game_id         bigint                               not null,
    platform_id     int                                  not null,
    target_price    decimal(10, 2)                       null comment 'NULL表示不启用',
    target_discount int                                  null comment 'NULL表示不启用',
    is_active       tinyint(1) default 1                 null,
    created_at      datetime   default CURRENT_TIMESTAMP null,
    updated_at      datetime   default CURRENT_TIMESTAMP null on update CURRENT_TIMESTAMP,
    constraint uk_user_game_platform
        unique (user_id, game_id, platform_id),
    constraint price_alert_subscription_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint price_alert_subscription_ibfk_2
        foreign key (game_id) references games (game_id),
    constraint price_alert_subscription_ibfk_3
        foreign key (platform_id) references platforms (platform_id)
)
    comment '价格提醒订阅表(愿望单)';

create table price_alert_log
(
    alert_id        bigint auto_increment
        primary key,
    subscription_id bigint                                   not null,
    price_id        bigint                                   not null,
    alert_type      enum ('target_price', 'target_discount') not null,
    alert_time      datetime default CURRENT_TIMESTAMP       null,
    notification_id bigint                                   null,
    constraint price_alert_log_ibfk_1
        foreign key (subscription_id) references price_alert_subscription (subscription_id),
    constraint price_alert_log_ibfk_2
        foreign key (price_id) references price_history (price_id),
    constraint price_alert_log_ibfk_3
        foreign key (notification_id) references notification_center (notification_id)
)
    comment '价格提醒日志表';

create index idx_alert_time
    on price_alert_log (alert_time);

create index notification_id
    on price_alert_log (notification_id);

create index price_id
    on price_alert_log (price_id);

create index subscription_id
    on price_alert_log (subscription_id);

create index game_id
    on price_alert_subscription (game_id);

create index idx_is_active
    on price_alert_subscription (is_active);

create index platform_id
    on price_alert_subscription (platform_id);

create table recommendation
(
    recommendation_id       int auto_increment
        primary key,
    user_id                 int                                                          not null,
    game_id                 bigint                                                       not null,
    recommendation_type     enum ('game', 'discount', 'similar', 'trending')             not null comment '推荐类型',
    recommendation_strategy enum ('collaborative', 'content_based', 'hybrid', 'popular') not null,
    reason                  text                                                         not null comment 'AI生成解释短文',
    created_at              datetime default CURRENT_TIMESTAMP                           not null,
    expire_time             datetime                                                     not null comment '默认7天',
    constraint recommendation_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint recommendation_ibfk_2
        foreign key (game_id) references games (game_id)
)
    comment 'AI推荐结果表-个性化推荐记录';

create index game_id
    on recommendation (game_id);

create index idx_expire_time
    on recommendation (expire_time);

create index idx_user_id
    on recommendation (user_id);

create table recommendation_feedback
(
    feedback_id       int auto_increment
        primary key,
    recommendation_id int                                not null,
    user_id           int                                not null,
    feedback_result   int                                not null comment '1喜欢/2不喜欢',
    feedback_time     datetime default CURRENT_TIMESTAMP not null,
    remark            text                               null,
    constraint recommendation_id
        unique (recommendation_id),
    constraint recommendation_feedback_ibfk_1
        foreign key (recommendation_id) references recommendation (recommendation_id),
    constraint recommendation_feedback_ibfk_2
        foreign key (user_id) references user (user_id)
)
    comment '推荐反馈表-AI算法优化反馈记录';

create index idx_feedback_time
    on recommendation_feedback (feedback_time);

create index user_id
    on recommendation_feedback (user_id);

create table report_generation_record
(
    report_id    varchar(20)                          not null
        primary key,
    user_id      int                                  not null,
    template_id  int                                  not null,
    generated_at datetime   default CURRENT_TIMESTAMP not null,
    status       tinyint(1) default 0                 not null comment '0代表未生成，1代表生成',
    output_path  varchar(2048)                        null,
    constraint report_generation_record_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint report_generation_record_ibfk_2
        foreign key (template_id) references report_template (template_id)
)
    comment '报表生成历史';

create index idx_generated_at
    on report_generation_record (generated_at);

create index idx_user_id
    on report_generation_record (user_id);

create index template_id
    on report_generation_record (template_id);

create index idx_email
    on user (email);

create index idx_status
    on user (status);

create index idx_username
    on user (username);

create index role_id
    on user (role_id);

create table user_achievements
(
    user_achievement_id bigint auto_increment
        primary key,
    user_id             int                                  not null,
    achievement_id      bigint                               not null,
    unlocked            tinyint(1) default 0                 not null comment '0未解锁/1已解锁',
    unlock_time         datetime                             null comment 'null表示未解锁',
    platform_id         int                                  not null,
    created_at          datetime   default CURRENT_TIMESTAMP null,
    constraint uk_user_achievement
        unique (user_id, achievement_id, platform_id),
    constraint user_achievements_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint user_achievements_ibfk_2
        foreign key (achievement_id) references achievements (achievement_id),
    constraint user_achievements_ibfk_3
        foreign key (platform_id) references platforms (platform_id)
)
    comment '用户成就解锁记录';

create index achievement_id
    on user_achievements (achievement_id);

create index idx_user_id
    on user_achievements (user_id);

create index platform_id
    on user_achievements (platform_id);

create table user_game_library
(
    user_id                 int           not null
        primary key,
    total_games_owned       int default 0 not null,
    games_played            int default 0 not null,
    total_playtime_minutes  int default 0 not null,
    total_achievements      int           null,
    unlocked_achievements   int           null,
    recently_played_count   int default 0 not null,
    recent_playtime_minutes int default 0 not null,
    constraint user_game_library_ibfk_1
        foreign key (user_id) references user (user_id)
)
    comment '用户统一游戏库统计';

create table user_platform_binding
(
    binding_id       int auto_increment
        primary key,
    user_id          int                                  not null,
    platform_id      int                                  not null,
    platform_user_id varchar(512)                         not null comment '第三方平台用户ID（如SteamID）',
    access_token     text                                 null comment 'AES-256加密存储',
    refresh_token    text                                 null comment 'AES-256加密存储',
    binding_status   tinyint(1) default 1                 not null comment '1已绑定/0已解绑',
    binding_time     datetime   default CURRENT_TIMESTAMP null,
    last_sync_time   datetime                             null,
    expire_time      datetime                             not null comment '按平台API规则设置',
    constraint uk_user_platform
        unique (user_id, platform_id),
    constraint user_platform_binding_ibfk_1
        foreign key (user_id) references user (user_id),
    constraint user_platform_binding_ibfk_2
        foreign key (platform_id) references platforms (platform_id),
    constraint user_platform_binding_ibfk_3
        foreign key (platform_user_id, platform_id) references player_platform (platform_user_id, platform_id)
)
    comment '跨平台账号OAuth绑定记录';

create index idx_user_id
    on user_platform_binding (user_id);

create index platform_id
    on user_platform_binding (platform_id);

create index platform_user_id
    on user_platform_binding (platform_user_id, platform_id);

create table user_platform_library
(
    platform_user_id          varchar(128)  not null comment '平台侧用户标识（平台唯一）',
    platform_id               int           not null,
    game_id                   bigint        not null comment '若为单款记录',
    playtime_minutes          int default 0 not null comment '累计游玩分钟数（该平台/该游戏）',
    playtime_minutes_twoweeks int default 0 not null comment '两周内游玩时间（分钟）',
    last_played               datetime      null,
    achievements_total        int           null comment '成就总数（平台/该游戏）',
    achievements_unlocked     int           null comment '已解锁成就数（平台/该游戏）',
    primary key (platform_user_id, platform_id, game_id),
    constraint user_platform_library_ibfk_1
        foreign key (platform_user_id, platform_id) references player_platform (platform_user_id, platform_id),
    constraint user_platform_library_ibfk_2
        foreign key (game_id) references games (game_id)
)
    comment '用户在某平台的单款游戏记录';

create index game_id
    on user_platform_library (game_id);

create index idx_last_played
    on user_platform_library (last_played);

create table user_playtime_history
(
    history_id       bigint auto_increment
        primary key,
    user_id          int                                not null comment '用户ID',
    game_id          bigint                             not null comment '游戏ID',
    platform_id      int                                not null comment '平台ID',
    playtime_forever int      default 0                 not null comment '总游玩时长(分钟)',
    playtime_2weeks  int      default 0                 null comment 'Steam接口返回的过去两周时长(分钟)',
    record_date      date                               not null comment '记录日期',
    created_at       datetime default CURRENT_TIMESTAMP null,
    constraint uk_user_game_platform_date
        unique (user_id, game_id, platform_id, record_date),
    constraint fk_uph_game_id
        foreign key (game_id) references games (game_id)
            on delete cascade,
    constraint fk_uph_platform_id
        foreign key (platform_id) references platforms (platform_id)
            on delete cascade,
    constraint fk_uph_user_id
        foreign key (user_id) references user (user_id)
            on delete cascade
)
    comment '用户游戏时长历史记录表';

create index idx_record_date
    on user_playtime_history (record_date);

create index idx_user_date
    on user_playtime_history (user_id, record_date);

create table user_preference
(
    preference_id     int auto_increment
        primary key,
    user_id           int                                not null,
    playtime_range    varchar(50)                        null comment '偏好游玩时长区间（如"1-3小时/天"）',
    price_sensitivity int      default 2                 not null comment '价格敏感度（1高/2中/3低）',
    updated_at        datetime default CURRENT_TIMESTAMP not null on update CURRENT_TIMESTAMP,
    constraint user_id
        unique (user_id),
    constraint user_preference_ibfk_1
        foreign key (user_id) references user (user_id)
)
    comment '用户偏好表-AI推荐算法支撑数据';

create table preference_genre
(
    id            int auto_increment
        primary key,
    preference_id int not null,
    genre_id      int not null,
    constraint uk_preference_genre
        unique (preference_id, genre_id),
    constraint preference_genre_ibfk_1
        foreign key (preference_id) references user_preference (preference_id)
            on delete cascade,
    constraint preference_genre_ibfk_2
        foreign key (genre_id) references genres (genre_id)
)
    comment '用户偏好与游戏题材关联表';

create index genre_id
    on preference_genre (genre_id);

create index idx_updated_at
    on user_preference (updated_at);

