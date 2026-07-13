// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using System.Text;
using ClipTrayPro.Settings;
using DTC.Core;
using DTC.Core.Extensions;

namespace ClipTrayPro.Services;

/// <summary>
/// Tracks recent clipboard text and launches an external diff app.
/// </summary>
/// <remarks>
/// Clipboard comparisons need short-lived files because most diff tools operate on paths, not raw text.
/// </remarks>
public sealed class TextCompareService : IDisposable
{
    private static readonly TimeSpan TempFileCleanupGracePeriod = TimeSpan.FromSeconds(30);

    private readonly AppSettings m_settings;
    private readonly List<string> m_history = [];
    private readonly List<TempFile> m_tempFiles = [];

    public TextCompareService(AppSettings settings)
    {
        m_settings = settings;
    }

    public bool CanCompare => HasDiffApp && m_history.Count == 2;

    public bool HasDiffApp =>
        !string.IsNullOrWhiteSpace(m_settings.DiffAppPath) &&
        (File.Exists(m_settings.DiffAppPath) || Directory.Exists(m_settings.DiffAppPath));

    public bool HasTextPair => m_history.Count == 2;
    internal int HistoryCount => m_history.Count;
    internal long RetainedCharacterCount => m_history.Sum(o => (long)o.Length);

    public void AddText(string text)
    {
        if (string.IsNullOrEmpty(text) || m_history.LastOrDefault() == text)
            return;

        m_history.Add(text);
        while (m_history.Count > 2)
            m_history.RemoveAt(0);
    }

    public void Compare()
    {
        if (!CanCompare)
            return;

        var left = CreateTempFile(m_history[0]);
        var right = CreateTempFile(m_history[1]);
        var arguments = BuildArguments(m_settings.DiffArguments, left.FullName, right.FullName);

        var process = Process.Start(CreateStartInfo(arguments));
        _ = DeleteTempFilesWhenSafeAsync(process, left, right);
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        m_history.Clear();
        ClearTempFiles();
    }

    private TempFile CreateTempFile(string text)
    {
        var tempFile = new TempFile(".txt").WriteAllText(text);
        m_tempFiles.Add(tempFile);
        return tempFile;
    }

    private void ClearTempFiles()
    {
        foreach (var tempFile in m_tempFiles)
            tempFile.Dispose();
        m_tempFiles.Clear();
    }

    private async Task DeleteTempFilesWhenSafeAsync(Process process, params TempFile[] tempFiles)
    {
        try
        {
            if (process != null)
                await process.WaitForExitAsync();
            await Task.Delay(TempFileCleanupGracePeriod);
        }
        catch
        {
            // Best effort cleanup still happens below.
        }

        foreach (var tempFile in tempFiles)
        {
            tempFile.Dispose();
            m_tempFiles.Remove(tempFile);
        }
    }

    private ProcessStartInfo CreateStartInfo(string arguments)
    {
        if (OperatingSystem.IsMacOS() && m_settings.DiffAppPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"-a {Quote(m_settings.DiffAppPath)} --args {arguments}",
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = m_settings.DiffAppPath,
            Arguments = arguments,
            UseShellExecute = false
        };
    }

    private static string BuildArguments(string template, string leftPath, string rightPath)
    {
        template = string.IsNullOrWhiteSpace(template) ? "$1 $2" : template;
        return template
            .Replace("$1", Quote(leftPath), StringComparison.Ordinal)
            .Replace("$2", Quote(rightPath), StringComparison.Ordinal);
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '"' or '\\')
                builder.Append('\\');
            builder.Append(c);
        }

        return builder.Append('"').ToString();
    }
}
