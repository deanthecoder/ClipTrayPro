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
using SkiaSharp;

namespace ClipTrayPro.Services;

/// <summary>
/// Display-ready images and statistics for a pair of clipboard snapshots.
/// </summary>
public sealed class ImageComparison : IDisposable
{
    private const double NormalizationPercentile = 0.99;

    public ImageComparison(ClipboardImageSnapshot previous, ClipboardImageSnapshot latest)
    {
        PreviousSnapshot = previous;
        LatestSnapshot = latest;

        using var previousBitmap = previous.Decode();
        using var latestBitmap = latest.Decode();
        var canvasWidth = Math.Max(previous.Width, latest.Width);
        var canvasHeight = Math.Max(previous.Height, latest.Height);

        PreviousBitmap = CreatePaddedBitmap(previousBitmap, canvasWidth, canvasHeight);
        LatestBitmap = CreatePaddedBitmap(latestBitmap, canvasWidth, canvasHeight);
        CanShowDifference = previous.Width == latest.Width && previous.Height == latest.Height;

        if (CanShowDifference)
            DifferenceBitmap = CreateDifferenceBitmap(previousBitmap, latestBitmap);
    }

    public ClipboardImageSnapshot PreviousSnapshot { get; }
    public ClipboardImageSnapshot LatestSnapshot { get; }
    public Bitmap PreviousBitmap { get; }
    public Bitmap LatestBitmap { get; }
    public Bitmap DifferenceBitmap { get; }
    public bool CanShowDifference { get; }
    public long ChangedPixelCount { get; private set; }
    public int MaskScale { get; private set; }

    public double ChangedPixelPercentage =>
        PreviousSnapshot.Width == 0 || PreviousSnapshot.Height == 0
            ? 0
            : ChangedPixelCount * 100.0 / (PreviousSnapshot.Width * (double)PreviousSnapshot.Height);

    public void Dispose()
    {
        PreviousBitmap.Dispose();
        LatestBitmap.Dispose();
        DifferenceBitmap?.Dispose();
    }

    private Bitmap CreateDifferenceBitmap(SKBitmap previous, SKBitmap latest)
    {
        var differences = new byte[previous.Width * previous.Height];
        var changedMagnitudes = new List<byte>();
        var previousPixels = previous.Pixels;
        var latestPixels = latest.Pixels;

        for (var index = 0; index < differences.Length; index++)
        {
            var a = previousPixels[index];
            var b = latestPixels[index];
            var magnitude = (byte)Math.Max(
                Math.Abs(a.Red - b.Red),
                Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));
            differences[index] = magnitude;
            if (magnitude > 0)
                changedMagnitudes.Add(magnitude);
        }

        ChangedPixelCount = changedMagnitudes.Count;
        changedMagnitudes.Sort();
        MaskScale = changedMagnitudes.Count == 0
            ? 1
            : changedMagnitudes[(int)Math.Floor((changedMagnitudes.Count - 1) * NormalizationPercentile)];
        MaskScale = Math.Max(1, MaskScale);

        using var mask = new SKBitmap(previous.Width, previous.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var maskPixels = new SKColor[differences.Length];
        for (var index = 0; index < differences.Length; index++)
        {
            var magnitude = differences[index];
            var intensity = Math.Min(255, (int)Math.Ceiling(magnitude * 255.0 / MaskScale));
            maskPixels[index] = new SKColor(255, (byte)(255 - intensity), (byte)(255 - intensity));
        }
        mask.Pixels = maskPixels;

        return CreateAvaloniaBitmap(mask);
    }

    private static Bitmap CreatePaddedBitmap(SKBitmap source, int width, int height)
    {
        using var padded = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(padded);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, 0, 0);
        return CreateAvaloniaBitmap(padded);
    }

    private static Bitmap CreateAvaloniaBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = encoded.AsStream();
        return new Bitmap(stream);
    }
}
