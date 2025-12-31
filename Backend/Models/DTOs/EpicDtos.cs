using System.Text.Json.Serialization;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// Epic Games 导入请求DTO
/// </summary>
public class EpicImportRequestDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    /// <summary>
    /// Epic 账户ID
    /// </summary>
    [JsonPropertyName("epicAccountId")]
    public string EpicAccountId { get; set; } = string.Empty;

    /// <summary>
    /// 是否导入游戏库
    /// </summary>
    [JsonPropertyName("importGames")]
    public bool ImportGames { get; set; } = true;

    /// <summary>
    /// 是否导入成就
    /// </summary>
    [JsonPropertyName("importAchievements")]
    public bool ImportAchievements { get; set; } = true;
}

/// <summary>
/// Epic Games 导入响应DTO
/// </summary>
public class EpicImportResponseDto
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
    public string Status { get; set; } = "processing";

    /// <summary>
    /// 消息
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
    public EpicImportItemsDto Items { get; set; } = new();
}

/// <summary>
/// Epic Games 导入项目统计DTO
/// </summary>
public class EpicImportItemsDto
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
/// Epic Games 用户信息DTO
/// </summary>
public class EpicUserDto
{
    /// <summary>
    /// Epic 账户ID
    /// </summary>
    [JsonPropertyName("epicAccountId")]
    public string EpicAccountId { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 头像URL
    /// </summary>
    [JsonPropertyName("avatarUrl")]
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 拥有游戏数量
    /// </summary>
    [JsonPropertyName("gamesOwned")]
    public int GamesOwned { get; set; }
}

/// <summary>
/// Epic Games 游戏信息DTO
/// </summary>
public class EpicGameDto
{
    /// <summary>
    /// 游戏ID (app_name)
    /// </summary>
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Namespace
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Offer ID
    /// </summary>
    [JsonPropertyName("offerId")]
    public string? OfferId { get; set; }

    /// <summary>
    /// Product ID
    /// </summary>
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

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
    /// 标签列表
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 发行日期
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    /// <summary>
    /// 价格显示
    /// </summary>
    [JsonPropertyName("priceDisplay")]
    public string? PriceDisplay { get; set; }

    /// <summary>
    /// 成就信息
    /// </summary>
    [JsonPropertyName("achievements")]
    public EpicAchievementsInfoDto? Achievements { get; set; }
}

/// <summary>
/// Epic Games 成就信息DTO
/// </summary>
public class EpicAchievementsInfoDto
{
    /// <summary>
    /// 成就总数
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// 已解锁数量
    /// </summary>
    [JsonPropertyName("unlockedCount")]
    public int UnlockedCount { get; set; }

    /// <summary>
    /// 成就列表
    /// </summary>
    [JsonPropertyName("achievements")]
    public List<EpicAchievementDto> Achievements { get; set; } = new();
}

/// <summary>
/// Epic Games 成就DTO
/// </summary>
public class EpicAchievementDto
{
    /// <summary>
    /// 成就ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 成就名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 成就描述
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 成就图标
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// 经验值
    /// </summary>
    [JsonPropertyName("xp")]
    public int Xp { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 解锁时间
    /// </summary>
    [JsonPropertyName("unlockedAt")]
    public string? UnlockedAt { get; set; }

    /// <summary>
    /// 进度值
    /// </summary>
    [JsonPropertyName("progressVal")]
    public int? ProgressVal { get; set; }
}

/// <summary>
/// Epic Games 认证请求DTO
/// </summary>
public class EpicAuthRequestDto
{
    /// <summary>
    /// 授权码（从Epic网页授权获取）
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    /// 是否强制重新认证
    /// </summary>
    [JsonPropertyName("forceReauth")]
    public bool ForceReauth { get; set; } = false;
}

