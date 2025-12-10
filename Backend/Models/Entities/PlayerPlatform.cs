using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 玩家在某一平台的账号资料
/// </summary>
[PrimaryKey("PlatformUserId", "PlatformId")]
[Table("player_platform")]
[Index("PlatformId", Name = "platform_id")]
public partial class PlayerPlatform
{
    /// <summary>
    /// 平台侧用户标识（平台唯一）
    /// </summary>
    [Key]
    [Column("platform_user_id")]
    [StringLength(128)]
    public string PlatformUserId { get; set; } = null!;

    /// <summary>
    /// 平台ID（标识Steam/Epic等）
    /// </summary>
    [Key]
    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("profile_name")]
    [StringLength(128)]
    public string ProfileName { get; set; } = null!;

    [Column("profile_url")]
    [StringLength(2048)]
    public string? ProfileUrl { get; set; }

    [Column("account_created", TypeName = "datetime")]
    public DateTime? AccountCreated { get; set; }

    [Column("country")]
    [StringLength(50)]
    public string? Country { get; set; }

    [ForeignKey("PlatformId")]
    [InverseProperty("PlayerPlatforms")]
    public virtual Platform Platform { get; set; } = null!;

    [InverseProperty("PlayerPlatform")]
    public virtual ICollection<UserPlatformBinding> UserPlatformBindings { get; set; } = new List<UserPlatformBinding>();

    [InverseProperty("PlayerPlatform")]
    public virtual ICollection<UserPlatformLibrary> UserPlatformLibraries { get; set; } = new List<UserPlatformLibrary>();
}
