using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Xbox API集成服务接口
/// 提供Xbox用户信息、游戏信息、数据导入等功能
/// </summary>
public interface IXboxService
{
    /// <summary>
    /// 导入Xbox数据
    /// </summary>
    Task<XboxImportResponseDto> ImportXboxData(XboxImportRequestDto request);

    /// <summary>
    /// 获取Xbox用户信息
    /// </summary>
    Task<XboxUserDto?> GetXboxUser(string xuid);

    /// <summary>
    /// 获取Xbox游戏信息
    /// </summary>
    Task<XboxGameDto?> GetXboxGame(string titleId);

    /// <summary>
    /// 获取Xbox用户成就
    /// </summary>
    Task<List<XboxUserAchievementDto>> GetXboxUserAchievements(string xuid);

    /// <summary>
    /// 获取Xbox用户的游戏列表（用于导入）
    /// </summary>
    Task<List<XboxGameDto>> GetXboxUserGames(string xuid);

    /// <summary>
    /// 执行Xbox认证
    /// </summary>
    Task<XboxAuthResponseDto> AuthenticateXbox(XboxAuthRequestDto request);

    /// <summary>
    /// 检查令牌状态
    /// </summary>
    Task<XboxAuthResponseDto> CheckTokenStatus(string? tokensPath = null);
}

