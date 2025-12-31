namespace PlayLinker.Models.DTOs;

/// <summary>
/// Mod 浏览相关 DTOs
/// </summary>

public class ModExploreRequest
{
    public long GameId { get; set; }
    public string Source { get; set; } = "NexusMods";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "downloads"; // downloads, updated, endorsements
}

public class ModExploreResponse
{
    public List<ExploreModItemDto> Mods { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string Source { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
}

/// <summary>
/// 第三方平台 Mod 项目（用于浏览）
/// </summary>
public class ExploreModItemDto
{
    public string ModId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ModPageUrl { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public int Downloads { get; set; }
    public int Endorsements { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool AdultContent { get; set; }
}

/// <summary>
/// 第三方平台 Mod 详情（用于浏览）
/// </summary>
public class ExploreModDetailDto : ExploreModItemDto
{
    public string Description { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<ExploreModFileDto> Files { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 第三方平台 Mod 文件信息
/// </summary>
public class ExploreModFileDto
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
}

public class GameModSourceDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public List<ModSourceInfo> Sources { get; set; } = new();
}

public class ModSourceInfo
{
    public string Source { get; set; } = string.Empty;
    public string ExternalGameId { get; set; } = string.Empty;
    public string? ExternalDomain { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
}
