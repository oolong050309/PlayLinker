using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏价格历史表
/// </summary>
[Table("price_history")]
[Index("GameId", "PlatformId", Name = "idx_game_platform")]
[Index("RecordDate", Name = "idx_record_date")]
[Index("PlatformId", Name = "platform_id")]
public partial class PriceHistory
{
    [Key]
    [Column("price_id")]
    public long PriceId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("current_price")]
    [Precision(10, 2)]
    public decimal CurrentPrice { get; set; }

    [Column("original_price")]
    [Precision(10, 2)]
    public decimal OriginalPrice { get; set; }

    /// <summary>
    /// 0-100
    /// </summary>
    [Column("discount_rate")]
    public int DiscountRate { get; set; }

    [Column("is_discount")]
    public bool IsDiscount { get; set; }

    [Column("record_date", TypeName = "datetime")]
    public DateTime RecordDate { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("PriceHistories")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("PlatformId")]
    [InverseProperty("PriceHistories")]
    public virtual Platform Platform { get; set; } = null!;

    [InverseProperty("Price")]
    public virtual ICollection<PriceAlertLog> PriceAlertLogs { get; set; } = new List<PriceAlertLog>();
}
