using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ModsController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<ModsController> _logger;

    public ModsController(PlayLinkerDbContext context, ILogger<ModsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 安装Mod
    /// </summary>
    /// <param name="request">安装请求</param>
    /// <returns>安装结果</returns>
    /// <remarks>
    /// 网页版限制：无法自动安装Mod文件，仅记录Mod信息到数据库
    /// 实际的Mod文件安装需要用户手动操作或本地客户端版本
    /// </remarks>
    [HttpPost("install")]
    [ProducesResponseType(typeof(ApiResponse<InstallModResponse>), 201)]
    public async Task<ActionResult<ApiResponse<InstallModResponse>>> InstallMod([FromBody] InstallModRequest request)
    {
        try
        {
            // 验证游戏安装是否存在
            var install = await _context.LocalGameInstalls
                .Include(lgi => lgi.Game)
                .FirstOrDefaultAsync(lgi => lgi.InstallId == request.InstallId);

            if (install == null)
            {
                return NotFound(ApiResponse<InstallModResponse>.ErrorResponse("ERR_INSTALL_NOT_FOUND", "游戏安装不存在"));
            }

            // 检查是否已存在同名Mod
            var existingMod = await _context.LocalMods
                .FirstOrDefaultAsync(m => m.InstallId == request.InstallId && m.ModName == request.ModName);

            if (existingMod != null)
            {
                return BadRequest(ApiResponse<InstallModResponse>.ErrorResponse("ERR_MOD_EXISTS", "该Mod已存在"));
            }

            // 计算文件大小（网页版无法获取真实大小，使用默认值）
            decimal sizeGB = 0.1m; // 默认100MB

            // 生成目标安装路径
            var gameName = install.Game.Name.Replace(" ", "").Replace(":", "");
            var modFolderName = request.ModName.ToLower().Replace(" ", "_").Replace(":", "");
            var targetPath = $"D:\\Games\\{gameName}\\mods\\{modFolderName}\\";

            // 创建新Mod记录
            var newMod = new LocalMod
            {
                InstallId = request.InstallId,
                ModName = request.ModName,
                Version = request.Version,
                Enabled = false, // 网页版默认禁用，等用户手动安装后启用
                LastModified = DateTime.UtcNow,
                InstallStatus = "pending_manual_install",
                TargetPath = targetPath,
                Description = request.Description,
                Author = request.Author,
                DownloadUrl = $"/api/v1/mods/{request.ModName}/download" // 模拟下载链接
            };

            _context.LocalMods.Add(newMod);
            await _context.SaveChangesAsync();

            // 生成安装指导步骤
            var installInstructions = new List<string>
            {
                "1. 下载Mod文件到本地",
                $"2. 解压到：{targetPath}",
                "3. 确保Mod文件结构正确",
                "4. 重启游戏以加载Mod",
                "5. 在PlayLinker中启用该Mod"
            };

            var response = new InstallModResponse
            {
                ModId = newMod.ModId,
                ModName = newMod.ModName,
                InstallId = newMod.InstallId,
                InstallStatus = newMod.InstallStatus,
                TargetPath = targetPath,
                Enabled = newMod.Enabled ?? false,
                InstalledAt = newMod.LastModified,
                SizeGB = sizeGB,
                InstallInstructions = installInstructions,
                DownloadUrl = newMod.DownloadUrl,
                WebLimitation = true
            };

            _logger.LogInformation("Mod installed: {ModName} for game {InstallId}", request.ModName, request.InstallId);

            return Created($"/api/v1/mods/{newMod.ModId}", ApiResponse<InstallModResponse>.SuccessResponse(response, "Mod安装成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing mod {ModName}", request.ModName);
            return StatusCode(500, ApiResponse<InstallModResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "安装Mod失败"));
        }
    }

    /// <summary>
    /// 启用/禁用Mod
    /// </summary>
    /// <param name="id">Mod ID</param>
    /// <param name="request">切换请求</param>
    /// <returns>切换结果</returns>
    [HttpPatch("{id}/toggle")]
    [ProducesResponseType(typeof(ApiResponse<ToggleModResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ToggleModResponse>>> ToggleMod(long id, [FromBody] ToggleModRequest request)
    {
        try
        {
            var mod = await _context.LocalMods.FindAsync(id);
            if (mod == null)
            {
                return NotFound(ApiResponse<ToggleModResponse>.ErrorResponse("ERR_MOD_NOT_FOUND", "Mod不存在"));
            }

            mod.Enabled = request.Enabled;
            mod.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var response = new ToggleModResponse
            {
                ModId = mod.ModId,
                ModName = mod.ModName,
                Enabled = mod.Enabled ?? false,
                UpdatedAt = mod.LastModified
            };

            string action = request.Enabled ? "启用" : "禁用";
            _logger.LogInformation("Mod {Action}: {ModName} (ID: {ModId})", action, mod.ModName, mod.ModId);

            return Ok(ApiResponse<ToggleModResponse>.SuccessResponse(response, $"Mod已{action}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling mod {ModId}", id);
            return StatusCode(500, ApiResponse<ToggleModResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "切换Mod状态失败"));
        }
    }

    /// <summary>
    /// 确认手动安装完成
    /// </summary>
    /// <param name="id">Mod ID</param>
    /// <returns>确认结果</returns>
    [HttpPost("{id}/confirm-install")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmManualInstall(long id)
    {
        try
        {
            var mod = await _context.LocalMods.FindAsync(id);
            if (mod == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("ERR_MOD_NOT_FOUND", "Mod不存在"));
            }

            if (mod.InstallStatus != "pending_manual_install")
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("ERR_INVALID_STATUS", "该Mod不在等待手动安装状态"));
            }

            // 更新安装状态
            mod.InstallStatus = "installed";
            mod.Enabled = true; // 确认安装后自动启用
            mod.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = new
            {
                modId = mod.ModId,
                modName = mod.ModName,
                installStatus = mod.InstallStatus,
                enabled = mod.Enabled,
                confirmedAt = mod.LastModified
            };

            _logger.LogInformation("Manual install confirmed: {ModName} (ID: {ModId})", mod.ModName, mod.ModId);

            return Ok(ApiResponse<object>.SuccessResponse(result, "手动安装确认成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming manual install for mod {ModId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "确认安装失败"));
        }
    }

    /// <summary>
    /// 卸载Mod
    /// </summary>
    /// <param name="id">Mod ID</param>
    /// <param name="request">卸载请求</param>
    /// <returns>卸载结果</returns>
    /// <remarks>
    /// 网页版限制：仅从数据库删除记录，无法删除Mod文件
    /// deleteFiles 参数在网页版中被忽略，固定为 false
    /// 删除Mod文件功能需要本地客户端版本
    /// </remarks>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UninstallModResponse>), 200)]
    public async Task<ActionResult<ApiResponse<UninstallModResponse>>> UninstallMod(long id, [FromBody] UninstallModRequest request)
    {
        try
        {
            var mod = await _context.LocalMods.FindAsync(id);
            if (mod == null)
            {
                return NotFound(ApiResponse<UninstallModResponse>.ErrorResponse("ERR_MOD_NOT_FOUND", "Mod不存在"));
            }

            var modName = mod.ModName;
            decimal freedSpaceGB = 0.1m; // 网页版无法计算真实大小

            _context.LocalMods.Remove(mod);
            await _context.SaveChangesAsync();

            var response = new UninstallModResponse
            {
                ModId = id,
                ModName = modName,
                DeletedFiles = false, // 网页版不删除文件
                FreedSpaceGB = freedSpaceGB,
                UninstalledAt = DateTime.UtcNow
            };

            _logger.LogInformation("Mod uninstalled: {ModName} (ID: {ModId})", modName, id);

            return Ok(ApiResponse<UninstallModResponse>.SuccessResponse(response, "Mod已卸载"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uninstalling mod {ModId}", id);
            return StatusCode(500, ApiResponse<UninstallModResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "卸载Mod失败"));
        }
    }

    /// <summary>
    /// 检测Mod冲突
    /// </summary>
    /// <param name="installId">游戏安装ID</param>
    /// <returns>冲突检测结果</returns>
    /// <remarks>
    /// 网页版实现：基于简单的名称匹配检测冲突
    /// 真正的文件级冲突检测需要本地客户端版本
    /// </remarks>
    [HttpGet("conflicts")]
    [ProducesResponseType(typeof(ApiResponse<ModConflictsResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ModConflictsResponse>>> GetModConflicts([FromQuery] long installId)
    {
        try
        {
            var install = await _context.LocalGameInstalls
                .Include(lgi => lgi.Game)
                .Include(lgi => lgi.LocalMods)
                .FirstOrDefaultAsync(lgi => lgi.InstallId == installId);

            if (install == null)
            {
                return NotFound(ApiResponse<ModConflictsResponse>.ErrorResponse("ERR_INSTALL_NOT_FOUND", "游戏安装不存在"));
            }

            var conflicts = new List<ModConflict>();
            var conflictId = 1;

            // 简单的冲突检测逻辑：检测名称相似的Mod
            var mods = install.LocalMods.ToList();
            for (int i = 0; i < mods.Count; i++)
            {
                for (int j = i + 1; j < mods.Count; j++)
                {
                    var mod1 = mods[i];
                    var mod2 = mods[j];

                    // 检测名称相似性（简单示例）
                    if (IsConflicting(mod1.ModName, mod2.ModName))
                    {
                        conflicts.Add(new ModConflict
                        {
                            ConflictId = conflictId++,
                            Severity = "medium",
                            Mods = new List<ConflictingMod>
                            {
                                new ConflictingMod { ModId = mod1.ModId, ModName = mod1.ModName },
                                new ConflictingMod { ModId = mod2.ModId, ModName = mod2.ModName }
                            },
                            Reason = "两个Mod可能修改了相同的游戏文件",
                            Recommendation = "建议只保留其中一个Mod或查看Mod兼容性"
                        });
                    }
                }
            }

            var response = new ModConflictsResponse
            {
                InstallId = installId,
                GameName = install.Game.Name,
                Conflicts = conflicts,
                TotalConflicts = conflicts.Count,
                HasBlockingConflicts = conflicts.Any(c => c.Severity == "high")
            };

            return Ok(ApiResponse<ModConflictsResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting mod conflicts for install {InstallId}", installId);
            return StatusCode(500, ApiResponse<ModConflictsResponse>.ErrorResponse("ERR_INTERNAL_SERVER_ERROR", "检测Mod冲突失败"));
        }
    }

    /// <summary>
    /// 简单的冲突检测逻辑
    /// </summary>
    private static bool IsConflicting(string modName1, string modName2)
    {
        // 简单的关键词匹配
        var keywords = new[] { "AI", "Texture", "Graphics", "Sound", "UI", "Combat" };
        
        foreach (var keyword in keywords)
        {
            if (modName1.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                modName2.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !modName1.Equals(modName2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}
