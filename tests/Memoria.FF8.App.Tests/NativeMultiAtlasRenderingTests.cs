using ImageMagick;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeMultiAtlasRenderingTests
{
    [Fact]
    public void GameRendererUsesIndependentImagesForLatinCyrillicAndJapaneseInOneLine()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-multilingual-font-" + Guid.NewGuid().ToString("N"));
        try
        {
            string directory = Path.Combine(root, "Encoding", "Multilingual");
            Directory.CreateDirectory(directory);
            string[] atlasIds = ["latin", "cyrillic", "japanese"];
            IMagickColor<byte>[] colors = [MagickColors.Red, MagickColors.Lime, MagickColors.Blue];
            for (int index = 0; index < atlasIds.Length; index++)
            {
                using MagickImage image = new(colors[index], 4, 8);
                image.Write(Path.Combine(directory, atlasIds[index] + ".png"), MagickFormat.Png);
            }
            NativeTextContext context = new("Classic", "Fields", "test", "en");
            NativeFontProfile profile = new()
            {
                Id = "multilingual",
                DisplayName = "Multilingual synthetic fixture",
                LineHeight = 8,
                Domains = [new("Classic", "Fields", DictionaryLanguage: "en")],
                Atlases = atlasIds.Select(id => new NativeFontAtlas(id, id + ".png", 4, 8)).ToArray(),
                Images = [new(70, "latin", 0, 0, 4, 8, 5), new(12, "cyrillic", 0, 0, 4, 8, 5), new(4096, "japanese", 0, 0, 4, 8, 5)],
                Glyphs = [new("A", 70), new("Б", 12), new("日", 4096)],
                Assignments = [new("Target", "mixed", "multilingual", context)]
            };
            NativeFontProfileIO.Save(Path.Combine(directory, "profile.json"), profile);
            using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
            ResourceService resources = new(source);
            using GpuTestDevice device = new();
            using NativeFontPresentation presentation = new(device.Device);
            ModPackage package = new("fixture", "Fixture", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root);
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (!presentation.Ready && DateTime.UtcNow < deadline)
            {
                presentation.Prepare(resources, [package], "multilingual", "mixed");
                Thread.Sleep(10);
            }
            Assert.True(presentation.Ready, presentation.Diagnostic);
            NativeFontLayoutResult layout = presentation.Layout("AБ日", context);
            Assert.True(layout.Success);
            Assert.Equal(new[] { 70, 12, 4096 }, layout.Glyphs.Select(glyph => glyph.Image.Index));
            using RenderTarget2D target = device.CreateTarget(20, 12);
            device.Device.SetRenderTarget(target);
            device.Device.Clear(Color.Transparent);
            using SpriteBatch batch = new(device.Device);
            batch.Begin(samplerState: SamplerState.PointClamp);
            Assert.True(presentation.Draw(batch, "AБ日", new(1, 1), 1, Color.White, context, new(0, 0, 20, 12)));
            batch.End();
            device.Device.SetRenderTarget(null);
            Color[] pixels = new Color[20 * 12];
            target.GetData(pixels);
            Assert.Equal(Color.Red, pixels[3 * 20 + 2]);
            Assert.Equal(Color.Lime, pixels[3 * 20 + 7]);
            Assert.Equal(Color.Blue, pixels[3 * 20 + 12]);
            Assert.Equal(Color.Transparent, pixels[3 * 20 + 5]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
