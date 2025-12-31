using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Mod 浏览服务接口
/// </summary>
public interface IModExploreService
{
    /// <summary>
    /// 获取 Mod 列表
    /// </summary>
    Task<ModExploreResponse> GetModsAsync(ModExploreRequest request);

    /// <summary>
    /// 获取 Mod 详情
    /// </summary>
    Task<ExploreModDetailDto?> GetModDetailAsync(string source, string modId, string? domain = null);

    /// <summary>
    /// 获取游戏支持的 Mod 来源
    /// </summary>
    Task<GameModSourceDto?> GetGameModSourcesAsync(long gameId);

    /// <summary>
    /// 搜索 Mod
    /// </summary>
    Task<ModExploreResponse> SearchModsAsync(string source, string query, string? domain = null, int page = 1);
}
