using System.Text.Json.Serialization;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// Xbox导入请求DTO
/// </summary>
public class XboxImportRequestDto
{
    /// <summary>
    /// 用户ID（必需）
    /// </summary>
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    /// <summary>
    /// Xbox用户ID（XUID格式）
    /// </summary>
    [JsonPropertyName("xboxUserId")]
    public string XboxUserId { get; set; } = string.Empty;

    /// <summary>
    /// 是否导入游戏库
    /// </summary>
    [JsonPropertyName("importGames")]
    public bool ImportGames { get; set; } = true;

    /// <summary>
    /// 是否导入成就数据
    /// </summary>
    [JsonPropertyName("importAchievements")]
    public bool ImportAchievements { get; set; } = true;
}

/// <summary>
/// Xbox导入响应DTO
/// </summary>
public class XboxImportResponseDto
{
    /// <summary>
    /// 任务ID
    /// </summary>
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 状态：processing, completed, failed
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 消息（成功或错误信息）
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 预计时间（秒）
    /// </summary>
    [JsonPropertyName("estimatedTime")]
    public int EstimatedTime { get; set; }

    /// <summary>
    /// 导入项目统计
    /// </summary>
    [JsonPropertyName("items")]
    public XboxImportItemsDto Items { get; set; } = new();
}

/// <summary>
/// Xbox导入项目统计DTO
/// </summary>
public class XboxImportItemsDto
{
    /// <summary>
    /// 游戏数量
    /// </summary>
    [JsonPropertyName("games")]
    public int Games { get; set; }

    /// <summary>
    /// 成就数量
    /// </summary>
    [JsonPropertyName("achievements")]
    public int Achievements { get; set; }
}

/// <summary>
/// Xbox用户信息DTO
/// </summary>
public class XboxUserDto
{
    /// <summary>
    /// Xbox用户ID（XUID）
    /// </summary>
    [JsonPropertyName("xuid")]
    public string Xuid { get; set; } = string.Empty;

    /// <summary>
    /// 玩家标签
    /// </summary>
    [JsonPropertyName("gamertag")]
    public string Gamertag { get; set; } = string.Empty;

    /// <summary>
    /// 个人资料URL
    /// </summary>
    [JsonPropertyName("profileUrl")]
    public string ProfileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 头像URL
    /// </summary>
    [JsonPropertyName("avatarUrl")]
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 账户创建时间
    /// </summary>
    [JsonPropertyName("accountCreated")]
    public string AccountCreated { get; set; } = string.Empty;

    /// <summary>
    /// 国家/地区
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// 玩家分数
    /// </summary>
    [JsonPropertyName("gamerscore")]
    public int Gamerscore { get; set; }

    /// <summary>
    /// 会员等级
    /// </summary>
    [JsonPropertyName("tier")]
    public string Tier { get; set; } = string.Empty;

    /// <summary>
    /// 拥有游戏数量
    /// </summary>
    [JsonPropertyName("gamesOwned")]
    public int GamesOwned { get; set; }

    /// <summary>
    /// 资料是否公开
    /// </summary>
    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }
}

/// <summary>
/// Xbox游戏信息DTO
/// </summary>
public class XboxGameDto
{
    /// <summary>
    /// Xbox游戏标题ID
    /// </summary>
    [JsonPropertyName("titleId")]
    public string TitleId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 游戏类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "game";

    /// <summary>
    /// 是否免费
    /// </summary>
    [JsonPropertyName("isFree")]
    public bool IsFree { get; set; }

    /// <summary>
    /// 简短描述
    /// </summary>
    [JsonPropertyName("shortDescription")]
    public string? ShortDescription { get; set; }

    /// <summary>
    /// 详细描述
    /// </summary>
    [JsonPropertyName("detailedDescription")]
    public string? DetailedDescription { get; set; }

    /// <summary>
    /// 头图URL
    /// </summary>
    [JsonPropertyName("headerImage")]
    public string HeaderImage { get; set; } = string.Empty;

    /// <summary>
    /// 开发商列表
    /// </summary>
    [JsonPropertyName("developers")]
    public List<string> Developers { get; set; } = new();

    /// <summary>
    /// 发行商列表
    /// </summary>
    [JsonPropertyName("publishers")]
    public List<string> Publishers { get; set; } = new();

    /// <summary>
    /// 平台支持
    /// </summary>
    [JsonPropertyName("platforms")]
    public PlatformSupportDto Platforms { get; set; } = new();

    /// <summary>
    /// 分类列表
    /// </summary>
    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// 题材列表
    /// </summary>
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// 发行日期
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public string ReleaseDate { get; set; } = string.Empty;

    /// <summary>
    /// 需要的年龄
    /// </summary>
    [JsonPropertyName("requiredAge")]
    public int RequiredAge { get; set; }

    /// <summary>
    /// 价格信息
    /// </summary>
    [JsonPropertyName("priceOverview")]
    public XboxPriceDto? PriceOverview { get; set; }

    /// <summary>
    /// 成就信息
    /// </summary>
    [JsonPropertyName("achievements")]
    public XboxAchievementsInfoDto? Achievements { get; set; }

    /// <summary>
    /// 游玩时长（分钟）
    /// </summary>
    [JsonPropertyName("playTimeMinutes")]
    public int PlayTimeMinutes { get; set; }

    /// <summary>
    /// 最后游玩时间
    /// </summary>
    [JsonPropertyName("lastPlayed")]
    public string? LastPlayed { get; set; }
}

/// <summary>
/// Xbox价格信息DTO
/// </summary>
public class XboxPriceDto
{
    /// <summary>
    /// 货币代码
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// 原价（分）
    /// </summary>
    [JsonPropertyName("initial")]
    public int Initial { get; set; }

    /// <summary>
    /// 现价（分）
    /// </summary>
    [JsonPropertyName("final")]
    public int Final { get; set; }

    /// <summary>
    /// 折扣百分比
    /// </summary>
    [JsonPropertyName("discountPercent")]
    public int DiscountPercent { get; set; }
}

/// <summary>
/// Xbox成就信息DTO
/// </summary>
public class XboxAchievementsInfoDto
{
    /// <summary>
    /// 成就总数
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// 当前已解锁成就数
    /// </summary>
    [JsonPropertyName("currentAchievements")]
    public int CurrentAchievements { get; set; }

    /// <summary>
    /// 当前玩家分数
    /// </summary>
    [JsonPropertyName("currentGamerscore")]
    public int CurrentGamerscore { get; set; }
}

/// <summary>
/// Xbox用户成就DTO
/// </summary>
public class XboxUserAchievementDto
{
    /// <summary>
    /// 成就ID
    /// </summary>
    [JsonPropertyName("achievementId")]
    public string AchievementId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏ID
    /// </summary>
    [JsonPropertyName("gameId")]
    public long GameId { get; set; }

    /// <summary>
    /// 游戏名称
    /// </summary>
    [JsonPropertyName("gameName")]
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// 成就名称
    /// </summary>
    [JsonPropertyName("achievementName")]
    public string AchievementName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 成就分数
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>
    /// 是否解锁
    /// </summary>
    [JsonPropertyName("unlocked")]
    public bool Unlocked { get; set; }

    /// <summary>
    /// 解锁时间
    /// </summary>
    [JsonPropertyName("unlockTime")]
    public string? UnlockTime { get; set; }

    /// <summary>
    /// 解锁图标URL
    /// </summary>
    [JsonPropertyName("iconUnlocked")]
    public string IconUnlocked { get; set; } = string.Empty;

    /// <summary>
    /// 锁定图标URL
    /// </summary>
    [JsonPropertyName("iconLocked")]
    public string IconLocked { get; set; } = string.Empty;
}

/// <summary>
/// Xbox认证请求DTO
/// </summary>
public class XboxAuthRequestDto
{
    /// <summary>
    /// 令牌文件路径（可选，默认使用项目内路径）
    /// </summary>
    [JsonPropertyName("tokensPath")]
    public string? TokensPath { get; set; }

    /// <summary>
    /// 是否强制重新认证（会删除现有令牌）
    /// </summary>
    [JsonPropertyName("forceReauth")]
    public bool ForceReauth { get; set; } = false;

    /// <summary>
    /// 是否启动浏览器进行首次认证
    /// 注意：首次认证必须设置为true，且需要在有图形界面的环境中运行
    /// 服务器部署时，建议在本地完成首次认证，然后上传tokens文件
    /// </summary>
    [JsonPropertyName("openBrowser")]
    public bool OpenBrowser { get; set; } = false;
}

/// <summary>
/// Xbox认证响应DTO
/// </summary>
public class XboxAuthResponseDto
{
    /// <summary>
    /// 是否认证成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// XUID
    /// </summary>
    [JsonPropertyName("xuid")]
    public string? Xuid { get; set; }

    /// <summary>
    /// 令牌路径
    /// </summary>
    [JsonPropertyName("tokensPath")]
    public string? TokensPath { get; set; }

    /// <summary>
    /// 令牌是否存在
    /// </summary>
    [JsonPropertyName("tokenExists")]
    public bool TokenExists { get; set; }

    /// <summary>
    /// 认证URL（需要浏览器认证时提供）
    /// </summary>
    [JsonPropertyName("authUrl")]
    public string? AuthUrl { get; set; }

    /// <summary>
    /// 是否需要浏览器认证
    /// </summary>
    [JsonPropertyName("needsBrowserAuth")]
    public bool NeedsBrowserAuth { get; set; }
}

