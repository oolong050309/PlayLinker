using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/cloud")]
public class CloudController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<CloudController> _logger;

    public CloudController(PlayLinkerDbContext context, ILogger<CloudController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取云存档列表
    /// </summary>
    /// <param name="gameId">游戏ID（可选）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>云存档列表</returns>
    [HttpGet("saves")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<CloudSaveListDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<CloudSaveListDto>>>> GetCloudSaves(
        [FromQuery] long? gameId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);
            page = Math.Max(page, 1);

            int userId = 1001; // 假设当前用户ID

            var query = _context.CloudSaveBackups
                .Include(csb => csb.Game)
                .Where(csb => csb.UserId == userId)
                .AsQueryable();

            if (gameId.HasValue)
            {
                query = query.Where(csb => csb.GameId == gameId.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(csb => csb.UploadTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(csb => new CloudSaveListDto
                {
                    CloudBackupId = csb.CloudBackupId,
                    GameId = csb.GameId,
                    GameName = csb.Game.Name,
                    UserId = csb.UserId,
                    UploadTime = csb.UploadTime,
                    FileSize = csb.FileSize,
                    FileSizeMB = Math.Round((decimal)csb.FileSize / 1024 / 1024, 2),
                    StorageUrl = csb.StorageUrl,
                    ExpiresAt = csb.ExpiresAt
                })
                .ToListAsync();

            var response = new PaginatedResponse<CloudSaveListDto>
            {
                Items = items,
                Meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<PaginatedResponse<CloudSaveListDto>>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cloud saves");
            return StatusCode(500, ApiResponse<PaginatedResponse<CloudSaveListDto>>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "获取云存档列表失败"));
        }
    }

    /// <summary>
    /// 上传存档到云端
    /// </summary>
    /// <param name="request">上传请求</param>
    /// <returns>上传结果</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponse<UploadToCloudResponse>), 201)]
    public async Task<ActionResult<ApiResponse<UploadToCloudResponse>>> UploadToCloud([FromBody] UploadToCloudRequest request)
    {
        try
        {
            var save = await _context.LocalSaveFiles
                .Include(lsf => lsf.LocalGameInstall)
                .FirstOrDefaultAsync(lsf => lsf.SaveId == request.SaveId);

            if (save == null)
            {
                return NotFound(ApiResponse<UploadToCloudResponse>.ErrorResponse("ERR_SAVE_NOT_FOUND", "存档不存在"));
            }

            var cloudBackupId = $"cloud_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var storageUrl = $"https://storage.playlinker.com/saves/{cloudBackupId}";
            var uploadedSize = request.Compress ? save.FileSize / 2 : save.FileSize;

            var cloudBackup = new CloudSaveBackup
            {
                CloudBackupId = cloudBackupId,
                GameId = save.LocalGameInstall.GameId,
                UserId = save.UserId,
                UploadTime = DateTime.UtcNow,
                FileSize = save.FileSize,
                StorageUrl = storageUrl,
                Compressed = request.Compress,
                Encrypted = request.Encrypt,
                Description = request.Description,
                ExpiresAt = DateTime.UtcNow.AddYears(1)
            };

            _context.CloudSaveBackups.Add(cloudBackup);
            await _context.SaveChangesAsync();

            var response = new UploadToCloudResponse
            {
                CloudBackupId = cloudBackupId,
                SaveId = request.SaveId,
                StorageUrl = storageUrl,
                OriginalSize = save.FileSize,
                UploadedSize = uploadedSize,
                Compressed = request.Compress,
                Encrypted = request.Encrypt,
                UploadTime = DateTime.UtcNow,
                ExpiresAt = cloudBackup.ExpiresAt
            };

            return CreatedAtAction(nameof(UploadToCloud), new { id = cloudBackupId },
                ApiResponse<UploadToCloudResponse>.SuccessResponse(response, "存档上传成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading save {SaveId} to cloud", request.SaveId);
            return StatusCode(500, ApiResponse<UploadToCloudResponse>.ErrorResponse(
                "ERR_UPLOAD_FAILED", "上传存档失败"));
        }
    }

    /// <summary>
    /// 从云端下载存档
    /// </summary>
    /// <param name="id">云备份ID</param>
    /// <param name="request">下载请求</param>
    /// <returns>下载结果</returns>
    [HttpPost("download/{id}")]
    [ProducesResponseType(typeof(ApiResponse<DownloadFromCloudResponse>), 200)]
    public async Task<ActionResult<ApiResponse<DownloadFromCloudResponse>>> DownloadFromCloud(
        string id,
        [FromBody] DownloadFromCloudRequest request)
    {
        try
        {
            var cloudBackup = await _context.CloudSaveBackups.FindAsync(id);
            if (cloudBackup == null)
            {
                return NotFound(ApiResponse<DownloadFromCloudResponse>.ErrorResponse("ERR_BACKUP_NOT_FOUND", "云存档不存在"));
            }

            var response = new DownloadFromCloudResponse
            {
                CloudBackupId = id,
                DownloadPath = request.TargetPath,
                FileSize = cloudBackup.FileSize,
                DownloadTime = DateTime.UtcNow
            };

            return Ok(ApiResponse<DownloadFromCloudResponse>.SuccessResponse(response, "存档下载成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading cloud save {CloudBackupId}", id);
            return StatusCode(500, ApiResponse<DownloadFromCloudResponse>.ErrorResponse(
                "ERR_DOWNLOAD_FAILED", "下载存档失败"));
        }
    }

    /// <summary>
    /// 删除云存档
    /// </summary>
    /// <param name="id">云备份ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("saves/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCloudSave(string id)
    {
        try
        {
            var cloudBackup = await _context.CloudSaveBackups.FindAsync(id);
            if (cloudBackup == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_BACKUP_NOT_FOUND", "云存档不存在"));
            }

            var freedSpaceMB = Math.Round((decimal)cloudBackup.FileSize / 1024 / 1024, 2);

            _context.CloudSaveBackups.Remove(cloudBackup);
            await _context.SaveChangesAsync();

            var result = new
            {
                cloudBackupId = id,
                freedSpaceMB = freedSpaceMB,
                deletedAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "云存档已删除"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cloud save {CloudBackupId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_DELETE_FAILED", "删除云存档失败"));
        }
    }

    /// <summary>
    /// 获取存储空间使用情况
    /// </summary>
    /// <returns>存储空间信息</returns>
    [HttpGet("storage/usage")]
    [ProducesResponseType(typeof(ApiResponse<CloudStorageUsageDto>), 200)]
    public async Task<ActionResult<ApiResponse<CloudStorageUsageDto>>> GetStorageUsage()
    {
        try
        {
            int userId = 1001; // 假设当前用户ID

            var cloudSaves = await _context.CloudSaveBackups
                .Include(csb => csb.Game)
                .Where(csb => csb.UserId == userId)
                .ToListAsync();

            var totalSize = cloudSaves.Sum(csb => csb.FileSize);
            var storageLimitMB = 1024m; // 1GB限制
            var storageUsedMB = Math.Round((decimal)totalSize / 1024 / 1024, 2);

            var usage = new CloudStorageUsageDto
            {
                UserId = userId,
                StorageUsedMB = storageUsedMB,
                StorageLimitMB = storageLimitMB,
                StorageUsedPercent = Math.Round((storageUsedMB / storageLimitMB) * 100, 2),
                TotalFiles = cloudSaves.Count,
                LargestFile = cloudSaves.OrderByDescending(csb => csb.FileSize).Select(csb => new CloudFileInfo
                {
                    CloudBackupId = csb.CloudBackupId,
                    GameName = csb.Game.Name,
                    UploadTime = csb.UploadTime,
                    FileSizeMB = Math.Round((decimal)csb.FileSize / 1024 / 1024, 2)
                }).FirstOrDefault(),
                OldestFile = cloudSaves.OrderBy(csb => csb.UploadTime).Select(csb => new CloudFileInfo
                {
                    CloudBackupId = csb.CloudBackupId,
                    GameName = csb.Game.Name,
                    UploadTime = csb.UploadTime,
                    FileSizeMB = Math.Round((decimal)csb.FileSize / 1024 / 1024, 2)
                }).FirstOrDefault(),
                RecentUploads = cloudSaves.OrderByDescending(csb => csb.UploadTime).Take(5).Select(csb => new CloudFileInfo
                {
                    CloudBackupId = csb.CloudBackupId,
                    GameName = csb.Game.Name,
                    UploadTime = csb.UploadTime,
                    FileSizeMB = Math.Round((decimal)csb.FileSize / 1024 / 1024, 2)
                }).ToList()
            };

            return Ok(ApiResponse<CloudStorageUsageDto>.SuccessResponse(usage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage usage");
            return StatusCode(500, ApiResponse<CloudStorageUsageDto>.ErrorResponse(
                "ERR_INTERNAL_SERVER_ERROR", "获取存储空间信息失败"));
        }
    }
}
