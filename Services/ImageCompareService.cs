// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace ClipTrayPro.Services;

/// <summary>
/// Retains the two most recent distinct clipboard images.
/// </summary>
public sealed class ImageCompareService
{
    private readonly List<ClipboardImageSnapshot> m_history = [];

    public bool HasImages => m_history.Count > 0;
    public bool CanCompare => m_history.Count == 2;
    public ClipboardImageSnapshot Latest => m_history.LastOrDefault();

    public void Add(ClipboardImageTarget target)
    {
        if (target == null)
            return;

        var snapshot = target.CreateSnapshot();
        if (m_history.LastOrDefault()?.ContentHash == snapshot.ContentHash)
            return;

        m_history.Add(snapshot);
        while (m_history.Count > 2)
            m_history.RemoveAt(0);
    }

    public ImageComparison CreateComparison()
    {
        if (!CanCompare)
            return null;

        return new ImageComparison(m_history[0], m_history[1]);
    }

    public void Clear() => m_history.Clear();
}
