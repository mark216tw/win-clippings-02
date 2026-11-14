using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clippings.App.Helpers;

public static class ShellIconHelper
{
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiLargeIcon = 0x00000000;
    private const uint ShgfiUseFileAttributes = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static ImageSource? _fallback;

    public static ImageSource? GetIcon(string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            lock (Sync)
            {
                if (Cache.TryGetValue(executablePath, out var cached))
                {
                    return cached;
                }
            }

            var icon = LoadIcon(executablePath, 0);
            if (icon is not null)
            {
                lock (Sync)
                {
                    Cache[executablePath] = icon;
                }

                return icon;
            }
        }

        lock (Sync)
        {
            _fallback ??= LoadIcon("application.exe", ShgfiUseFileAttributes) ?? CreateEmptyIcon();
            return _fallback;
        }
    }

    private static ImageSource? LoadIcon(string path, uint extraFlags)
    {
        var fileInfo = new ShFileInfo();
        var result = SHGetFileInfo(
            path,
            FileAttributeNormal,
            ref fileInfo,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiLargeIcon | extraFlags);
        if (result == IntPtr.Zero || fileInfo.Icon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.Icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            _ = DestroyIcon(fileInfo.Icon);
        }
    }

    private static ImageSource CreateEmptyIcon()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            new byte[4],
            4);
        bitmap.Freeze();
        return bitmap;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;

        public int IconIndex;

        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}
