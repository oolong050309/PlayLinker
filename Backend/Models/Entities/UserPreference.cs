using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 用户偏好表-AI推荐算法支撑数据
/// </summary>
[Table("user_preference")]
[Index("UpdatedAt", Name = "idx_updated_at")]
[Index("UserId", Name = "user_id", IsUnique = true)]
public partial class UserPreference
{
    [Key]
    [Column("preference_id")]
    public int PreferenceId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// 偏好游玩时长区间（如&quot;1-3小时/天&quot;）
    /// </summary>
    [Column("playtime_range")]
    [StringLength(50)]
    public string? PlaytimeRange { get; set; }

    /// <summary>
    /// 价格敏感度（1高/2中/3低）
    /// </summary>
    [Column("price_sensitivity")]
    public int PriceSensitivity { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Preference")]
    public virtual ICollection<PreferenceGenre> PreferenceGenres { get; set; } = new List<PreferenceGenre>();

    [ForeignKey("UserId")]
    [InverseProperty("UserPreference")]
    public virtual User User { get; set; } = null!;
}
