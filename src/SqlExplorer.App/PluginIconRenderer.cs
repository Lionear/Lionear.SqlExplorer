using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SqlExplorer.Sdk.Branding;

namespace SqlExplorer.App;

/// <summary>
/// Renders a plugin's <see cref="ProviderIcon"/> to an Avalonia image when the host can decode it — raster
/// only (SVG needs an extra renderer). Shared by the connection tree (<c>MainViewModel</c>) and the Plugin
/// Store's Installed list so the same embedded <c>icon.png</c> shows in both places. Returns null when
/// there's no renderable image, leaving the caller to fall back to a glyph / remote icon / generic vector.
/// </summary>
public static class PluginIconRenderer
{
    public static IImage? Render(ProviderIcon? icon)
    {
        if (icon?.ImageData is not { Length: > 0 } bytes || !CanRender(icon.ImageMediaType))
        {
            return null;
        }

        try
        {
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }

    private static bool CanRender(string? mediaType) =>
        mediaType is not null
        && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        && !mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase);
}
