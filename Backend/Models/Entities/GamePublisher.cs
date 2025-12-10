using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与发行商关联表
/// </summary>
[Table("game_publishers")]
[Index("PublisherId", Name = "game_publishers_ibfk_2")]
[Index("GameId", "PublisherId", Name = "uk_game_publisher", IsUnique = true)]
public partial class GamePublisher
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("publisher_id")]
    public int PublisherId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("GameId")]
    [InverseProperty("GamePublishers")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("PublisherId")]
    [InverseProperty("GamePublishers")]
    public virtual Publisher Publisher { get; set; } = null!;
}
