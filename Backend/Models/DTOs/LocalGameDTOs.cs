namespace PlayLinker.Models.DTOs;

public class ScanLocalGamesRequest
{
    public List<string> Directories { get; set; } = new();
    public bool DeepScan { get; set; } = true;
}

public class ScanLocalGamesResponse
{
    public string ScanId { get; set; } = string.Empty;
    public List<LocalGameFoundDto> GamesFound { get; set; } = new();
    public int TotalFound { get; set; }
    public double ScanDuration { get; set; }
    public int ScannedDirectories { get; set; }
}

public class LocalGameFoundDto
{
    public long InstallId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string? Version { get; set; }
    public decimal SizeGB { get; set; }
    public DateTime DetectedTime { get; set; }
    public DateTime? LastPlayed { get; set; }
}

public class LocalGameListDto
{
    public long InstallId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string? Version { get; set; }
    public decimal SizeGB { get; set; }
    public DateTime DetectedTime { get; set; }
    public DateTime? LastPlayed { get; set; }
    public int SavesCount { get; set; }
    public int ModsCount { get; set; }
}

public class LocalGameDetailDto
{
    public long InstallId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string? Version { get; set; }
    public decimal SizeGB { get; set; }
    public DateTime DetectedTime { get; set; }
    public DateTime? LastPlayed { get; set; }
    public string? ExecutablePath { get; set; }
    public string? ConfigPath { get; set; }
    public List<SaveFileDto> Saves { get; set; } = new();
    public List<ModDto> Mods { get; set; } = new();
}

public class SaveFileDto
{
    public long SaveId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ModDto
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool Enabled { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

public class PaginationMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
