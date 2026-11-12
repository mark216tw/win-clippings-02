using Clippings.Core.Helpers;

namespace Clippings.Platform.Storage;

public static class LegacyDataCleanup
{
    public static Task RunAsync(string dataRoot)
        => Task.Run(() => Run(dataRoot));

    private static void Run(string dataRoot)
    {
        try
        {
            var clipboardDirectory = Path.Combine(dataRoot, "clipboard");
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
                PathUtil.ThumbnailsDirectory(dataRoot),
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

    private static void TryDeleteFile(string path)
    {
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
