using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionBattleInstalledTests
{
    private static readonly string[] Roles = ["stages", "characters", "enemies"];

    [Fact]
    public void OriginalPackageSetHasOnlySupportedOrProvenUnusedTargets()
    {
        string archive = TestResources.FixturePath("junction-battle-stages");
        string junction = Path.GetDirectoryName(Path.GetDirectoryName(archive))!;
        Assert.Equal("Junction", Path.GetFileName(junction));
        using GameResourceHost host = new(TestResources.GameDirectory, watch: false, modsDirectory: Directory.GetParent(junction)!.FullName);
        foreach (string role in Roles)
        {
            string path = TestResources.FixturePath("junction-battle-" + role);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(path, StringComparison.OrdinalIgnoreCase)).Status);
        }
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "ffnx-unsupported-target" or "junction-setting-conflict");
    }

    [Fact]
    public void OriginalPackagesAffectTheLiveSceneAndPreserveAttackSnapshotContinuation()
    {
        Dictionary<string, string> archives = Roles.ToDictionary(role => role, role => TestResources.FixturePath("junction-battle-" + role));
        Dictionary<string, byte[]?> settings = archives.Values.ToDictionary(path => path + ".ini", path => File.Exists(path + ".ini") ? File.ReadAllBytes(path + ".ini") : null);
        string? junction = Path.GetDirectoryName(archives.Values.First());
        while (junction is not null && !Path.GetFileName(junction).Equals("Junction", StringComparison.OrdinalIgnoreCase))
        {
            junction = Path.GetDirectoryName(junction);
        }
        Assert.NotNull(junction);
        string mods = Directory.GetParent(junction)!.FullName;
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        try
        {
            using GameResourceHost host = new(TestResources.GameDirectory, watch: false, modsDirectory: mods);
            Assert.All(archives.Values, path => Assert.Equal(ModEffectiveStatus.Active, Package(path).Status));
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "junction-setting-conflict" || diagnostic.Code == "ffnx-unsupported-target" && diagnostic.RelativePath?.StartsWith("mods/textures/battle/", StringComparison.OrdinalIgnoreCase) == true);
            GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-battle-source")));
            using GameAssets game = new(TestResources.GameDirectory, resources: host.Resources);
            using GameWorld world = new(session, TestResources.GameDirectory, resources: host.Resources, resolveGameFile: host.ResolveGameFile, modelOverrides: host);
            world.EnsureField();
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Bind(battle);
            using BattleAssets assets = new(TestResources.GameDirectory, host.Resources);
            BattleScenePresentation scene = new(assets, battle.Capture().Definition, true);
            Console.WriteLine($"junction-battle-source field={session.State.FieldId} battle={battle.Frame} native={battle.NativeGraphics!.Callback} stage={battle.Capture().Definition.StageId}");
            Select();
            string original = Signature(scene.Render(battle, 640, 448));
            foreach (string role in Roles)
            {
                Select(role);
                Assert.NotEqual(original, Signature(scene.Render(battle, 640, 448)));
                Select();
                Assert.Equal(original, Signature(scene.Render(battle, 640, 448)));
            }
            Select(Roles);
            string actor = battle.Capture().Definition.Combatants.First(candidate => candidate.IsPlayer && battle.CanChoosePlayerCommand(candidate.Id)).Id;
            string target = battle.Capture().Definition.Combatants.First(candidate => !candidate.IsPlayer).Id;
            Assert.Null(battle.ActiveAction);
            Assert.Empty(battle.QueuedActions);
            int events = battle.Events.Count;
            battle.QueueAction(actor, target, BattleAbility.Attack, 0);
            bool approach = false;
            bool contact = false;
            bool completed = false;
            int modelIndex = battle.NativeGraphics.ActorIds.IndexOf(actor);
            for (int callback = 0; callback < 4096; callback++)
            {
                battle.Tick();
                NativeBattleSequenceState sequence = battle.NativeGraphics!.Models[modelIndex].SequenceState!;
                if (!approach && (sequence.Header36 != 0 || sequence.Header38 != 0 || sequence.Header40 != 0))
                {
                    approach = true;
                    Capture("approach");
                    CompareNext();
                    using GpuTestDevice device = new();
                    using GpuTriangleRenderer gpu = new(device.Device);
                    scene.Render(battle, 640, 448, gpu);
                    Assert.Contains(GpuTestDevice.Read(gpu.Target), pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                    using FileStream image = File.Create(Path.Combine(output, "battle-approach-gpu.png"));
                    gpu.Target.SaveAsPng(image, 640, 448);
                }
                if (!contact && battle.Events.Skip(events).Any(entry => entry.Kind == "Damage" && entry.ActorId == actor))
                {
                    contact = true;
                    Capture("contact");
                    CompareNext();
                }
                if (battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeGraphics.OriginalEffect is not { ReturnedDescriptor: > 0 })
                {
                    completed = true;
                    Capture("completed");
                    CompareNext();
                    break;
                }
            }
            Assert.True(approach && contact && completed, "The actual physical action must approach, hit and complete.");
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "replacement-failed" or "junction-import-failed");

            ModPackage Package(string path)
            {
                return Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(path, StringComparison.OrdinalIgnoreCase));
            }

            void Select(params string[] enabled)
            {
                foreach ((string role, string path) in archives)
                {
                    Assert.True(host.Mods.SetEnabled(Package(path).Id, enabled.Contains(role, StringComparer.Ordinal)).Success);
                }
                host.Pump(true);
            }

            void Bind(BattleSession selected)
            {
                selected.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            }

            void CompareNext()
            {
                BattleSnapshot snapshot = battle.Capture();
                BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                Bind(clone);
                battle.Tick();
                clone.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                Assert.Equal(scene.Render(battle, 640, 448), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 640, 448));
            }

            void Capture(string name)
            {
                world.CaptureBattleState();
                string path = Path.Combine(output, "battle-" + name + ".json");
                session.Save(path, compact: true);
                Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(GameSession.Load(path)));
                byte[] pixels = scene.Render(battle, 640, 448);
                using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 640, 448);
                image.Save(Path.Combine(output, "battle-" + name + ".png"));
                Console.WriteLine($"junction-battle-checkpoint name={name} field={session.State.FieldId} battle={battle.Frame} native={battle.NativeGraphics!.Callback} path={path}");
            }
        }
        finally
        {
            foreach ((string path, byte[]? content) in settings)
            {
                if (content is null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, content);
                }
            }
        }
    }

    private static string Signature(byte[] pixels)
    {
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
