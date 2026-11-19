using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class InstalledUiTexturePublicationTests
{
    [Fact]
    public void CachedPortraitAndPointerPublishAtomicallyWhileUnrelatedCardsAndHostImagesRetainOwnership()
    {
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        using GpuTestDevice graphics = new();
        using SpriteBatch batch = new(graphics.Device);
        using Texture2D pixel = new(graphics.Device, 1, 1);
        pixel.SetData([Color.White]);
        using SystemTextRenderer text = new(graphics.Device);
        using MonoGameUiCanvas canvas = new(graphics.Device, batch, pixel, text, game, resources);
        Texture2D host = new(graphics.Device, 1, 1);
        host.SetData([Color.Cyan]);
        canvas.RegisterHostImage(90909, host);
        using RenderTarget2D target = new(graphics.Device, 320, 160);

        Color[] Draw()
        {
            graphics.Device.SetRenderTarget(target);
            graphics.Device.Clear(Color.Black);
            canvas.BeginFrame(TimeSpan.Zero);
            batch.Begin(samplerState: SamplerState.PointClamp);
            canvas.Image(new(UiImageKind.CharacterPortrait, 0), new(0, 0, 64, 96));
            canvas.MenuPointer(new(80, 16, 24, 24));
            canvas.Image(new(UiImageKind.Card, 0), new(128, 0, 64, 64));
            canvas.Image(new(UiImageKind.Magazine, 0), new(192, 0, 128, 128));
            canvas.Image(new(UiImageKind.HostArtwork, 90909), new(0, 112, 16, 16));
            batch.End();
            graphics.Device.SetRenderTarget(null);
            return GpuTestDevice.Read(target);
        }

        Color[] before = Draw();
        TextureSourceSnapshot[] affected = resources.CaptureTextureSourcesForImages(canvas.PresentedTextureImages).Where(snapshot => snapshot.Target.Owner.StartsWith("menu/face_b", StringComparison.Ordinal) || snapshot.Target.Owner == "menu/icon.tex" && snapshot.Target.Palette == NativeMenuPointer.Palette).ToArray();
        Assert.Equal(2, affected.Length);
        List<ModFile> files = [];
        foreach (TextureSourceSnapshot snapshot in affected)
        {
            int width = snapshot.Original.Width * 2;
            int height = snapshot.Original.Height * 2;
            PixelImage image = new(width, height, Enumerable.Repeat(new Rgba32(255, 0, 255), width * height).ToArray());
            byte[] png = LocalizationArtworkExporter.EncodePng(image);
            files.Add(new("Memoria/ui-textures", ModFormat.Memoria, snapshot.Target.CanonicalPath(), 1, () => png));
        }
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods native, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        ResourceService.PreparedResources candidate = resources.Prepare(native);
        Assert.Throws<InvalidDataException>(() => canvas.PrepareTexturePublication(candidate, candidate.TextureValues, resources.Generation + 1, maximumBytes: 1));
        Assert.Equal(before, Draw());
        Assert.False(host.IsDisposed);
        using (TexturePublication discarded = Assert.IsType<TexturePublication>(canvas.PrepareTexturePublication(candidate, candidate.TextureValues, resources.Generation + 1)))
        {
            Assert.Equal(before, Draw());
        }
        Assert.Equal(before, Draw());
        Assert.Equal(0, resources.Generation);
        using TexturePublication publication = Assert.IsType<TexturePublication>(canvas.PrepareTexturePublication(candidate, candidate.TextureValues, resources.Generation + 1));
        long uploaded = canvas.ImageUploadCount;
        candidate.Commit(beforePublish: publication.Commit);
        Color[] after = Draw();
        Assert.Contains(after.Zip(before), pair => pair.First != pair.Second);
        Assert.Equal(uploaded, canvas.ImageUploadCount);
        Assert.False(host.IsDisposed);
        Assert.Equal(Color.Cyan, after[120 * 320 + 8]);
        Assert.Contains(resources.CaptureTextureSourcesForImages(canvas.PresentedTextureImages), snapshot => snapshot.Target == affected[0].Target);
        Assert.Contains(resources.CaptureTextureSourcesForImages(canvas.PresentedTextureImages), snapshot => snapshot.Target == affected[1].Target);
        ResourceService.PreparedResources unchanged = resources.Prepare(native);
        using TexturePublication unchangedUi = Assert.IsType<TexturePublication>(canvas.PrepareTexturePublication(unchanged, unchanged.TextureValues, resources.Generation + 1));
        long retainedUploads = canvas.ImageUploadCount;
        unchanged.Commit(beforePublish: unchangedUi.Commit);
        Assert.Equal(after, Draw());
        Assert.Equal(retainedUploads, canvas.ImageUploadCount);
        Assert.False(host.IsDisposed);
    }
}
