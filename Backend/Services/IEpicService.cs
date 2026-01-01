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
    /// <param name="epicAccountId">Epic账户ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="includeGamesCount">是否获取游戏数量（可能较慢，默认false）</param>
    Task<EpicUserDto?> GetEpicUser(string epicAccountId, int userId, bool includeGamesCount = false);

    /// <summary>
    /// 获取Epic Games游戏信息
    /// </summary>
    Task<EpicGameDto?> GetEpicGame(string gameId, int userId);

    /// <summary>
    /// 获取Epic Games用户的游戏列表
    /// </summary>
    Task<List<EpicGameDto>> GetEpicUserGames(string epicAccountId, int userId);

    /// <summary>
    /// 获取游戏详细信息（包括开发商、发行商、描述等）
    /// </summary>
    Task<EpicGameDto?> GetGameDetails(string namespaceId, string? offerId);

    /// <summary>
    /// 获取游戏成就列表
    /// </summary>
    Task<EpicAchievementsInfoDto?> GetGameAchievements(string namespaceId);
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

