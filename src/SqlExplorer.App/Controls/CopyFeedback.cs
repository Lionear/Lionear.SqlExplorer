using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace SqlExplorer.App.Controls;

/// <summary>
/// A shared, non-modal "Copied" confirmation. Writes text to the clipboard and floats a small toast in the
/// anchor's window via its <see cref="OverlayLayer"/>, so every copy action gets the same feedback without
/// each window carrying its own toast markup. Only opacity is animated (no positional motion), which keeps it
/// unobtrusive and reduced-motion-safe.
/// </summary>
public static class CopyFeedback
{
    private static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(140);

    /// <summary>Copy <paramref name="text"/> to the clipboard, then show the <paramref name="message"/> toast
    /// in <paramref name="anchor"/>'s window. No-op if there's no top-level (e.g. during teardown).</summary>
    public static async Task CopyAsync(Visual? anchor, string text, string message)
    {
        if (anchor is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(anchor)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }

        Show(anchor, message);
    }

    /// <summary>Float the confirmation toast without touching the clipboard — for callers that copied through
    /// another path (e.g. a built-in editor command) but still want the shared feedback.</summary>
    public static void Show(Visual? anchor, string message)
    {
        if (anchor is null
            || TopLevel.GetTopLevel(anchor) is not { } top
            || OverlayLayer.GetOverlayLayer(anchor) is not { } layer)
        {
            return;
        }

        IBrush Brush(string key, Color fallback) =>
            top.TryFindResource(key, top.ActualThemeVariant, out var value) && value is IBrush brush
                ? brush
                : new SolidColorBrush(fallback);

        var toast = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 52),
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
            Opacity = 0,
            Background = Brush("SEPanelBgBrush", Color.FromArgb(0xF0, 0x24, 0x24, 0x24)),
            BorderBrush = Brush("SEHairlineBrush", Colors.Gray),
            Child = new TextBlock
            {
                Text = message,
                FontSize = 12.5,
                Foreground = Brush("SETextPrimaryBrush", Colors.White),
            },
            Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = Fade }],
        };

        layer.Children.Add(toast);
        // Flip opacity on the next render pass so the transition animates from 0 → 1 (setting it in the same
        // pass as the add wouldn't animate).
        Dispatcher.UIThread.Post(() => toast.Opacity = 1, DispatcherPriority.Render);

        DispatcherTimer.RunOnce(() =>
        {
            toast.Opacity = 0;
            DispatcherTimer.RunOnce(() => layer.Children.Remove(toast), Fade);
        }, Dwell);
    }
}
