namespace PlayLinker.Models.DTOs;

/// <summary>
/// Steam导入请求DTO
/// </summary>
public class SteamImportRequestDto
{
    public int UserId { get; set; }
    public string SteamId { get; set; } = string.Empty;
    public bool ImportGames { get; set; } = true;
    public bool ImportAchievements { get; set; } = true;
    public bool ImportFriends { get; set; } = false;
}

/// <summary>
/// Steam导入响应DTO
/// </summary>
public class SteamImportResponseDto
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public int EstimatedTime { get; set; }
    public SteamImportItemsDto Items { get; set; } = new();
}

/// <summary>
/// Steam导入项目DTO
/// </summary>
public class SteamImportItemsDto
{
    public int Games { get; set; }
    public int Achievements { get; set; }
    public int Friends { get; set; }
}

/// <summary>
/// Steam用户信息响应DTO
/// </summary>
public class SteamUserDto
{
    public string SteamId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string AccountCreated { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Level { get; set; }
    public int GamesOwned { get; set; }
    public int Badges { get; set; }
    public bool IsPublic { get; set; }
}

/// <summary>
/// Steam游戏信息响应DTO
/// </summary>
public class SteamGameDto
{
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "game";
    public bool IsFree { get; set; }
    public string? ShortDescription { get; set; }
    public string? DetailedDescription { get; set; }
    public string HeaderImage { get; set; } = string.Empty;
    public List<string> Developers { get; set; } = new();
    public List<string> Publishers { get; set; } = new();
    public PlatformSupportDto Platforms { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Genres { get; set; } = new();
    public string ReleaseDate { get; set; } = string.Empty;
    public int RequiredAge { get; set; }
    public SteamPriceDto? PriceOverview { get; set; }
    public SteamAchievementsInfoDto? Achievements { get; set; }
    public SteamRecommendationsDto? Recommendations { get; set; }
}

/// <summary>
/// Steam价格DTO
/// </summary>
public class SteamPriceDto
{
    public string Currency { get; set; } = "CNY";
    public int Initial { get; set; }
    public int Final { get; set; }
    public int DiscountPercent { get; set; }
}

/// <summary>
/// Steam成就信息DTO
/// </summary>
public class SteamAchievementsInfoDto
{
    public int Total { get; set; }
}

/// <summary>
/// Steam推荐DTO
/// </summary>
public class SteamRecommendationsDto
{
    public int Total { get; set; }
}

