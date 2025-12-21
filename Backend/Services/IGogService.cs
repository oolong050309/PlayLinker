using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// GOG API集成服务接口
/// 提供GOG用户信息、游戏信息、数据导入等功能
/// </summary>
public interface IGogService
{
    /// <summary>
    /// 导入GOG数据（需要用户ID）
    /// </summary>
    Task<GogImportResponseDto> ImportGogData(GogImportRequestDto request, int userId);

    /// <summary>
    /// 获取GOG用户信息（需要用户ID）
    /// </summary>
    Task<GogUserDto?> GetGogUser(string gogUserId, int userId);

    /// <summary>
    /// 获取GOG游戏信息（需要用户ID）
    /// </summary>
    Task<GogGameDto?> GetGogGame(string gogGameId, int userId);

    /// <summary>
    /// 获取GOG用户的游戏列表(用于导入)（需要用户ID）
    /// </summary>
    Task<List<GogGameDto>> GetGogUserGames(string gogUserId, int userId);

    /// <summary>
    /// 执行GOG认证
    /// </summary>
    Task<GogAuthResponseDto> AuthenticateGog(GogAuthRequestDto request, int userId);

    /// <summary>
    /// 检查令牌状态（需要用户ID）
    /// </summary>
    Task<GogAuthResponseDto> CheckTokenStatus(int userId, int platformId = 5);
}


