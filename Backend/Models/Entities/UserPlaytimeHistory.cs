using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlayLinker.Models.Entities;

[Table("user_playtime_history")]
public class UserPlaytimeHistory
{
    [Key]
    [Column("history_id")]
    public long HistoryId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [Column("playtime_forever")]
    public int PlaytimeForever { get; set; } // 总时长(分钟)

    [Column("playtime_2weeks")]
    public int Playtime2Weeks { get; set; } // 过去两周时长(分钟)

    [Column("record_date")]
    public DateTime RecordDate { get; set; } // 记录日期 (yyyy-MM-dd)

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("GameId")]
    public virtual Game? Game { get; set; }
}