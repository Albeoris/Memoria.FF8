using System.Buffers.Binary;
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
    public void SuppliedPrimaryGfAtlasReplacesTheWholeOriginalWindowAndSurvivesReload()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ATLAS_FIXTURES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && File.Exists(input), "Set MEMORIA_FF8_GF_ATLAS_FIXTURES to an optional authenticated primary-source atlas fixture catalog.");
        GfCinematicFixture fixture = JsonSerializer.Deserialize<GfCinematicFixture>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Expected an authenticated primary-source atlas fixture.");
        string fixtureBase = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        string gameDirectory = Path.GetFullPath(fixture.GameDirectory, fixtureBase);
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        Assert.Equal(GameDataEdition.Remastered, source.Descriptor.Edition);
        ResourceService resources = new(source);
        using GameAssets originalAssets = new(Path.GetFullPath(fixture.OriginalGameDirectory, fixtureBase));
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(fixture.Effect);
        NativeBattleCinematicResources cinematic = originalAssets.ReadNativeCinematicResources(fixture.Effect);
        ImmutableArray<NativeBattleEffectInitializerAsset> initialFiles = NativeBattleEffectInitializerPolicy.Assets(originalAssets.ReadNativeEffectCatalogForEffects([checked((ushort)fixture.Effect)])[fixture.Effect - 1]);
        NativeBattleWaterExecutionFrame frame = CreateCinematicFrame(profile, cinematic, initialFiles);
        NativeBattleEffectTextureRectangle pixelRectangle = cinematic.Rectangle(fixture.TextureIndex, false);
        NativeBattleCinematicTextureSelection palette = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, cinematic, fixture.TextureIndex);
        NativeBattleArchiveFile originalFile = Assert.Single(cinematic.Files, file => file.FileId == profile.SourceBase + fixture.SourceFileIndex);
        Assert.InRange(palette.SourceMapByte, (byte)0, (byte)1);
        NativeBattleEffectInitializerAsset paletteFile = initialFiles[palette.SourceMapByte];
        Assert.Equal(originalFile.Bytes.ToArray(), resources.Read(new ResourceKey("battle/" + originalFile.Name)));
        Assert.Equal(paletteFile.Data.ToArray(), resources.Read(new ResourceKey("magic/" + paletteFile.Name)));
        uint pixelAddress = BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x2798A68 + (uint)fixture.SourceFileIndex * 4, 4));
        frame = frame with { Core = frame.Core.Write(0x2797690 + 0xB4, BitConverter.GetBytes(pixelAddress)) };
        NativeBattleEffectTextureState queued = NativeBattleCinematicTextureMetadata.Enqueue505DF0(NativeBattleEffectTextureState.FromColdProcess(), frame, palette);
        queued = queued.EnqueueArenaRectangle505DF0(pixelRectangle, frame.Arena, checked((int)(pixelAddress - NativeBattleEffectNodeMemory.ArenaAddress)));
        NativeBattleEffectTextureState state = queued.Drain505D20(frame.Arena).State;
        NativeBattleTextureRegistration registration = Assert.Single(state.TextureRegistrations!.Value, registration => registration.Origin.Role == NativeBattleTextureRole.Pixels);
        Assert.Equal(NativeBattleTextureDomain.Battle, registration.Origin.Domain);
        Assert.Equal(NativeBattleTextureSourceKind.Image, registration.Origin.Kind);
        Assert.Equal(originalFile.Name, registration.Origin.Owner, ignoreCase: true);
        Assert.Equal(8, registration.Origin.BitsPerPixel);
        Assert.Equal(fixture.Page, (ushort)((pixelRectangle.X / 64 & 15) | (pixelRectangle.Y & 256) / 16 | 128));
        Assert.Equal(fixture.Clut, (ushort)(palette.Rectangle.Y * 64 + palette.Rectangle.X / 16));
        INativeBattleGfAtlasSource native = Assert.IsAssignableFrom<INativeBattleGfAtlasSource>(resources.GetSource<INativeBattleGfAtlasSource>());
        TextureSourceAtlasAddress address = Assert.IsType<TextureSourceAtlasAddress>(native.TryGetGfAtlasAddress(state.TextureRegistrations!.Value, fixture.Page, fixture.Clut, 0, 0, fixture.Width, fixture.Height));
        Assert.Equal((fixture.ExpectedName, fixture.ExpectedFirstImage), (address.Name, address.FirstImage));
        Assert.Equal((fixture.Width, fixture.Height, 0, 0, fixture.Width, fixture.Height), (address.CanvasWidth, address.CanvasHeight, address.X, address.Y, address.Width, address.Height));
        Assert.Equal(TextureSourceAtlasReplacementMode.WholeTexture, address.ReplacementMode);
        PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, fixture.Page, fixture.Clut, 0, 0, fixture.Width, fixture.Height);
        string modsDirectory = Path.GetFullPath(fixture.ModsDirectory, fixtureBase);
        TextureBuildContext? observed = null;
        List<Exception> failures = [];
        resources.ReplacementFailed += (_, exception) => failures.Add(exception);
        using ModRuntime mods = new(applicationBaseDirectory: Path.GetDirectoryName(modsDirectory), modsDirectory: modsDirectory, watch: false, prepare: catalog =>
        {
            RecordingResolver resolver = new(new DemasterTextureResolver(catalog), context =>
            {
                if (context.NativeAtlas is not null)
                {
                    observed = context;
                }
            }, onlyUploads: false);
            ResourceService.PreparedResources prepared = resources.Prepare(resolver);
            return new(true, Commit: () => prepared.Commit(notify: false), AfterCommit: prepared.Notify);
        });
        Pump();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage enabled = Apply();
        Assert.Equal(address, observed!.NativeAtlas);
        string target = $"textures/battle.fs/hd_new/{address.Name}_{address.FirstImage}{address.Variant}.dds";
        ModFile file = mods.Catalog.FindAll(ModFormat.Demaster, target).First();
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
            Assert.Empty(failures);
            if (fixture.EvidencePath is string output)
            {
                string destination = Path.GetFullPath(output, fixtureBase);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllText(destination, JsonSerializer.Serialize(new { sourceEdition = source.Descriptor.Edition.ToString(), fixture.Effect, fixture.TextureIndex, fixture.SourceFileIndex, originalFile = originalFile.Name, originalSha256 = Convert.ToHexString(SHA256.HashData(originalFile.Bytes.AsSpan())), paletteFile = paletteFile.Name, paletteSha256 = Convert.ToHexString(SHA256.HashData(paletteFile.Data.AsSpan())), usedFilesMatchPrimaryArchive = true, pixelRectangle, pixelAddress, palette, registrations = state.TextureRegistrations, address, target, original = new { original.Width, original.Height, sha256 = Signature(original) }, replacement = new { reloaded.Width, reloaded.Height, sha256 = Signature(reloaded) }, revisionBefore = revision, revisionAfter = refreshed.Revision, toggleRestoredOriginal = true, entireAtlasMatchesDecodedArtwork = true }, new JsonSerializerOptions { WriteIndented = true }));
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

    private static NativeBattleWaterExecutionFrame CreateCinematicFrame(NativeBattleCinematicProfile profile, NativeBattleCinematicResources resources, ImmutableArray<NativeBattleEffectInitializerAsset> initialFiles)
    {
        const uint manager = 0x2797690;
        uint arenaAddress = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        Store(0x27973EC, manager);
        Store(0x27973E8, arenaAddress + 0x100);
        Store(manager + 0x98, resources.TextureProfile.PixelRectangleAddress);
        Store(manager + 0x9C, resources.TextureProfile.ClutRectangleAddress);
        Store(manager + 0xA4, resources.TextureProfile.PixelSourceMapAddress);
        Store(manager + 0xA8, resources.TextureProfile.ClutSourceMapAddress);
        int offset = 0x1000;
        for (int index = 0; index <= profile.LastSource - profile.SourceBase; index++)
        {
            NativeBattleArchiveFile file = index < initialFiles.Length ? new(checked((ushort)(profile.SourceBase + index)), initialFiles[index].Name, initialFiles[index].Data) : Assert.Single(resources.Files, file => file.FileId == profile.SourceBase + index);
            frame = frame with { Arena = frame.Arena.Write(offset, file.Bytes.ToArray()).RememberTextureFile(offset, file.Bytes.AsSpan(), new(file.Name, NativeBattleTextureDomain.Battle)) };
            Store(0x2798A68 + (uint)index * 4, arenaAddress + (uint)offset);
            offset = checked((offset + file.Bytes.Length + 4095) / 4096 * 4096);
        }
        return frame;

        void Store(uint address, uint value)
        {
            frame = frame with { Core = frame.Core.Write(address, BitConverter.GetBytes(value)) };
        }
    }

    [Fact]
    public void SuppliedModelGroupsResolveActiveFilesThroughOriginalMaterialContexts()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_GROUP_FIXTURES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && File.Exists(input), "Set MEMORIA_FF8_MODEL_GROUP_FIXTURES to an optional original model group fixture catalog.");
        ModelGroupCatalog fixture = JsonSerializer.Deserialize<ModelGroupCatalog>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Expected a model group fixture catalog.");
        string fixtureBase = Path.GetDirectoryName(Path.GetFullPath(input!))!;
        string game = Path.GetFullPath(fixture.GameDirectory, fixtureBase);
        string package = Path.GetFullPath(fixture.PackageDirectory, fixtureBase);
        using IResourceSource source = GameDataSourceFactory.Open(game);
        Assert.Equal(GameDataEdition.Remastered, source.Descriptor.Edition);
        ModFile[] files = Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories).Where(path => Path.GetExtension(path).ToLowerInvariant() is ".dds" or ".png").Select(path => new ModFile("Demaster/Fixture", ModFormat.Demaster, Path.GetRelativePath(package, path).Replace('\\', '/'), 1, () => File.ReadAllBytes(path))).ToArray();
        ModCatalogSnapshot catalog = new(1, [], files);
        DemasterTextureResolver resolver = new(catalog);
        List<TextureBuildContext> captured = [];
        ResourceService resources = new(source);
        resources.Prepare(new RecordingResolver(new EmptyResolver(), context => captured.Add(context), onlyUploads: false)).Commit();
        using (BattleAssets battle = new(game, resources))
        {
            foreach (string owner in fixture.Targets.Where(target => target.Kind == "battle").Select(target => target.Owner).Distinct())
            {
                BattleModel model = battle.LoadModel(owner + ".dat");
                foreach (PixelImage image in model.Textures)
                {
                    Assert.True(image.Width > 0 && image.Height > 0);
                }
            }
        }
        HashSet<string> wantedFields = fixture.Targets.Where(target => target.Kind == "field").Select(target => target.Owner).ToHashSet(StringComparer.OrdinalIgnoreCase);
        using (GameAssets inventory = new(game, resources: resources))
        {
            ModelCatalogEntry[] entries = inventory.EnumerateFieldModelSources().Where(entry => entry.LoadError is null && entry.Name.Length >= 4 && wantedFields.Contains(ExternalFieldName(entry.Name[..4]))).ToArray();
            Assert.True(wantedFields.Count == 0 || entries.Length > 0);
            foreach (ModelCatalogEntry entry in entries)
            {
                FieldModel model = inventory.LoadFieldModelSource(entry);
                foreach (PixelImage image in model.Textures)
                {
                    Assert.True(image.Width > 0 && image.Height > 0);
                }
            }
        }
        List<object> results = [];
        List<object> failures = [];
        foreach (ModelTarget target in fixture.Targets)
        {
            TextureBuildContext[] contexts = captured.Where(context => context.NativeSheet is TextureSourceAddress address && address.Subresource == $"row/{target.Row}" && (target.Kind == "battle" ? address.Owner.Path == $"battle/{target.Owner}.dat" : ExternalFieldName(address.Owner.Path.Split('/')[^1]) == target.Owner)).DistinctBy(context => (context.NativeSheet, context.LogicalWidth, context.LogicalHeight, context.Key.Owner.Path.EndsWith(".mch", StringComparison.Ordinal))).ToArray();
            Assert.NotEmpty(contexts);
            foreach (TextureBuildContext context in contexts)
            {
                try
                {
                    TextureSourceAddress address = context.NativeSheet!;
                    Assert.NotNull(context.EncodedSource);
                    ModFile winner = DemasterTextureResolver.CandidatePaths(context).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).First();
                    string prefix = winner.RelativePath[..winner.RelativePath.LastIndexOf('_')];
                    ModFile anchor = new[] { prefix + "_0.dds", prefix + "_0.png" }.SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).First();
                    PixelImage anchorImage = ModImageDecoder.Decode(anchor).Image;
                    int planeWidth = target.Kind == "field" ? anchorImage.Height : anchorImage.Width;
                    Assert.True(target.Kind != "field" || anchorImage.Width == planeWidth || anchorImage.Width == 2 * planeWidth);
                    Assert.True(planeWidth > 0 && planeWidth % 128 == 0);
                    DecodedModImage decoded = ModImageDecoder.Decode(winner);
                    TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
                    Assert.Equal($"{winner.PackageId}/{winner.RelativePath}@{winner.Revision}", replacement.Provenance);
                    Assert.Equal((context.LogicalWidth, context.LogicalHeight), (replacement.LogicalWidth, replacement.LogicalHeight));
                    VerifyPlane(replacement.Image, decoded.Image, address, planeWidth, 0);
                    bool closed = target.Kind == "field" && decoded.Image.Width > planeWidth;
                    Assert.Equal(closed, replacement.Variants?.ContainsKey("eyes-closed") == true);
                    if (closed)
                    {
                        VerifyPlane(replacement.Variants!["eyes-closed"], decoded.Image, address, planeWidth, 1);
                    }
                    results.Add(new { target.Path, target.Kind, target.Owner, target.Row, activePath = winner.RelativePath, shadowed = !target.Path.Equals(winner.RelativePath, StringComparison.OrdinalIgnoreCase), sourceOwner = context.Key.Owner.Path, sourceMaterial = context.Key.Subresource, address, anchor = anchor.RelativePath, planeWidth, physicalWidth = decoded.Image.Width, physicalHeight = decoded.Image.Height, closed, sha256 = Signature(replacement.Image) });
                }
                catch (Exception exception)
                {
                    failures.Add(new { target.Path, sourceOwner = context.Key.Owner.Path, sourceMaterial = context.Key.Subresource, error = exception.ToString() });
                }
            }
        }
        Assert.NotEmpty(results);
        if (fixture.EvidencePath is string output)
        {
            string destination = Path.GetFullPath(output, fixtureBase);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, JsonSerializer.Serialize(new { sourceEdition = source.Descriptor.Edition.ToString(), targetCount = fixture.Targets.Length, actualConsumers = results.Count, targets = results, failures }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.True(failures.Count == 0, JsonSerializer.Serialize(failures));
    }

    private static void VerifyPlane(PixelImage actual, PixelImage encoded, TextureSourceAddress address, int planeWidth, int plane)
    {
        int scale = planeWidth / 128;
        PixelImage expected = new(address.Width * scale, address.Height * scale);
        int x = plane * planeWidth + address.X * scale;
        int y = address.Y * scale;
        int width = Math.Min(expected.Width, encoded.Width - x);
        int height = Math.Min(expected.Height, encoded.Height - y);
        if (width > 0 && height > 0)
        {
            for (int row = 0; row < height; row++)
            {
                encoded.Pixels.AsSpan((y + row) * encoded.Width + x, width).CopyTo(expected.Pixels.AsSpan(row * expected.Width, width));
            }
        }
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        Assert.Equal(Signature(expected), Signature(actual));
    }

    private static string ExternalFieldName(string name) => name.StartsWith('c') ? "n" + name[1..].ToLowerInvariant() : name.ToLowerInvariant();

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

    private sealed class RecordingResolver(ITextureReplacementResolver inner, Action<TextureBuildContext> observe, bool onlyUploads = true) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (!onlyUploads || context.NativeUpload is not null)
            {
                observe(context);
            }
            return inner.Resolve(context);
        }
    }

    private sealed record Fixture(string Id, string TimPath, string ModsDirectory, string Fingerprint, ushort Page, ushort Clut, int Width, int Height, string? EvidenceDirectory = null);
    private sealed record AssetFixture(string Path, string Category, string Format, int Width, int Height, int MipCount);
    private sealed record AssetCatalog(string RootDirectory, int ExpectedContentCount, int ExpectedOtherCount, int ExpectedUnusedCount, AssetFixture[] Assets, string[] UnusedContentPaths, string? EvidencePath = null);
    private sealed record ModelTarget(string Path, string Kind, string Owner, int Row);
    private sealed record ModelGroupCatalog(string GameDirectory, string PackageDirectory, ModelTarget[] Targets, string? EvidencePath = null);
    private sealed record GfCinematicFixture(string GameDirectory, string OriginalGameDirectory, string ModsDirectory, int Effect, int TextureIndex, int SourceFileIndex, ushort Page, ushort Clut, int Width, int Height, string ExpectedName, int ExpectedFirstImage, string? EvidencePath = null);
    private sealed class EmptyResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => null;
    }
}
