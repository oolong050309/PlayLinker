using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// PSN API集成服务接口
/// 提供PSN用户信息、游戏信息、奖杯数据导入等功能
/// </summary>
public interface IPsnService
{
    /// <summary>
    /// 导入PSN数据
    /// </summary>
    Task<PsnImportResponseDto> ImportPsnData(PsnImportRequestDto request);

    /// <summary>
    /// 获取PSN用户信息
    /// </summary>
    Task<PsnUserDto?> GetPsnUser(string onlineId);

    /// <summary>
    /// 获取PSN游戏信息
    /// </summary>
    Task<PsnGameDto?> GetPsnGame(string titleId);

    /// <summary>
    /// 获取PSN用户奖杯
    /// </summary>
    Task<PsnUserTrophiesResponseDto> GetPsnUserTrophies(string onlineId);

    /// <summary>
    /// 获取PSN用户的游戏列表(用于导入)
    /// </summary>
    Task<List<PsnGameDto>> GetPsnUserGames(string onlineId);

    /// <summary>
    /// 执行PSN认证
    /// </summary>
    Task<PsnAuthResponseDto> AuthenticatePsn(PsnAuthRequestDto request);

    /// <summary>
    /// 检查令牌状态
    /// </summary>
    Task<PsnAuthResponseDto> CheckTokenStatus(string? tokensPath = null);
}
