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
            if (firstFile?.Path?.IsFile == true)
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
        var firstLine = text?
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .FirstOrDefault();
        return ClipboardTarget.FromPath(firstLine);
    }

    public static async Task<string> GetFingerprintAsync(IClipboard clipboard)
    {
        if (clipboard == null)
            return string.Empty;

        try
        {
            var formats = (await clipboard.GetDataFormatsAsync()).Select(o => o.ToString()).OrderBy(o => o, StringComparer.Ordinal).ToArray();
            var files = await clipboard.TryGetFilesAsync();
            var filePaths = files?
                .Where(o => o.Path?.IsFile == true)
                .Select(o => o.Path.LocalPath)
                .OrderBy(o => o, StringComparer.Ordinal)
                .ToArray() ?? [];
            var text = await clipboard.TryGetTextAsync();
            if (formats.Length == 0 && filePaths.Length == 0 && string.IsNullOrEmpty(text))
                return string.Empty;
            return $"{string.Join("|", formats)}:{string.Join("|", filePaths)}:{text}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
