-- 插入更多游戏的 Mod 来源映射
-- 表名: game_mod_source
-- 基于 NexusMods 和 3DM 平台
-- 执行前请确保没有重复的 game_id + source 组合

-- ============================================
-- 第一批: 热门大作 (已有部分数据，这里补充更多)
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 求生之路2
(10000, 'NexusMods', '206', 'left4dead2', NOW()),
(10000, '3DM', '50', NULL, NOW()),

-- CS2
(10001, 'NexusMods', '3970', 'counterstrike2', NOW()),

-- 群星 Stellaris
(10004, 'NexusMods', '394', 'stellaris', NOW()),
(10004, '3DM', '120', NULL, NOW()),

-- 文明6
(10006, 'NexusMods', '2513', 'civilisationvi', NOW()),
(10006, '3DM', '130', NULL, NOW()),

-- 巫师3
(10007, 'NexusMods', '952', 'witcher3', NOW()),
(10007, '3DM', '1', NULL, NOW()),

-- 饥荒联机版
(10008, 'NexusMods', '457', 'dontstarvetogether', NOW()),

-- 星露谷物语
(10014, 'NexusMods', '1303', 'stardewvalley', NOW()),
(10014, '3DM', '85', NULL, NOW()),

-- 尼尔：机械纪元
(10024, 'NexusMods', '1151', 'nierautomata', NOW()),
(10024, '3DM', '150', NULL, NOW()),

-- 深岩银河
(10025, 'NexusMods', '4384', 'deeprockgalactic', NOW()),

-- 地狱潜者2
(10026, 'NexusMods', '6681', 'helldivers2', NOW()),
(10026, '3DM', '380', NULL, NOW()),

-- 极乐迪斯科
(10028, 'NexusMods', '3410', 'discoelysium', NOW()),

-- 如龙0
(10030, 'NexusMods', '2022', 'yakuza0', NOW()),

-- 只狼
(10038, 'NexusMods', '2763', 'sekiro', NOW()),
(10038, '3DM', '184', NULL, NOW()),

-- 外部世界
(10036, 'NexusMods', '3526', 'outerwilds', NOW()),

-- 精灵与萤火意志
(10046, 'NexusMods', '3531', 'oriandthewillofthewisps', NOW()),

-- 严阵以待
(10048, 'NexusMods', '4635', 'readyornot', NOW()),
(10048, '3DM', '290', NULL, NOW()),

-- 哈迪斯2
(10049, 'NexusMods', '6903', 'hades2', NOW()),

-- 哈迪斯
(10050, 'NexusMods', '3527', 'hades', NOW()),

-- 荒野大镖客2
(10051, 'NexusMods', '3212', 'reddeadredemption2', NOW()),
(10051, '3DM', '200', NULL, NOW()),

-- 底特律：变人
(10054, 'NexusMods', '2801', 'detroitbecomehuman', NOW()),

-- 艾尔登法环
(10058, 'NexusMods', '4333', 'eldenring', NOW()),
(10058, '3DM', '275', NULL, NOW()),

-- 双人成行
(10065, 'NexusMods', '4169', 'ittakestwo', NOW()),

-- 极限竞速：地平线5
(10069, 'NexusMods', '4350', 'forzahorizon5', NOW()),
(10069, '3DM', '280', NULL, NOW()),

-- 幻兽帕鲁
(10070, 'NexusMods', '6180', 'palworld', NOW()),
(10070, '3DM', '335', NULL, NOW()),

-- 致命公司
(10077, 'NexusMods', '5620', 'lethalcompany', NOW()),
(10077, '3DM', '350', NULL, NOW()),

-- 对马岛之魂
(10081, 'NexusMods', '4891', 'ghostoftsushima', NOW()),
(10081, '3DM', '310', NULL, NOW()),

-- 黑神话悟空
(10083, 'NexusMods', '6713', 'blackmythwukong', NOW()),
(10083, '3DM', '376', NULL, NOW()),

-- 暗黑破坏神4
(10084, 'NexusMods', '5656', 'diablo4', NOW()),
(10084, '3DM', '320', NULL, NOW()),

-- 女神异闻录5皇家版
(10090, 'NexusMods', '4697', 'persona5royal', NOW()),
(10090, '3DM', '300', NULL, NOW()),

-- 女神异闻录3 Reload
(10091, 'NexusMods', '6400', 'persona3reload', NOW()),
(10091, '3DM', '370', NULL, NOW()),

-- Metaphor: ReFantazio
(10096, 'NexusMods', '6850', 'metaphorreFantazio', NOW()),
(10096, '3DM', '390', NULL, NOW()),

-- GTA5
(10116, 'NexusMods', '1846', 'gta5', NOW()),
(10116, '3DM', '7', NULL, NOW()),

-- 怪物猎人世界
(10119, 'NexusMods', '2531', 'monsterhunterworld', NOW()),
(10119, '3DM', '162', NULL, NOW()),

-- 森林之子
(10121, 'NexusMods', '4287', 'sonsoftheforest', NOW()),
(10121, '3DM', '260', NULL, NOW()),

-- 七日杀
(10128, 'NexusMods', '1059', '7daystodie', NOW()),
(10128, '3DM', '70', NULL, NOW()),

-- 精灵与森林
(10129, 'NexusMods', '1610', 'oriandtheblindforest', NOW()),

-- 冰汽时代
(10133, 'NexusMods', '1950', 'frostpunk', NOW()),

-- 空洞骑士
(10134, 'NexusMods', '2086', 'hollowknight', NOW()),
(10134, '3DM', '100', NULL, NOW()),

-- 刺客信条：起源
(10137, 'NexusMods', '2503', 'assassinscreedorigins', NOW()),

-- 杀戮尖塔
(10138, 'NexusMods', '2868', 'slaythespire', NOW()),

-- 赛博朋克2077
(10143, 'NexusMods', '3333', 'cyberpunk2077', NOW()),
(10143, '3DM', '195', NULL, NOW()),

-- 邪恶铭刻
(10144, 'NexusMods', '3641', 'inscryption', NOW()),

-- 战地2042
(10148, 'NexusMods', '4156', 'battlefield2042', NOW()),

-- 森林
(10173, 'NexusMods', '3173', 'theforest', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);


-- ============================================
-- 第二批: 更多热门游戏 (10200-10999)
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 死亡搁浅
(10200, 'NexusMods', '3231', 'deathstranding', NOW()),
(10200, '3DM', '210', NULL, NOW()),

-- 无主之地3
(10210, 'NexusMods', '2847', 'borderlands3', NOW()),
(10210, '3DM', '190', NULL, NOW()),

-- 战神4
(10220, 'NexusMods', '4394', 'godofwar', NOW()),
(10220, '3DM', '270', NULL, NOW()),

-- 战神：诸神黄昏
(10221, 'NexusMods', '5604', 'godofwarragnarok', NOW()),
(10221, '3DM', '360', NULL, NOW()),

-- 地平线：零之曙光
(10230, 'NexusMods', '3635', 'horizonzerodawn', NOW()),
(10230, '3DM', '220', NULL, NOW()),

-- 地平线：西之绝境
(10231, 'NexusMods', '5438', 'horizonforbiddenwest', NOW()),
(10231, '3DM', '340', NULL, NOW()),

-- 漫威蜘蛛侠
(10240, 'NexusMods', '5179', 'marvelsspidermanremastered', NOW()),
(10240, '3DM', '330', NULL, NOW()),

-- 漫威蜘蛛侠2
(10241, 'NexusMods', '6500', 'marvelsspiderman2', NOW()),

-- 最终幻想7重制版
(10250, 'NexusMods', '4202', 'finalfantasy7remake', NOW()),
(10250, '3DM', '250', NULL, NOW()),

-- 最终幻想16
(10251, 'NexusMods', '6700', 'finalfantasy16', NOW()),
(10251, '3DM', '385', NULL, NOW()),

-- 艾尔登法环 黑夜君临
(10095, 'NexusMods', '4333', 'eldenring', NOW()),

-- 星空
(10812, 'NexusMods', '4187', 'starfield', NOW()),
(10812, '3DM', '324', NULL, NOW()),

-- 龙之信条2
(10850, 'NexusMods', '6451', 'dragonsdogma2', NOW()),
(10850, '3DM', '375', NULL, NOW()),

-- 装甲核心6
(10860, 'NexusMods', '5721', 'armoredcore6', NOW()),
(10860, '3DM', '355', NULL, NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);

-- ============================================
-- 第三批: 11000-12000 范围游戏
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 暗黑地牢
(11050, 'NexusMods', '804', 'darkestdungeon', NOW()),

-- 暗黑地牢2
(11051, 'NexusMods', '4545', 'darkestdungeon2', NOW()),

-- 死亡细胞
(11100, 'NexusMods', '2478', 'deadcells', NOW()),

-- 茶杯头
(11150, 'NexusMods', '2200', 'cuphead', NOW()),

-- 蔚蓝
(11200, 'NexusMods', '2273', 'celeste', NOW()),

-- 泰拉瑞亚
(11250, 'NexusMods', '531', 'terraria', NOW()),
(11250, '3DM', '40', NULL, NOW()),

-- 缺氧
(11300, 'NexusMods', '2352', 'oxygennotincluded', NOW()),

-- 环世界
(11350, 'NexusMods', '1149', 'rimworld', NOW()),
(11350, '3DM', '80', NULL, NOW()),

-- 戴森球计划
(11400, 'NexusMods', '4046', 'dysonsphereprogramm', NOW()),
(11400, '3DM', '230', NULL, NOW()),

-- 异星工厂
(11450, 'NexusMods', '2229', 'factorio', NOW()),
(11450, '3DM', '110', NULL, NOW()),

-- 饥荒
(11500, 'NexusMods', '457', 'dontstarve', NOW()),

-- 雨中冒险2
(11550, 'NexusMods', '2966', 'riskofrain2', NOW()),

-- 吸血鬼幸存者
(11600, 'NexusMods', '4758', 'vampiresurvivors', NOW()),

-- 哈迪斯
(11650, 'NexusMods', '3527', 'hades', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);

-- ============================================
-- 第四批: 12000-13000 范围游戏
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 博德之门3
(12039, 'NexusMods', '3474', 'baldursgate3', NOW()),
(12039, '3DM', '240', NULL, NOW()),

-- 辐射4
(12077, 'NexusMods', '1151', 'fallout4', NOW()),
(12077, '3DM', '6', NULL, NOW()),

-- 生化危机4重制版
(12076, 'NexusMods', '5481', 'residentevil42023', NOW()),
(12076, '3DM', '305', NULL, NOW()),

-- 生化危机2重制版
(12078, 'NexusMods', '2679', 'residentevil22019', NOW()),
(12078, '3DM', '180', NULL, NOW()),

-- 生化危机3重制版
(12079, 'NexusMods', '3060', 'residentevil32020', NOW()),
(12079, '3DM', '205', NULL, NOW()),

-- 生化危机8：村庄
(12080, 'NexusMods', '3976', 'residentevilvillage', NOW()),
(12080, '3DM', '235', NULL, NOW()),

-- 上古卷轴5特别版
(12111, 'NexusMods', '1704', 'skyrimspecialedition', NOW()),
(12111, '3DM', '3', NULL, NOW()),

-- 庄园领主
(12169, 'NexusMods', '6366', 'manorlords', NOW()),
(12169, '3DM', '345', NULL, NOW()),

-- 鬼泣5
(12369, 'NexusMods', '2666', 'devilmaycry5', NOW()),
(12369, '3DM', '183', NULL, NOW()),

-- 怪物猎人崛起
(12764, 'NexusMods', '4286', 'monsterhunterrise', NOW()),
(12764, '3DM', '263', NULL, NOW()),

-- 怪物猎人崛起：曙光
(12765, 'NexusMods', '4286', 'monsterhunterrise', NOW()),

-- 刺客信条：奥德赛
(12800, 'NexusMods', '2673', 'assassinscreedodyssey', NOW()),
(12800, '3DM', '170', NULL, NOW()),

-- 刺客信条：英灵殿
(12810, 'NexusMods', '3829', 'assassinscreedvalhalla', NOW()),
(12810, '3DM', '215', NULL, NOW()),

-- 刺客信条：幻景
(12820, 'NexusMods', '5800', 'assassinscreedmirage', NOW()),
(12820, '3DM', '365', NULL, NOW()),

-- 看门狗2
(12850, 'NexusMods', '1407', 'watchdogs2', NOW()),

-- 看门狗：军团
(12860, 'NexusMods', '3700', 'watchdogslegion', NOW()),

-- 孤岛惊魂6
(12900, 'NexusMods', '4100', 'farcry6', NOW()),
(12900, '3DM', '245', NULL, NOW()),

-- 孤岛惊魂5
(12910, 'NexusMods', '2425', 'farcry5', NOW()),
(12910, '3DM', '160', NULL, NOW()),

-- 全境封锁2
(12950, 'NexusMods', '2659', 'thedivision2', NOW()),

-- 彩虹六号：围攻
(12960, 'NexusMods', '1286', 'rainbowsixsiege', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);


-- ============================================
-- 第五批: 13000+ 范围游戏
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 霍格沃茨之遗
(13000, 'NexusMods', '5113', 'hogwartslegacy', NOW()),
(13000, '3DM', '295', NULL, NOW()),

-- 原子之心
(13010, 'NexusMods', '5200', 'atomicheart', NOW()),
(13010, '3DM', '300', NULL, NOW()),

-- 卧龙：苍天陨落
(13020, 'NexusMods', '5300', 'wulongfallendynasty', NOW()),
(13020, '3DM', '310', NULL, NOW()),

-- 匹诺曹的谎言
(13030, 'NexusMods', '5750', 'liesofp', NOW()),
(13030, '3DM', '358', NULL, NOW()),

-- 星际拓荒
(13040, 'NexusMods', '3526', 'outerwilds', NOW()),

-- 潜水员戴夫
(13050, 'NexusMods', '5850', 'davethediver', NOW()),

-- 暗黑破坏神2重制版
(13060, 'NexusMods', '4062', 'diablo2resurrected', NOW()),

-- 永劫无间
(13070, 'NexusMods', '4150', 'naraka', NOW()),
(13070, '3DM', '255', NULL, NOW()),

-- 影子武士3
(13080, 'NexusMods', '4500', 'shadowwarrior3', NOW()),

-- 死亡循环
(13090, 'NexusMods', '4030', 'deathloop', NOW()),
(13090, '3DM', '248', NULL, NOW()),

-- 幽灵线：东京
(13100, 'NexusMods', '4400', 'ghostwiretokyo', NOW()),
(13100, '3DM', '285', NULL, NOW()),

-- 小缇娜的奇幻之地
(13110, 'NexusMods', '4600', 'tinytinawonderlands', NOW()),

-- 暗邪西部
(13120, 'NexusMods', '4800', 'evilwest', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);

-- ============================================
-- 第六批: 经典老游戏和独立游戏
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 我的世界 (如果有)
(10500, 'NexusMods', '0', 'minecraft', NOW()),
(10500, '3DM', '10', NULL, NOW()),

-- 骑马与砍杀2
(10550, 'NexusMods', '3174', 'mountandblade2bannerlord', NOW()),
(10550, '3DM', '200', NULL, NOW()),

-- 十字军之王3
(10600, 'NexusMods', '3275', 'crusaderkings3', NOW()),
(10600, '3DM', '210', NULL, NOW()),

-- 欧陆风云4
(10650, 'NexusMods', '1078', 'europauniversalis4', NOW()),
(10650, '3DM', '60', NULL, NOW()),

-- 钢铁雄心4
(10700, 'NexusMods', '1329', 'heartsofironiv', NOW()),
(10700, '3DM', '90', NULL, NOW()),

-- 维多利亚3
(10750, 'NexusMods', '4900', 'victoria3', NOW()),
(10750, '3DM', '315', NULL, NOW()),

-- 全面战争：战锤3
(10780, 'NexusMods', '4717', 'totalwarwarhammer3', NOW()),
(10780, '3DM', '280', NULL, NOW()),

-- 全面战争：三国
(10790, 'NexusMods', '2782', 'totalwarthreekingdoms', NOW()),
(10790, '3DM', '185', NULL, NOW()),

-- 模拟城市
(10820, 'NexusMods', '1000', 'citiesskylines', NOW()),
(10820, '3DM', '55', NULL, NOW()),

-- 城市：天际线2
(10830, 'NexusMods', '5900', 'citiesskylines2', NOW()),
(10830, '3DM', '362', NULL, NOW()),

-- 模拟人生4
(10840, 'NexusMods', '641', 'thesims4', NOW()),
(10840, '3DM', '25', NULL, NOW()),

-- 监狱建筑师
(10870, 'NexusMods', '1200', 'prisonarchitect', NOW()),

-- 过山车之星
(10880, 'NexusMods', '1500', 'planetcoaster', NOW()),

-- 动物园之星
(10890, 'NexusMods', '2900', 'planetzoo', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);

-- ============================================
-- 第七批: 更多独立游戏和小众游戏
-- ============================================

INSERT INTO game_mod_source (game_id, source, external_game_id, external_domain, created_at) VALUES
-- 糖豆人
(10920, 'NexusMods', '3400', 'fallguys', NOW()),

-- 盗贼之海
(10930, 'NexusMods', '2600', 'seaofthieves', NOW()),

-- 腐蚀
(10940, 'NexusMods', '1800', 'rust', NOW()),
(10940, '3DM', '95', NULL, NOW()),

-- 方舟：生存进化
(10950, 'NexusMods', '800', 'arksurvivalevolved', NOW()),
(10950, '3DM', '45', NULL, NOW()),

-- 方舟：生存飞升
(10960, 'NexusMods', '5950', 'arksurvivalascended', NOW()),
(10960, '3DM', '368', NULL, NOW()),

-- 英灵神殿
(10970, 'NexusMods', '3667', 'valheim', NOW()),
(10970, '3DM', '225', NULL, NOW()),

-- 绿色地狱
(10980, 'NexusMods', '2750', 'greenhell', NOW()),

-- 漫长的黑暗
(10990, 'NexusMods', '1100', 'thelongdark', NOW()),

-- 腐烂国度2
(11000, 'NexusMods', '2450', 'stateofDecay2', NOW()),

-- 消逝的光芒2
(11010, 'NexusMods', '4200', 'dyinglight2', NOW()),
(11010, '3DM', '265', NULL, NOW()),

-- 消逝的光芒
(11020, 'NexusMods', '1030', 'dyinglight', NOW()),
(11020, '3DM', '65', NULL, NOW()),

-- 无人深空
(11030, 'NexusMods', '1634', 'nomanssky', NOW()),
(11030, '3DM', '140', NULL, NOW()),

-- 深海迷航
(11040, 'NexusMods', '1155', 'subnautica', NOW()),
(11040, '3DM', '105', NULL, NOW()),

-- 深海迷航：零度之下
(11045, 'NexusMods', '3600', 'subnauticabelowzero', NOW())

ON DUPLICATE KEY UPDATE external_game_id = VALUES(external_game_id), external_domain = VALUES(external_domain);

-- ============================================
-- 查询验证 (可选执行)
-- ============================================
-- SELECT COUNT(*) as total_mappings FROM game_mod_source;
-- SELECT source, COUNT(*) as count FROM game_mod_source GROUP BY source;
