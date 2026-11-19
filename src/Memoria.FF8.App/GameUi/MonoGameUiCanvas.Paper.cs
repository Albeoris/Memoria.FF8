using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas
{
    private Texture2D? paper;
    private UiColor paperColor;
    private static readonly TextureKey PaperSourceKey = new(new("menu/magita.tex"), "atlas");
    private AssetHandle<TextureValue>? paperSourceHandle;
    private TextureValue? boundPaperValue;
    private UiColor boundPaperColor;

    private UiColor ResolvePaperColor(UiColor fallback)
    {
        if (resources is null)
        {
            return fallback;
        }
        if (paperSourceHandle is null)
        {
            ResourceContent encoded = resources.Get(PaperSourceKey.Owner);
            byte[] bytes = resources.Read(PaperSourceKey.Owner);
            _ = NativeMagazineArtwork.GetPaperColor(bytes);
            var original = TexFile.Read(bytes).Decode();
            paperSourceHandle = resources.GetTexture(new(PaperSourceKey, resources.Source, encoded.Revision, "native-artwork", original.Width, original.Height, () => original, encodedSource: encoded));
        }
        TextureValue current = paperSourceHandle.Value;
        if (!ReferenceEquals(boundPaperValue, current))
        {
            var mean = NativeMagazineArtwork.GetPaperColor(current.Image);
            boundPaperColor = new(mean.R, mean.G, mean.B);
            boundPaperValue = current;
        }
        presentedTextureImages.Add(current.Image);
        return boundPaperColor;
    }

    public void Paper(UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        color = ResolvePaperColor(color);
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        if (paper is null || paper.Width != width || paper.Height != height || paperColor != color)
        {
            paper?.Dispose();
            paper = new Texture2D(device, width, height);
            paper.SetData(UiPaper.CreatePixels(width, height, color).Select(ColorFor).ToArray());
            paperColor = color;
            ImageUploadCount++;
        }
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        SystemTextRenderer.DrawClipped(batch, paper, new Vector2(bounds.X, bounds.Y), Color.White, clip, 1);
    }
}
