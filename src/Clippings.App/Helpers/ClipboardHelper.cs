using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clippings.App.Helpers;

public static class ClipboardHelper
{
    private const int RetryCount = 5;
    private const string PngFormat = "PNG";

    public static bool TryGetImage(out BitmapSource? source)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                if (TryGetPngImage(out source))
                {
                    return true;
                }

                if (!Clipboard.ContainsImage())
                {
                    source = null;
                    return false;
                }

                var image = Clipboard.GetImage();
                if (image is null)
                {
                    source = null;
                    return false;
                }

                var opaque = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
                if (opaque.CanFreeze)
                {
                    opaque.Freeze();
                }

                source = opaque;
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        source = null;
        return false;
    }

    private static bool TryGetPngImage(out BitmapSource? source)
    {
        source = null;
        try
        {
            if (!Clipboard.ContainsData(PngFormat))
            {
                return false;
            }

            using var stream = CreatePngStream(Clipboard.GetData(PngFormat));
            if (stream is null || stream.Length == 0)
            {
                return false;
            }

            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return false;
            }

            source = decoder.Frames[0];
            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IOException or NotSupportedException or InvalidOperationException)
        {
            source = null;
            return false;
        }
    }

    private static MemoryStream? CreatePngStream(object? data)
    {
        switch (data)
        {
            case byte[] bytes:
                return new MemoryStream(bytes);
            case MemoryStream memory:
                return new MemoryStream(memory.ToArray());
            case Stream stream:
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                var copy = new MemoryStream();
                stream.CopyTo(copy);
                copy.Position = 0;
                return copy;
            default:
                return null;
        }
    }

    public static bool TrySetImage(BitmapSource source)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Clipboard.SetImage(source);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }
}
