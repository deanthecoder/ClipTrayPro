// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Settings;

namespace ClipTrayPro.Settings;

/// <summary>
/// Persistent user preferences for ClipTrayPro.
/// </summary>
/// <remarks>
/// The app has no settings window, so tray toggles use DTC.Core settings for durable state.
/// </remarks>
public sealed class AppSettings : UserSettingsBase
{
    public static AppSettings Instance { get; } = new AppSettings();

    protected override string SettingsFileName => "cliptraypro-settings.json";

    public bool AutoClearClipboard
    {
        get => Get<bool>();
        set => Set(value);
    }

    public string DiffAppPath
    {
        get => Get<string>();
        set => Set(value ?? string.Empty);
    }

    public string DiffArguments
    {
        get => Get<string>();
        set => Set(value ?? string.Empty);
    }

    public int MemoryReportThresholdMb
    {
        get => Get<int>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        AutoClearClipboard = false;
        DiffAppPath = string.Empty;
        DiffArguments = "$1 $2";
        MemoryReportThresholdMb = 512;
    }
}
