using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionEffectNativePresentationInstalledTests
{
    private static readonly string[] Fixtures = ["effect-textures-1", "effect-textures-2"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Theory]
    [InlineData((ushort)185, true)]
    [InlineData((ushort)201, true)]
    [InlineData((ushort)205, true)]
    public void OriginalControlledComponentUsesInstalledTexturesAndPreservesColdCompletion(ushort effectId, bool requiresCommonTextures)
    {
        using Inputs input = new(effectId);
        using GameResourceHost host = input.Open();
        using GameAssets game = new(input.GameDirectory, resources: host.Resources);
        using BattleAssets assets = new(input.GameDirectory, host.Resources);
        List<object> observations = [];
        string[] requiredFixtures = requiresCommonTextures ? Fixtures : [Fixtures[1]];
        HashSet<string> changedFixtures = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> nativeSources = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, NativeBattleTextureOrigin> nativeOrigins = new(StringComparer.OrdinalIgnoreCase);
        List<string> restored = [];
        BattleSession? battle = null;
        BattleSnapshot? current = null;
        BattleSnapshot? largestNativeDraw = null;
        int largestNativeDrawCount = 0;
        string boundary = "original-component-selection";
        bool initialized = false;
        bool liveRestored = false;
        int completions = 0;
        int callbacks = 0;
        try
        {
            BattleNativeAction action = Assert.Single(assets.NativeActions, candidate => candidate.Command == 254 && candidate.MagicId == effectId);
            NativeBattleEffectBinding originalBinding = game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1];
            NativeEffectFixtureRequest request = new($"effect-component-{effectId}", effectId, action.Command, action.ActionId, 3, 800, 240) { DeferredGuardianFormulaAdmission = true };
            boundary = "export-original-controlled-component";
            string fixture = Path.Combine(input.OutputDirectory, "fixture");
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, input.GameDirectory, fixture);
            Write("fixture-result.json", exported);
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            Assert.True(exported.Selection!.OriginalKernelEffectReference);
            Assert.Equal(effectId, exported.Selection.ShellEffect);
            BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
            current = queued;
            Assert.Equal(JsonSerializer.Serialize(originalBinding), JsonSerializer.Serialize(queued.Definition.NativeEffectCatalog[effectId - 1]));
            battle = Restore(queued, game);
            BattleScenePresentation scene = new(assets, queued.Definition, true);
            Dictionary<string, int> hitPoints = queued.Combatants.ToDictionary(actor => actor.Id, actor => actor.CurrentHp);
            ColdNext("queued", queued);
            // A diagnostic guard bounds a failed continuation; it does not prescribe the native effect duration.
            for (; callbacks < 4096; callbacks++)
            {
                boundary = "native-callback";
                BattleSnapshot prior = battle.Capture();
                NativeBattleOriginalEffectState? before = Selected(prior);
                battle.Tick();
                current = battle.Capture();
                NativeBattleOriginalEffectState? after = Selected(current);
                if (current.NativeEffectMemory?.Textures?.LiveTextureSources is { } sources)
                {
                    foreach (NativeBattleTextureSourceRegion source in sources)
                    {
                        nativeSources.Add($"{source.Origin.Domain}/{source.Origin.Kind}/{source.Origin.Owner}/{source.Origin.Member}/{source.Origin.Role}");
                        nativeOrigins.TryAdd($"{source.Origin.Domain}/{source.Origin.Kind}/{source.Origin.Owner}/{source.Origin.Member}/{source.Origin.Role}/{source.Origin.SourceOwner}/{source.Origin.SourceOffset}", source.Origin);
                    }
                }
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    initialized = true;
                    ColdNext("initialized", current);
                }
                if (after?.ReturnedDescriptor is > 0 && requiredFixtures.Any(fixtureId => !changedFixtures.Contains(fixtureId)) && current.NativeEffectMemory?.Textures?.LiveTextureSources?.Any(source => source.Origin.Role == NativeBattleTextureRole.Pixels) == true)
                {
                    boundary = "actual-native-texture-presentation";
                    var owned = NativeBattleTargetGuardianCastTests.GuardianPacketReferences(prior, current, effectId, game);
                    int ownedDraws = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Count(command => owned.Contains(command.Command.Packet));
                    if (ownedDraws > largestNativeDrawCount)
                    {
                        largestNativeDrawCount = ownedDraws;
                        largestNativeDraw = current;
                    }
                    string beforeRender = JsonSerializer.Serialize(current);
                    Select();
                    byte[] original = scene.Render(Restore(current, game), 320, 224).ToArray();
                    foreach (string selected in requiredFixtures.Where(fixtureId => !changedFixtures.Contains(fixtureId)))
                    {
                        Select(selected);
                        byte[] pixels = scene.Render(Restore(current, game), 320, 224).ToArray();
                        int changed = CountRgbChanges(original, pixels);
                        if (changed == 0)
                        {
                            continue;
                        }
                        changedFixtures.Add(selected);
                        observations.Add(new { fixture = selected, current.Frame, callback = current.NativeGraphics!.Callback, changedRgbPixels = changed, originalSha256 = Hash(original), enabledSha256 = Hash(pixels), nativeSources = nativeSources.Order().ToArray() });
                        NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, selected + "-original", current, original);
                        NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, selected + "-enabled", current, pixels);
                    }
                    Select(Fixtures);
                    Assert.Equal(beforeRender, JsonSerializer.Serialize(current));
                    if (!liveRestored && changedFixtures.Count > 0)
                    {
                        ColdNext("live", current);
                        liveRestored = true;
                    }
                }
                if (before?.ReturnedDescriptor is > 0 && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (initialized && Released(current))
                {
                    break;
                }
            }
            boundary = "acceptance";
            Assert.DoesNotContain(host.Mods.Diagnostics, IsReplacementFailure);
            Assert.True(initialized, "The selected original component did not initialize.");
            Assert.True(liveRestored, "No installed texture replacement changed rendered native component RGB pixels.");
            Assert.Equal(requiredFixtures.Order(), changedFixtures.Order());
            Assert.Equal(1, completions);
            Assert.True(Released(current!), "The original task, pools and action104 descriptor must release through native execution.");
            ColdNext("completed", current!);
            Write("result.json", new { status = "completed", effectId, requiredFixtures, initialized, callbacks, diagnosticMaximumCallbacks = 4096, completions, restored, observations, initialHitPoints = hitPoints, finalHitPoints = current!.Combatants.ToDictionary(actor => actor.Id, actor => actor.CurrentHp), nativeSources = nativeSources.Order(), nativeOrigins = nativeOrigins.Values, diagnostics = host.Mods.Diagnostics, input.ArchiveHashes, scope = "Original-resource controlled guardian component with the actual deferred Boost formula, texture-only host toggles, actual CPU scene pixels and independent cold next callbacks; no ordinary command/UI or gameplay-route admission." });

            NativeBattleOriginalEffectState? Selected(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { } effect && effect.Binding.Effect == effectId ? effect : null;

            void Select(params string[] enabled)
            {
                foreach (string fixtureId in Fixtures)
                {
                    string packageId = input.PackageId(fixtureId);
                    ModPackage package = Assert.Single(host.Mods.Catalog.Packages, candidate => candidate.Id == packageId);
                    bool requested = enabled.Contains(fixtureId, StringComparer.OrdinalIgnoreCase);
                    if (package.RequestedEnabled != requested)
                    {
                        Assert.True(host.Mods.SetEnabled(packageId, requested).Success);
                    }
                }
                host.Pump(true);
            }

            void ColdNext(string name, BattleSnapshot snapshot)
            {
                boundary = "cold-" + name;
                Select(Fixtures);
                byte[] pixels = scene.Render(Restore(snapshot, game), 320, 224).ToArray();
                NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, name, snapshot, pixels);
                BattleSnapshot serialized = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(input.OutputDirectory, name + ".battle.json"));
                using GameResourceHost freshHost = input.Open();
                using GameAssets freshGame = new(input.GameDirectory, resources: freshHost.Resources);
                using BattleAssets freshAssets = new(input.GameDirectory, freshHost.Resources);
                BattleSession cold = Restore(serialized, freshGame);
                BattleSession direct = Restore(snapshot, game);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                BattleScenePresentation freshScene = new(freshAssets, snapshot.Definition, true);
                Assert.Equal(pixels, freshScene.Render(cold, 320, 224).ToArray());
                cold.Tick();
                direct.Tick();
                Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] next = freshScene.Render(cold, 320, 224).ToArray();
                Assert.Equal(scene.Render(direct, 320, 224).ToArray(), next);
                Write("cold-" + name + "-diagnostics.json", freshHost.Mods.Diagnostics);
                Assert.DoesNotContain(freshHost.Mods.Diagnostics, IsReplacementFailure);
                NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, name + "-next", cold.Capture(), next);
                restored.Add(name);
            }
        }
        catch (Exception error)
        {
            if (current is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, "unsupported-boundary", current, null);
            }
            if (largestNativeDraw is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(input.OutputDirectory, "largest-native-draw", largestNativeDraw, null);
            }
            Write("result.json", new { status = "blocked", effectId, requiredFixtures, boundary, initialized, callbacks, completions, restored, observations, largestNativeDrawCount, nativeSources = nativeSources.Order(), nativeOrigins = nativeOrigins.Values, diagnostics = host.Mods.Diagnostics, error = error.ToString(), input.ArchiveHashes });
            throw;
        }

        void Write(string filename, object value) => File.WriteAllText(Path.Combine(input.OutputDirectory, filename), JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
    }

    private static BattleSession Restore(BattleSnapshot snapshot, GameAssets assets)
    {
        BattleSession battle = BattleSession.Restore(snapshot);
        battle.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        return battle;
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool IsReplacementFailure(ModDiagnostic diagnostic) => diagnostic.Code is "replacement-failed" or "resource-rejected" or "junction-replacement-failed" or "junction-import-failed" or "junction-setting-conflict";
    private static string FileHash(string path)
    {
        using Stream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class Inputs : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-native-effect-packages-" + Guid.NewGuid().ToString("N"));
        private readonly IsolatedFieldGame game;
        private readonly Dictionary<string, string> working = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ArchiveHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string GameDirectory => game.GameDirectory;
        public string OutputDirectory { get; }

        public Inputs(ushort effect)
        {
            string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_TEXTURE_FIXTURES");
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
            Assert.SkipUnless(File.Exists(catalogPath) && Directory.Exists(TestResources.GameDirectory) && !string.IsNullOrWhiteSpace(output), "Configure optional effect texture fixtures, installed original resources and a private evidence directory.");
            Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath!, Encoding.UTF8))!;
            string fixtureRoot = Path.GetDirectoryName(Path.GetFullPath(catalogPath!))!;
            Assert.SkipUnless(Fixtures.All(fixture => catalog.ContainsKey(fixture) && File.Exists(Path.GetFullPath(catalog[fixture], fixtureRoot))), "Configure both optional texture archives.");
            OutputDirectory = Path.Combine(output!, "EffectPackages", "Native", $"component-{effect}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(OutputDirectory);
            game = new IsolatedFieldGame();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Mods", "Junction"));
                for (int index = 0; index < Fixtures.Length; index++)
                {
                    string fixture = Fixtures[index];
                    string source = Path.GetFullPath(catalog[fixture], fixtureRoot);
                    string hash = FileHash(source);
                    sources.Add(source, hash);
                    ArchiveHashes.Add(fixture, hash);
                    string destination = Path.Combine(root, "Mods", "Junction", $"fixture-{index + 1}.iroj");
                    File.Copy(source, destination);
                    working.Add(fixture, destination);
                    IReadOnlyDictionary<string, int> options = JunctionModImporter.Open(destination).Manifest.Options;
                    File.WriteAllLines(destination + ".ini", new[] { "enabled = 1" }.Concat(options.Select(option => $"option.{option.Key} = {option.Value.ToString(CultureInfo.InvariantCulture)}")), new UTF8Encoding(false));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string PackageId(string fixture) => "Junction/" + Path.GetFileName(working[fixture]);
        public GameResourceHost Open() => new(GameDirectory, applicationBaseDirectory: Path.Combine(root, "Application"), watch: false, modsDirectory: Path.Combine(root, "Mods"));

        public void Dispose()
        {
            try
            {
                foreach ((string path, string hash) in sources)
                {
                    Assert.Equal(hash, FileHash(path));
                }
                foreach ((string fixture, string path) in working)
                {
                    Assert.Equal(ArchiveHashes[fixture], FileHash(path));
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                finally
                {
                    game.Dispose();
                }
            }
        }
    }
}
