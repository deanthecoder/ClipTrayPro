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
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClipTrayPro.Settings;

namespace ClipTrayPro;

/// <summary>
/// Edits ClipTrayPro user settings.
/// </summary>
/// <remarks>
/// The tray remains the primary UI, so this window only contains configuration that cannot fit comfortably in a menu.
/// </remarks>
public partial class SettingsWindow : Window
{
    private readonly AppSettings m_settings;

    public event EventHandler Saved;

    public SettingsWindow()
    {
        InitializeComponent();
        ConfigurePlatformUi();
    }

    public SettingsWindow(AppSettings settings)
        : this()
    {
        m_settings = settings;
        DiffAppPathBox.Text = settings.DiffAppPath;
        DiffArgumentsBox.Text = string.IsNullOrWhiteSpace(settings.DiffArguments) ? "$1 $2" : settings.DiffArguments;
        SelectSavedMacApp(settings.DiffAppPath);
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select diff app",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Applications")
                {
                    Patterns = ["*.exe", "*.cmd", "*.bat"]
                },
                FilePickerFileTypes.All
            ]
        });

        var selectedFile = files.FirstOrDefault();
        if (selectedFile != null)
        {
            DiffAppPathBox.Text = selectedFile.Path.LocalPath;
            DiffAppStatusText.Text = string.Empty;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        m_settings.DiffAppPath = OperatingSystem.IsMacOS()
            ? (MacDiffAppBox.SelectedItem as MacApplication)?.Path ?? string.Empty
            : DiffAppPathBox.Text?.Trim() ?? string.Empty;
        m_settings.DiffArguments = string.IsNullOrWhiteSpace(DiffArgumentsBox.Text) ? "$1 $2" : DiffArgumentsBox.Text.Trim();
        m_settings.Save();
        Saved?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ConfigurePlatformUi()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        DiffAppPathPanel.IsVisible = false;
        MacDiffAppBox.IsVisible = true;
        MacDiffAppBox.ItemsSource = Directory
            .EnumerateDirectories("/Applications", "*.app", SearchOption.TopDirectoryOnly)
            .Select(o => new MacApplication(o))
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SelectSavedMacApp(string path)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrWhiteSpace(path))
            return;

        var match = MacDiffAppBox.Items
            .OfType<MacApplication>()
            .FirstOrDefault(o => string.Equals(o.Path, path, StringComparison.Ordinal));
        if (match != null)
            MacDiffAppBox.SelectedItem = match;
    }

    public sealed class MacApplication
    {
        public MacApplication(string path)
        {
            Path = path;
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public string Path { get; }
        public string DisplayName { get; }
    }
}
