// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Media.Imaging;
using DTC.Core;
using DTC.Core.Extensions;
using SkiaSharp;

namespace ClipTrayPro.Services;

/// <summary>
/// Represents image data currently available from the clipboard.
/// </summary>
/// <remarks>
/// Clipboard images do not have stable file paths, so this type materializes PNG files only when a command needs one.
/// </remarks>
public sealed class ClipboardImageTarget : IDisposable
{
    private readonly Bitmap m_bitmap;
    private readonly byte[] m_pngData;
    private readonly int m_bitsPerPixel;
    private TempFile m_tempFile;

    public ClipboardImageTarget(Bitmap bitmap)
    {
        m_bitmap = bitmap;
        using var stream = new MemoryStream();
        m_bitmap.Save(stream);
        m_pngData = stream.ToArray();
        m_bitsPerPixel = GetBitsPerPixel();
    }

    public string ToolTip =>
        $"{m_bitmap.PixelSize.Width:N0} x {m_bitmap.PixelSize.Height:N0} px{Environment.NewLine}{m_bitsPerPixel} bpp";

    public void Open()
    {
        var file = GetTempPngFile();
        file.OpenWithDefaultViewer();
    }

    public ClipboardImageSnapshot CreateSnapshot() => new(m_pngData);

    public static ClipboardImageTarget FromFile(string path)
    {
        if (!IsSupportedImagePath(path))
            return null;

        try
        {
            return new ClipboardImageTarget(new Bitmap(path));
        }
        catch
        {
            return null;
        }
    }

    public static bool IsSupportedImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".tif" or ".tiff" or ".jpg" or ".jpeg";
    }

    public async Task SaveAsync(Stream stream, string fileName)
    {
        await using (stream)
        {
            var format = GetImageFormat(fileName);
            if (format == SKEncodedImageFormat.Png)
            {
                m_bitmap.Save(stream);
                return;
            }

            using var pngStream = new MemoryStream();
            m_bitmap.Save(pngStream);
            pngStream.Position = 0;

            using var sourceBitmap = SKBitmap.Decode(pngStream);
            if (sourceBitmap == null)
                throw new InvalidOperationException("Failed to decode clipboard image.");

            using var image = SKImage.FromBitmap(sourceBitmap);
            using var encoded = image.Encode(format, 90);
            if (encoded == null)
                throw new InvalidOperationException($"Failed to encode clipboard image as {format}.");

            encoded.SaveTo(stream);
        }
    }

    public void Dispose()
    {
        m_tempFile?.Dispose();
        m_tempFile = null;
        m_bitmap.Dispose();
    }

    private FileInfo GetTempPngFile()
    {
        if (m_tempFile == null || !m_tempFile.ReallyExists())
        {
            m_tempFile = new TempFile(".png");
            m_bitmap.Save(m_tempFile.FullName);
        }

        return new FileInfo(m_tempFile.FullName);
    }

    private int GetBitsPerPixel()
    {
        using var bitmap = SKBitmap.Decode(m_pngData);
        return bitmap?.BytesPerPixel * 8 ?? 0;
    }

    private static SKEncodedImageFormat GetImageFormat(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Png
        };
    }
}
