using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;
using System.IO.Compression;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/cloud")]
public class CloudController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<CloudController> _logger;
    private readonly IConfiguration _configuration;

    public CloudController(PlayLinkerDbContext context, ILogger<CloudController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
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
                    Metadata = null, // 网页版不支持存档元数据解析
                    ExpiresAt = DateTime.UtcNow.AddYears(1) // 默认1年后过期
                })
                .ToListAsync();

            // 计算汇总信息（基于所有云存档，不只是当前页）
            var allCloudSaves = await query.ToListAsync();
            var storageLimitMB = 1024m; // 1GB 存储限制
            var totalSizeMB = Math.Round(allCloudSaves.Sum(s => s.FileSize) / 1024.0m / 1024, 2);
            
            var summary = new CloudSavesSummary
            {
                TotalCloudSaves = total,
                TotalSizeMB = totalSizeMB,
                StorageUsedMB = totalSizeMB,
                StorageLimitMB = storageLimitMB
            };

            var response = new PaginatedResponse<CloudSaveListDto>
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
    /// <param name="file">存档文件</param>
    /// <param name="saveId">本地存档ID</param>
    /// <param name="compress">是否压缩</param>
    /// <param name="encrypt">是否加密</param>
    /// <param name="description">描述</param>
    /// <returns>上传结果</returns>
    /// <remarks>
    /// 网页版实现：用户手动选择存档文件上传到云服务器磁盘
    /// </remarks>
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)] // 限制100MB
    [ProducesResponseType(typeof(ApiResponse<UploadToCloudResponse>), 201)]
    public async Task<ActionResult<ApiResponse<UploadToCloudResponse>>> UploadToCloud(
        [FromForm] IFormFile file,
        [FromForm] long saveId,
        [FromForm] bool compress = false,
        [FromForm] bool encrypt = false,
        [FromForm] string? description = null)
    {
        try
        {
            // 1. 验证文件
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<UploadToCloudResponse>.ErrorResponse(
                    "ERR_NO_FILE", "请选择要上传的文件"));
            }

            var maxSizeMB = _configuration.GetValue<int>("CloudStorage:MaxFileSizeMB");
            if (file.Length > maxSizeMB * 1024 * 1024)
            {
                return BadRequest(ApiResponse<UploadToCloudResponse>.ErrorResponse(
                    "ERR_FILE_TOO_LARGE", $"文件大小不能超过{maxSizeMB}MB"));
            }

            // 2. 验证存档是否存在
            var save = await _context.LocalSaveFiles
                .Include(lsf => lsf.Install)
                .FirstOrDefaultAsync(lsf => lsf.SaveId == saveId);

            if (save == null)
            {
                return NotFound(ApiResponse<UploadToCloudResponse>.ErrorResponse(
                    "ERR_SAVE_NOT_FOUND", "存档不存在"));
            }

            // 3. 生成云备份ID和存储路径
            int userId = save.Install.UserId;
            var cloudBackupId = $"cloud_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            var relativePath = $"user_{userId}\\game_{save.Install.GameId}\\{cloudBackupId}.dat";
            
            var localPath = _configuration["CloudStorage:LocalPath"];
            var fullPath = Path.Combine(localPath, relativePath);
            
            // 4. 创建目录
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created directory: {Directory}", directory);
            }

            // 5. 保存文件
            long uploadedSize = 0;
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                if (compress)
                {
                    // 压缩后保存
                    using (var gzipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        await file.CopyToAsync(gzipStream);
                    }
                    uploadedSize = new FileInfo(fullPath).Length;
                    _logger.LogInformation("File compressed and saved: {Size} bytes", uploadedSize);
                }
                else
                {
                    await file.CopyToAsync(stream);
                    uploadedSize = stream.Length;
                    _logger.LogInformation("File saved: {Size} bytes", uploadedSize);
                }
            }

            // 6. 记录到数据库
            var baseUrl = _configuration["CloudStorage:BaseUrl"];
            var storageUrl = $"{baseUrl}/{relativePath.Replace("\\", "/")}";
            
            var cloudBackup = new CloudSaveBackup
            {
                CloudBackupId = cloudBackupId,
                GameId = save.Install.GameId,
                UserId = userId,
                UploadTime = DateTime.UtcNow,
                FileSize = (int)(uploadedSize / 1024 / 1024), // 转换为MB
                StorageUrl = storageUrl
            };

            _context.CloudSaveBackups.Add(cloudBackup);
            await _context.SaveChangesAsync();

            // 7. 返回响应
            var response = new UploadToCloudResponse
            {
                CloudBackupId = cloudBackupId,
                SaveId = saveId,
                StorageUrl = storageUrl,
                OriginalSize = file.Length,
                UploadedSize = uploadedSize,
                Compressed = compress,
                Encrypted = encrypt,
                UploadTime = DateTime.UtcNow
            };

            return CreatedAtAction(nameof(UploadToCloud), new { id = cloudBackupId },
                ApiResponse<UploadToCloudResponse>.SuccessResponse(response, "存档上传成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading save {SaveId} to cloud", saveId);
            return StatusCode(500, ApiResponse<UploadToCloudResponse>.ErrorResponse(
                "ERR_UPLOAD_FAILED", $"上传存档失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 从云端下载存档
    /// </summary>
    /// <param name="id">云备份ID</param>
    /// <returns>存档文件</returns>
    /// <remarks>
    /// 网页版实现：直接返回文件流供用户下载
    /// </remarks>
    [HttpGet("download/{id}")]
    [ProducesResponseType(typeof(FileResult), 200)]
    public async Task<IActionResult> DownloadFromCloud(string id)
    {
        try
        {
            var cloudBackup = await _context.CloudSaveBackups.FindAsync(id);
            if (cloudBackup == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "ERR_BACKUP_NOT_FOUND", "云备份不存在"));
            }

            // 从 storage_url 提取相对路径
            var baseUrl = _configuration["CloudStorage:BaseUrl"];
            var relativePath = cloudBackup.StorageUrl.Replace(baseUrl + "/", "").Replace("/", "\\");
            
            var localPath = _configuration["CloudStorage:LocalPath"];
            var fullPath = Path.Combine(localPath, relativePath);

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogError("File not found: {Path}", fullPath);
                return NotFound(ApiResponse<object>.ErrorResponse(
                    "ERR_FILE_NOT_FOUND", "存档文件不存在"));
            }

            // 读取文件
            var memory = new MemoryStream();
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            var fileName = $"{id}.dat";
            _logger.LogInformation("Downloading file: {FileName}, Size: {Size} bytes", fileName, memory.Length);

            return File(memory, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading cloud backup {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse(
                "ERR_DOWNLOAD_FAILED", $"下载存档失败: {ex.Message}"));
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
