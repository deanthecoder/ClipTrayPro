// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ClipTrayPro.Services;

/// <summary>
/// An immutable PNG snapshot of an image seen on the clipboard.
/// </summary>
public sealed class ClipboardImageSnapshot
{
    private const int UniqueColourLimit = 1_000_000;
    private readonly byte[] m_pngData;

    public ClipboardImageSnapshot(byte[] pngData)
    {
        m_pngData = pngData.ToArray();
        ContentHash = Convert.ToHexString(SHA256.HashData(m_pngData));

        using var bitmap = Decode();
        Width = bitmap.Width;
        Height = bitmap.Height;
        UniqueColourCount = CountUniqueColours(bitmap, out var wasLimited);
        IsUniqueColourCountLimited = wasLimited;
    }

    public int Width { get; }
    public int Height { get; }
    public int UniqueColourCount { get; }
    public bool IsUniqueColourCountLimited { get; }
    public string ContentHash { get; }

    public string Description =>
        $"{Width:N0} × {Height:N0} px · RGB 8-bit · " +
        (IsUniqueColourCountLimited ? $">{UniqueColourLimit:N0} colours" : $"{UniqueColourCount:N0} colours");

    public Bitmap CreateBitmap() => new(new MemoryStream(m_pngData, writable: false));

    public SKBitmap Decode() =>
        SKBitmap.Decode(m_pngData) ?? throw new InvalidOperationException("Failed to decode clipboard image.");

    private static int CountUniqueColours(SKBitmap bitmap, out bool wasLimited)
    {
        var colours = new HashSet<uint>();
        foreach (var colour in bitmap.Pixels)
        {
            colours.Add(((uint)colour.Red << 16) | ((uint)colour.Green << 8) | colour.Blue);
            if (colours.Count > UniqueColourLimit)
            {
                wasLimited = true;
                return UniqueColourLimit;
            }
        }

        wasLimited = false;
        return colours.Count;
    }
}
