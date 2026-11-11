using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Clippings.Platform.Storage;

public sealed class ClippingsStore : IClippingsStore
{
    private readonly string _dataRoot;
    private readonly DbContextOptions<ClippingsDbContext> _options;

    public ClippingsStore()
        : this(PathUtil.DefaultDataRoot)
    {
    }

    public ClippingsStore(string dataRoot)
    {
        _dataRoot = dataRoot;
        _options = new DbContextOptionsBuilder<ClippingsDbContext>()
            .UseSqlite($"Data Source={PathUtil.DatabasePath(_dataRoot)};Default Timeout=5;Pooling=True")
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
    }

    public string DataRoot => _dataRoot;

    public void Initialize()
    {
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(PathUtil.ImagesDirectory(_dataRoot));
        Directory.CreateDirectory(PathUtil.ThumbnailsDirectory(_dataRoot));
        Directory.CreateDirectory(PathUtil.LogDirectory(_dataRoot));

        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS IX_Captures_IsFavorite_CreatedAt ON Captures (IsFavorite, CreatedAt);");
        RemoveLegacyClipboardHistory(db);
    }

    private void RemoveLegacyClipboardHistory(ClippingsDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ClipboardItems;");
        }
        catch (Exception)
        {
        }

        try
        {
            var clipboardDirectory = Path.Combine(_dataRoot, "clipboard");
            if (Directory.Exists(clipboardDirectory))
            {
                Directory.Delete(clipboardDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                PathUtil.ThumbnailsDirectory(_dataRoot),
                "clip_*.jpg",
                SearchOption.TopDirectoryOnly))
            {
                TryDeleteFile(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public async Task<CaptureItem> AddCaptureAsync(
        CaptureItem item,
        byte[] imageBytes,
        string extension,
        byte[] thumbnailBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(thumbnailBytes);

        if (string.IsNullOrWhiteSpace(item.Hash))
        {
            item.Hash = HashUtil.Sha256Hex(imageBytes);
        }

        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? "png"
            : extension.TrimStart('.').ToLowerInvariant();

        var fileName = PathUtil.BuildImageFileName(item.CreatedAt, item.Hash, normalizedExtension);
        var imageDirectory = PathUtil.ImagesDirectory(_dataRoot);
        var thumbDirectory = PathUtil.ThumbnailsDirectory(_dataRoot);

        var imagePath = Path.Combine(imageDirectory, fileName);
        var thumbPath = Path.Combine(thumbDirectory, Path.ChangeExtension(fileName, ".jpg"));

        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(thumbPath, thumbnailBytes, cancellationToken).ConfigureAwait(false);

        item.FilePath = imagePath;
        item.ThumbnailPath = thumbPath;

        await using var db = CreateContext();
        var entity = ToEntity(item);
        db.Captures.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        item.Id = entity.Id;

        return item;
    }

    public async Task<CaptureItem?> UpdateCaptureImageAsync(
        int id,
        byte[] imageBytes,
        string extension,
        byte[] thumbnailBytes,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(thumbnailBytes);

        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var createdAt = DateTimeOffset.Now;
        var hash = HashUtil.Sha256Hex(imageBytes);
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? "png"
            : extension.TrimStart('.').ToLowerInvariant();

        var imagePath = entity.FilePath;
        var thumbPath = entity.ThumbnailPath;
        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(thumbPath) ||
            !File.Exists(imagePath) || !File.Exists(thumbPath))
        {
            var fileName = PathUtil.BuildImageFileName(createdAt, hash, normalizedExtension);
            imagePath = Path.Combine(PathUtil.ImagesDirectory(_dataRoot), fileName);
            thumbPath = Path.Combine(PathUtil.ThumbnailsDirectory(_dataRoot), Path.ChangeExtension(fileName, ".jpg"));
        }

        var imageDirectory = Path.GetDirectoryName(imagePath);
        if (!string.IsNullOrEmpty(imageDirectory))
        {
            Directory.CreateDirectory(imageDirectory);
        }

        var thumbDirectory = Path.GetDirectoryName(thumbPath);
        if (!string.IsNullOrEmpty(thumbDirectory))
        {
            Directory.CreateDirectory(thumbDirectory);
        }

        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(thumbPath, thumbnailBytes, cancellationToken).ConfigureAwait(false);

        entity.FilePath = imagePath;
        entity.ThumbnailPath = thumbPath;
        entity.Hash = hash;
        entity.Width = width;
        entity.Height = height;
        entity.CreatedAt = createdAt.ToUniversalTime().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToModel(entity);
    }

    public async Task<IReadOnlyList<CaptureItem>> GetCapturesAsync(
        string? search,
        bool favoritesOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var query = db.Captures.AsNoTracking();

        if (favoritesOnly)
        {
            query = query.Where(item => item.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Source, pattern) ||
                EF.Functions.Like(item.FilePath, pattern));
        }

        var entities = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 5000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToModel).ToList();
    }

    public async Task<CaptureItem?> GetCaptureAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var entity = await db.Captures
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    public async Task DeleteCaptureAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.Captures.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        TryDeleteFile(entity.FilePath);
        TryDeleteFile(entity.ThumbnailPath);
    }

    public async Task<int> DeleteNonFavoriteCapturesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var targets = await db.Captures
            .Where(item => !item.IsFavorite)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targets.Count == 0)
        {
            return 0;
        }

        var ids = targets.Select(item => item.Id).ToList();
        var deleted = await db.Captures
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            TryDeleteFile(target.FilePath);
            TryDeleteFile(target.ThumbnailPath);
        }

        return deleted;
    }

    public async Task SetCaptureFavoriteAsync(int id, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.IsFavorite = isFavorite;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountCapturesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        return await db.Captures.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TrimCapturesAsync(int maxItems, int retentionDays, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, retentionDays));
        var stale = await db.Captures
            .Where(item => !item.IsFavorite && item.CreatedAt < cutoff)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var overflow = await db.Captures
            .Where(item => !item.IsFavorite)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(Math.Max(0, maxItems))
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targets = stale.Concat(overflow).DistinctBy(item => item.Id).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var ids = targets.Select(item => item.Id).ToList();
        await db.Captures
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            TryDeleteFile(target.FilePath);
            TryDeleteFile(target.ThumbnailPath);
        }
    }

    private ClippingsDbContext CreateContext() => new(_options);

    private readonly record struct TrimTarget(int Id, string? FilePath, string? ThumbnailPath);

    private static CaptureEntity ToEntity(CaptureItem item) => new()
    {
        Id = item.Id,
        CreatedAt = item.CreatedAt.ToUniversalTime().UtcDateTime,
        Width = item.Width,
        Height = item.Height,
        FilePath = item.FilePath,
        ThumbnailPath = item.ThumbnailPath,
        Hash = item.Hash,
        IsFavorite = item.IsFavorite,
        Source = item.Source,
    };

    private static CaptureItem ToModel(CaptureEntity entity) => new()
    {
        Id = entity.Id,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)).ToLocalTime(),
        Width = entity.Width,
        Height = entity.Height,
        FilePath = entity.FilePath,
        ThumbnailPath = entity.ThumbnailPath,
        Hash = entity.Hash,
        IsFavorite = entity.IsFavorite,
        Source = entity.Source,
    };

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}



