using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 云端备份存档
/// </summary>
[Table("cloud_save_backup")]
[Index("GameId", Name = "game_id")]
[Index("UploadTime", Name = "idx_upload_time")]
[Index("UserId", Name = "idx_user_id")]
[Index("StorageUrl", Name = "storage_url", IsUnique = true)]
public partial class CloudSaveBackup
{
    [Key]
    [Column("cloud_backup_id")]
    [StringLength(20)]
    public string CloudBackupId { get; set; } = null!;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("upload_time", TypeName = "datetime")]
    public DateTime UploadTime { get; set; }

    /// <summary>
    /// 大小MB
    /// </summary>
    [Column("file_size")]
    public int FileSize { get; set; }

    [Column("storage_url")]
    [StringLength(750)]
    public string StorageUrl { get; set; } = null!;

    [ForeignKey("GameId")]
    [InverseProperty("CloudSaveBackups")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("CloudSaveBackups")]
    public virtual User User { get; set; } = null!;
}
