using Microsoft.AspNetCore.Mvc;
using PlayLinker.Models.DTOs;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

/// <summary>
/// Mod 浏览控制器
/// 聚合多个 Mod 平台的数据
/// </summary>
[ApiController]
[Route("api/v1/mod-explore")]
public class ModExploreController : ControllerBase
{
    private readonly IModExploreService _modExploreService;
    private readonly ILogger<ModExploreController> _logger;

    public ModExploreController(IModExploreService modExploreService, ILogger<ModExploreController> logger)
    {
        _modExploreService = modExploreService;
        _logger = logger;
    }

    /// <summary>
    /// 获取游戏支持的 Mod 来源
    /// </summary>
    /// <param name="gameId">游戏ID</param>
    /// <returns>支持的 Mod 平台列表</returns>
    [HttpGet("sources/{gameId}")]
    [ProducesResponseType(typeof(ApiResponse<GameModSourceDto>), 200)]
    public async Task<ActionResult<ApiResponse<GameModSourceDto>>> GetGameModSources(long gameId)
    {
        try
        {
            var result = await _modExploreService.GetGameModSourcesAsync(gameId);
            if (result == null)
            {
                return NotFound(ApiResponse<GameModSourceDto>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }
            return Ok(ApiResponse<GameModSourceDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mod sources for game {GameId}", gameId);
            return StatusCode(500, ApiResponse<GameModSourceDto>.ErrorResponse("ERR_INTERNAL", "获取 Mod 来源失败"));
        }
    }

    /// <summary>
    /// 获取 Mod 列表
    /// </summary>
    /// <param name="gameId">游戏ID</param>
    /// <param name="source">Mod来源: NexusMods, 3DM, GameBanana</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="sortBy">排序: downloads, updated, endorsements</param>
    /// <returns>Mod 列表</returns>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<ModExploreResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ModExploreResponse>>> GetMods(
        [FromQuery] long gameId,
        [FromQuery] string source = "NexusMods",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "downloads")
    {
        try
        {
            var request = new ModExploreRequest
            {
                GameId = gameId,
                Source = source,
                Page = page,
                PageSize = Math.Min(pageSize, 50),
                SortBy = sortBy
            };

            var result = await _modExploreService.GetModsAsync(request);
            return Ok(ApiResponse<ModExploreResponse>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mods for game {GameId} from {Source}", gameId, source);
            return StatusCode(500, ApiResponse<ModExploreResponse>.ErrorResponse("ERR_INTERNAL", "获取 Mod 列表失败"));
        }
    }

    /// <summary>
    /// 获取 Mod 详情
    /// </summary>
    /// <param name="source">Mod来源</param>
    /// <param name="modId">Mod ID</param>
    /// <param name="domain">域名（NexusMods 需要）</param>
    /// <returns>Mod 详情</returns>
    [HttpGet("detail")]
    [ProducesResponseType(typeof(ApiResponse<ExploreModDetailDto>), 200)]
    public async Task<ActionResult<ApiResponse<ExploreModDetailDto>>> GetModDetail(
        [FromQuery] string source,
        [FromQuery] string modId,
        [FromQuery] string? domain = null)
    {
        try
        {
            var result = await _modExploreService.GetModDetailAsync(source, modId, domain);
            if (result == null)
            {
                return NotFound(ApiResponse<ExploreModDetailDto>.ErrorResponse("ERR_MOD_NOT_FOUND", "Mod 不存在"));
            }
            return Ok(ApiResponse<ExploreModDetailDto>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mod detail {ModId} from {Source}", modId, source);
            return StatusCode(500, ApiResponse<ExploreModDetailDto>.ErrorResponse("ERR_INTERNAL", "获取 Mod 详情失败"));
        }
    }

    /// <summary>
    /// 搜索有 Mod 来源的游戏
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>游戏列表</returns>
    [HttpGet("games/search")]
    [ProducesResponseType(typeof(ApiResponse<ModGameSearchResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ModGameSearchResponse>>> SearchModGames(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(ApiResponse<ModGameSearchResponse>.SuccessResponse(new ModGameSearchResponse()));
            }

            var result = await _modExploreService.SearchModGamesAsync(query, page, pageSize);
            return Ok(ApiResponse<ModGameSearchResponse>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching mod games with query {Query}", query);
            return StatusCode(500, ApiResponse<ModGameSearchResponse>.ErrorResponse("ERR_INTERNAL", "搜索游戏失败"));
        }
    }

    /// <summary>
    /// 搜索 Mod
    /// </summary>
    /// <param name="source">Mod来源</param>
    /// <param name="query">搜索关键词</param>
    /// <param name="domain">域名（NexusMods 需要）</param>
    /// <param name="page">页码</param>
    /// <returns>搜索结果</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<ModExploreResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ModExploreResponse>>> SearchMods(
        [FromQuery] string source,
        [FromQuery] string query,
        [FromQuery] string? domain = null,
        [FromQuery] int page = 1)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(ApiResponse<ModExploreResponse>.ErrorResponse("ERR_INVALID_QUERY", "搜索关键词不能为空"));
            }

            var result = await _modExploreService.SearchModsAsync(source, query, domain, page);
            return Ok(ApiResponse<ModExploreResponse>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching mods with query {Query} from {Source}", query, source);
            return StatusCode(500, ApiResponse<ModExploreResponse>.ErrorResponse("ERR_INTERNAL", "搜索 Mod 失败"));
        }
    }
}
