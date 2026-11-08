using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect301LifecycleTests(ITestOutputHelper output)
{
    [Fact]
    public void Selected_original301_reports_bounded_native_admission_and_script_lifecycle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT301_CONTINUATION_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and a standalone original301 source for native lifecycle diagnosis.");
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.True(source.NativeEffectMemory!.Effect301!.ScriptSourcePending);
        using GameAssets game = new(directory!);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        int reports = 0;
        int dispatches = 0;
        int releases = 0;
        Lifecycle before = Observe(battle);
        Report("source", before, before, null);
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ExecutedEffect == 301)
            {
                dispatches++;
                if (dispatches <= 4)
                {
                    output.WriteLine(JsonSerializer.Serialize(new { Boundary = "dispatch", Before = before, After = Observe(battle), Dispatch = dispatch }));
                }
            }
        };
        for (int callback = 0; callback < 360; callback++)
        {
            before = Observe(battle);
            try
            {
                battle.Tick();
            }
            catch (Exception error)
            {
                output.WriteLine(JsonSerializer.Serialize(new { Boundary = "failure", Before = before, After = Observe(battle), Dispatches = dispatches, Releases = releases, Error = error.Message }));
                throw;
            }
            Lifecycle after = Observe(battle);
            if (before.Returned is > 0 && after.Returned == 0)
            {
                releases++;
            }
            if (before.Returned != after.Returned || before.Pending != after.Pending || after.MainAge is 1 or 101 or 102 or 142 || after.MainAge < before.MainAge)
            {
                Report("transition", before, after, null);
            }
            if (releases > 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0)
            {
                output.WriteLine(JsonSerializer.Serialize(new { Boundary = "drained", State = after, Dispatches = dispatches, Releases = releases }));
                Assert.Equal(1, dispatches);
                Assert.Equal(0x103CFB8u, after.Cursor);
                Assert.False(after.Pending);
                return;
            }
        }
        output.WriteLine(JsonSerializer.Serialize(new { Boundary = "bounded-end", State = Observe(battle), Dispatches = dispatches, Releases = releases, Blockers = battle.ObserveDiagnosticDrainBlockers() }));
        Assert.Fail("The selected original301 source did not drain within the bounded lifecycle window.");

        void Report(string boundary, Lifecycle prior, Lifecycle current, BattleDiagnosticEffectDispatch? dispatch)
        {
            if (reports < 8)
            {
                reports++;
                output.WriteLine(JsonSerializer.Serialize(new { Boundary = boundary, Before = prior, After = current, Dispatch = dispatch }));
            }
        }
    }

    private static Lifecycle Observe(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        NativeBattleEffect301State state = battle.NativeEffectMemory!.Effect301!;
        (byte[] bytes, bool[] known) = state.ScriptCursor4.ReadSnapshot(0, 4);
        uint? cursor = known.All(value => value) ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : null;
        NativeBattleEffectTaskPool? main = effect?.Pools.FirstOrDefault(pool => pool.DescriptorAddress == NativeBattleEffect301Profile.MainDescriptor);
        NativeBattleModulePoolTask? task = main?.Slots.FirstOrDefault(slot => (slot.Flags & 1) != 0);
        short? age = task is null ? null : unchecked((short)task.ReadWord(12));
        Pool[] pools = effect?.Pools.Select(pool => new Pool(pool.DescriptorAddress, pool.Head, pool.Tail, pool.Slots.Count(slot => (slot.Flags & 1) != 0))).ToArray() ?? [];
        return new(graphics.Callback, effect?.RequestedEffect, effect?.ReturnedDescriptor, state.ScriptSourcePending, cursor, age, pools);
    }

    private sealed record Lifecycle(long Callback, ushort? Effect, uint? Returned, bool? Pending, uint? Cursor, short? MainAge, Pool[] Pools);
    private sealed record Pool(uint Descriptor, int? Head, int? Tail, int Active);
}
