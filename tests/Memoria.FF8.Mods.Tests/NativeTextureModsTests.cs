using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class NativeTextureModsTests
{
    private static readonly TextureTarget Target = new("ff8", "field/sample/model.mch", "texture/0");

    [Fact]
    public void ConventionalImageLoadsWithoutMappingAndIgnoresUnrelatedImages()
    {
        string path = Target.CanonicalPath();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File(path, Png()), File("items/icon.png", [1, 2])]), out NativeTextureMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(mods.Resolve(Context()));
        Assert.Equal(2, replacement.Image.Width);
        Assert.Contains(path, replacement.Provenance);
    }

    [Fact]
    public void RenamedImageUsesSidecarAndOptionalExtensionsSurvive()
    {
        TextureModMapping mapping = new(1, Target, "friendly.png", new("image", 2, 2))
        {
            Extensions = new() { ["extensions"] = System.Text.Json.JsonSerializer.SerializeToElement(new { author = "sample" }) }
        };
        TextureModMapping parsed = TextureModMapping.Parse(mapping.ToBytes());
        Assert.Equal("sample", parsed.Extensions!["extensions"].GetProperty("author").GetString());
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File("textures/friendly.png", Png()), File("textures/friendly.texture.json", mapping.ToBytes())]), out NativeTextureMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.NotNull(mods.Resolve(Context()));
    }

    [Fact]
    public void ConflictingExplicitIdentityRejectsEntireCandidate()
    {
        string path = Target.CanonicalPath();
        TextureModMapping mapping = new(1, Target with { Palette = 1 }, Path.GetFileName(path));
        Assert.False(NativeTextureMods.TryPrepare(new(1, [], [File(path, Png()), File(path + TextureModMapping.Extension, mapping.ToBytes())]), out NativeTextureMods mods, out var diagnostics));
        Assert.Empty(mods.Variants);
        Assert.Contains(diagnostics, item => item.Message.Contains("conflicts"));
    }

    [Fact]
    public void DuplicateTargetsInsideOnePackageRejectButDifferentPackagesUseCatalogOrder()
    {
        TextureModMapping mapping = new(1, Target, "friendly.png");
        Assert.False(NativeTextureMods.TryPrepare(new(1, [], [File(Target.CanonicalPath(), Png()), File("textures/friendly.png", Png()), File("textures/friendly.texture.json", mapping.ToBytes())]), out _, out var diagnostics));
        Assert.Contains(diagnostics, item => item.Message.Contains("same texture target"));
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File(Target.CanonicalPath(), Png(), "Memoria/first"), File(Target.CanonicalPath(), Png(), "Memoria/second")]), out NativeTextureMods mods, out _));
        Assert.StartsWith("Memoria/first/", mods.Resolve(Context())!.Provenance);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"target\":null,\"image\":\"x.png\"}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    public void UnsupportedAndDuplicateMetadataFails(string json)
    {
        Assert.Throws<InvalidDataException>(() => TextureModMapping.Parse(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void PublicationFailureRollsBackAndSuccessPreservesDescriptorSettingsAndOtherContent()
    {
        using TemporaryPackage temporary = new();
        string path = Path.Combine(temporary.Package, Target.CanonicalPath().Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] original = Png("red");
        System.IO.File.WriteAllBytes(path, original);
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png("blue"))]);
        using (TextureModInstallTransaction transaction = TextureModInstaller.BeginInstall(temporary.Root, temporary.Package, plan))
        {
            Assert.NotEqual(original, System.IO.File.ReadAllBytes(path));
        }
        Assert.Equal(original, System.IO.File.ReadAllBytes(path));
        using (TextureModInstallTransaction transaction = TextureModInstaller.BeginInstall(temporary.Root, temporary.Package, plan))
        {
            transaction.Commit();
        }
        Assert.Equal("descriptor", System.IO.File.ReadAllText(Path.Combine(temporary.Package, "mod.json")));
        Assert.Equal("Enabled=0", System.IO.File.ReadAllText(Path.Combine(temporary.Package, "mod.ini")));
        Assert.Equal("other", System.IO.File.ReadAllText(Path.Combine(temporary.Package, "unrelated.txt")));
        Assert.Empty(Directory.EnumerateDirectories(temporary.Package, ".texture-stage-*"));
    }

    [Fact]
    public void ConcurrentDestinationEditIsRetainedAndRequiresReview()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        using TextureModInstallTransaction transaction = TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, plan);
        System.IO.File.WriteAllText(Path.Combine(temporary.Package, "mod.ini"), "Enabled=1");
        Assert.Throws<IOException>(() => transaction.Apply());
        Assert.Equal("Enabled=1", System.IO.File.ReadAllText(Path.Combine(temporary.Package, "mod.ini")));
    }

    [Fact]
    public void InterruptedAppliedTransactionCanRecoverPreviousFiles()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        TextureModInstallTransaction abandoned = TextureModInstaller.BeginInstall(temporary.Root, temporary.Package, plan);
        Assert.Throws<IOException>(() => TextureModInstaller.Recover(temporary.Root, temporary.Package));
        SimulateProcessTermination(abandoned);
        TextureModInstaller.Recover(temporary.Root, temporary.Package);
        Assert.False(System.IO.File.Exists(Path.Combine(temporary.Package, Target.CanonicalPath())));
    }

    [Fact]
    public void InstallationRejectsDestinationEscapeAndOutputTraversal()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        Assert.Throws<InvalidDataException>(() => TextureModInstaller.PrepareInstall(temporary.Root, temporary.Root + "-elsewhere", plan));
        Assert.Throws<InvalidDataException>(() => TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, new([new("textures/../../escape.png", Png())])));
    }

    [Fact]
    public void CandidatePreparationReadsImmutableOverlayBeforeAnyDestinationWrite()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        using TextureModInstallTransaction transaction = TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, plan);
        byte[] expected = plan.Files[0].Content.ToArray();
        plan.Files[0].Content[0] = 0;
        transaction.Files[0].Content[0] = 0;
        bool published = false;
        using ModRuntime runtime = new(modsDirectory: temporary.Root, watch: false, prepare: candidate =>
        {
            Assert.False(System.IO.File.Exists(Path.Combine(temporary.Package, Target.CanonicalPath())));
            Assert.Equal(expected, Assert.Single(candidate.Files).ReadBytes());
            return new(true, Commit: () => published = true);
        });
        ModPackage package = new("Memoria/sample", "Sample", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, temporary.Package);
        Assert.True(runtime.ApplyTextureImport(transaction, package));
        Assert.True(published);
        Assert.Equal(expected, System.IO.File.ReadAllBytes(Path.Combine(temporary.Package, Target.CanonicalPath())));
        Assert.Equal(1, runtime.Catalog.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CandidateOrPublicationFailureRetainsPreviousPackageAndGeneration(bool failPublication)
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        using TextureModInstallTransaction transaction = TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, plan);
        using ModRuntime runtime = new(modsDirectory: temporary.Root, watch: false, prepare: _ => failPublication ? new(true, Commit: () => throw new IOException("publication failed")) : new(false));
        ModPackage package = new("Memoria/sample", "Sample", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, temporary.Package);
        Assert.False(runtime.ApplyTextureImport(transaction, package));
        Assert.False(System.IO.File.Exists(Path.Combine(temporary.Package, Target.CanonicalPath())));
        Assert.Equal(0, runtime.Catalog.Generation);
    }

    [Fact]
    public void CoverageRestoresNativeStpAndRetainsAuthorTransparency()
    {
        TextureModReplacement replacement = new(Target, Png(), Contract: new("image", 2, 2))
        {
            NativeCoverage = new(2, 2, Convert.ToBase64String([128, 0, 255, 128]))
        };
        TextureModInstallPlan plan = TextureModInstaller.Plan([replacement]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureReplacement resolved = Assert.IsType<TextureReplacement>(mods.Resolve(Context()));
        Assert.Equal(128, resolved.Image[0, 0].A);
        Assert.Equal(255, resolved.Image[1, 0].A);
        Assert.Equal(128, resolved.Image[1, 1].A);
    }

    [Fact]
    public void ExportedAuthoringSidecarLoadsWhenCopiedAlongsideCanonicalImage()
    {
        TextureExchangeMetadata metadata = new(1, Target, new("image", 2, 2), NativeFlagsBase64: Convert.ToBase64String([128, 255, 255, 255])) { NativeWidth = 2, NativeHeight = 2 };
        string path = Target.CanonicalPath();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File(path, Png()), File(path + TextureModMapping.Extension, metadata.ToBytes())]), out NativeTextureMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(128, mods.Resolve(Context())!.Image[0, 0].A);
    }

    [Fact]
    public void UnchangedContributionsReuseDecodedPixelAndReplacementReferencesAcrossGenerations()
    {
        byte[] bytes = Png();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File(Target.CanonicalPath(), bytes)]), out NativeTextureMods first, out _));
        TextureReplacement original = first.Resolve(Context())!;
        Assert.True(NativeTextureMods.TryPrepare(new(2, [], [File(Target.CanonicalPath(), bytes.ToArray())]), out NativeTextureMods second, out _, first));
        Assert.Same(original, second.Resolve(Context()));
        Assert.Same(original.Image, second.Resolve(Context())!.Image);
    }

    [Theory]
    [InlineData("image", "image", 255)]
    [InlineData("native-effect-texture", "native-effect-source", 127)]
    public void NativeSourceAliasLoadsPersistentOwnerAndCropsOnlyProvedSourceRegion(string runtimeLayout, string sourceLayout, int alpha)
    {
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png(), Contract: new(sourceLayout, 2, 2))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureBuildContext crop = new(new(new("render/transient"), "upload/99"), Context().Source, 1, runtimeLayout, 1, 2, () => new(1, 2))
        {
            NativeAlias = new(Target.Key.Owner, Target.Subresource, Target.Palette, 1, 0, 1, 2, 2, 2)
        };
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(mods.Resolve(crop));
        Assert.Equal((1, 2, 1, 2), (replacement.Image.Width, replacement.Image.Height, replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(alpha, replacement.Image[0, 0].A);
        Assert.Same(replacement, mods.Resolve(crop));
        TextureBuildContext invalid = new(crop.Key, crop.Source, 1, "image", 1, 2, () => new(1, 2)) { NativeAlias = crop.NativeAlias with { CanvasWidth = 3 } };
        Assert.Throws<InvalidDataException>(() => mods.Resolve(invalid));
    }

    [Fact]
    public void NewModEnablementIsLocalAndTransactionallyCommittedWithoutChangingPriority()
    {
        using TemporaryPackage temporary = new();
        string destination = Path.Combine(temporary.Root, "new-package");
        TextureModInstallPlan plan = TextureModInstaller.WithNewDescriptor(TextureModInstaller.Plan([new(Target, Png())]), "new-package", "New package");
        using (TextureModInstallTransaction transaction = TextureModInstaller.BeginInstall(temporary.Root, destination, plan))
        {
            transaction.Commit();
        }
        Assert.Equal("enabled=1\n", System.IO.File.ReadAllText(Path.Combine(destination, "mod.ini")));
        Assert.Equal("new-package", NativeModDescriptor.Read(Path.Combine(destination, "mod.json")).Id);
        Assert.Throws<InvalidDataException>(() => TextureModInstaller.PrepareInstall(temporary.Root, destination, plan));
    }

    [Fact]
    public void ReviewedDestinationSnapshotCannotBeSilentlyRebasedOntoNewDestinationEdits()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]) with { ExpectedDestinationHashes = new Dictionary<string, string?> { [Target.CanonicalPath()] = null } };
        string target = Path.Combine(temporary.Package, Target.CanonicalPath());
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        System.IO.File.WriteAllBytes(target, Png("blue"));
        Assert.Throws<IOException>(() => TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, plan));
        Assert.Empty(Directory.EnumerateDirectories(temporary.Package, ".texture-stage-*"));
    }

    [Fact]
    public void EmbeddedClaimsIdentifyMovedImagesAndConflictsRejectWholeCandidate()
    {
        using TemporaryPackage temporary = new();
        TextureBuildContext context = Context();
        _ = TextureExchange.Export([new(context, new(context.Original, 2, 2, "original"), 1)], temporary.Package, cancellationToken: TestContext.Current.CancellationToken);
        byte[] png = System.IO.File.ReadAllBytes(Path.Combine(temporary.Package, Target.CanonicalPath()));
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], [File("textures/renamed.png", png)]), out NativeTextureMods moved, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.NotNull(moved.Resolve(context));
        Assert.False(NativeTextureMods.TryPrepare(new(2, [], [File((Target with { Palette = 1 }).CanonicalPath(), png)]), out _, out diagnostics));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("conflicts"));
    }

    [Fact]
    public void DiscoveryRecoversInterruptedFilesBeforePreparingAnyNativeContribution()
    {
        using TemporaryPackage temporary = new();
        string nativeRoot = Path.Combine(temporary.Root, "Memoria");
        string destination = Path.Combine(nativeRoot, "recovery");
        TextureModInstallPlan initial = TextureModInstaller.WithNewDescriptor(TextureModInstaller.Plan([new(Target, Png("red"))]), "recovery", "Recovery");
        using (TextureModInstallTransaction transaction = TextureModInstaller.BeginInstall(nativeRoot, destination, initial))
        {
            transaction.Commit();
        }
        byte[] original = System.IO.File.ReadAllBytes(Path.Combine(destination, Target.CanonicalPath()));
        TextureModInstallTransaction abandoned = TextureModInstaller.BeginInstall(nativeRoot, destination, TextureModInstaller.Plan([new(Target, Png("blue"))]));
        SimulateProcessTermination(abandoned);
        using ModRuntime runtime = new(modsDirectory: temporary.Root, watch: false, prepare: catalog =>
        {
            Assert.Equal(original, Assert.Single(catalog.Files, file => file.RelativePath == Target.CanonicalPath()).ReadBytes());
            return new(true);
        });
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.Equal(original, System.IO.File.ReadAllBytes(Path.Combine(destination, Target.CanonicalPath())));
        Assert.Empty(Directory.EnumerateDirectories(destination, ".texture-stage-*"));
    }

    private static void SimulateProcessTermination(TextureModInstallTransaction transaction)
    {
        // These handles/registrations disappear when a process exits; the disk journal remains.
        IDisposable lease = (IDisposable)typeof(TextureModInstallTransaction).GetField("lease", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(transaction)!;
        lease.Dispose();
        var active = (System.Collections.Concurrent.ConcurrentDictionary<string, byte>)typeof(TextureModInstaller).GetField("ActiveTransactions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        active.TryRemove(transaction.Destination, out _);
    }

    private static TextureBuildContext Context() => new(Target.Key, new("synthetic", GameDataEdition.Classic, ["image"]), 1, "image", 2, 2, () => new(2, 2));
    private static ModFile File(string path, byte[] bytes, string package = "Memoria/sample") => new(package, ModFormat.Memoria, path, 1, () => bytes);
    private static byte[] Png(string color = "red")
    {
        using MagickImage image = new(new MagickColor(color), 2, 2);
        return image.ToByteArray(MagickFormat.Png);
    }

    private sealed class TemporaryPackage : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "memoria-textures-" + Guid.NewGuid().ToString("N"));
        public string Package => Path.Combine(Root, "sample");

        public TemporaryPackage()
        {
            Directory.CreateDirectory(Package);
            System.IO.File.WriteAllText(Path.Combine(Package, "mod.json"), "descriptor");
            System.IO.File.WriteAllText(Path.Combine(Package, "mod.ini"), "Enabled=0");
            System.IO.File.WriteAllText(Path.Combine(Package, "unrelated.txt"), "other");
        }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
