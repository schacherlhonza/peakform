using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Integrations.StravaArchive;

namespace TrainCoach.Infrastructure.Integrations;

/// <summary>Plain local-disk store for archives awaiting processing. Keys are generated here (a
/// Guid + ".zip"), never taken from user input, so there's no path-traversal surface.</summary>
public class StravaArchiveFileStore(IOptions<StravaArchiveImportOptions> options, ILogger<StravaArchiveFileStore> logger) : IStravaArchiveFileStore
{
    private string Root
    {
        get
        {
            var root = string.IsNullOrWhiteSpace(options.Value.StoragePath)
                ? Path.Combine(Path.GetTempPath(), "peakform", "strava-archives")
                : options.Value.StoragePath;
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public string CreateKey() => $"{Guid.NewGuid():N}.zip";

    public Stream OpenWrite(string key) =>
        new FileStream(GetPath(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

    public string GetPath(string key)
    {
        if (key.Contains('/') || key.Contains('\\') || key.Contains(".."))
        {
            throw new ArgumentException("Neplatný klíč archivu.", nameof(key));
        }
        return Path.Combine(Root, key);
    }

    public void Delete(string key)
    {
        try
        {
            File.Delete(GetPath(key));
        }
        catch (IOException ex)
        {
            // Still open by a running job (Windows) — the job deletes it itself when it stops,
            // and the retention sweep catches anything left over.
            logger.LogDebug(ex, "Archiv {Key} teď nelze smazat.", key);
        }
    }

    public int DeleteOlderThan(DateTime olderThanUtc, IReadOnlySet<string> keep)
    {
        var deleted = 0;
        foreach (var file in new DirectoryInfo(Root).EnumerateFiles("*.zip"))
        {
            if (file.LastWriteTimeUtc < olderThanUtc && !keep.Contains(file.Name))
            {
                try
                {
                    file.Delete();
                    deleted++;
                }
                catch (IOException ex)
                {
                    logger.LogDebug(ex, "Starý archiv {File} nelze smazat.", file.Name);
                }
            }
        }
        return deleted;
    }
}
