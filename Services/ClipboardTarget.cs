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

        path = path.Trim(' ', '\t', '"', '\'');

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return new ClipboardTarget(uri);

        var file = new FileInfo(path);
        if (file.Exists())
            return new ClipboardTarget(file);

        var directory = new DirectoryInfo(path);
        return directory.Exists() ? new ClipboardTarget(directory) : null;
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
