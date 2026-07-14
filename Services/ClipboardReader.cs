// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ClipTrayPro.Services;

/// <summary>
/// Reads actionable clipboard state.
/// </summary>
/// <remarks>
/// Clipboard formats vary between platforms, so this helper prefers native file items and falls back to the first text line.
/// </remarks>
public static class ClipboardReader
{
    public static async Task<ClipboardTarget> GetTargetAsync(IClipboard clipboard)
    {
        if (clipboard == null)
            return null;

        try
        {
            var files = await clipboard.TryGetFilesAsync();
            var firstFile = files?.FirstOrDefault();
            if (firstFile?.Path.IsFile == true)
            {
                var target = ClipboardTarget.FromPath(firstFile.Path.LocalPath);
                if (target != null)
                    return target;
            }
        }
        catch
        {
            // Some platforms expose copied files as text only.
        }

        var text = await clipboard.TryGetTextAsync();
        return ClipboardTarget.FromText(text);
    }

    public static async Task<ClipboardImageTarget> GetImageTargetAsync(IClipboard clipboard)
    {
        if (clipboard == null)
            return null;

        var target = await GetTargetAsync(clipboard);
        var fileImage = ClipboardImageTarget.FromFile(target?.FullPath);
        if (fileImage != null)
            return fileImage;

        try
        {
            var bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap != null)
                return new ClipboardImageTarget(bitmap);
        }
        catch
        {
            // Fall through to paths exposed as file-drop data or text.
        }

        return null;
    }

    public static async Task<string> GetFingerprintAsync(IClipboard clipboard)
    {
        if (clipboard == null)
            return string.Empty;

        if (OperatingSystem.IsWindows())
            return $"windows:{GetClipboardSequenceNumber():X8}";

        try
        {
            var formats = (await clipboard.GetDataFormatsAsync()).Select(o => o.ToString()).OrderBy(o => o, StringComparer.Ordinal).ToArray();
            var files = await clipboard.TryGetFilesAsync();
            var filePaths = files?
                .Where(o => o.Path.IsFile)
                .Select(o => o.Path.LocalPath)
                .OrderBy(o => o, StringComparer.Ordinal)
                .ToArray() ?? [];
            var text = await clipboard.TryGetTextAsync();
            var imageHash = await GetImageHashAsync(clipboard);
            if (formats.Length == 0 && filePaths.Length == 0 && string.IsNullOrEmpty(text) && string.IsNullOrEmpty(imageHash))
                return string.Empty;
            return $"{string.Join("|", formats)}:{string.Join("|", filePaths)}:{text}:{imageHash}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetImageHashAsync(IClipboard clipboard)
    {
        Bitmap bitmap;
        try
        {
            bitmap = await clipboard.TryGetBitmapAsync();
        }
        catch
        {
            return string.Empty;
        }

        if (bitmap == null)
            return string.Empty;

        using (bitmap)
        {
            try
            {
                return GetRawPixelHash(bitmap);
            }
            catch
            {
                try
                {
                    using var stream = new MemoryStream();
                    bitmap.Save(stream);
                    return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    private static string GetRawPixelHash(Bitmap bitmap)
    {
        using var pixels = new WriteableBitmap(
            bitmap.PixelSize,
            bitmap.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using var framebuffer = pixels.Lock();
        bitmap.CopyPixels(framebuffer, AlphaFormat.Unpremul);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var row = new byte[bitmap.PixelSize.Width * 4];
        for (var y = 0; y < bitmap.PixelSize.Height; y++)
        {
            Marshal.Copy(framebuffer.Address + y * framebuffer.RowBytes, row, 0, row.Length);
            hash.AppendData(row);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
