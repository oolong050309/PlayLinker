using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PlayLinker.Models.Entities;

/// <summary>
/// 登录权限控制表
/// </summary>
[Table("role")]
[Index("RoleName", Name = "idx_role_name", IsUnique = true)]
public partial class Role
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("role_name", TypeName = "enum('user','parent','admin')")]
    public string RoleName { get; set; } = null!;

    [Column("role_desc")]
    [StringLength(300)]
    public string? RoleDesc { get; set; }

    [InverseProperty("Role")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
