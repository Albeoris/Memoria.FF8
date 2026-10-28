using Memoria.FF8.Platform;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas
{
    private readonly ModCatalogPreviewCache modPreviews = new(Path.Combine(ApplicationPaths.Current.Cache, "mod-previews"));
    private readonly Dictionary<string, (Texture2D Texture, long Used)> modPreviewTextures = new(StringComparer.Ordinal);
    private long modPreviewUse;
    private string? selectedModPreview;
    private bool modPreviewDemanded;

    public bool ModPreview(string url, UiRect bounds)
    {
        modPreviewDemanded = true;
        selectedModPreview = url;
        if (!ModCatalogPreviewCache.IsAllowedUrl(url) || bounds.IsEmpty)
        {
            modPreviews.Request("");
            return false;
        }
        if (!modPreviewTextures.TryGetValue(url, out var cached))
        {
            modPreviews.Request(url);
            return false;
        }
        modPreviewTextures[url] = (cached.Texture, ++modPreviewUse);
        Texture2D texture = cached.Texture;
        UiRect fitted = UiImage.Fit(bounds, texture.Width, texture.Height);
        Rectangle target = RectangleFor(fitted);
        Rectangle clipped = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        clipped = Rectangle.Intersect(target, clipped);
        if (clipped.Width > 0 && clipped.Height > 0 && target.Width > 0 && target.Height > 0)
        {
            Rectangle source = new((clipped.X - target.X) * texture.Width / target.Width, (clipped.Y - target.Y) * texture.Height / target.Height, Math.Max(1, clipped.Width * texture.Width / target.Width), Math.Max(1, clipped.Height * texture.Height / target.Height));
            batch.Draw(texture, clipped, source, Color.White);
        }
        return true;
    }

    private void PumpModPreview()
    {
        if (!modPreviewDemanded)
        {
            selectedModPreview = null;
            modPreviews.Request("");
        }
        modPreviewDemanded = false;
        ModCatalogPreviewCache.Decoded? ready = modPreviews.TakeCompleted();
        if (ready == null || ready.Url != selectedModPreview || modPreviewTextures.ContainsKey(ready.Url))
        {
            return;
        }
        // Worker output is capped at 512 x 512 RGBA; create at most one texture per frame.
        Texture2D texture = new(device, ready.Width, ready.Height);
        try
        {
            texture.SetData(ready.Pixels);
            while (modPreviewTextures.Count >= 4)
            {
                string oldest = modPreviewTextures.MinBy(pair => pair.Value.Used).Key;
                modPreviewTextures[oldest].Texture.Dispose();
                modPreviewTextures.Remove(oldest);
            }
            modPreviewTextures[ready.Url] = (texture, ++modPreviewUse);
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private void DisposeModPreviews()
    {
        modPreviews.Dispose();
        foreach (var cached in modPreviewTextures.Values)
        {
            cached.Texture.Dispose();
        }
        modPreviewTextures.Clear();
    }
}

