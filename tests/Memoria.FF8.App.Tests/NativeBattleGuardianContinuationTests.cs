using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianContinuationTests
{
    [Fact]
    public void Continue_the_named_completed_guardian_save_for_sixteen_native_callbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SETTLE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SETTLE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "A concrete completed GF source and a distinct after-event output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.Equal((ushort)116, snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, snapshot.ReadFixturePresentation().NativeEffectMemory!.Active);
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        List<string> colors = [$"{snapshot.ReadFixturePresentation().NativeGraphics!.Callback}:{ReadOriginalModelCell(snapshot.ReadFixturePresentation().NativeGraphics!, 0).Color40:X8}"];
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int index = 0; index < 16; index++)
        {
            battle.AdvanceNativeGraphicsPresentation(callback);
            independent.AdvanceNativeGraphicsPresentation(callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
            colors.Add($"{battle.Presentation!.Graphics.Callback}:{ReadOriginalModelCell(battle.Presentation!.Graphics, 0).Color40:X8}");
        }
        BattleSnapshot after = battle.CaptureForContinuation();
        Assert.Equal(snapshot.ReadFixturePresentation().NativeGraphics!.Callback + 16, after.ReadFixturePresentation().NativeGraphics!.Callback);
        Assert.Equal(0u, after.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, after.ReadFixturePresentation().NativeEffectMemory!.Active);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition);
        Assert.Equal(scene.Render(battle, 320, 224).ToArray(), scene.Render(independent, 320, 224).ToArray());
        using FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, after);
        Console.WriteLine($"Completed GF after-event continuation: frame{after.Frame}/callback{after.ReadFixturePresentation().NativeGraphics!.Callback}; caster Color40 trace {string.Join(',', colors)}; independent states and copied render agree.");
    }

    [Fact]
    public void Continue_the_named_standalone_guardian_save_to_its_next_native_boundary_or_completed_cast()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_CONTINUE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_CONTINUE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "A concrete standalone GF source and distinct boundary output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal((ushort)116, snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Binding.Effect);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition);
        Assert.Equal(320 * 224 * 4, scene.Render(battle, 320, 224).Length);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        bool sawPhase73Flash = false;
        for (int index = 0; index < 512; index++)
        {
            BattleSnapshot before = battle.CaptureForContinuation();
            ushort phase = before.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12);
            try
            {
                battle.AdvanceNativeGraphicsPresentation(callback);
                Assert.Equal(320 * 224 * 4, scene.Render(battle, 320, 224).Length);
                if (phase == 73)
                {
                    Assert.Contains(battle.Presentation!.Graphics.PrimitiveFrame!.LastCommands, command => command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x2A && command.Command.OrderingBucket == 8);
                    sawPhase73Flash = true;
                }
                if (phase == 212)
                {
                    uint streamNumber = unchecked((uint)battle.Presentation!.Memory.SummonStream!.Number!.Value);
                    Assert.Single(battle.Events.Skip(before.Events.Length), item => item.NativeModuleSound is { Flags: 0, RawPan: 128, Volume: 127 } sound && sound.Sound == streamNumber);
                }
                if (battle.Presentation?.Memory is { Active: 0 } && battle.Presentation?.Graphics is { OriginalEffect: { ReturnedDescriptor: 0 } } completedGraphics && !completedGraphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104))
                {
                    BattleSnapshot completed = battle.CaptureForContinuation();
                    BattleSession completedClone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
                    Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(completedClone.Capture()));
                    Assert.True(completed.ReadFixturePresentation().NativeGraphics!.Callback > snapshot.ReadFixturePresentation().NativeGraphics!.Callback);
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), scene.Render(completedClone, 320, 224).ToArray());
                    using FileStream completedStream = new(output!, FileMode.CreateNew, FileAccess.Write);
                    JsonSerializer.Serialize(completedStream, completed);
                    Console.WriteLine($"Completed the saved current GF cast at frame{completed.Frame}/callback{completed.ReadFixturePresentation().NativeGraphics!.Callback}: native effect inactive, main descriptor released and Action104 unlinked; independent JSON and copied render agree.");
                    return;
                }
            }
            catch (NotSupportedException exception)
            {
                Console.WriteLine($"Reached stable frame{before.Frame}/callback{before.ReadFixturePresentation().NativeGraphics!.Callback}/phase{phase}: {exception.Message}; combatants={string.Join(", ", before.Combatants.Select(actor => $"{actor.Id}:HP{actor.CurrentHp}/ATB{actor.Atb}"))}");
                BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(clone.Capture()));
                NotSupportedException repeated = Assert.Throws<NotSupportedException>(() => clone.AdvanceNativeGraphicsPresentation(callback));
                Assert.Equal(exception.Message, repeated.Message);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                Assert.True(before.ReadFixturePresentation().NativeGraphics!.Callback > snapshot.ReadFixturePresentation().NativeGraphics!.Callback && before.Frame >= snapshot.Frame, "The standalone continuation must advance actual graphics callbacks without rolling back the battle frame before its next unsupported boundary.");
                if (snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12) <= 73 && phase > 73)
                {
                    Assert.True(sawPhase73Flash);
                }
                if (snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12) == 207 && phase > 212)
                {
                    BattleEvent[] newEvents = battle.Events.Skip(snapshot.Events.Length).ToArray();
                    Assert.Single(newEvents, item => item.NativeModuleSound is { Sound: 240072, Flags: 1, Pan: 128 });
                }
                using FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(stream, before);
                Console.WriteLine($"Continued frame{snapshot.Frame}/callback{snapshot.ReadFixturePresentation().NativeGraphics!.Callback} to stable frame{before.Frame}/callback{before.ReadFixturePresentation().NativeGraphics!.Callback}, phase{phase}: {exception.Message}");
                return;
            }
        }
        BattleSnapshot diagnostic = battle.CaptureForContinuation();
        using (FileStream diagnosticStream = new(output! + ".bounded.battle.json", FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(diagnosticStream, diagnostic);
        }
        string taskSummary = string.Join(',', diagnostic.ReadFixturePresentation().NativeGraphics!.Descriptors.Tasks.Select(task => $"{task.Kind}:{task.Flags:X}/{task.Phase}"));
        Assert.Fail($"The bounded standalone continuation did not reach its next strict boundary or completed cast: frame{diagnostic.Frame}/callback{diagnostic.ReadFixturePresentation().NativeGraphics!.Callback}, effectActive={diagnostic.ReadFixturePresentation().NativeEffectMemory?.Active}, root={diagnostic.ReadFixturePresentation().NativeGraphics!.OriginalEffect?.ReturnedDescriptor:X}, completion={diagnostic.ReadFixturePresentation().NativeEffectMemory?.QuezacotlCompletion17A8}, IO={diagnostic.ReadFixturePresentation().NativeEffectIoResult}, tasks={taskSummary}. A distinct bounded diagnostic was saved.");
    }
}
