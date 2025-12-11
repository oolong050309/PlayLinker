using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 本地mod
/// </summary>
[Table("local_mod")]
[Index("InstallId", Name = "install_id")]
public partial class LocalMod
{
    [Key]
    [Column("mod_id")]
    public long ModId { get; set; }

    [Column("mod_name")]
    [StringLength(128)]
    public string ModName { get; set; } = null!;

    [Column("version")]
    public int Version { get; set; }

    [Column("file_path")]
    [StringLength(2048)]
    public string FilePath { get; set; } = null!;

    /// <summary>
    /// 0代表不启用，1代表启用
    /// </summary>
    [Required]
    [Column("enabled")]
    public bool? Enabled { get; set; }

    [Column("last_modified", TypeName = "datetime")]
    public DateTime LastModified { get; set; }

    [Column("install_id")]
    public long InstallId { get; set; }

    /// <summary>
    /// 安装状态：pending_manual_install, installed, failed
    /// </summary>
    [Column("install_status")]
    [StringLength(50)]
    public string InstallStatus { get; set; } = "pending_manual_install";

    /// <summary>
    /// 目标安装路径
    /// </summary>
    [Column("target_path")]
    [StringLength(2048)]
    public string? TargetPath { get; set; }

    /// <summary>
    /// Mod描述
    /// </summary>
    [Column("description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Mod作者
    /// </summary>
    [Column("author")]
    [StringLength(128)]
    public string? Author { get; set; }

    /// <summary>
    /// 下载URL
    /// </summary>
    [Column("download_url")]
    [StringLength(2048)]
    public string? DownloadUrl { get; set; }

    [ForeignKey("InstallId")]
    [InverseProperty("LocalMods")]
    public virtual LocalGameInstall Install { get; set; } = null!;
}
