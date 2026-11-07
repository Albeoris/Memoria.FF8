using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattleBreathPerformanceTests
{
    [Theory]
    [InlineData((ushort)129, 8, 102, 338)]
    [InlineData((ushort)157, 8, 37, 413)]
    [InlineData((ushort)48, 8, 120, 147)]
    [InlineData((ushort)131, 8, 184, 376)]
    [InlineData((ushort)300, 15, 10, 368)]
    [InlineData((ushort)230, 15, 9, 368)]
    [InlineData((ushort)113, 8, 17, 22)]
    [InlineData((ushort)11, 8, 9, 284)]
    [InlineData((ushort)235, 8, 94, -41)]
    public void Original_breath_completes_with_gpu_submission_and_independent_live_continuation(ushort effect, int command, int action, int formation)
    {
        // Match the Release application runtime with DOTNET_TC_QuickJit=0 when collecting timings.
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(output is not null, "Select a private output directory for installed breath performance diagnostics.");
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH_PERFORMANCE_EFFECT");
        Assert.SkipUnless(selected is null || selected.Split(',').Contains(effect.ToString(System.Globalization.CultureInfo.InvariantCulture)), "A different breath diagnostic was selected.");
        string directory = TestResources.GameDirectory;
        Directory.CreateDirectory(output!);
        string fixture = TestResources.ArtifactFile("breath-" + effect);
        if (formation < 0)
        {
            using BattleAssets selection = new(directory);
            formation = Enumerable.Range(0, 1024).Select(selection.LoadEncounter).First(item => item.Slots.Any(enemy => enemy.ModelId == -formation)).Id;
        }
        bool player = command == 15;
        NativeEffectFixtureRequest request = new("breath", effect, (byte)command, (ushort)action, player ? (byte)3 : (byte)0, 800, 240)
        {
            EncounterId = formation,
            CasterActorIndex = player ? (byte)0 : (byte)3,
            PartyModelId = player ? 3 : 0,
            CrisisLevel = player ? (byte)1 : null,
            PreserveOriginalFormationEnemies = true,
            ControlledContactValue = effect == 300 ? (ushort)0 : (ushort)7
        };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory, fixture);
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        List<FrameMeasurement> rows = [];
        List<object> continuations = [];
        for (int cast = 0; cast < 2; cast++)
        {
            BattleSession battle = Restore(source);
            BattleScenePresentation scene = new(assets, source.Definition, true);
            bool entered = false;
            bool completed = false;
            for (int display = 0; display < 1600; display++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                Advance(battle, display);
                double update = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                start = Stopwatch.GetTimestamp();
                scene.Render(battle, 960, 720, gpu);
                double render = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                NativeBattleOriginalEffectState? current = battle.NativeGraphics!.OriginalEffect;
                bool live = current is { ReturnedDescriptor: > 0 };
                entered |= live;
                rows.Add(new(cast, display, live, update, render, bytes));
                if (entered && !live && battle.NativeEffectMemory!.Active == 0 && current is not null && current.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0)))
                {
                    completed = true;
                    Assert.Equal(0, battle.NativeEffectMemory!.Active);
                    Assert.All(current!.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
                    break;
                }
            }
            Assert.True(entered && completed, $"Effect {effect}: entered={entered}, completed={completed}.");
        }
        // Keep snapshot serialization and its GC pressure outside both timing passes.
        BattleSession direct = Restore(source);
        int[] liveDisplays = rows.Where(row => row.Cast == 0 && row.Live).Select(row => row.Display).ToArray();
        Assert.NotEmpty(liveDisplays);
        int boundary = liveDisplays[liveDisplays.Length / 2];
        for (int display = 0; display <= boundary; display++)
        {
            Advance(direct, display);
        }
        Assert.True(direct.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0 });
        string json = JsonSerializer.Serialize(direct.Capture());
        BattleSession restored = Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Advance(direct, boundary + 1);
        Advance(restored, boundary + 1);
        string next = JsonSerializer.Serialize(direct.Capture());
        Assert.Equal(next, JsonSerializer.Serialize(restored.Capture()));
        continuations.Add(new { Display = boundary, NextSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(next))) });
        File.WriteAllText(Path.Combine(output!, $"breath-{effect}.json"), JsonSerializer.Serialize(new { Effect = effect, Width = 960, Height = 720, FramesPerSecond = 60, Timing = "CPU update and GPU submission; two independent component casts; readback excluded", Adapter = device.Device.Adapter.Description, Continuations = continuations, Frames = rows }));

        static void Advance(BattleSession battle, int display)
        {
            battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(166667));
            if ((display & 1) != 0)
            {
                battle.Tick();
            }
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession battle = BattleSession.Restore(snapshot);
            battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            return battle;
        }
    }

    private sealed record FrameMeasurement(int Cast, int Display, bool Live, double UpdateMilliseconds, double RenderMilliseconds, long AllocatedBytes);
}
