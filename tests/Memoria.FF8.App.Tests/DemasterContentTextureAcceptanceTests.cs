using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterContentTextureAcceptanceTests
{
    [Fact]
    public void SuppliedImageCatalogDecodesEveryDeclaredMipAndReportsUnusedContentNames()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CONTENT_TEXTURE_ASSET_FIXTURES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && File.Exists(input), "Set MEMORIA_FF8_CONTENT_TEXTURE_ASSET_FIXTURES to an optional isolated image fixture catalog.");
        AssetCatalog fixture = JsonSerializer.Deserialize<AssetCatalog>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Expected an image fixture catalog.");
        string fixtureBase = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        string root = Path.GetFullPath(fixture.RootDirectory, fixtureBase);
        Assert.Equal(fixture.ExpectedContentCount, fixture.Assets.Count(asset => asset.Category == "content"));
        Assert.Equal(fixture.ExpectedOtherCount, fixture.Assets.Count(asset => asset.Category == "other"));
        Assert.Equal(fixture.ExpectedContentCount + fixture.ExpectedOtherCount, fixture.Assets.Length);
        Assert.Equal(fixture.ExpectedUnusedCount, fixture.UnusedContentPaths.Length);
        List<object> results = [];
        foreach (AssetFixture asset in fixture.Assets)
        {
            string physical = Path.GetFullPath(asset.Path, root);
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, physical, StringComparison.OrdinalIgnoreCase);
            ModFile file = new("Demaster/Fixture", ModFormat.Demaster, asset.Path, 1, () => File.ReadAllBytes(physical));
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            Assert.Equal((asset.Width, asset.Height), (decoded.Image.Width, decoded.Image.Height));
            Assert.Equal(asset.MipCount, decoded.MipLevels.Count);
            long pixels = 0;
            for (int level = 0; level < decoded.MipLevels.Count; level++)
            {
                PixelImage mip = decoded.MipLevels[level];
                Assert.Equal((Math.Max(1, asset.Width >> level), Math.Max(1, asset.Height >> level)), (mip.Width, mip.Height));
                Assert.Equal((long)mip.Width * mip.Height, mip.Pixels.LongLength);
                pixels += mip.Pixels.LongLength;
            }
            results.Add(new { asset.Path, asset.Category, asset.Format, asset.Width, asset.Height, mipCount = decoded.MipLevels.Count, decodedPixels = pixels });
        }
        ModFile[] inspectionFiles = fixture.Assets.Select(asset => asset.Path).Concat(fixture.UnusedContentPaths).Select(path => new ModFile("Demaster/Fixture", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Inspection must not decode assets."))).ToArray();
        DemasterInspection inspection = DemasterTextureResolver.Inspect(new("fixture", GameDataEdition.Remastered, ["demaster-content-upload"]), new(1, [], inspectionFiles));
        string[] unused = inspection.Diagnostics.Where(diagnostic => diagnostic.RelativePath?.StartsWith("hashOutput/", StringComparison.OrdinalIgnoreCase) == true).Select(diagnostic => diagnostic.RelativePath!).ToArray();
        Assert.Equal(fixture.UnusedContentPaths.Order(), unused.Order());
        if (fixture.EvidencePath is string output)
        {
            string destination = Path.GetFullPath(output, fixtureBase);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, JsonSerializer.Serialize(new { decodedContentCount = fixture.ExpectedContentCount, decodedOtherCount = fixture.ExpectedOtherCount, unusedContentPaths = unused, assets = results }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void SuppliedNativeWindowLoadsActualArtworkAndSurvivesToggleAndRevisionReload()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CONTENT_TEXTURE_FIXTURES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && File.Exists(input), "Set MEMORIA_FF8_CONTENT_TEXTURE_FIXTURES to an optional isolated native upload fixture catalog.");
        Fixture[] fixtures = JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Expected a native upload fixture array.");
        Assert.NotEmpty(fixtures);
        string fixtureBase = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        foreach (Fixture fixture in fixtures)
        {
            Verify(fixture, fixtureBase);
        }
    }

    private static void Verify(Fixture fixture, string fixtureBase)
    {
        string modsDirectory = Path.GetFullPath(fixture.ModsDirectory, fixtureBase);
        string timPath = Path.GetFullPath(fixture.TimPath, fixtureBase);
        byte[] tim = File.ReadAllBytes(timPath);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(new(fixture.Id, tim.ToImmutableArray())).State.Drain505D20().State;
        PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, fixture.Page, fixture.Clut, 0, 0, fixture.Width, fixture.Height);
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Remastered, ["demaster-content-upload"]));
        ResourceService resources = new(source);
        List<Exception> failures = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        TextureBuildContext? observed = null;
        using ModRuntime mods = new(applicationBaseDirectory: Path.GetDirectoryName(modsDirectory), modsDirectory: modsDirectory, watch: false, prepare: catalog =>
        {
            RecordingResolver resolver = new(new DemasterTextureResolver(catalog), context => observed = context);
            ResourceService.PreparedResources prepared = resources.Prepare(resolver);
            return new(true, Commit: () => prepared.Commit(notify: false), AfterCommit: prepared.Notify);
        });
        Pump();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage enabled = Apply();
        TextureSourceUpload upload = Assert.IsType<TextureSourceUpload>(observed!.NativeUpload);
        Assert.True(DemasterTextureFingerprint.TryCalculate(upload.Pixels.Span, upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode, out DemasterTextureFingerprint fingerprint));
        Assert.Equal(fixture.Fingerprint, fingerprint.High64.ToString("X16", CultureInfo.InvariantCulture), ignoreCase: true);
        Assert.Equal((fixture.Width, fixture.Height, 4, 7), (upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode));
        string target = $"hashOutput/{fingerprint.High64:X16}_HD.dds";
        ModFile file = Assert.IsType<ModFile>(mods.Catalog.FindAll(ModFormat.Demaster, target).FirstOrDefault());
        DecodedModImage expected = ModImageDecoder.Decode(file);
        Assert.Equal((expected.Image.Width, expected.Image.Height), (enabled.Width, enabled.Height));
        Assert.Equal(Signature(expected.Image), Signature(enabled));
        Assert.NotEqual(Signature(original), Signature(enabled));
        string packageDirectory = Assert.Single(mods.Catalog.Packages, package => package.Id == file.PackageId).RootDirectory;
        string configuration = Path.Combine(packageDirectory, "mod.ini");
        byte[]? priorConfiguration = File.Exists(configuration) ? File.ReadAllBytes(configuration) : null;
        string physical = Path.Combine(packageDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(physical);
        long revision = file.Revision;
        long generation = resources.Generation;
        try
        {
            Assert.True(mods.SetEnabled(file.PackageId, false).Success);
            Pump();
            Assert.Same(original, Apply());
            Assert.True(mods.SetEnabled(file.PackageId, true).Success);
            Pump();
            Assert.Equal(Signature(enabled), Signature(Apply()));
            File.SetLastWriteTimeUtc(physical, originalWriteTime.AddMinutes(1));
            mods.Refresh();
            Pump();
            ModFile refreshed = Assert.Single(mods.Catalog.FindAll(ModFormat.Demaster, target), candidate => candidate.PackageId == file.PackageId);
            Assert.True(refreshed.Revision > revision);
            PixelImage reloaded = Apply();
            Assert.NotSame(enabled, reloaded);
            Assert.Equal(Signature(enabled), Signature(reloaded));
            Assert.True(resources.Generation > generation);
            Assert.Empty(failures);
            Assert.DoesNotContain(mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "reload-failed");
            if (fixture.EvidenceDirectory is string output)
            {
                string directory = Path.GetFullPath(output, fixtureBase);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, fixture.Id + ".json"), JsonSerializer.Serialize(new { fixture.Id, fingerprint = fingerprint.High64.ToString("X16", CultureInfo.InvariantCulture), original = new { original.Width, original.Height, sha256 = Signature(original) }, replacement = new { reloaded.Width, reloaded.Height, sha256 = Signature(reloaded) }, target, revisionBefore = revision, revisionAfter = refreshed.Revision, generationBefore = generation, generationAfter = resources.Generation, toggleRestoredOriginal = true }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            File.SetLastWriteTimeUtc(physical, originalWriteTime);
            if (priorConfiguration is null)
            {
                File.Delete(configuration);
            }
            else
            {
                File.WriteAllBytes(configuration, priorConfiguration);
            }
        }

        PixelImage Apply() => textures.Apply(original, state, fixture.Page, fixture.Clut, 0, 0, fixture.Width, fixture.Height);

        void Pump()
        {
            Assert.True(mods.PreparePending(force: true));
            Assert.True(mods.CommitPending());
        }
    }

    private static string Signature(PixelImage image)
    {
        return Convert.ToHexString(SHA256.HashData(image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));
    }

    private sealed class RecordingResolver(ITextureReplacementResolver inner, Action<TextureBuildContext> observe) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (context.NativeUpload is not null)
            {
                observe(context);
            }
            return inner.Resolve(context);
        }
    }

    private sealed record Fixture(string Id, string TimPath, string ModsDirectory, string Fingerprint, ushort Page, ushort Clut, int Width, int Height, string? EvidenceDirectory = null);
    private sealed record AssetFixture(string Path, string Category, string Format, int Width, int Height, int MipCount);
    private sealed record AssetCatalog(string RootDirectory, int ExpectedContentCount, int ExpectedOtherCount, int ExpectedUnusedCount, AssetFixture[] Assets, string[] UnusedContentPaths, string? EvidencePath = null);
}
