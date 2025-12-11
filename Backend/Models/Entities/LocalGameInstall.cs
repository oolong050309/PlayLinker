using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 本地安装信息
/// </summary>
[Table("local_game_install")]
[Index("GameId", Name = "game_id")]
[Index("UserId", Name = "idx_user_id")]
[Index("InstallPath", Name = "install_path", IsUnique = true)]
[Index("PlatformId", Name = "platform_id")]
public partial class LocalGameInstall
{
    [Key]
    [Column("install_id")]
    public long InstallId { get; set; }

    [Column("platform_id")]
    public int? PlatformId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("install_path")]
    [StringLength(750)]
    public string InstallPath { get; set; } = null!;

    [Column("detected_time", TypeName = "datetime")]
    public DateTime DetectedTime { get; set; }

    [Column("version")]
    [StringLength(100)]
    public string Version { get; set; } = null!;

    [Column("size_gb", TypeName = "decimal(10, 2)")]
    public decimal SizeGb { get; set; }

    [Column("last_played", TypeName = "datetime")]
    public DateTime? LastPlayed { get; set; }

    [Column("executable_path")]
    [StringLength(750)]
    public string? ExecutablePath { get; set; }

    [Column("config_path")]
    [StringLength(750)]
    public string? ConfigPath { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("LocalGameInstalls")]
    public virtual Game Game { get; set; } = null!;

    [InverseProperty("Install")]
    public virtual ICollection<LocalMod> LocalMods { get; set; } = new List<LocalMod>();

    [InverseProperty("Install")]
    public virtual ICollection<LocalSaveFile> LocalSaveFiles { get; set; } = new List<LocalSaveFile>();

    [ForeignKey("PlatformId")]
    [InverseProperty("LocalGameInstalls")]
    public virtual Platform? Platform { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("LocalGameInstalls")]
    public virtual User User { get; set; } = null!;
}
