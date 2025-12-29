using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Steam API集成服务接口
/// 提供Steam用户信息、游戏信息、数据导入等功能
/// </summary>
public interface ISteamService
{
    /// <summary>
    /// 导入Steam数据（需要用户的API Key）
    /// </summary>
    Task<SteamImportResponseDto> ImportSteamData(SteamImportRequestDto request, string apiKey);

    /// <summary>
    /// 获取Steam用户信息
    /// </summary>
    Task<SteamUserDto?> GetSteamUser(string steamId, string apiKey);

    /// <summary>
    /// 获取Steam游戏信息
    /// </summary>
    Task<SteamGameDto?> GetSteamGame(int appId, string apiKey);

    /// <summary>
    /// 获取游戏详情(从Steam API)
    /// </summary>
    Task<object?> GetGameDetails(int appId);

    /// <summary>
    /// 获取最受欢迎的游戏
    /// </summary>
    Task<object?> GetMostPlayedGames(int count, string apiKey);

    /// <summary>
    /// 获取游戏评价
    /// </summary>
    Task<object?> GetGameReviews(int appId);

    /// <summary>
    /// 获取游戏成就信息
    /// </summary>
    Task<object?> GetGameAchievements(int appId, string apiKey);

    /// <summary>
    /// 获取游戏新闻
    /// </summary>
    /// <param name="appId">Steam AppID</param>
    /// <param name="count">获取的新闻数量，0表示获取所有新闻</param>
    /// <param name="apiKey">Steam API Key</param>
    /// <returns>新闻数据和是否获取完所有新闻的标识</returns>
    Task<(object? NewsData, bool IsAll)> GetGameNews(int appId, int count, string apiKey);
}

