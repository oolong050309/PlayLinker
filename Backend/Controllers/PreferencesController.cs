using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models;
using PlayLinker.Models.DTOs;
using PlayLinker.Models.Entities;

namespace PlayLinker.Controllers;

[ApiController]
[Route("api/v1/preferences")]
[Authorize]
public class PreferencesController : ControllerBase
{
    private readonly PlayLinkerDbContext _context;
    
    public PreferencesController(PlayLinkerDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value ?? User.FindFirst("sub")?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }

    // GET /api/v1/preferences
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserPreferenceDto>>> GetPreferences()
    {
        var userId = GetCurrentUserId();
        var pref = await _context.UserPreferences
            .Include(p => p.PreferenceGenres).ThenInclude(pg => pg.Genre)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref == null)
        {
            // 如果不存在，创建一个默认的
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

    // PATCH /api/v1/preferences
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

        // 更新基本字段
        pref.PlaytimeRange = request.PlaytimeRange;
        pref.PriceSensitivity = request.PriceSensitivity;
        pref.UpdatedAt = DateTime.UtcNow;

        // 更新题材关联 (先删后加)
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
}