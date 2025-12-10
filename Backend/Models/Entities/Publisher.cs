using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏发行商
/// </summary>
[Table("publishers")]
[Index("Name", Name = "name", IsUnique = true)]
public partial class Publisher
{
    [Key]
    [Column("publisher_id")]
    public int PublisherId { get; set; }

    [Column("name")]
    [StringLength(20)]
    public string Name { get; set; } = null!;

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [InverseProperty("Publisher")]
    public virtual ICollection<GamePublisher> GamePublishers { get; set; } = new List<GamePublisher>();
}
