using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs; // [修复] 引用 DTO
using PlayLinker.Models.Entities;
using PlayLinker.Services;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/preferences")]
[Authorize]
public class PreferencesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    private readonly IAiService _aiService;

    public PreferencesController(PlayLinkerDbContext context, IAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserPreferenceDto>>> GetPreferences()
    {
        var userId = GetCurrentUserId();
        var pref = await _context.UserPreferences
            .Include(p => p.PreferenceGenres).ThenInclude(pg => pg.Genre)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref == null)
        {
            pref = new UserPreference { UserId = userId };
            _context.UserPreferences.Add(pref);
            await _context.SaveChangesAsync();
        }

        var dto = new UserPreferenceDto
        {
            PreferenceId = pref.PreferenceId,
            UserId = pref.UserId,
            PlaytimeRange = pref.PlaytimeRange,
            PriceSensitivity = pref.PriceSensitivity,
            UpdatedAt = pref.UpdatedAt,
            FavoriteGenres = pref.PreferenceGenres.Select(pg => new PreferenceGenreDto
            {
                GenreId = pg.GenreId,
                Name = pg.Genre?.Name ?? ""
            }).ToList()
        };

        return Ok(ApiResponse<UserPreferenceDto>.SuccessResponse(dto));
    }

    [HttpPatch]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePreferences([FromBody] UpdatePreferenceDto request)
    {
        var userId = GetCurrentUserId();
        var pref = await _context.UserPreferences
            .Include(p => p.PreferenceGenres)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref == null)
        {
            pref = new UserPreference { UserId = userId };
            _context.UserPreferences.Add(pref);
            await _context.SaveChangesAsync();
        }

        pref.PlaytimeRange = request.PlaytimeRange;
        pref.PriceSensitivity = request.PriceSensitivity;
        pref.UpdatedAt = DateTime.UtcNow;

        _context.PreferenceGenres.RemoveRange(pref.PreferenceGenres);
        foreach (var genreId in request.FavoriteGenres)
        {
            _context.PreferenceGenres.Add(new PreferenceGenre
                {
                    PreferenceId = pref.PreferenceId,
                    GenreId = genreId
                });
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new { pref.PreferenceId, pref.UpdatedAt }, "偏好设置已更新"));
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<ApiResponse<object>>> AnalyzePreferences([FromBody] AnalyzePreferenceRequestDto request)
    {
        var userId = GetCurrentUserId();
        var library = await _context.UserPlatformLibraries
            .Include(l => l.Game)
            .Include(l => l.PlayerPlatform).ThenInclude(pp => pp.UserPlatformBindings)
            .Where(l => l.PlayerPlatform.UserPlatformBindings.Any(b => b.UserId == userId))
            .OrderByDescending(l => l.LastPlayed)
            .Take(20)
            .ToListAsync();

        if (!library.Any())
        {
            return Ok(ApiResponse<object>.SuccessResponse(new { message = "没有足够的数据进行分析" }));
        }

        var gameNames = library.Select(l => l.Game.Name).Distinct().ToList();
        var aiResult = await _aiService.AnalyzeUserPreferencesAsync(userId, gameNames);

        return Ok(ApiResponse<object>.SuccessResponse(aiResult, "偏好分析完成"));
    }
}