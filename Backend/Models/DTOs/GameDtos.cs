namespace PlayLinker.Models.DTOs;

/// <summary>
/// 游戏列表响应DTO
/// </summary>
public class GameListDto
{
    public List<GameItemDto> Items { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// 游戏项DTO
/// </summary>
public class GameItemDto
{
    public long GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public string ReleaseDate { get; set; } = string.Empty;
    public string HeaderImage { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public PlatformSupportDto Platforms { get; set; } = new();
    public int ReviewScore { get; set; }
    public int TotalPositive { get; set; }
    public int CurrentPlayers { get; set; }
}

/// <summary>
/// 平台支持DTO
/// </summary>
public class PlatformSupportDto
{
    public bool Windows { get; set; }
    public bool Mac { get; set; }
    public bool Linux { get; set; }
}

/// <summary>
/// 游戏详情响应DTO
/// </summary>
public class GameDetailDto
{
    public long GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public byte? RequireAge { get; set; }
    public string? ShortDescription { get; set; }
    public string? DetailedDescription { get; set; }
    public GameMediaDto Media { get; set; } = new();
    public GameRequirementsDto Requirements { get; set; } = new();
    public List<GenreDto> Genres { get; set; } = new();
    public List<DeveloperDto> Developers { get; set; } = new();
    public List<PublisherDto> Publishers { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();
    public PlatformSupportDto Platforms { get; set; } = new();
    public List<int> PlatformIds { get; set; } = new(); // 游戏支持的所有平台ID（Steam、Xbox等）
    public List<GamePlatformDto> GamePlatforms { get; set; } = new(); // 游戏平台信息（包含商店链接）
    public string ReleaseDate { get; set; } = string.Empty;
    public GameReviewsDto Reviews { get; set; } = new();
}

/// <summary>
/// 游戏媒体DTO
/// </summary>
public class GameMediaDto
{
    public string HeaderImage { get; set; } = string.Empty;
    public string CapsuleImage { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public List<string> Screenshots { get; set; } = new();
    public List<string> Videos { get; set; } = new();
}

/// <summary>
/// 游戏系统需求DTO
/// </summary>
public class GameRequirementsDto
{
    public string? PcMinimum { get; set; }
    public string? PcRecommended { get; set; }
    public string? MacMinimum { get; set; }
    public string? MacRecommended { get; set; }
    public string? LinuxMinimum { get; set; }
    public string? LinuxRecommended { get; set; }
}

/// <summary>
/// 游戏评价DTO
/// </summary>
public class GameReviewsDto
{
    public int Score { get; set; }
    public string ScoreDesc { get; set; } = string.Empty;
    public int TotalReviews { get; set; }
    public int TotalPositive { get; set; }
}

/// <summary>
/// 题材DTO
/// </summary>
public class GenreDto
{
    public int GenreId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 开发商DTO
/// </summary>
public class DeveloperDto
{
    public long DeveloperId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? GamesCount { get; set; }
}

/// <summary>
/// 发行商DTO
/// </summary>
public class PublisherDto
{
    public long PublisherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? GamesCount { get; set; }
}

/// <summary>
/// 分类DTO
/// </summary>
public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 语言DTO
/// </summary>
public class LanguageDto
{
    public int LanguageId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 游戏平台DTO（包含商店链接）
/// </summary>
public class GamePlatformDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string? GamePlatformUrl { get; set; }
}

/// <summary>
/// 游戏排行榜响应DTO
/// </summary>
public class GameRankingListDto
{
    public List<GameRankingItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// 游戏排行榜项DTO
/// </summary>
public class GameRankingItemDto
{
    public long RankId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int? CurrentRank { get; set; }
    public int? LastWeekRank { get; set; }
    public int? PeakPlayers { get; set; }
    public string HeaderImage { get; set; } = string.Empty;
}

/// <summary>
/// 添加游戏请求DTO
/// </summary>
public class AddGameRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string? ShortDescription { get; set; }
    public string? DetailedDescription { get; set; }
    public string HeaderImage { get; set; } = string.Empty;
    public string CapsuleImage { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public byte? RequireAge { get; set; }
    public PlatformSupportDto Platforms { get; set; } = new();
}

/// <summary>
/// 更新游戏请求DTO
/// </summary>
public class UpdateGameRequestDto
{
    public string? Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? HeaderImage { get; set; }
}

/// <summary>
/// 更新游戏信息请求DTO
/// </summary>
public class UpdateGameInfoRequestDto
{
    public long GameId { get; set; }
    public bool UpdateAchievement { get; set; } = false;
}

/// <summary>
/// 更新游戏信息响应DTO
/// </summary>
public class UpdateGameInfoResponseDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool AchievementUpdated { get; set; }
}

/// <summary>
/// 批量更新所有Steam游戏请求DTO
/// </summary>
public class UpdateAllSteamGamesRequestDto
{
    public bool UpdateAchievement { get; set; } = false;
}

/// <summary>
/// 批量更新所有Steam游戏响应DTO
/// </summary>
public class UpdateAllSteamGamesResponseDto
{
    public int TotalGames { get; set; }
    public int UpdatedGames { get; set; }
    public int FailedGames { get; set; }
    public int UpdatedAchievements { get; set; }
    public List<string> Errors { get; set; } = new();
}

