using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

// 游戏Mod列表响应
public class GameModsResponse
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public List<ModDetailDto> Mods { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
    public ModsSummary Summary { get; set; } = new();
}

// 详细的Mod信息
public class ModDetailDto
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime LastModified { get; set; }
    public decimal SizeGB { get; set; }
    public long InstallId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<long> Conflicts { get; set; } = new();
}

// Mod汇总信息
public class ModsSummary
{
    public int TotalMods { get; set; }
    public int EnabledMods { get; set; }
    public decimal TotalSizeGB { get; set; }
    public int ConflictsCount { get; set; }
}

// 安装Mod请求
public class InstallModRequest
{
    [Required]
    public long InstallId { get; set; }
    
    [Required]
    public string ModName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool AutoEnable { get; set; } = true;
}

// 安装Mod响应
public class InstallModResponse
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public long InstallId { get; set; }
    public string InstallStatus { get; set; } = string.Empty; // pending_manual_install, installed, failed
    public string? TargetPath { get; set; }
    public bool Enabled { get; set; }
    public DateTime InstalledAt { get; set; }
    public decimal SizeGB { get; set; }
    
    // 网页版手动安装指导
    public List<string> InstallInstructions { get; set; } = new();
    public string? DownloadUrl { get; set; }
    public bool WebLimitation { get; set; } = true; // 标识网页版限制
}

// 切换Mod状态请求
public class ToggleModRequest
{
    [Required]
    public bool Enabled { get; set; }
}

// 切换Mod状态响应
public class ToggleModResponse
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Mod冲突检测响应
public class ModConflictsResponse
{
    public long InstallId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public List<ModConflict> Conflicts { get; set; } = new();
    public int TotalConflicts { get; set; }
    public bool HasBlockingConflicts { get; set; }
}

// Mod冲突信息
public class ModConflict
{
    public int ConflictId { get; set; }
    public string Severity { get; set; } = string.Empty; // high, medium, low
    public List<ConflictingMod> Mods { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

// 冲突的Mod信息
public class ConflictingMod
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
}

// 卸载Mod请求
public class UninstallModRequest
{
    public bool DeleteFiles { get; set; } = false; // 网页版固定为false
}

// 卸载Mod响应
public class UninstallModResponse
{
    public long ModId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public bool DeletedFiles { get; set; } // 修改为与文档一致的字段名
    public decimal FreedSpaceGB { get; set; }
    public DateTime UninstalledAt { get; set; }
}
