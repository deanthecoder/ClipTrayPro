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
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using DTC.Core.Extensions;

namespace ClipTrayPro.Services;

/// <summary>
/// Represents a file, directory, or web address found on the clipboard.
/// </summary>
/// <remarks>
/// The tray menu only needs one actionable target, so this type normalizes clipboard text and file-drop data.
/// </remarks>
public sealed class ClipboardTarget
{
    private enum TargetType
    {
        File,
        Directory,
        WebAddress
    }

    private static readonly Regex BareWebAddressPattern = new(
        @"^(?:www\.|[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?::\d+)?(?:[/?#].*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TargetType m_type;
    private readonly FileInfo m_file;
    private readonly DirectoryInfo m_directory;
    private readonly Uri m_uri;

    private ClipboardTarget(FileInfo file)
    {
        m_type = TargetType.File;
        m_file = file;
    }

    private ClipboardTarget(DirectoryInfo directory)
    {
        m_type = TargetType.Directory;
        m_directory = directory;
    }

    private ClipboardTarget(Uri uri)
    {
        m_type = TargetType.WebAddress;
        m_uri = uri;
    }

    public string DisplayName =>
        m_type switch
        {
            TargetType.File => m_file.Name,
            TargetType.Directory => m_directory.Name,
            TargetType.WebAddress => m_uri.Host,
            _ => string.Empty
        };

    public string FullPath =>
        m_type switch
        {
            TargetType.File => m_file.FullName,
            TargetType.Directory => m_directory.FullName,
            TargetType.WebAddress => m_uri.AbsoluteUri,
            _ => string.Empty
        };

    public string ToolTip =>
        m_type switch
        {
            TargetType.File => $"{m_file.FullName}{Environment.NewLine}Size: {m_file.Length.ToSize()}",
            TargetType.Directory => $"{m_directory.FullName}{Environment.NewLine}{GetDirectoryDetails(m_directory)}",
            TargetType.WebAddress => m_uri.AbsoluteUri,
            _ => string.Empty
        };

    public bool CanReveal => m_type != TargetType.WebAddress;

    public static ClipboardTarget FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var candidate in GetPathCandidates(path))
        {
            var target = TryCreate(candidate);
            if (target != null)
                return target;
        }

        return null;
    }

    private static ClipboardTarget TryCreate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
            path = uri.LocalPath;
        else if (Uri.TryCreate(path, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return new ClipboardTarget(uri);
        else if (BareWebAddressPattern.IsMatch(path) && Uri.TryCreate($"https://{path}", UriKind.Absolute, out uri))
            return new ClipboardTarget(uri);

        var file = new FileInfo(path);
        if (file.Exists())
            return new ClipboardTarget(file);

        var directory = new DirectoryInfo(path);
        return directory.Exists() ? new ClipboardTarget(directory) : null;
    }

    private static IEnumerable<string> GetPathCandidates(string path)
    {
        path = path.Trim();
        if (string.IsNullOrEmpty(path))
            yield break;

        yield return path;

        var unquoted = TrimMatchingQuotes(path);
        if (!string.Equals(unquoted, path, StringComparison.Ordinal))
            yield return unquoted;

        var jsonUnescaped = TryJsonUnescape(path);
        if (!string.IsNullOrEmpty(jsonUnescaped))
            yield return jsonUnescaped;

        jsonUnescaped = TryJsonUnescape(unquoted);
        if (!string.IsNullOrEmpty(jsonUnescaped))
            yield return jsonUnescaped;

        var slashUnescaped = unquoted.Replace(@"\\", @"\", StringComparison.Ordinal);
        if (!string.Equals(slashUnescaped, unquoted, StringComparison.Ordinal))
            yield return slashUnescaped;

        var quoteUnescaped = slashUnescaped
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\'", "'", StringComparison.Ordinal);
        if (!string.Equals(quoteUnescaped, slashUnescaped, StringComparison.Ordinal))
            yield return quoteUnescaped;
    }

    private static string TrimMatchingQuotes(string value)
    {
        value = value.Trim();
        if (value.Length < 2)
            return value;

        return (value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')
            ? value[1..^1].Trim()
            : value;
    }

    private static string TryJsonUnescape(string value)
    {
        value = TrimMatchingQuotes(value);
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        try
        {
            return JsonConvert.DeserializeObject<string>($"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"");
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Open()
    {
        switch (m_type)
        {
            case TargetType.File:
                m_file.OpenWithDefaultViewer();
                break;
            case TargetType.Directory:
                m_directory.Explore();
                break;
            case TargetType.WebAddress:
                m_uri.Open();
                break;
            default:
                throw new UnreachableException();
        }
    }

    public void Reveal()
    {
        switch (m_type)
        {
            case TargetType.File:
                m_file.Explore();
                break;
            case TargetType.Directory:
                m_directory.Explore();
                break;
            case TargetType.WebAddress:
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static string GetDirectoryDetails(DirectoryInfo directory)
    {
        try
        {
            return $"Contains {directory.EnumerateDirectories().Count():N0} folders, {directory.EnumerateFiles().Count():N0} files";
        }
        catch
        {
            return "Folder details unavailable";
        }
    }
}
