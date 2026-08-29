namespace HaladeHighSchool.Api.Services;

public record StoredFile
{
    /// <summary>Path relative to the storage root, e.g. "lessons/3f2a....pdf".</summary>
    public string RelativePath { get; init; } = string.Empty;

    public string OriginalFileName { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string ContentType { get; init; } = "application/octet-stream";
}

public interface ILessonFileStorage
{
    IReadOnlySet<string> AllowedExtensions { get; }

    bool IsAllowed(string fileName);

    Task<StoredFile> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a stored path to a physical file, refusing anything that escapes the
    /// storage root. Returns null when the file is missing or the path is not contained.
    /// </summary>
    string? ResolveExistingPath(string relativePath);

    void Delete(string relativePath);
}

/// <summary>
/// Lesson attachments are stored outside wwwroot so they can only be fetched through the
/// authorised download endpoint, never by guessing a static URL.
/// </summary>
public class LessonFileStorage : ILessonFileStorage
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
        ".txt", ".csv", ".rtf", ".odt", ".zip",
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".mp3", ".mp4"
    };

    private readonly string _root;
    private readonly ILogger<LessonFileStorage> _logger;

    public LessonFileStorage(IWebHostEnvironment environment, ILogger<LessonFileStorage> logger)
    {
        _root = Path.Combine(environment.ContentRootPath, "storage");
        _logger = logger;
    }

    public IReadOnlySet<string> AllowedExtensions => Allowed;

    public bool IsAllowed(string fileName) => Allowed.Contains(Path.GetExtension(fileName));

    public async Task<StoredFile> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        var directory = Path.Combine(_root, "lessons");
        Directory.CreateDirectory(directory);

        // The stored name is a fresh GUID: the client supplied name is never used on disk.
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        _logger.LogInformation("Stored lesson attachment {StoredName} ({Bytes} bytes)", storedName, file.Length);

        return new StoredFile
        {
            RelativePath = $"lessons/{storedName}",
            OriginalFileName = Path.GetFileName(file.FileName),
            SizeBytes = file.Length,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType
        };
    }

    public string? ResolveExistingPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected lesson file path outside the storage root: {Path}", relativePath);
            return null;
        }

        return File.Exists(candidate) ? candidate : null;
    }

    public void Delete(string relativePath)
    {
        var path = ResolveExistingPath(relativePath);
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete lesson attachment {Path}", relativePath);
        }
    }
}
