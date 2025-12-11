using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/saves")]
public class SavesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<SavesController> _logger;

    public SavesController(PlayLinkerDbContext context, ILogger<SavesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取本地存档列表
    /// </summary>
    /// <param name="gameId">游戏ID（可选）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>本地存档列表</returns>
    [HttpGet("local")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<LocalSaveListDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LocalSaveListDto>>>> GetLocalSaves(
        [FromQuery] long? gameId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);
            page = Math.Max(page, 1);

            int userId = 1001; // 假设当前用户ID

            var query = _context.LocalSaveFiles
                .Include(lsf => lsf.Install)
                    .ThenInclude(lgi => lgi.Game)
                .Where(lsf => lsf.Install.UserId == userId)
                .AsQueryable();

            if (gameId.HasValue)
            {
                query = query.Where(lsf => lsf.Install.GameId == gameId.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(lsf => lsf.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(lsf => new LocalSaveListDto
                {
                    SaveId = lsf.SaveId,
                    GameId = lsf.Install.GameId,
                    GameName = lsf.Install.Game.Name,
                    InstallId = lsf.InstallId,
                    FilePath = lsf.FilePath,
                    FileSize = lsf.FileSize,
                    FileSizeMB = Math.Round((decimal)lsf.FileSize / 1024 / 1024, 2),
                    UpdatedAt = lsf.UpdatedAt,
                    IsBackupLocal = lsf.IsBackupLocal
                })
                .ToListAsync();

            var response = new PaginatedResponse<LocalSaveListDto>
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<PaginatedResponse<LocalSaveListDto>>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local saves");
            return StatusCode(500, ApiResponse<PaginatedResponse<LocalSaveListDto>>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "获取存档列表失败"));
        }
    }

    /// <summary>
    /// 备份存档
    /// </summary>
    /// <param name="request">备份请求</param>
    /// <returns>备份结果</returns>
    [HttpPost("backup")]
    [ProducesResponseType(typeof(ApiResponse<BackupSaveResponse>), 201)]
    public async Task<ActionResult<ApiResponse<BackupSaveResponse>>> BackupSave([FromBody] BackupSaveRequest request)
    {
        try
        {
            var save = await _context.LocalSaveFiles.FindAsync(request.SaveId);
            if (save == null)
            {
                return NotFound(ApiResponse<BackupSaveResponse>.ErrorResponse("ERR_SAVE_NOT_FOUND", "存档不存在"));
            }

            var backupId = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var backupPath = $"C:\\Users\\Player\\PlayLinker\\Backups\\save{request.SaveId}_{DateTime.UtcNow:yyyyMMdd}.bak";

            // 模拟备份逻辑
            var backupSize = request.Compress ? save.FileSize / 2 : save.FileSize;

            var response = new BackupSaveResponse
            {
                BackupId = backupId,
                SaveId = request.SaveId,
                BackupName = request.BackupName,
                BackupPath = backupPath,
                OriginalSize = save.FileSize,
                BackupSize = backupSize,
                Compressed = request.Compress,
                CreatedAt = DateTime.UtcNow
            };

            return CreatedAtAction(nameof(BackupSave), new { id = backupId }, 
                ApiResponse<BackupSaveResponse>.SuccessResponse(response, "存档备份成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error backing up save {SaveId}", request.SaveId);
            return StatusCode(500, ApiResponse<BackupSaveResponse>.ErrorResponse(
                "ERR_BACKUP_FAILED", "备份存档失败"));
        }
    }

    /// <summary>
    /// 恢复存档
    /// </summary>
    /// <param name="id">备份ID</param>
    /// <param name="request">恢复请求</param>
    /// <returns>恢复结果</returns>
    [HttpPost("restore/{id}")]
    [ProducesResponseType(typeof(ApiResponse<RestoreSaveResponse>), 200)]
    public async Task<ActionResult<ApiResponse<RestoreSaveResponse>>> RestoreSave(
        string id, 
        [FromBody] RestoreSaveRequest request)
    {
        try
        {
            // 模拟恢复逻辑
            var response = new RestoreSaveResponse
            {
                BackupId = id,
                SaveId = 1,
                RestoredPath = "C:\\Users\\Player\\Saved Games\\game\\save001.dat",
                RestoredAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<RestoreSaveResponse>.SuccessResponse(response, "存档恢复成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring save from backup {BackupId}", id);
            return StatusCode(500, ApiResponse<RestoreSaveResponse>.ErrorResponse(
                "ERR_RESTORE_FAILED", "恢复存档失败"));
        }
    }

    /// <summary>
    /// 删除存档
    /// </summary>
    /// <param name="id">存档ID</param>
    /// <param name="request">删除请求</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSave(long id, [FromBody] DeleteSaveRequest request)
    {
        try
        {
            var save = await _context.LocalSaveFiles.FindAsync(id);
            if (save == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_SAVE_NOT_FOUND", "存档不存在"));
            }

            _context.LocalSaveFiles.Remove(save);
            await _context.SaveChangesAsync();

            var result = new
            {
                saveId = id,
                deletedFile = request.DeleteFile,
                deletedBackups = request.DeleteBackups,
                deletedAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "存档已删除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting save {SaveId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_DELETE_FAILED", "删除存档失败"));
        }
    }
}
