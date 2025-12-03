using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Steam API集成服务接口
/// 提供Steam用户信息、游戏信息、数据导入等功能
/// </summary>
public interface ISteamService
{
    /// <summary>
    /// 导入Steam数据
    /// </summary>
    Task<SteamImportResponseDto> ImportSteamData(SteamImportRequestDto request);

    /// <summary>
    /// 获取Steam用户信息
    /// </summary>
    Task<SteamUserDto?> GetSteamUser(string steamId);

    /// <summary>
    /// 获取Steam游戏信息
    /// </summary>
    Task<SteamGameDto?> GetSteamGame(int appId);

    /// <summary>
    /// 获取游戏详情(从Steam API)
    /// </summary>
    Task<object?> GetGameDetails(int appId);

    /// <summary>
    /// 获取最受欢迎的游戏
    /// </summary>
    Task<object?> GetMostPlayedGames(int count = 50);

    /// <summary>
    /// 获取游戏评价
    /// </summary>
    Task<object?> GetGameReviews(int appId);

    /// <summary>
    /// 获取游戏成就信息
    /// </summary>
    Task<object?> GetGameAchievements(int appId);

    /// <summary>
    /// 获取游戏新闻
    /// </summary>
    Task<object?> GetGameNews(int appId, int count = 20);
}

