using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 游戏与分类关联表
/// </summary>
[Table("game_categories")]
[Index("CategoryId", Name = "category_id")]
[Index("GameId", "CategoryId", Name = "uk_game_category", IsUnique = true)]
public partial class GameCategory
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("game_id")]
    public long GameId { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("GameCategories")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("GameId")]
    [InverseProperty("GameCategories")]
    public virtual Game Game { get; set; } = null!;
}
