using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// PSN API集成服务接口
/// 提供PSN用户信息、游戏信息、奖杯数据导入等功能
/// </summary>
public interface IPsnService
{
    /// <summary>
    /// 导入PSN数据（需要用户ID）
    /// </summary>
    Task<PsnImportResponseDto> ImportPsnData(PsnImportRequestDto request, int userId);

    /// <summary>
    /// 获取PSN用户信息（需要用户ID）
    /// </summary>
    Task<PsnUserDto?> GetPsnUser(string onlineId, int userId);

    /// <summary>
    /// 获取PSN游戏信息（需要用户ID）
    /// </summary>
    Task<PsnGameDto?> GetPsnGame(string titleId, int userId);

    /// <summary>
    /// 获取PSN用户奖杯（需要用户ID）
    /// </summary>
    Task<PsnUserTrophiesResponseDto> GetPsnUserTrophies(string onlineId, int userId);

    /// <summary>
    /// 获取PSN用户的游戏列表(用于导入)（需要用户ID）
    /// </summary>
    Task<List<PsnGameDto>> GetPsnUserGames(string onlineId, int userId);

    /// <summary>
    /// 执行PSN认证
    /// </summary>
    Task<PsnAuthResponseDto> AuthenticatePsn(PsnAuthRequestDto request, int userId);

    /// <summary>
    /// 检查令牌状态（需要用户ID）
    /// </summary>
    Task<PsnAuthResponseDto> CheckTokenStatus(int userId, int platformId = 6);
}
