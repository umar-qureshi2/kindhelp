using KindHelp.Web.Data;
using KindHelp.Web.Models;
using KindHelp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Pages.Admin.Cases;

[Authorize(Policy = "AdminOnly")]
public class PhotosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IConfiguration _config;

    public PhotosModel(ApplicationDbContext db, IFileStorageService files, IConfiguration config)
    {
        _db = db;
        _files = files;
        _config = config;
    }

    public Case? Case { get; private set; }
    public IReadOnlyList<CasePhoto> Photos { get; private set; } = Array.Empty<CasePhoto>();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Case = await _db.Cases.FindAsync(new object?[] { id }, ct);
        if (Case is null) return NotFound();

        Photos = await _db.CasePhotos
            .Where(p => p.CaseId == id)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(int caseId, IFormFile? upload, string? caption, bool isCover, CancellationToken ct)
    {
        var c = await _db.Cases.FindAsync(new object?[] { caseId }, ct);
        if (c is null) return NotFound();

        if (upload is null || upload.Length == 0)
        {
            TempData["StatusMessage"] = "Please choose a file.";
            return RedirectToPage(new { id = caseId });
        }

        var maxMb = _config.GetValue<int>("KindHelp:MaxUploadMb", 5);
        if (upload.Length > maxMb * 1024L * 1024L)
        {
            TempData["StatusMessage"] = $"File too large (max {maxMb} MB).";
            return RedirectToPage(new { id = caseId });
        }

        await using var stream = upload.OpenReadStream();
        string relPath;
        try
        {
            relPath = await _files.SaveCaseImageAsync(caseId, stream, upload.FileName, ct);
        }
        catch (InvalidOperationException ex)
        {
            TempData["StatusMessage"] = ex.Message;
            return RedirectToPage(new { id = caseId });
        }

        var maxOrder = await _db.CasePhotos.Where(p => p.CaseId == caseId).MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

        var photo = new CasePhoto
        {
            CaseId = caseId,
            RelativePath = relPath,
            Caption = caption,
            IsCover = isCover,
            SortOrder = maxOrder + 1,
            UploadedAtUtc = DateTime.UtcNow
        };

        if (isCover)
        {
            await _db.CasePhotos
                .Where(p => p.CaseId == caseId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCover, false), ct);
        }

        _db.CasePhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        TempData["StatusMessage"] = "Photo uploaded.";
        return RedirectToPage(new { id = caseId });
    }

    public async Task<IActionResult> OnPostSetCoverAsync(int caseId, int photoId, CancellationToken ct)
    {
        var photo = await _db.CasePhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.CaseId == caseId, ct);
        if (photo is null) return NotFound();

        await _db.CasePhotos.Where(p => p.CaseId == caseId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCover, false), ct);

        photo.IsCover = true;
        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Cover photo updated.";
        return RedirectToPage(new { id = caseId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int caseId, int photoId, CancellationToken ct)
    {
        var photo = await _db.CasePhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.CaseId == caseId, ct);
        if (photo is null) return NotFound();

        _files.DeleteIfExists(photo.RelativePath);
        _db.CasePhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);
        TempData["StatusMessage"] = "Photo deleted.";
        return RedirectToPage(new { id = caseId });
    }
}
