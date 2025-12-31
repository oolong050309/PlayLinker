CREATE TABLE IF NOT EXISTS user_playtime_history (

history_id BIGINT NOT NULL AUTO_INCREMENT,

user_id INT NOT NULL COMMENT '用户ID',

game_id BIGINT NOT NULL COMMENT '游戏ID',

platform_id INT NOT NULL COMMENT '平台ID',

playtime_forever INT NOT NULL DEFAULT 0 COMMENT '总游玩时长(分钟)',

playtime_2weeks INT DEFAULT 0 COMMENT 'Steam接口返回的过去两周时长(分钟)',

record_date DATE NOT NULL COMMENT '记录日期',

created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

PRIMARY KEY (history_id),

UNIQUE KEY uk_user_game_platform_date (user_id,game_id,platform_id,record_date),

KEY idx_user_date (user_id,record_date),

KEY idx_record_date (record_date),

CONSTRAINT fk_uph_user_id FOREIGN KEY (user_id) REFERENCES user (user_id) ON DELETE CASCADE,

CONSTRAINT fk_uph_game_id FOREIGN KEY (game_id) REFERENCES games (game_id) ON DELETE CASCADE,

CONSTRAINT fk_uph_platform_id FOREIGN KEY (platform_id) REFERENCES platforms (platform_id) ON DELETE CASCADE

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户游戏时长历史记录表';