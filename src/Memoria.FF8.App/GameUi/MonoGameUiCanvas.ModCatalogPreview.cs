using Albeoris.Games.FF8.Imaging;
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

    private sealed class ComparisonTexture(Texture2D texture)
    {
        public Texture2D Texture { get; } = texture;
        public Color[] Pixels { get; } = new Color[checked(texture.Width * texture.Height)];
        public PixelImage? Image { get; set; }
        public long Revision { get; set; }
    }

    private readonly Dictionary<int, ComparisonTexture> comparisonTextures = [];
    private bool comparisonDrawn;

    public void RasterImage(PixelImage image, UiRect bounds, int slot, long revision = 0)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        comparisonDrawn = true;
        if (!comparisonTextures.TryGetValue(slot, out ComparisonTexture? cached) || cached.Texture.Width != image.Width || cached.Texture.Height != image.Height)
        {
            Texture2D texture = new(device, image.Width, image.Height);
            ComparisonTexture replacement;
            try
            {
                replacement = new(texture);
                Upload(replacement, image, revision);
            }
            catch
            {
                texture.Dispose();
                throw;
            }
            cached?.Texture.Dispose();
            comparisonTextures[slot] = cached = replacement;
        }
        else if (!ReferenceEquals(cached.Image, image) || cached.Revision != revision)
        {
            Upload(cached, image, revision);
        }
        UiRect fitted = UiImage.Fit(bounds, image.Width, image.Height);
        SystemTextRenderer.DrawClipped(batch, cached.Texture, new Vector2(fitted.X, fitted.Y), Color.White, CurrentClip, fitted.Width / image.Width);
    }

    private static void Upload(ComparisonTexture destination, PixelImage image, long revision)
    {
        for (int index = 0; index < image.Pixels.Length; index++)
        {
            Rgba32 color = image.Pixels[index];
            destination.Pixels[index] = Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
        }
        destination.Texture.SetData(destination.Pixels);
        destination.Image = image;
        destination.Revision = revision;
    }

    private void TrimComparisonTextures()
    {
        if (!comparisonDrawn)
        {
            foreach (var cached in comparisonTextures.Values)
            {
                cached.Texture.Dispose();
            }
            comparisonTextures.Clear();
        }
        comparisonDrawn = false;
    }

    private void DisposeModPreviews()
    {
        comparisonDrawn = false;
        TrimComparisonTextures();
        modPreviews.Dispose();
        foreach (var cached in modPreviewTextures.Values)
        {
            cached.Texture.Dispose();
        }
        modPreviewTextures.Clear();
    }
}
