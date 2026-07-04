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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClipTrayPro.Services;

namespace ClipTrayPro;

/// <summary>
/// Fades between two clipboard images, optionally passing through a normalized difference mask.
/// </summary>
public partial class ImageCompareWindow : Window
{
    private readonly ImageComparison m_comparison;

    public ImageCompareWindow()
    {
        InitializeComponent();
    }

    public ImageCompareWindow(ImageComparison comparison)
        : this()
    {
        m_comparison = comparison;
        PreviousImage.Source = comparison.PreviousBitmap;
        LatestImage.Source = comparison.LatestBitmap;
        DifferenceImage.Source = comparison.DifferenceBitmap;

        DifferenceMaskBox.IsEnabled = comparison.CanShowDifference;
        DifferenceMaskBox.IsChecked = comparison.CanShowDifference;
        PreviousDetailsText.Text = comparison.PreviousSnapshot.Description;
        LatestDetailsText.Text = comparison.LatestSnapshot.Description;
        DifferenceDetailsText.Text = comparison.CanShowDifference
            ? $"{comparison.ChangedPixelCount:N0} pixels changed ({comparison.ChangedPixelPercentage:N3}%) · " +
              $"Mask normalized to Δ{comparison.MaskScale} (99th percentile)"
            : "The image dimensions differ, so the difference mask is unavailable.";

        UpdateBlend();
        Closed += (_, _) => m_comparison.Dispose();
    }

    private void OnBlendValueChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateBlend();

    private void OnDifferenceMaskChanged(object sender, RoutedEventArgs e) => UpdateBlend();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var closeModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.W && e.KeyModifiers.HasFlag(closeModifier))
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private void UpdateBlend()
    {
        if (m_comparison == null)
            return;

        var position = BlendSlider.Value / 100.0;
        var useMask = DifferenceMaskBox.IsChecked == true && m_comparison.CanShowDifference;
        DifferenceImage.IsVisible = useMask;
        DifferenceLabel.IsVisible = useMask;

        if (!useMask)
        {
            PreviousImage.Opacity = 1.0 - position;
            LatestImage.Opacity = position;
            DifferenceImage.Opacity = 0;
            return;
        }

        if (position <= 0.5)
        {
            var blend = position * 2.0;
            PreviousImage.Opacity = 1.0 - blend;
            DifferenceImage.Opacity = blend;
            LatestImage.Opacity = 0;
        }
        else
        {
            var blend = (position - 0.5) * 2.0;
            PreviousImage.Opacity = 0;
            DifferenceImage.Opacity = 1.0 - blend;
            LatestImage.Opacity = blend;
        }
    }
}
