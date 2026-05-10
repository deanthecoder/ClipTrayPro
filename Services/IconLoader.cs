// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ClipTrayPro.Services;

/// <summary>
/// Loads packaged application icons.
/// </summary>
/// <remarks>
/// Keeping resource lookup centralized keeps tray and About dialog icon usage consistent.
/// </remarks>
public static class IconLoader
{
    public static WindowIcon LoadWindowIcon() =>
        new WindowIcon(AssetLoader.Open(new Uri("avares://ClipTrayPro/Assets/app.ico")));

    public static Bitmap LoadBitmap() =>
        new Bitmap(AssetLoader.Open(new Uri("avares://ClipTrayPro/Assets/app.png")));
}
