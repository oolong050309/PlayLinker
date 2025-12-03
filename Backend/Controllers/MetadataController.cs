using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;

namespace PlayLinker.Controllers;

/// <summary>
/// 游戏元数据API控制器
/// 提供题材、分类、开发商、发行商等元数据查询
/// </summary>
[ApiController]
[Route("api/v1")]
public class MetadataController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly ILogger<MetadataController> _logger;

    public MetadataController(PlayLinkerDbContext context, ILogger<MetadataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有游戏题材
    /// </summary>
    [HttpGet("genres")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetGenres()
    {
        try
        {
            _logger.LogInformation("获取所有游戏题材");

            var genres = await _context.Genres
                .Select(g => new GenreDto
                {
                    GenreId = g.GenreId,
                    Name = g.Name
                })
                .ToListAsync();

            var result = new
            {
                items = genres,
                totalCount = genres.Count
            };

            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏题材时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取所有游戏分类
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetCategories()
    {
        try
        {
            _logger.LogInformation("获取所有游戏分类");

            var categories = await _context.Categories
                .Select(c => new CategoryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name
                })
                .ToListAsync();

            var result = new
            {
                items = categories,
                totalCount = categories.Count
            };

            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏分类时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取开发商列表
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    [HttpGet("developers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetDevelopers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("获取开发商列表: page={Page}, pageSize={PageSize}", page, pageSize);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Developers.AsQueryable();
            var total = await query.CountAsync();

            var developers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DeveloperDto
                {
                    DeveloperId = d.DeveloperId,
                    Name = d.Name,
                    GamesCount = d.GameDevelopers.Count
                })
                .ToListAsync();

            var result = new
            {
                items = developers,
                meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取开发商列表时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }

    /// <summary>
    /// 获取发行商列表
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    [HttpGet("publishers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetPublishers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("获取发行商列表: page={Page}, pageSize={PageSize}", page, pageSize);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Publishers.AsQueryable();
            var total = await query.CountAsync();

            var publishers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PublisherDto
                {
                    PublisherId = p.PublisherId,
                    Name = p.Name,
                    GamesCount = p.GamePublishers.Count
                })
                .ToListAsync();

            var result = new
            {
                items = publishers,
                meta = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    Total = total
                }
            };

            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取发行商列表时发生错误");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("ERR_INTERNAL", "服务器内部错误"));
        }
    }
}

