using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 家长监管关系表
/// </summary>
[Table("parental_control_relationship")]
[Index("ChildUserId", Name = "child_user_id", IsUnique = true)]
[Index("ParentUserId", Name = "parent_user_id")]
public partial class ParentalControlRelationship
{
    [Key]
    [Column("relationship_id")]
    public int RelationshipId { get; set; }

    [Column("parent_user_id")]
    public int ParentUserId { get; set; }

    [Column("child_user_id")]
    public int ChildUserId { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ChildUserId")]
    [InverseProperty("ParentalControlRelationshipChildUser")]
    public virtual User ChildUser { get; set; } = null!;

    [ForeignKey("ParentUserId")]
    [InverseProperty("ParentalControlRelationshipParentUsers")]
    public virtual User ParentUser { get; set; } = null!;
}
