using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

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
    /// 获取当前用户ID
    /// </summary>
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
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

            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized(ApiResponse<PaginatedResponse<LocalSaveListDto>>.ErrorResponse(
                    "ERR_UNAUTHORIZED", "请先登录"));
            }

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
                    IsBackupLocal = lsf.IsBackupLocal,
                    Metadata = null // 网页版不支持存档元数据解析
                })
                .ToListAsync();

            // 计算汇总信息（基于所有存档，不只是当前页）
            var allSaves = await query.ToListAsync();
            var summary = new LocalSavesSummary
            {
                TotalSaves = total,
                TotalSizeMB = Math.Round(allSaves.Sum(s => s.FileSize) / 1024.0m / 1024, 2),
                BackedUpCount = allSaves.Count(s => s.IsBackupLocal)
            };

            var response = new PaginatedResponse<LocalSaveListDto>
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                },
                Summary = summary
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
    /// 手动添加本地存档记录
    /// </summary>
    /// <param name="request">添加存档请求</param>
    /// <returns>添加结果</returns>
    /// <remarks>
    /// 网页版功能：用户手动选择存档文件，系统记录文件信息到数据库
    /// 不上传文件本身，只记录文件路径、大小等元数据
    /// </remarks>
    [HttpPost("local")]
    [ProducesResponseType(typeof(ApiResponse<LocalSaveListDto>), 201)]
    public async Task<ActionResult<ApiResponse<LocalSaveListDto>>> AddLocalSave([FromBody] AddLocalSaveRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized(ApiResponse<LocalSaveListDto>.ErrorResponse(
                    "ERR_UNAUTHORIZED", "请先登录"));
            }

            // 验证游戏安装记录是否存在
            var install = await _context.LocalGameInstalls
                .Include(lgi => lgi.Game)
                .FirstOrDefaultAsync(lgi => lgi.InstallId == request.InstallId && lgi.UserId == userId);

            if (install == null)
            {
                return NotFound(ApiResponse<LocalSaveListDto>.ErrorResponse("ERR_INSTALL_NOT_FOUND", "游戏安装记录不存在"));
            }

            // 检查是否已存在相同路径的存档记录
            var existingSave = await _context.LocalSaveFiles
                .FirstOrDefaultAsync(lsf => lsf.InstallId == request.InstallId && lsf.FilePath == request.FilePath);

            if (existingSave != null)
            {
                return Conflict(ApiResponse<LocalSaveListDto>.ErrorResponse("ERR_SAVE_EXISTS", "该存档记录已存在"));
            }

            // 创建存档记录
            var save = new LocalSaveFile
            {
                InstallId = request.InstallId,
                FilePath = request.FilePath,
                FileSize = (int)request.FileSize, // 转换为int，数据库字段类型为int
                UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow,
                IsBackupLocal = false
            };

            _context.LocalSaveFiles.Add(save);
            await _context.SaveChangesAsync();

            // 返回创建的存档信息
            var result = new LocalSaveListDto
            {
                SaveId = save.SaveId,
                GameId = install.GameId,
                GameName = install.Game.Name,
                InstallId = save.InstallId,
                FilePath = save.FilePath,
                FileSize = save.FileSize,
                FileSizeMB = Math.Round((decimal)save.FileSize / 1024 / 1024, 2),
                UpdatedAt = save.UpdatedAt,
                IsBackupLocal = save.IsBackupLocal,
                Metadata = null
            };

            return CreatedAtAction(nameof(GetLocalSaves), new { }, 
                ApiResponse<LocalSaveListDto>.SuccessResponse(result, "存档记录添加成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding local save");
            return StatusCode(500, ApiResponse<LocalSaveListDto>.ErrorResponse(
                "ERR_ADD_SAVE_FAILED", "添加存档记录失败"));
        }
    }

    /// <summary>
    /// 备份存档
    /// </summary>
    /// <param name="request">备份请求</param>
    /// <returns>备份结果</returns>
    /// <remarks>
    /// ⚠️ 网页版限制：此接口仅返回模拟数据，不执行实际文件备份操作
    /// 原因：浏览器无法访问本地文件系统进行文件复制和压缩
    /// 真正的文件备份功能需要本地客户端版本
    /// </remarks>
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

            // ⚠️ 网页版：仅模拟备份逻辑，不执行实际文件操作
            // TODO: 本地客户端版本需要实现真实的文件复制和压缩
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
    /// <remarks>
    /// ⚠️ 网页版限制：此接口仅返回模拟数据，不执行实际文件恢复操作
    /// 原因：浏览器无法读取备份文件并写入到存档位置
    /// 真正的文件恢复功能需要本地客户端版本
    /// </remarks>
    [HttpPost("restore/{id}")]
    [ProducesResponseType(typeof(ApiResponse<RestoreSaveResponse>), 200)]
    public Task<ActionResult<ApiResponse<RestoreSaveResponse>>> RestoreSave(
        string id, 
        [FromBody] RestoreSaveRequest request)
    {
        try
        {
            // ⚠️ 网页版：仅模拟恢复逻辑，不执行实际文件操作
            // TODO: 本地客户端版本需要实现真实的文件恢复
            var response = new RestoreSaveResponse
            {
                BackupId = id,
                SaveId = 1,
                RestoredPath = "C:\\Users\\Player\\Saved Games\\game\\save001.dat",
                RestoredAt = DateTime.UtcNow
            };

            return Task.FromResult<ActionResult<ApiResponse<RestoreSaveResponse>>>(Ok(ApiResponse<RestoreSaveResponse>.SuccessResponse(response, "存档恢复成功")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring save from backup {BackupId}", id);
            return Task.FromResult<ActionResult<ApiResponse<RestoreSaveResponse>>>(StatusCode(500, ApiResponse<RestoreSaveResponse>.ErrorResponse(
                "ERR_RESTORE_FAILED", "恢复存档失败")));
        }
    }

    /// <summary>
    /// 删除存档
    /// </summary>
    /// <param name="id">存档ID</param>
    /// <param name="request">删除请求</param>
    /// <returns>删除结果</returns>
    /// <remarks>
    /// ⚠️ 网页版限制：仅删除数据库记录，不删除物理文件
    /// deleteFile 和 deleteBackups 参数在网页版中被忽略
    /// 删除本地文件功能需要本地客户端版本
    /// </remarks>
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

            // 网页版：仅删除数据库记录，不删除物理文件
            // request.DeleteFile 和 request.DeleteBackups 参数被忽略
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
