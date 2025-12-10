using System.Text.Json.Serialization;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// PSN导入请求DTO
/// </summary>
public class PsnImportRequestDto
{
    /// <summary>
    /// 用户ID(必需)
    /// </summary>
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    /// <summary>
    /// PSN在线ID
    /// </summary>
    [JsonPropertyName("psnOnlineId")]
    public string PsnOnlineId { get; set; } = string.Empty;

    /// <summary>
    /// 是否导入游戏库
    /// </summary>
    [JsonPropertyName("importGames")]
    public bool ImportGames { get; set; } = true;

    /// <summary>
    /// 是否导入奖杯数据(PSN的成就系统)
    /// </summary>
    [JsonPropertyName("importTrophies")]
    public bool ImportTrophies { get; set; } = true;
}

/// <summary>
/// PSN导入响应DTO
/// </summary>
public class PsnImportResponseDto
{
    /// <summary>
    /// 任务ID
    /// </summary>
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 状态: processing, completed, failed
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 消息(成功或错误信息)
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 预计时间(秒)
    /// </summary>
    [JsonPropertyName("estimatedTime")]
    public int EstimatedTime { get; set; }

    /// <summary>
    /// 导入项目统计
    /// </summary>
    [JsonPropertyName("items")]
    public PsnImportItemsDto Items { get; set; } = new();
}

/// <summary>
/// PSN导入项目统计DTO
/// </summary>
public class PsnImportItemsDto
{
    /// <summary>
    /// 游戏数量
    /// </summary>
    [JsonPropertyName("games")]
    public int Games { get; set; }

    /// <summary>
    /// 奖杯数量
    /// </summary>
    [JsonPropertyName("achievements")]
    public int Achievements { get; set; }
}

/// <summary>
/// PSN用户信息DTO
/// </summary>
public class PsnUserDto
{
    /// <summary>
    /// PSN在线ID
    /// </summary>
    [JsonPropertyName("onlineId")]
    public string OnlineId { get; set; } = string.Empty;

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
    public string? AccountCreated { get; set; }

    /// <summary>
    /// 国家/地区
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// 拥有游戏数量
    /// </summary>
    [JsonPropertyName("gamesOwned")]
    public int GamesOwned { get; set; }

    /// <summary>
    /// 奖杯统计
    /// </summary>
    [JsonPropertyName("trophySummary")]
    public PsnTrophySummaryDto TrophySummary { get; set; } = new();

    /// <summary>
    /// 奖杯等级
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    /// 资料是否公开
    /// </summary>
    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }
}

/// <summary>
/// PSN奖杯统计DTO
/// </summary>
public class PsnTrophySummaryDto
{
    /// <summary>
    /// 铜杯数量
    /// </summary>
    [JsonPropertyName("bronze")]
    public int Bronze { get; set; }

    /// <summary>
    /// 银杯数量
    /// </summary>
    [JsonPropertyName("silver")]
    public int Silver { get; set; }

    /// <summary>
    /// 金杯数量
    /// </summary>
    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    /// <summary>
    /// 白金杯数量
    /// </summary>
    [JsonPropertyName("platinum")]
    public int Platinum { get; set; }

    /// <summary>
    /// 总奖杯数
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// PSN游戏信息DTO
/// </summary>
public class PsnGameDto
{
    /// <summary>
    /// PSN游戏标题ID
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
    public PsnPriceDto? PriceOverview { get; set; }

    /// <summary>
    /// 成就(奖杯)信息
    /// </summary>
    [JsonPropertyName("achievements")]
    public PsnAchievementsInfoDto? Achievements { get; set; }

    /// <summary>
    /// 游戏平台(PS4/PS5等)
    /// </summary>
    [JsonPropertyName("trophyTitlePlatform")]
    public string? TrophyTitlePlatform { get; set; }

    /// <summary>
    /// 奖杯进度百分比
    /// </summary>
    [JsonPropertyName("progress")]
    public int Progress { get; set; }
}

/// <summary>
/// PSN价格信息DTO
/// </summary>
public class PsnPriceDto
{
    /// <summary>
    /// 货币代码
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// 原价(分)
    /// </summary>
    [JsonPropertyName("initial")]
    public int Initial { get; set; }

    /// <summary>
    /// 现价(分)
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
/// PSN成就(奖杯)信息DTO
/// </summary>
public class PsnAchievementsInfoDto
{
    /// <summary>
    /// 成就总数
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// PSN用户奖杯DTO
/// </summary>
public class PsnUserTrophyDto
{
    /// <summary>
    /// 奖杯ID
    /// </summary>
    [JsonPropertyName("trophyId")]
    public string TrophyId { get; set; } = string.Empty;

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
    /// 奖杯类型: bronze/silver/gold/platinum
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 奖杯分数(映射为成就分数)
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

    /// <summary>
    /// 稀有度: common/rare/very_rare/ultra_rare
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;
}

/// <summary>
/// PSN用户奖杯列表响应DTO
/// </summary>
public class PsnUserTrophiesResponseDto
{
    /// <summary>
    /// 奖杯列表
    /// </summary>
    [JsonPropertyName("items")]
    public List<PsnUserTrophyDto> Items { get; set; } = new();

    /// <summary>
    /// 总数量
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// PSN认证请求DTO
/// </summary>
public class PsnAuthRequestDto
{
    /// <summary>
    /// NPSSO令牌(从PlayStation网站获取)
    /// </summary>
    [JsonPropertyName("npsso")]
    public string Npsso { get; set; } = string.Empty;

    /// <summary>
    /// 令牌文件路径(可选,默认使用项目内路径)
    /// </summary>
    [JsonPropertyName("tokensPath")]
    public string? TokensPath { get; set; }

    /// <summary>
    /// 是否强制重新认证(会删除现有令牌)
    /// </summary>
    [JsonPropertyName("forceReauth")]
    public bool ForceReauth { get; set; } = false;
}

/// <summary>
/// PSN认证响应DTO
/// </summary>
public class PsnAuthResponseDto
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
    /// 账户ID
    /// </summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; set; }

    /// <summary>
    /// 在线ID
    /// </summary>
    [JsonPropertyName("onlineId")]
    public string? OnlineId { get; set; }

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
}
