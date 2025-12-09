using System.Text.Json.Serialization;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// GOG导入请求DTO
/// </summary>
public class GogImportRequestDto
{
    /// <summary>
    /// 用户ID(必需)
    /// </summary>
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    /// <summary>
    /// GOG用户ID
    /// </summary>
    [JsonPropertyName("gogUserId")]
    public string GogUserId { get; set; } = string.Empty;

    /// <summary>
    /// 是否导入游戏库
    /// </summary>
    [JsonPropertyName("importGames")]
    public bool ImportGames { get; set; } = true;
}

/// <summary>
/// GOG导入响应DTO
/// </summary>
public class GogImportResponseDto
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
    public GogImportItemsDto Items { get; set; } = new();
}

/// <summary>
/// GOG导入项目统计DTO
/// </summary>
public class GogImportItemsDto
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
/// GOG用户信息DTO
/// </summary>
public class GogUserDto
{
    /// <summary>
    /// GOG用户ID
    /// </summary>
    [JsonPropertyName("gogUserId")]
    public string GogUserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

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
    /// 资料是否公开
    /// </summary>
    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }
}

/// <summary>
/// GOG游戏信息DTO
/// </summary>
public class GogGameDto
{
    /// <summary>
    /// GOG游戏ID
    /// </summary>
    [JsonPropertyName("gogGameId")]
    public string GogGameId { get; set; } = string.Empty;

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
    public GogPriceDto? PriceOverview { get; set; }

    /// <summary>
    /// 成就信息
    /// </summary>
    [JsonPropertyName("achievements")]
    public GogAchievementsInfoDto? Achievements { get; set; }

    /// <summary>
    /// 游玩时长(分钟) - 从游戏会话统计
    /// </summary>
    [JsonPropertyName("playTimeMinutes")]
    public int PlayTimeMinutes { get; set; }
}

/// <summary>
/// GOG价格信息DTO
/// </summary>
public class GogPriceDto
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
/// GOG成就信息DTO
/// </summary>
public class GogAchievementsInfoDto
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
}

/// <summary>
/// GOG认证请求DTO
/// </summary>
public class GogAuthRequestDto
{
    /// <summary>
    /// 浏览器重定向后的完整URL
    /// 登录成功后浏览器地址栏的URL,例如: https://embed.gog.com/on_login_success?origin=client&amp;code=xxxxx
    /// 系统会自动从URL中提取授权码完成认证
    /// 如果不提供,将返回需要访问的认证URL
    /// </summary>
    [JsonPropertyName("redirectUrl")]
    public string? RedirectUrl { get; set; }

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
/// GOG认证响应DTO
/// </summary>
public class GogAuthResponseDto
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
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

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
    /// 认证URL(需要浏览器认证时提供)
    /// </summary>
    [JsonPropertyName("authUrl")]
    public string? AuthUrl { get; set; }

    /// <summary>
    /// 是否需要浏览器认证
    /// </summary>
    [JsonPropertyName("needsBrowserAuth")]
    public bool NeedsBrowserAuth { get; set; }
}
