using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 本地存档
/// </summary>
[Table("local_save_file")]
[Index("FilePath", Name = "file_path", IsUnique = true)]
[Index("InstallId", Name = "install_id")]
public partial class LocalSaveFile
{
    [Key]
    [Column("save_id")]
    public long SaveId { get; set; }

    [Column("file_path")]
    [StringLength(750)]
    public string FilePath { get; set; } = null!;

    /// <summary>
    /// 文件大小KB
    /// </summary>
    [Column("file_size")]
    public int FileSize { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 0代表不备份，1代表备份
    /// </summary>
    [Column("is_backup_local")]
    public bool IsBackupLocal { get; set; }

    [Column("install_id")]
    public long InstallId { get; set; }

    [ForeignKey("InstallId")]
    [InverseProperty("LocalSaveFiles")]
    public virtual LocalGameInstall Install { get; set; } = null!;
}
