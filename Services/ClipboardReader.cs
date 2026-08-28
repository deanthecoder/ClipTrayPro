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
    private const uint CF_DIB = 8;
    private const uint CF_DIBV5 = 17;
    private const uint BI_BITFIELDS = 3;
    private const uint BI_ALPHABITFIELDS = 6;

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

        Bitmap bitmap = null;
        try
        {
            bitmap = await clipboard.TryGetBitmapAsync();
        }
        catch
        {
            // Some Windows clipboard providers expose only a native DIB. Try that below.
        }

        bitmap ??= await TryGetWindowsDibBitmapAsync();
        return bitmap == null ? null : new ClipboardImageTarget(bitmap);
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

    /// <summary>
    /// Reads Windows CF_DIB/CF_DIBV5 data when Avalonia cannot decode it through TryGetBitmapAsync.
    /// </summary>
    /// <remarks>
    /// Several Windows applications put DeviceIndependentBitmap on the clipboard without a compatible PNG or bitmap
    /// representation. A DIB is a BMP file without its 14-byte file header, so restoring that header lets Avalonia
    /// decode the image normally.
    /// </remarks>
    private static async Task<Bitmap> TryGetWindowsDibBitmapAsync()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        // Clipboard ownership can briefly change while an image is being rendered lazily. Retry a couple of times
        // rather than treating that short window as an empty clipboard.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (TryGetWindowsDibBitmap(out var bitmap))
                return bitmap;

            if (attempt < 2)
                await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        return null;
    }

    private static bool TryGetWindowsDibBitmap(out Bitmap bitmap)
    {
        bitmap = null;
        if (!OpenClipboard(0))
            return false;

        try
        {
            var format = IsClipboardFormatAvailable(CF_DIBV5) ? CF_DIBV5 :
                         IsClipboardFormatAvailable(CF_DIB) ? CF_DIB : 0;
            if (format == 0)
                return false;

            var handle = GetClipboardData(format);
            if (handle == 0)
                return false;

            var address = GlobalLock(handle);
            if (address == 0)
                return false;

            try
            {
                var size = checked((long)GlobalSize(handle));
                if (size is < 40 or > int.MaxValue - 14)
                    return false;

                var dib = new byte[(int)size];
                Marshal.Copy(address, dib, 0, dib.Length);
                var pixelOffset = GetDibPixelOffset(dib);
                if (pixelOffset < 40 || pixelOffset > dib.Length)
                    return false;

                var bmp = new byte[dib.Length + 14];
                bmp[0] = (byte)'B';
                bmp[1] = (byte)'M';
                WriteUInt32(bmp, 2, (uint)bmp.Length);
                WriteUInt32(bmp, 10, (uint)(pixelOffset + 14));
                Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);
                bitmap = new Bitmap(new MemoryStream(bmp, writable: false));
                return true;
            }
            catch
            {
                bitmap?.Dispose();
                bitmap = null;
                return false;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static int GetDibPixelOffset(byte[] dib)
    {
        var headerSize = ReadUInt32(dib, 0);
        if (headerSize < 40 || headerSize > dib.Length)
            return -1;

        var bitCount = ReadUInt16(dib, 14);
        var compression = ReadUInt32(dib, 16);
        var colourCount = ReadUInt32(dib, 32);
        var masksSize = headerSize == 40 && compression is BI_BITFIELDS or BI_ALPHABITFIELDS
            ? compression == BI_ALPHABITFIELDS ? 16 : 12
            : 0;
        var paletteSize = bitCount <= 8
            ? checked((int)(colourCount == 0 ? 1u << bitCount : colourCount) * 4)
            : 0;

        return checked((int)headerSize + masksSize + paletteSize);
    }

    private static ushort ReadUInt16(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | buffer[offset + 1] << 8);

    private static uint ReadUInt32(byte[] buffer, int offset) =>
        (uint)(buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24);

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint GlobalSize(nint hMem);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
