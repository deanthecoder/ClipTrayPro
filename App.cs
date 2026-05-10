// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClipTrayPro.Services;
using ClipTrayPro.Settings;
using DTC.Core.Commands;
using DTC.Core.UI;

namespace ClipTrayPro;

/// <summary>
/// Wires the desktop lifetime to the hidden clipboard host and tray icon.
/// </summary>
/// <remarks>
/// ClipTrayPro has no normal main UI, so this class owns all visible interaction through the tray and About dialog.
/// </remarks>
public sealed partial class App : Application
{
    private static readonly TimeSpan AutoClearDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ClipboardPollInterval = TimeSpan.FromSeconds(1);

    private readonly AppSettings m_settings = AppSettings.Instance;
    private Window m_clipboardHost;
    private TrayIcons m_trayIcons;
    private TrayIcon m_trayIcon;
    private NativeMenuItem m_openItem;
    private NativeMenuItem m_revealItem;
    private NativeMenuItem m_clearItem;
    private NativeMenuItem m_autoClearItem;
    private NativeMenuItem m_removeFormattingItem;
    private DispatcherTimer m_clipboardTimer;
    private ClipboardTarget m_clipboardTarget;
    private string m_lastClipboardFingerprint = string.Empty;
    private DateTimeOffset? m_autoClearAt;

    public App()
    {
        AboutCommand = new RelayCommand(_ => ShowAboutDialog());
        DataContext = this;
    }

    public ICommand AboutCommand { get; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            m_clipboardHost = CreateClipboardHost();
            desktop.MainWindow = m_clipboardHost;

            m_trayIcon = CreateTrayIcon(desktop);
            m_trayIcons = [m_trayIcon];
            TrayIcon.SetIcons(this, m_trayIcons);

            m_clipboardTimer = new DispatcherTimer { Interval = ClipboardPollInterval };
            m_clipboardTimer.Tick += async (_, _) => await OnClipboardTimerTick();
            m_clipboardTimer.Start();

            desktop.Exit += (_, _) =>
            {
                m_clipboardTimer?.Stop();
                TrayIcon.SetIcons(this, null);
                m_trayIcon?.Dispose();
                m_clipboardHost?.Close();
                m_settings.Save();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Window CreateClipboardHost()
    {
        var window = new Window
        {
            Title = "ClipTrayPro",
            Width = 1,
            Height = 1,
            ShowInTaskbar = false,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-32000, -32000),
            Opacity = 0,
            Icon = IconLoader.LoadWindowIcon()
        };
        window.Show();
        return window;
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        m_openItem = new NativeMenuItem("Open...")
        {
            Command = new RelayCommand(_ => m_clipboardTarget?.Open())
        };

        m_revealItem = new NativeMenuItem("Reveal...")
        {
            Command = new RelayCommand(_ => m_clipboardTarget?.Reveal())
        };

        m_clearItem = new NativeMenuItem("Clear Clipboard")
        {
            Command = new RelayCommand(async _ => await ClearClipboardAsync())
        };

        m_autoClearItem = new NativeMenuItem("Auto-clear after 1 minute")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            Command = new RelayCommand(_ => SetAutoClear(!m_settings.AutoClearClipboard))
        };

        m_removeFormattingItem = new NativeMenuItem("Remove Formatting")
        {
            Command = new RelayCommand(async _ => await RemoveFormattingAsync())
        };

        var menu = new NativeMenu
        {
            m_openItem,
            m_revealItem,
            m_removeFormattingItem,
            new NativeMenuItemSeparator(),
            m_clearItem,
            m_autoClearItem,
            new NativeMenuItemSeparator(),
            new NativeMenuItem("About")
            {
                Command = new RelayCommand(_ => ShowAboutDialog())
            }
        };

#if DEBUG
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(new NativeMenuItem("Exit")
        {
            ToolTip = "Exit",
            Command = new RelayCommand(_ => desktop.Shutdown())
        });
#endif

        menu.NeedsUpdate += async (_, _) => await UpdateMenuAsync();

        var trayIcon = new TrayIcon
        {
            Icon = IconLoader.LoadWindowIcon(),
            IsVisible = true,
            Menu = menu,
            ToolTipText = "ClipTrayPro"
        };

        if (OperatingSystem.IsMacOS())
        {
            MacOSProperties.SetIsTemplateIcon(trayIcon, false);
        }

        return trayIcon;
    }

    private IClipboard Clipboard => m_clipboardHost?.Clipboard;

    private async Task UpdateMenuAsync()
    {
        m_clipboardTarget = await ClipboardReader.GetTargetAsync(Clipboard);

        var hasTarget = m_clipboardTarget != null;
        m_openItem.Header = hasTarget ? $"Open {m_clipboardTarget.DisplayName}" : "Open...";
        m_openItem.ToolTip = hasTarget ? m_clipboardTarget.ToolTip : null;
        m_openItem.IsEnabled = hasTarget;

        m_revealItem.Header = hasTarget ? $"Reveal {m_clipboardTarget.DisplayName}" : "Reveal...";
        m_revealItem.ToolTip = hasTarget ? m_clipboardTarget.ToolTip : null;
        m_revealItem.IsEnabled = hasTarget && m_clipboardTarget.CanReveal;

        m_clearItem.IsEnabled = Clipboard != null;
        m_autoClearItem.IsChecked = m_settings.AutoClearClipboard;
        m_autoClearItem.IsEnabled = Clipboard != null;
        m_removeFormattingItem.IsEnabled = Clipboard != null && !string.IsNullOrEmpty(await Clipboard.TryGetTextAsync());
    }

    private async Task ClearClipboardAsync()
    {
        if (Clipboard == null)
            return;

        await Clipboard.ClearAsync();
        m_autoClearAt = null;
        m_lastClipboardFingerprint = string.Empty;
        await UpdateMenuAsync();
    }

    private void SetAutoClear(bool value)
    {
        m_settings.AutoClearClipboard = value;
        m_settings.Save();
        m_autoClearAt = null;
        m_autoClearItem.IsChecked = value;
    }

    private async Task RemoveFormattingAsync()
    {
        if (Clipboard == null)
            return;

        var text = await Clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
            await Clipboard.SetTextAsync(text);
    }

    private async Task OnClipboardTimerTick()
    {
        await UpdateMenuAsync();

        if (!m_settings.AutoClearClipboard || Clipboard == null)
            return;

        var fingerprint = await ClipboardReader.GetFingerprintAsync(Clipboard);
        if (string.IsNullOrEmpty(fingerprint))
        {
            m_lastClipboardFingerprint = string.Empty;
            m_autoClearAt = null;
            return;
        }

        if (!string.Equals(fingerprint, m_lastClipboardFingerprint, StringComparison.Ordinal))
        {
            m_lastClipboardFingerprint = fingerprint;
            m_autoClearAt = DateTimeOffset.UtcNow + AutoClearDelay;
            return;
        }

        if (m_autoClearAt <= DateTimeOffset.UtcNow)
            await ClearClipboardAsync();
    }

    private void ShowAboutDialog()
    {
        var assembly = Assembly.GetEntryAssembly();
        var dialog = new AboutDialog(new AboutInfo
        {
            Title = "ClipTrayPro",
            Version = assembly?.GetName().Version?.ToString(3) ?? "0.1",
            Copyright = "Copyright (c) 2026 Dean Edis",
            WebsiteUrl = "https://github.com/deanthecoder",
            Icon = IconLoader.LoadBitmap()
        })
        {
            Icon = IconLoader.LoadWindowIcon(),
            ShowInTaskbar = false
        };
        dialog.Show();
    }
}
