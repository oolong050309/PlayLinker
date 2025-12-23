using System.ComponentModel.DataAnnotations;

namespace PlayLinker.Models.DTOs;

/// <summary>
/// 用户个人信息DTO
/// </summary>
public class UserProfileDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 性别 (1男/2女/0未知)
    /// </summary>
    public int Gender { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 更新用户个人信息请求DTO
/// </summary>
public class UpdateUserProfileRequestDto
{
    /// <summary>
    /// 邮箱
    /// </summary>
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? Phone { get; set; }

    /// <summary>
    /// 性别 (1男/2女/0未知)
    /// </summary>
    [Range(0, 2, ErrorMessage = "性别值必须为0、1或2")]
    public int? Gender { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    [Url(ErrorMessage = "头像URL格式不正确")]
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// 修改密码请求DTO
/// </summary>
public class ChangePasswordRequestDto
{
    /// <summary>
    /// 旧密码
    /// </summary>
    [Required(ErrorMessage = "旧密码不能为空")]
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>
    /// 新密码
    /// </summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "新密码长度必须在8-128之间")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 更新用户角色请求DTO
/// </summary>
public class UpdateUserRoleRequestDto
{
    /// <summary>
    /// 角色名称 (user, parent, admin)
    /// </summary>
    [Required(ErrorMessage = "角色不能为空")]
    [RegularExpression("^(user|parent|admin)$", ErrorMessage = "角色必须是 user、parent 或 admin")]
    public string Role { get; set; } = string.Empty;
}

