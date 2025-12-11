using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/local")]
public class LocalGamesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<LocalGamesController> _logger;

    public LocalGamesController(PlayLinkerDbContext context, ILogger<LocalGamesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 扫描本地游戏
    /// </summary>
    /// <param name="request">扫描请求参数</param>
    /// <returns>扫描结果</returns>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(ApiResponse<ScanLocalGamesResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ScanLocalGamesResponse>>> ScanLocalGames([FromBody] ScanLocalGamesRequest request)
    {
        try
        {
            var scanId = $"scan_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var startTime = DateTime.UtcNow;
            var gamesFound = new List<LocalGameFoundDto>();

            // 模拟扫描逻辑（实际项目中需要实现真实的文件系统扫描）
            // 这里仅作为示例，返回空列表
            _logger.LogInformation("Scanning directories: {Directories}", string.Join(", ", request.Directories));

            var scanDuration = (DateTime.UtcNow - startTime).TotalSeconds;

            var response = new ScanLocalGamesResponse
            {
                ScanId = scanId,
                GamesFound = gamesFound,
                TotalFound = gamesFound.Count,
                ScanDuration = scanDuration,
                ScannedDirectories = request.Directories.Count
            };

            return Ok(ApiResponse<ScanLocalGamesResponse>.SuccessResponse(response, "扫描完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning local games");
            return StatusCode(500, ApiResponse<ScanLocalGamesResponse>.ErrorResponse(
                "ERR_SCAN_FAILED", "扫描失败"));
        }
    }

    /// <summary>
    /// 获取本地游戏列表
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="sortBy">排序字段</param>
    /// <returns>本地游戏列表</returns>
    [HttpGet("games")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<LocalGameListDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LocalGameListDto>>>> GetLocalGames(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "detected_time")
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);
            page = Math.Max(page, 1);

            // 假设当前用户ID为1001（实际项目中应从JWT Token获取）
            int userId = 1001;

            var query = _context.LocalGameInstalls
                .Include(lgi => lgi.Game)
                .Include(lgi => lgi.Platform)
                .Include(lgi => lgi.LocalSaveFiles)
                .Include(lgi => lgi.LocalMods)
                .Where(lgi => lgi.UserId == userId)
                .AsQueryable();

            query = sortBy?.ToLower() switch
            {
                "name" => query.OrderBy(lgi => lgi.Game.Name),
                "detected_time" => query.OrderByDescending(lgi => lgi.DetectedTime),
                _ => query.OrderByDescending(lgi => lgi.DetectedTime)
            };

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(lgi => new LocalGameListDto
                {
                    InstallId = lgi.InstallId,
                    GameId = lgi.GameId,
                    GameName = lgi.Game.Name,
                    PlatformId = lgi.PlatformId,
                    PlatformName = lgi.Platform != null ? lgi.Platform.PlatformName : "Unknown",
                    InstallPath = lgi.InstallPath,
                    Version = lgi.Version,
                    SizeGB = 0, // 数据库表中没有此字段
                    DetectedTime = lgi.DetectedTime,
                    LastPlayed = null, // 数据库表中没有此字段
                    SavesCount = lgi.LocalSaveFiles.Count,
                    ModsCount = lgi.LocalMods.Count
                })
                .ToListAsync();

            var response = new PaginatedResponse<LocalGameListDto>
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<PaginatedResponse<LocalGameListDto>>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local games");
            return StatusCode(500, ApiResponse<PaginatedResponse<LocalGameListDto>>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "获取本地游戏列表失败"));
        }
    }

    /// <summary>
    /// 获取本地游戏详情
    /// </summary>
    /// <param name="id">安装ID</param>
    /// <returns>游戏详情</returns>
    [HttpGet("games/{id}")]
    [ProducesResponseType(typeof(ApiResponse<LocalGameDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<LocalGameDetailDto>>> GetLocalGameById(long id)
    {
        try
        {
            var install = await _context.LocalGameInstalls
                .Include(lgi => lgi.Game)
                .Include(lgi => lgi.Platform)
                .Include(lgi => lgi.LocalSaveFiles)
                .Include(lgi => lgi.LocalMods)
                .FirstOrDefaultAsync(lgi => lgi.InstallId == id);

            if (install == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            var detail = new LocalGameDetailDto
            {
                InstallId = install.InstallId,
                GameId = install.GameId,
                GameName = install.Game.Name,
                PlatformId = install.PlatformId,
                PlatformName = install.Platform != null ? install.Platform.PlatformName : "Unknown",
                InstallPath = install.InstallPath,
                Version = install.Version,
                SizeGB = 0, // 数据库表中没有此字段
                DetectedTime = install.DetectedTime,
                LastPlayed = null, // 数据库表中没有此字段
                ExecutablePath = null, // 数据库表中没有此字段
                ConfigPath = null, // 数据库表中没有此字段
                Saves = install.LocalSaveFiles.Select(sf => new SaveFileDto
                {
                    SaveId = sf.SaveId,
                    FilePath = sf.FilePath,
                    FileSize = sf.FileSize,
                    UpdatedAt = sf.UpdatedAt
                }).ToList(),
                Mods = install.LocalMods.Select(m => new ModDto
                {
                    ModId = m.ModId,
                    ModName = m.ModName,
                    Version = m.Version,
                    Enabled = m.Enabled
                }).ToList()
            };

            return Ok(ApiResponse<LocalGameDetailDto>.SuccessResponse(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local game detail for id {InstallId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "获取游戏详情失败"));
        }
    }

    /// <summary>
    /// 删除本地游戏
    /// </summary>
    /// <param name="id">安装ID</param>
    /// <param name="request">删除请求</param>
    /// <returns>删除结果</returns>
    [HttpDelete("games/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLocalGame(long id, [FromBody] DeleteGameRequest request)
    {
        try
        {
            var install = await _context.LocalGameInstalls.FindAsync(id);
            if (install == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            _context.LocalGameInstalls.Remove(install);
            await _context.SaveChangesAsync();

            var result = new
            {
                installId = id,
                gameName = install.Game?.Name ?? "Unknown",
                deletedFiles = request.DeleteFiles,
                removedAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "游戏已移除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting local game {InstallId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "删除游戏失败"));
        }
    }

    /// <summary>
    /// 更新游戏安装路径
    /// </summary>
    /// <param name="id">安装ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPatch("games/{id}/path")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateGamePath(long id, [FromBody] UpdatePathRequest request)
    {
        try
        {
            var install = await _context.LocalGameInstalls.FindAsync(id);
            if (install == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_GAME_NOT_FOUND", "游戏不存在"));
            }

            var oldPath = install.InstallPath;
            install.InstallPath = request.NewPath;
            await _context.SaveChangesAsync();

            var result = new
            {
                installId = id,
                oldPath = oldPath,
                newPath = request.NewPath,
                updatedAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "路径更新成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating game path for {InstallId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "更新路径失败"));
        }
    }
}

public class DeleteGameRequest
{
    public bool DeleteFiles { get; set; }
}

public class UpdatePathRequest
{
    public string NewPath { get; set; } = string.Empty;
}
