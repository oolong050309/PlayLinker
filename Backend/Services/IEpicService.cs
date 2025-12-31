using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Epic Games API集成服务接口
/// </summary>
public interface IEpicService
{
    /// <summary>
    /// 检查令牌状态
    /// </summary>
    Task<EpicAuthResponseDto> CheckTokenStatus(int userId, int platformId = 2);

    /// <summary>
    /// Epic Games认证（通过授权码）
    /// </summary>
    Task<EpicAuthResponseDto> AuthenticateEpic(EpicAuthRequestDto request, int userId);

    /// <summary>
    /// 导入Epic Games数据
    /// </summary>
    Task<EpicImportResponseDto> ImportEpicData(EpicImportRequestDto request, int userId);

    /// <summary>
    /// 获取Epic Games用户信息
    /// </summary>
    Task<EpicUserDto?> GetEpicUser(string epicAccountId, int userId);

    /// <summary>
    /// 获取Epic Games游戏信息
    /// </summary>
    Task<EpicGameDto?> GetEpicGame(string gameId, int userId);

    /// <summary>
    /// 获取Epic Games用户的游戏列表
    /// </summary>
    Task<List<EpicGameDto>> GetEpicUserGames(string epicAccountId, int userId);
}

/// <summary>
/// Epic Games 认证响应DTO
/// </summary>
public class EpicAuthResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? EpicAccountId { get; set; }
    public bool TokenExists { get; set; }
    public bool NeedsAuth { get; set; }
}

