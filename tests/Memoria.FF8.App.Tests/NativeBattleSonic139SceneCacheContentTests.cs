using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSonic139SceneCacheContentTests(ITestOutputHelper testOutput)
{
    [Fact]
    public void Original_low_band_Sonic_capture_distinguishes_content_changes_from_wrapper_changes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selectedOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_SONIC139_CONTENT_OUTPUT");
        Assert.SkipUnless(directory is not null && selectedOutput is not null, "Select installed resources and a fresh external content diagnostic directory.");
        string output = Path.GetFullPath(selectedOutput!);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(79);
        Assert.Equal(16, formation.StageId);
        BattleEnemyPlacement caster = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true)[0];
        Assert.Equal(0, caster.Slot);
        Assert.Equal(99, caster.ModelId);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(99, 1);
        Assert.Equal(0, ai.LevelBand);
        Assert.Equal((byte)8, ai.Abilities[1].Command);
        Assert.Equal((ushort)182, ai.Abilities[1].Id);
        Assert.Equal((byte)11, ai.Abilities[1].Sequence);
        NativeEffectFixtureRequest request = new("sonic-content", 139, 8, 182, 0, 800, 240) { EncounterId = 79, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
        string fixture = Path.Combine(output, "input");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
        NativeBattleOrdinaryMagicFixtureFlagTests.DeleteFixtureSnapshots(fixture, exported);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        Dictionary<WrapperKey, int> wrappers = [];
        Dictionary<string, List<Content>> contentBuckets = [];
        List<Content> contents = [];
        HashSet<string> pixelContents = [];
        HashSet<string> paletteContents = [];
        HashSet<Profile> profiles = [];
        Dictionary<int, HashSet<WrapperKey>> previous = [];
        List<object> frames = [];
        long? previousCallback = null;
        int adjacentContentMatches = 0;
        int adjacentWrapperMissesWithEqualContent = 0;
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        for (int tick = 0; tick < 512 && frames.Count < 32; tick++)
        {
            battle.Tick();
            battle.RefreshPreparedPresentation();
            NativeBattlePreparedPresentation? presentation = battle.PreparedPresentation;
            if (presentation?.PilotEffect != 139)
            {
                continue;
            }
            NativeBattlePreparedPrimitiveFrame frame = presentation.Primitives;
            NativeBattleLogicalResolvedCommand[] commands = frame.LastCommands.Where(command => command.Pass > 0 && command.DrawMode.SceneCaptureSample is not null).ToArray();
            if (commands.Length == 0)
            {
                continue;
            }
            if (previousCallback is long callback)
            {
                Assert.Equal(callback + 1, presentation.Callback);
            }
            previousCallback = presentation.Callback;
            ImmutableArray<NativeBattlePcTextureWindowPixels> snapshots = frame.LastTextureSnapshots!.Value;
            NativeBattlePrimitiveColorState colors = frame.LastColorState!;
            NativeBattlePrimitiveCapabilities capabilities = source.Definition.NativeGraphics!.PrimitiveCapabilities!;
            Dictionary<WrapperKey, int> currentWrappers = [];
            foreach (NativeBattleLogicalResolvedCommand command in commands)
            {
                NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample!;
                Profile profile = new(sample.Capture, command.DrawMode.TexturePage!.Value >> 7 & 3, command.DrawMode.BlendMode, capabilities.DirectTextureFormat, colors.AlternatePaletteShift, colors.SubtractUsesNormalizedColor);
                Assert.Equal(0, profile.Depth);
                WrapperKey key = new(snapshots, sample, profile);
                currentWrappers[key] = currentWrappers.GetValueOrDefault(key) + 1;
            }
            Dictionary<int, HashSet<WrapperKey>> current = [];
            Dictionary<ImmutableArray<int>, Pixels> selectedPages = [];
            foreach ((WrapperKey key, int commandCount) in currentWrappers)
            {
                if (!selectedPages.TryGetValue(key.Sample.PixelSnapshots, out Pixels? pixels))
                {
                    NativeBattlePcTextureWindowPixels[] pages = key.Sample.PixelSnapshots.Select(index => snapshots[index]).ToArray();
                    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    foreach (NativeBattlePcTextureWindowPixels page in pages)
                    {
                        hash.AppendData(page.Data.AsSpan());
                    }
                    pixels = new(pages, Convert.ToHexString(hash.GetHashAndReset()));
                    selectedPages.Add(key.Sample.PixelSnapshots, pixels);
                }
                string paletteHash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(key.Sample.Palette.AsSpan())));
                string digest = pixels.Hash + paletteHash;
                if (!contentBuckets.TryGetValue(digest, out List<Content>? bucket))
                {
                    bucket = [];
                    contentBuckets.Add(digest, bucket);
                }
                Content? content = bucket.FirstOrDefault(candidate => candidate.Profile == key.Profile && candidate.Palette.AsSpan().SequenceEqual(key.Sample.Palette.AsSpan()) && EqualPages(candidate.Pixels.Pages, pixels.Pages));
                if (content is null)
                {
                    content = new(contents.Count, pixels, key.Sample.Palette, paletteHash, key.Profile);
                    contents.Add(content);
                    bucket.Add(content);
                }
                content.Commands += commandCount;
                content.Frames.Add(frames.Count);
                content.Wrappers.Add(key);
                if (!current.TryGetValue(content.Id, out HashSet<WrapperKey>? frameKeys))
                {
                    frameKeys = [];
                    current.Add(content.Id, frameKeys);
                }
                frameKeys.Add(key);
                wrappers[key] = wrappers.GetValueOrDefault(key) + commandCount;
                pixelContents.Add(pixels.Hash);
                paletteContents.Add(paletteHash);
                profiles.Add(key.Profile);
            }
            int equalAdjacent = current.Keys.Count(previous.ContainsKey);
            int changedWrappers = current.Where(item => previous.ContainsKey(item.Key)).Sum(item => item.Value.Count(key => !previous[item.Key].Contains(key)));
            adjacentContentMatches += equalAdjacent;
            adjacentWrapperMissesWithEqualContent += changedWrappers;
            frames.Add(new { presentation.Callback, presentation.Frame, commands = commands.Length, wrapperKeys = currentWrappers.Count, exactContents = current.Count, withinFrameContentDuplicateWrappers = currentWrappers.Count - current.Count, equalAdjacentContents = equalAdjacent, identityChangedForEqualAdjacentContents = changedWrappers, selectedPixelBytes = selectedPages.Values.Sum(pixels => pixels.Pages.Sum(page => page.Data.Length)), allSnapshotBytes = snapshots.Sum(page => page.Data.Length) });
            previous = current;
        }
        var report = new
        {
            effect = 139, encounter = 79, stage = 16, model = 99, levelBand = 0, abilitySlot = 1, command = 8, action = 182, sequence = 11,
            scenario = "Installed-resource controlled seven-HP presentation component matching the original low-band action/formation; not an authenticated replay of the ordinary-driver performance recipe.",
            observedCallbacks = frames.Count, uniqueWrapperKeys = wrappers.Count, exactContentGroups = contents.Count, distinctPixelSha256 = pixelContents.Count, distinctPaletteSha256 = paletteContents.Count, decodeProfiles = profiles.Count,
            adjacentContentMatches, adjacentWrapperMissesWithEqualContent,
            repeatedContentGroups = contents.Count(content => content.Wrappers.Count > 1),
            examples = contents.OrderByDescending(content => content.Wrappers.Count).Take(8).Select(content => new { content.Id, pixelSha256 = content.Pixels.Hash, paletteSha256 = content.PaletteHash, content.Commands, callbackCount = content.Frames.Count, wrapperKeys = content.Wrappers.Count, content.Profile }).ToArray(),
            frames,
            interpretation = "Wrapper comparison uses the current renderer identity key fields without executing or changing that cache. Content groups require exact selected-page dimensions/bytes, palette words and complete decode-profile equality after hash lookup. Pixel/palette SHA counts are descriptive only. No cache-hit, eviction, timing or GPU conclusion is inferred."
        };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true });
        Assert.True(json.Length <= 100 * 1024, "The compact content diagnostic exceeded its output budget.");
        File.WriteAllBytes(Path.Combine(output, "content.json"), json);
        testOutput.WriteLine($"Sonic content: {frames.Count} adjacent callbacks, {wrappers.Count} identity keys, {contents.Count} exact content groups, {adjacentWrapperMissesWithEqualContent} adjacent identity-only differences.");
        Assert.Equal(32, frames.Count);
        Assert.NotEmpty(contents);
    }

    private static bool EqualPages(NativeBattlePcTextureWindowPixels[] first, NativeBattlePcTextureWindowPixels[] second)
    {
        return first.Length == second.Length && first.Zip(second).All(pair => pair.First.X == pair.Second.X && pair.First.Y == pair.Second.Y && pair.First.Width == pair.Second.Width && pair.First.Height == pair.Second.Height && pair.First.Data.AsSpan().SequenceEqual(pair.Second.Data.AsSpan()));
    }

    private sealed record Profile(NativeBattleSceneCapture Capture, int Depth, int? Blend, NativeBattlePcDirectTextureFormat? Format, byte? PaletteShift, bool SubtractUsesNormalizedColor);
    private readonly record struct WrapperKey(ImmutableArray<NativeBattlePcTextureWindowPixels> Snapshots, NativeBattleSceneCaptureSample Sample, Profile Profile);
    private sealed record Pixels(NativeBattlePcTextureWindowPixels[] Pages, string Hash);
    private sealed class Content(int id, Pixels pixels, ImmutableArray<ushort> palette, string paletteHash, Profile profile)
    {
        public int Id { get; } = id;
        public Pixels Pixels { get; } = pixels;
        public ImmutableArray<ushort> Palette { get; } = palette;
        public string PaletteHash { get; } = paletteHash;
        public Profile Profile { get; } = profile;
        public int Commands { get; set; }
        public HashSet<int> Frames { get; } = [];
        public HashSet<WrapperKey> Wrappers { get; } = [];
    }
}
