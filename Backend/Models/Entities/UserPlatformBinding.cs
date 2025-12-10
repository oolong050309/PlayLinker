using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 跨平台账号OAuth绑定记录
/// </summary>
[Table("user_platform_binding")]
[Index("UserId", Name = "idx_user_id")]
[Index("PlatformId", Name = "platform_id")]
[Index("PlatformUserId", "PlatformId", Name = "platform_user_id")]
[Index("UserId", "PlatformId", Name = "uk_user_platform", IsUnique = true)]
public partial class UserPlatformBinding
{
    [Key]
    [Column("binding_id")]
    public int BindingId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    /// <summary>
    /// 第三方平台用户ID（如SteamID）
    /// </summary>
    [Column("platform_user_id")]
    [StringLength(128)]
    public string PlatformUserId { get; set; } = null!;

    /// <summary>
    /// AES-256加密存储
    /// </summary>
    [Column("access_token")]
    [StringLength(128)]
    public string? AccessToken { get; set; }

    /// <summary>
    /// AES-256加密存储
    /// </summary>
    [Column("refresh_token")]
    [StringLength(128)]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 1已绑定/0已解绑
    /// </summary>
    [Required]
    [Column("binding_status")]
    public bool? BindingStatus { get; set; }

    [Column("binding_time", TypeName = "datetime")]
    public DateTime? BindingTime { get; set; }

    [Column("last_sync_time", TypeName = "datetime")]
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// 按平台API规则设置
    /// </summary>
    [Column("expire_time", TypeName = "datetime")]
    public DateTime ExpireTime { get; set; }

    [ForeignKey("PlatformId")]
    [InverseProperty("UserPlatformBindings")]
    public virtual Platform Platform { get; set; } = null!;

    [ForeignKey("PlatformUserId, PlatformId")]
    [InverseProperty("UserPlatformBindings")]
    public virtual PlayerPlatform PlayerPlatform { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserPlatformBindings")]
    public virtual User User { get; set; } = null!;
}
