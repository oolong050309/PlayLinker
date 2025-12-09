using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// GOG API集成服务接口
/// 提供GOG用户信息、游戏信息、数据导入等功能
/// </summary>
public interface IGogService
{
    /// <summary>
    /// 导入GOG数据
    /// </summary>
    Task<GogImportResponseDto> ImportGogData(GogImportRequestDto request);

    /// <summary>
    /// 获取GOG用户信息
    /// </summary>
    Task<GogUserDto?> GetGogUser(string gogUserId);

    /// <summary>
    /// 获取GOG游戏信息
    /// </summary>
    Task<GogGameDto?> GetGogGame(string gogGameId);

    /// <summary>
    /// 获取GOG用户的游戏列表(用于导入)
    /// </summary>
    Task<List<GogGameDto>> GetGogUserGames(string gogUserId);

    /// <summary>
    /// 执行GOG认证
    /// </summary>
    Task<GogAuthResponseDto> AuthenticateGog(GogAuthRequestDto request);

    /// <summary>
    /// 检查令牌状态
    /// </summary>
    Task<GogAuthResponseDto> CheckTokenStatus(string? tokensPath = null);
}


