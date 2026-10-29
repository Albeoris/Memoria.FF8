using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFontProfileCatalogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-font-catalog-" + Guid.NewGuid().ToString("N"));
    private static readonly NativeTextContext Context = new("Classic", "Fields", "map", "en");

    [Theory]
    [InlineData(8, 1, 7)]
    [InlineData(15, 1, 13)]
    [InlineData(24, 3, 18)]
    public void FractionalClippingStaysWithinItsGlyphAndPreservesOriginalRasterScale(int destinationWidth, int clipLeft, int clipWidth)
    {
        Rectangle original = new(0, 0, destinationWidth, destinationWidth);
        Rectangle image = new(24, 36, 12, 12);
        NativeGlyphPlacement placement = NativeGlyphClipping.Clip(original, image, new(clipLeft, 0, clipWidth, destinationWidth))!.Value;
        Assert.InRange(placement.Source.Left, image.Left, image.Right);
        Assert.InRange(placement.Source.Right, image.Left, image.Right);
        Assert.Equal(destinationWidth / 12f, placement.Scale.X);
        Assert.Equal(original.X + (placement.Source.X - image.X) * placement.Scale.X, placement.Position.X);
        Assert.True(placement.Position.X >= clipLeft);
        Assert.True(placement.Position.X + placement.Source.Width * placement.Scale.X <= clipLeft + clipWidth + 0.0001f);
    }

    [Fact]
    public void LoaderUsesImmediateFoldersAndDependencyContentRatherThanTimestamps()
    {
        string directory = WriteProfile();
        string profilePath = Path.Combine(directory, "profile.json");
        File.WriteAllText(profilePath, File.ReadAllText(profilePath), new System.Text.UTF8Encoding(true));
        string extra = Path.Combine(directory, "fonts", "ignored");
        Directory.CreateDirectory(extra);
        File.WriteAllText(Path.Combine(extra, "profile.json"), "not a profile");
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        NativeFontProfileCatalog first = NativeFontPresentation.LoadCatalog(resources, [Package()]);
        Assert.Equal(new[] { "Auto", "one" }, first.AvailableProfileIds);
        string atlas = Path.Combine(directory, "font.png");
        DateTime timestamp = File.GetLastWriteTimeUtc(atlas);
        WritePixel(atlas, MagickColors.Black);
        File.SetLastWriteTimeUtc(atlas, timestamp);
        NativeFontProfileCatalog second = NativeFontPresentation.LoadCatalog(resources, [Package()], first);
        Assert.NotEqual(first.Revision, second.Revision);
        Assert.Equal((byte)0, second.Resolve("one", Context, "Target", "ru").Entry!.Atlases["font"][0, 0].R);
        Assert.Same(second, NativeFontPresentation.LoadCatalog(resources, [Package()], second));
    }

    [Fact]
    public async Task FailedBackgroundReloadRetainsTheLastValidatedCatalog()
    {
        string directory = WriteProfile();
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        using NativeFontPresentation presentation = new(null!);
        async Task Pump(Func<bool> complete)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            do
            {
                presentation.Prepare(resources, [Package()], "one", "ru");
                if (complete())
                {
                    return;
                }
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            while (DateTime.UtcNow < deadline);
            Assert.Fail("Font background preparation did not reach its expected bounded state.");
        }
        await Pump(() => presentation.Ready);
        long revision = presentation.Revision;
        File.WriteAllText(Path.Combine(directory, "profile.json"), "broken JSON");
        await Pump(() => presentation.Diagnostic?.Contains("retaining", StringComparison.Ordinal) == true);
        Assert.True(presentation.Ready);
        Assert.Equal(revision, presentation.Revision);
        Assert.Equal("one", presentation.ProfileFor(Context)!.Id);
        Assert.True(presentation.Layout("A", Context).Success);
    }

    [Fact]
    public void LoaderRejectsPortableCaseMismatchBeforeOpeningAProfile()
    {
        string directory = WriteProfile();
        File.Move(Path.Combine(directory, "profile.json"), Path.Combine(directory, "Profile.json"));
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
        Assert.Throws<InvalidDataException>(() => NativeFontPresentation.LoadCatalog(new(source), [Package()]));
    }

    private string WriteProfile()
    {
        string directory = Path.Combine(root, "Encoding", "One");
        Directory.CreateDirectory(directory);
        WritePixel(Path.Combine(directory, "font.png"), MagickColors.White);
        NativeFontProfile profile = Profile("one") with { Atlases = [new("font", "font.png", 1, 1)], Images = [new(7, "font", 0, 0, 1, 1, 1)], Glyphs = [new("A", 7)], Assignments = [new("Target", "ru", "one", Context)] };
        NativeFontProfileIO.Save(Path.Combine(directory, "profile.json"), profile);
        return directory;
    }

    private static void WritePixel(string path, IMagickColor<byte> color)
    {
        using MagickImage image = new(color, 1, 1);
        image.Write(path, MagickFormat.Png);
    }

    private ModPackage Package() => new("test", "Synthetic", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root);
    private static NativeFontProfile Profile(string id) => new() { Id = id, DisplayName = id };


    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("memoria-font-catalog-", StringComparison.Ordinal))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
