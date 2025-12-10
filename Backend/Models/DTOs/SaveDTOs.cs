namespace PlayLinker.Models.DTOs;

// 本地存档相关
public class LocalSaveListDto
{
    public long SaveId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public long InstallId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public decimal FileSizeMB { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsBackupLocal { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class BackupSaveRequest
{
    public long SaveId { get; set; }
    public string? BackupName { get; set; }
    public bool Compress { get; set; } = true;
}

public class BackupSaveResponse
{
    public string BackupId { get; set; } = string.Empty;
    public long SaveId { get; set; }
    public string? BackupName { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long BackupSize { get; set; }
    public bool Compressed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RestoreSaveRequest
{
    public bool Overwrite { get; set; } = true;
}

public class RestoreSaveResponse
{
    public string BackupId { get; set; } = string.Empty;
    public long SaveId { get; set; }
    public string RestoredPath { get; set; } = string.Empty;
    public DateTime RestoredAt { get; set; }
}

public class DeleteSaveRequest
{
    public bool DeleteFile { get; set; }
    public bool DeleteBackups { get; set; }
}

// 云存档相关
public class CloudSaveListDto
{
    public string CloudBackupId { get; set; } = string.Empty;
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime UploadTime { get; set; }
    public long FileSize { get; set; }
    public decimal FileSizeMB { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class UploadToCloudRequest
{
    public long SaveId { get; set; }
    public bool Compress { get; set; } = true;
    public bool Encrypt { get; set; } = true;
    public string? Description { get; set; }
}

public class UploadToCloudResponse
{
    public string CloudBackupId { get; set; } = string.Empty;
    public long SaveId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long UploadedSize { get; set; }
    public bool Compressed { get; set; }
    public bool Encrypted { get; set; }
    public DateTime UploadTime { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class DownloadFromCloudRequest
{
    public string TargetPath { get; set; } = string.Empty;
}

public class DownloadFromCloudResponse
{
    public string CloudBackupId { get; set; } = string.Empty;
    public string DownloadPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime DownloadTime { get; set; }
}

public class CloudStorageUsageDto
{
    public int UserId { get; set; }
    public decimal StorageUsedMB { get; set; }
    public decimal StorageLimitMB { get; set; }
    public decimal StorageUsedPercent { get; set; }
    public int TotalFiles { get; set; }
    public CloudFileInfo? LargestFile { get; set; }
    public CloudFileInfo? OldestFile { get; set; }
    public List<CloudFileInfo> RecentUploads { get; set; } = new();
}

public class CloudFileInfo
{
    public string CloudBackupId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public DateTime UploadTime { get; set; }
    public decimal FileSizeMB { get; set; }
}
