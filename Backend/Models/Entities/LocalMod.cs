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

    [ForeignKey("InstallId")]
    [InverseProperty("LocalMods")]
    public virtual LocalGameInstall Install { get; set; } = null!;
}
