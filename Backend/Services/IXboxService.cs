using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Xbox API集成服务接口
/// 提供Xbox用户信息、游戏信息、数据导入等功能
/// </summary>
public interface IXboxService
{
    /// <summary>
    /// 导入Xbox数据（需要用户ID以获取令牌）
    /// </summary>
    Task<XboxImportResponseDto> ImportXboxData(XboxImportRequestDto request, int userId);

    /// <summary>
    /// 获取Xbox用户信息（需要用户ID以获取令牌）
    /// </summary>
    Task<XboxUserDto?> GetXboxUser(string xuid, int userId);

    /// <summary>
    /// 获取Xbox游戏信息（需要用户ID以获取令牌）
    /// </summary>
    Task<XboxGameDto?> GetXboxGame(string titleId, int userId);

    /// <summary>
    /// 获取Xbox用户成就（需要用户ID以获取令牌）
    /// </summary>
    Task<List<XboxUserAchievementDto>> GetXboxUserAchievements(string xuid, int userId);

    /// <summary>
    /// 获取Xbox用户的游戏列表（用于导入）（需要用户ID以获取令牌）
    /// </summary>
    Task<List<XboxGameDto>> GetXboxUserGames(string xuid, int userId);

    /// <summary>
    /// 执行Xbox认证（首次认证，创建令牌）
    /// </summary>
    Task<XboxAuthResponseDto> AuthenticateXbox(XboxAuthRequestDto request, int userId);

    /// <summary>
    /// 检查令牌状态（需要用户ID）
    /// </summary>
    Task<XboxAuthResponseDto> CheckTokenStatus(int userId, int platformId = 7);
}

