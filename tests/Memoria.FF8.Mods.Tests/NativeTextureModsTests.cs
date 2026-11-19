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
        using TextureModInstallTransaction transaction = PrepareInstall(temporary.Root, temporary.Package, plan);
        Assert.Throws<IOException>(() => System.IO.File.WriteAllText(Path.Combine(temporary.Package, "mod.ini"), "Enabled=1"));
        Assert.Equal("Enabled=0", System.IO.File.ReadAllText(Path.Combine(temporary.Package, "mod.ini")));
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
        Assert.Throws<InvalidDataException>(() => PrepareInstall(temporary.Root, temporary.Root + "-elsewhere", plan));
        Assert.Throws<InvalidDataException>(() => PrepareInstall(temporary.Root, temporary.Package, new([new("textures/../../escape.png", Png())])));
    }

    [Fact]
    public void CandidatePreparationReadsImmutableOverlayBeforeAnyDestinationWrite()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        using TextureModInstallTransaction transaction = PrepareInstall(temporary.Root, temporary.Package, plan);
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
        using TextureModInstallTransaction transaction = PrepareInstall(temporary.Root, temporary.Package, plan);
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
        Assert.Throws<InvalidDataException>(() => PrepareInstall(temporary.Root, destination, plan));
    }

    [Fact]
    public void ReviewedDestinationSnapshotCannotBeSilentlyRebasedOntoNewDestinationEdits()
    {
        using TemporaryPackage temporary = new();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]) with { ExpectedDestinationHashes = new Dictionary<string, string?> { [Target.CanonicalPath()] = null } };
        string target = Path.Combine(temporary.Package, Target.CanonicalPath());
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        System.IO.File.WriteAllBytes(target, Png("blue"));
        Assert.Throws<IOException>(() => PrepareInstall(temporary.Root, temporary.Package, plan));
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
    public void NormalDiscoveryReportsMaskedNativeOwnerWithoutEnablingDisabledPackages()
    {
        using TemporaryPackage temporary = new();
        foreach ((string name, int priority, bool enabled) in new[] { ("first", 20, true), ("second", 10, true), ("disabled", 100, false) })
        {
            string package = Path.Combine(temporary.Root, "Memoria", name);
            string image = Path.Combine(package, Target.CanonicalPath());
            Directory.CreateDirectory(Path.GetDirectoryName(image)!);
            System.IO.File.WriteAllBytes(image, Png());
            System.IO.File.WriteAllText(Path.Combine(package, "mod.json"), System.Text.Json.JsonSerializer.Serialize(new { id = name, name, package = "Examples", description = new { en = "Synthetic test texture." } }));
            System.IO.File.WriteAllText(Path.Combine(package, "mod.ini"), $"enabled={(enabled ? 1 : 0)}\npriority={priority}\n");
        }
        NativeTextureMods? prepared = null;
        using ModRuntime runtime = new(modsDirectory: temporary.Root, watch: false, prepare: catalog =>
        {
            bool valid = NativeTextureMods.TryPrepare(catalog, out NativeTextureMods textures, out var diagnostics);
            prepared = textures;
            return new(valid, Diagnostics: diagnostics);
        });
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.StartsWith("Memoria/first/", prepared!.Resolve(Context())!.Provenance);
        ModDiagnostic overridden = Assert.Single(runtime.Catalog.Diagnostics, diagnostic => diagnostic.Code == "Memoria.TextureOverridden");
        Assert.Equal("Memoria/second", overridden.PackageId);
        Assert.Equal(Target.CanonicalPath(), overridden.RelativePath);
        Assert.Contains("Memoria/first", overridden.Message);
        Assert.DoesNotContain(prepared.Variants, variant => variant.PackageId == "Memoria/disabled");
        Assert.False(Assert.Single(runtime.Catalog.Packages, package => package.Id == "Memoria/disabled").RequestedEnabled);
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

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void DeclaredEyeCompanionsPreserveSourceStateAndApplyOnlySelectedVariants(bool replaceDefault, bool replaceClosed)
    {
        using VariantSource source = new(declareClosed: true);
        ResourceService resources = new(source);
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Open);
        _ = resources.GetTexture(context);
        List<TextureModReplacement> replacements = [];
        if (replaceDefault)
        {
            replacements.Add(new(Target, Png("yellow")));
        }
        if (replaceClosed)
        {
            replacements.Add(new(Target with { Variant = "eyes-closed" }, Png("blue")));
        }
        TextureModInstallPlan plan = TextureModInstaller.Plan(replacements);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(mods.Resolve(context));
        Assert.Equal(replaceDefault ? (byte)255 : source.Open[0, 0].G, replacement.Image[0, 0].G);
        if (!replaceDefault)
        {
            Assert.Same(source.Open, replacement.Image);
            Assert.StartsWith("installed/provider", replacement.Provenance);
        }
        if (replaceClosed)
        {
            Assert.Equal((byte)255, replacement.Variants!["eyes-closed"][0, 0].B);
            Assert.Contains("variant eyes-closed: Memoria/sample/", replacement.Provenance);
        }
        else
        {
            Assert.Same(source.Closed, replacement.Variants!["eyes-closed"]);
        }
        Assert.Same(source.Animation, replacement.Animation);
        Assert.Same(source.Animation!.At(TimeSpan.FromMilliseconds(130)), replacement.Animation!.At(TimeSpan.FromMilliseconds(130)));
        Assert.Same(replacement, mods.Resolve(context));
        Assert.Same(source.Closed, source.Original.Variants!["eyes-closed"]);
        Assert.True(NativeTextureMods.TryPrepare(new(2, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods unchanged, out _, mods));
        Assert.Same(replacement, unchanged.Resolve(context));
    }

    [Fact]
    public void CompanionOnlyRevisionInvalidatesCombinedBindingWithoutReplacingInstalledBase()
    {
        using VariantSource source = new(declareClosed: true);
        ResourceService resources = new(source);
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Open);
        _ = resources.GetTexture(context);
        TextureModInstallPlan firstPlan = TextureModInstaller.Plan([new(Target with { Variant = "eyes-closed" }, Png("blue"))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], firstPlan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods first, out _));
        TextureReplacement previous = first.Resolve(context)!;
        TextureModInstallPlan nextPlan = TextureModInstaller.Plan([new(Target with { Variant = "eyes-closed" }, Png("yellow"))]);
        Assert.True(NativeTextureMods.TryPrepare(new(2, [], nextPlan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods next, out _, first));
        TextureReplacement current = next.Resolve(context)!;
        Assert.NotSame(previous, current);
        Assert.Same(source.Open, current.Image);
        Assert.NotEqual(previous.Variants!["eyes-closed"][0, 0], current.Variants!["eyes-closed"][0, 0]);
        Assert.Same(source.Animation, current.Animation);
    }

    [Fact]
    public void UndeclaredCompanionDoesNotInventDefaultClassicVariantButExplicitRequestedTargetStillResolves()
    {
        using VariantSource source = new(declareClosed: false);
        ResourceService resources = new(source);
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Open);
        _ = resources.GetTexture(context);
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target with { Variant = "eyes-closed" }, Png("blue"))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        Assert.Null(mods.Resolve(context));
        TextureBuildContext requested = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Closed) { RequestedVariant = "eyes-closed" };
        Assert.NotNull(mods.Resolve(requested));
    }

    [Fact]
    public void LargerAuthoredCompanionUpscalesInstalledBaseWithoutDownsamplingTheCompanion()
    {
        using VariantSource source = new(declareClosed: true);
        ResourceService resources = new(source);
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Open);
        _ = resources.GetTexture(context);
        using MagickImage enlarged = new(new MagickColor("blue"), 4, 4);
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target with { Variant = "eyes-closed" }, enlarged.ToByteArray(MagickFormat.Png))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureReplacement result = Assert.IsType<TextureReplacement>(mods.Resolve(context));
        Assert.Equal((4, 4), (result.Image.Width, result.Image.Height));
        Assert.Equal((4, 4), (result.Variants!["eyes-closed"].Width, result.Variants["eyes-closed"].Height));
        Assert.Equal((byte)255, result.Variants["eyes-closed"][0, 0].B);
        Assert.Equal(source.Open[0, 0], result.Image[0, 0]);
        Assert.Same(source.Animation, result.Animation);
    }

    [Fact]
    public void SharedNativeSheetTargetsCropBothFieldOwnersWithoutMatchingUnrelatedImages()
    {
        TextureTarget sheetTarget = new("ff8", "field/model/main_chr/d001", "row/0");
        Albeoris.Games.FF8.Imaging.PixelImage sheetImage = new(128, 128);
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                sheetImage[x, y] = new((byte)x, (byte)y, 17, 255);
            }
        }
        byte[] png = Memoria.FF8.Assets.Localization.LocalizationArtworkExporter.EncodePng(sheetImage);
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(sheetTarget, png, Contract: new("native-field-model-sheet", 128, 128))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureSourceAddress first = new(sheetTarget.Key.Owner, sheetTarget.Subresource, 0, 0, 0, 64, 64, 128, 128);
        TextureSourceAddress second = first with { X = 64, Y = 32, Width = 32 };
        TextureBuildContext firstField = SheetContext("one", first);
        TextureBuildContext secondField = SheetContext("two", second, layout: "classic-model");
        TextureReplacement firstResult = Assert.IsType<TextureReplacement>(mods.Resolve(firstField));
        TextureReplacement secondResult = Assert.IsType<TextureReplacement>(mods.Resolve(secondField));
        Assert.Equal((64, 64), (firstResult.Image.Width, firstResult.Image.Height));
        Assert.Equal((32, 64), (secondResult.Image.Width, secondResult.Image.Height));
        Assert.Equal(sheetImage[64, 32], secondResult.Image[0, 0]);
        Assert.Equal(sheetImage[95, 95], secondResult.Image[31, 63]);
        Assert.Equal(firstResult.Provenance, secondResult.Provenance);
        TextureBuildContext unrelated = new(secondField.Key, secondField.Source, 1, "classic-field-model", 32, 64, () => Opaque(32, 64, 90));
        Assert.Null(mods.Resolve(unrelated));
    }

    [Fact]
    public void FieldSpecificTargetThenNativeAliasThenGlobalSheetRetainTheirExistingPriority()
    {
        TextureTarget sheetTarget = new("ff8", "field/model/main_chr/d001", "row/0");
        TextureSourceAddress sheet = new(sheetTarget.Key.Owner, sheetTarget.Subresource, 0, 0, 0, 64, 64, 128, 128);
        TextureBuildContext context = SheetContext("one", sheet);
        TextureTarget aliasTarget = new("ff8", "field/original/chara.one", "model/0/texture/0");
        context = new(context.Key, context.Source, 1, context.Layout, 64, 64, () => Opaque(64, 64, 1)) { NativeSheet = sheet, NativeAlias = new(aliasTarget.Key.Owner, aliasTarget.Subresource, 0, 0, 0, 64, 64, 64, 64) };
        TextureModReplacement[] replacements = [new(TextureTarget.FromContext(context), ImagePng(Opaque(64, 64, 10)), Contract: new(context.Layout, 64, 64)), new(aliasTarget, ImagePng(Opaque(64, 64, 20)), Contract: new(context.Layout, 64, 64)), new(sheetTarget, ImagePng(Opaque(128, 128, 30)), Contract: new("native-field-model-sheet", 128, 128))];
        for (int skip = 0; skip < replacements.Length; skip++)
        {
            TextureModInstallPlan plan = TextureModInstaller.Plan(replacements.Skip(skip));
            Assert.True(NativeTextureMods.TryPrepare(new(skip, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
            Assert.Equal((byte)((skip + 1) * 10), mods.Resolve(context)!.Image[0, 0].R);
        }
    }

    [Fact]
    public void GlobalSheetAlphaSourceIsDistinctAndEquivalenceRequiresThePublishedFieldLayout()
    {
        TextureTarget normal = new("ff8", "field/model/main_chr/o006", "row/0");
        TextureTarget alpha = normal with { Subresource = "row/0/alpha" };
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(normal, ImagePng(Opaque(128, 128, 20)), Contract: new("native-field-model-sheet", 128, 128)), new(alpha, ImagePng(Opaque(128, 128, 40)), Contract: new("native-field-model-sheet", 128, 128))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureSourceAddress address = new(normal.Key.Owner, normal.Subresource, 0, 0, 0, 64, 64, 128, 128);
        Assert.Equal((byte)20, mods.Resolve(SheetContext("normal", address))!.Image[0, 0].R);
        Assert.Equal((byte)40, mods.Resolve(SheetContext("alpha", address with { Subresource = alpha.Subresource }))!.Image[0, 0].R);
        TextureBuildContext invalid = new(new(new("menu/other"), "image"), Context().Source, 1, "native-artwork", 64, 64, () => Opaque(64, 64, 1)) { NativeSheet = address };
        Assert.Null(mods.Resolve(invalid));
        TextureModInstallPlan incompatible = TextureModInstaller.Plan([new(normal, ImagePng(Opaque(128, 128, 20)), Contract: new("image", 128, 128))]);
        Assert.True(NativeTextureMods.TryPrepare(new(2, [], incompatible.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods wrongContract, out _));
        Assert.Throws<InvalidDataException>(() => wrongContract.Resolve(SheetContext("normal", address)));
    }

    [Fact]
    public void SheetCompanionsUseDeclaredSourceAndCoherentlyUpscaleWithoutChangingTheirFiles()
    {
        using InMemoryResourceSource source = new(Context().Source);
        ResourceService resources = new(source);
        TextureTarget target = new("ff8", "field/model/main_chr/d001", "row/0");
        TextureSourceAddress address = new(target.Key.Owner, target.Subresource, 0, 0, 0, 64, 64, 128, 128);
        Albeoris.Games.FF8.Imaging.PixelImage closed = Opaque(64, 64, 31);
        TextureBuildContext context = SheetContext("one", address, () => new Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> { ["eyes-closed"] = closed });
        _ = resources.GetTexture(context);
        byte[] originalFile = ImagePng(Opaque(256, 256, 88));
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(target, originalFile, Contract: new("native-field-model-sheet", 128, 128))]);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureReplacement result = mods.Resolve(context)!;
        Assert.Equal((128, 128), (result.Image.Width, result.Image.Height));
        Assert.Equal((128, 128), (result.Variants!["eyes-closed"].Width, result.Variants["eyes-closed"].Height));
        Assert.Equal(closed[0, 0], result.Variants["eyes-closed"][0, 0]);
        Assert.Equal(originalFile, plan.Files[0].Content);
        Assert.Same(result, mods.Resolve(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProvedNativeCopyProjectionUsesEditedBaseAndExplicitCompanionWins(bool replaceClosed)
    {
        using InMemoryResourceSource source = new(Context().Source);
        ResourceService resources = new(source);
        List<string> projected = [];
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "classic-field-model", 2, 2, () => Opaque(2, 2, 1))
        {
            NativeVariants = () => new Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> { ["eyes-open"] = Opaque(2, 2, 2), ["eyes-closed"] = Opaque(2, 2, 3) },
            ProjectNativeVariant = (image, name) =>
            {
                projected.Add(name);
                return Opaque(image.Width, image.Height, image[0, 0].R);
            }
        };
        _ = resources.GetTexture(context);
        List<TextureModReplacement> replacements = [new(Target, ImagePng(Opaque(4, 4, 80)))];
        if (replaceClosed)
        {
            replacements.Add(new(Target with { Variant = "eyes-closed" }, ImagePng(Opaque(8, 8, 90))));
        }
        TextureModInstallPlan plan = TextureModInstaller.Plan(replacements);
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out NativeTextureMods mods, out _));
        TextureReplacement result = mods.Resolve(context)!;
        Assert.Equal(replaceClosed ? (8, 8) : (4, 4), (result.Image.Width, result.Image.Height));
        Assert.Equal((byte)80, result.Variants!["eyes-open"][0, 0].R);
        Assert.Equal(replaceClosed ? (byte)90 : (byte)80, result.Variants["eyes-closed"][0, 0].R);
        Assert.Equal(replaceClosed ? new[] { "eyes-open" } : new[] { "eyes-open", "eyes-closed" }, projected);
        resources.Prepare(mods).Commit();
        TextureBuildContext closedContext = new(Target.Key, source.Descriptor, 1, context.Layout, 2, 2, () => Opaque(2, 2, 1)) { RequestedVariant = "eyes-closed", NativeVariants = context.NativeVariants, ProjectNativeVariant = context.ProjectNativeVariant };
        Assert.Equal(replaceClosed ? (byte)90 : (byte)80, resources.ResolveForExport(closedContext).Image[0, 0].R);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PackagePrecedenceGovernsDirectAndSharedSheetTargetsIndependentlyForEachVariant(bool higherPackageUsesSheet)
    {
        using InMemoryResourceSource source = new(Context().Source);
        ResourceService resources = new(source);
        TextureTarget sheetTarget = new("ff8", "field/model/main_chr/d001", "row/0");
        TextureSourceAddress address = new(sheetTarget.Key.Owner, sheetTarget.Subresource, 0, 0, 0, 64, 64, 128, 128);
        TextureBuildContext context = SheetContext("one", address, () => new Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> { ["eyes-closed"] = Opaque(64, 64, 5) });
        _ = resources.GetTexture(context);
        TextureTarget directTarget = TextureTarget.FromContext(context);
        TextureModReplacement direct = new(directTarget, ImagePng(Opaque(64, 64, 10)), Contract: new(context.Layout, 64, 64));
        TextureModReplacement sheet = new(sheetTarget, ImagePng(Opaque(128, 128, 30)), Contract: new("native-field-model-sheet", 128, 128));
        TextureModInstallPlan high = TextureModInstaller.Plan([higherPackageUsesSheet ? sheet : direct]);
        TextureModReplacement lowerVariant = higherPackageUsesSheet ? new(directTarget with { Variant = "eyes-closed" }, ImagePng(Opaque(64, 64, 60)), Contract: new(context.Layout, 64, 64)) : new(sheetTarget with { Variant = "eyes-closed" }, ImagePng(Opaque(128, 128, 50)), Contract: new("native-field-model-sheet", 128, 128));
        TextureModInstallPlan low = TextureModInstaller.Plan([higherPackageUsesSheet ? direct : sheet, lowerVariant]);
        ModFile[] files = high.Files.Select(file => File(file.RelativePath, file.Content, "Memoria/high")).Concat(low.Files.Select(file => File(file.RelativePath, file.Content, "Memoria/low"))).ToArray();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods mods, out _));
        TextureReplacement result = mods.Resolve(context)!;
        Assert.Equal(higherPackageUsesSheet ? (byte)30 : (byte)10, result.Image[0, 0].R);
        Assert.StartsWith("Memoria/high/", result.Provenance);
        Assert.Equal(higherPackageUsesSheet ? (byte)60 : (byte)50, result.Variants!["eyes-closed"][0, 0].R);
        Assert.Contains("variant eyes-closed: Memoria/low/", result.Provenance);
    }

    [Fact]
    public void HigherPackageCompanionDoesNotMaskIndependentLowerPackageDefault()
    {
        using VariantSource source = new(declareClosed: true);
        ResourceService resources = new(source);
        TextureBuildContext context = new(Target.Key, source.Descriptor, 1, "image", 2, 2, () => source.Open);
        _ = resources.GetTexture(context);
        TextureModInstallPlan closed = TextureModInstaller.Plan([new(Target with { Variant = "eyes-closed" }, Png("blue"))]);
        TextureModInstallPlan open = TextureModInstaller.Plan([new(Target, Png("yellow"))]);
        ModFile[] files = closed.Files.Select(file => File(file.RelativePath, file.Content, "Memoria/high")).Concat(open.Files.Select(file => File(file.RelativePath, file.Content, "Memoria/low"))).ToArray();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods mods, out _));
        TextureReplacement result = mods.Resolve(context)!;
        Assert.Equal((byte)255, result.Image[0, 0].G);
        Assert.Equal((byte)255, result.Variants!["eyes-closed"][0, 0].B);
        Assert.StartsWith("Memoria/low/", result.Provenance);
        Assert.Contains("variant eyes-closed: Memoria/high/", result.Provenance);
    }

    [Fact]
    public void PreparationPinsOnlyRequestedFamiliesAndOwnerResolveDoesNotDecodeAgain()
    {
        TextureTarget other = Target with { Owner = "field/other/model.mch" };
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png("blue")), new(other, Png("red"))]);
        NativeTextureRequest[] requests = [NativeTextureRequest.FromContext(Context())];
        ModFile[] files = plan.Files.Select(file => File(file.RelativePath, file.Content)).Append(File(Target.CanonicalPath(), Png("yellow"), "Memoria/masked")).ToArray();
        Assert.True(NativeTextureMods.TryPrepare(new(1, [], files), out NativeTextureMods mods, out _, TestContext.Current.CancellationToken, requests: requests));
        Assert.Equal(1, mods.PreparationImageCount);
        Assert.Equal(3, mods.ImageDecodeCount);
        Assert.NotNull(mods.Resolve(Context()));
        Assert.Equal(3, mods.ImageDecodeCount);
        mods.ReleasePreparationPixels();
        Assert.Equal(0, mods.PreparationImageCount);
    }

    [Fact]
    public void CancelledPreparationLeavesDestinationUntouchedAndNativeDecodeUnstarted()
    {
        using TemporaryPackage temporary = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png())]);
        Assert.Throws<OperationCanceledException>(() => TextureModInstaller.PrepareInstall(temporary.Root, temporary.Package, plan, cancellation.Token));
        Assert.Empty(Directory.EnumerateDirectories(temporary.Package, ".texture-stage-*"));
        Assert.False(System.IO.File.Exists(Path.Combine(temporary.Package, Target.CanonicalPath())));
        Assert.Throws<OperationCanceledException>(() => NativeTextureMods.TryPrepare(new(1, [], plan.Files.Select(file => File(file.RelativePath, file.Content))), out _, out _, cancellation.Token, requests: [NativeTextureRequest.FromContext(Context())]));
    }

    private static TextureBuildContext SheetContext(string field, TextureSourceAddress address, Func<IReadOnlyDictionary<string, Albeoris.Games.FF8.Imaging.PixelImage>>? variants = null, string layout = "classic-field-model") => new(new(new($"field/{field}/chara.one"), "model/0/texture/0"), Context().Source, 1, layout, address.Width, address.Height, () => Opaque(address.Width, address.Height, 1)) { NativeSheet = address, NativeVariants = variants };
    private static Albeoris.Games.FF8.Imaging.PixelImage Opaque(int width, int height, byte red) => new(width, height, Enumerable.Repeat(new Albeoris.Games.FF8.Imaging.Rgba32(red, 2, 3, 255), width * height).ToArray());
    private static byte[] ImagePng(Albeoris.Games.FF8.Imaging.PixelImage image) => Memoria.FF8.Assets.Localization.LocalizationArtworkExporter.EncodePng(image);

    private sealed class VariantSource : IResourceSource, ITextureReplacementResolver
    {
        public VariantSource(bool declareClosed)
        {
            Descriptor = new("synthetic-provider", declareClosed ? GameDataEdition.Remastered : GameDataEdition.Classic, ["image"]);
            Open = TextureImageDecoder.Decode(Png("red"), "source.png").Image;
            Closed = TextureImageDecoder.Decode(Png("green"), "source.png").Image;
            Animation = declareClosed ? new([Open, Closed], 123) : null;
            Original = new(Open, "installed/provider", 2, 2, Variants: declareClosed ? new Dictionary<string, Albeoris.Games.FF8.Imaging.PixelImage> { ["eyes-closed"] = Closed } : null) { Animation = Animation };
        }

        public ResourceSourceDescriptor Descriptor { get; }
        public Albeoris.Games.FF8.Imaging.PixelImage Open { get; }
        public Albeoris.Games.FF8.Imaging.PixelImage Closed { get; }
        public TextureAnimation? Animation { get; }
        public TextureReplacement Original { get; }
        public ResourceContent? Find(ResourceKey key) => null;
        public TextureReplacement? Resolve(TextureBuildContext context) => context.Key == Target.Key ? Original : null;
        public void Dispose()
        {
        }
    }

    private static TextureModInstallTransaction PrepareInstall(string root, string destination, TextureModInstallPlan plan) => TextureModInstaller.PrepareInstall(root, destination, plan, TestContext.Current.CancellationToken);

    private static void SimulateProcessTermination(TextureModInstallTransaction transaction)
    {
        // These handles/registrations disappear when a process exits; the disk journal remains.
        IDisposable lease = (IDisposable)typeof(TextureModInstallTransaction).GetField("lease", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(transaction)!;
        lease.Dispose();
        var revisions = (System.Collections.IEnumerable)typeof(TextureModInstallTransaction).GetField("destinationRevisions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(transaction)!;
        foreach (object revision in revisions)
        {
            (revision.GetType().GetProperty("Stream")!.GetValue(revision) as IDisposable)?.Dispose();
        }
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
