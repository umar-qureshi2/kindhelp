namespace KindHelp.Web.Services;

public interface IFileStorageService
{
    /// <summary>Saves an uploaded image and returns its web-accessible relative path (e.g. "/uploads/cases/12/abc.jpg").</summary>
    Task<string> SaveCaseImageAsync(int caseId, Stream content, string originalFileName, CancellationToken ct = default);

    void DeleteIfExists(string relativePath);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public LocalFileStorageService(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    public async Task<string> SaveCaseImageAsync(int caseId, Stream content, string originalFileName, CancellationToken ct = default)
    {
        var allowed = _config.GetSection("KindHelp:AllowedImageExtensions").Get<string[]>()
            ?? new[] { ".jpg", ".jpeg", ".png", ".webp" };

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
            throw new InvalidOperationException($"Unsupported image type: {ext}");

        var dir = Path.Combine(_env.WebRootPath, "uploads", "cases", caseId.ToString());
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
        }

        return $"/uploads/cases/{caseId}/{fileName}";
    }

    public void DeleteIfExists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var full = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }
}
