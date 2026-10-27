using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325SecondStreamGateTests
{
    [Fact]
    public void File_poll_wait_keeps_draw_progress_then_publishes_new_stream_without_playing_it()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, new byte[0x3890].ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0]));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25051A0, NativeBattleEffectNodeMemory.ArenaAddress, 0x994, 1);
        (pool, int index) = pool.Append(0x6545F0);
        NativeBattleModulePoolTask task = (NativeBattleTaskFixture.EditTask(pool.Slots[index], taskData => taskData with { Payload = new byte[0x994 - 12].ToImmutableArray() })).WriteWord(0xC, 87);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(index, task) });
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x4C14);
        memory = memory with { Diablos325 = memory.Diablos325! with { Mode51F0 = 3 }, SummonStream = new(690161, NativeBattleEffectNodeMemory.ArenaAddress + 0x1685Cu), Arena = memory.Arena!.Write(0x4C14, [1, 0, 1, 1, 0]).Write(0x1685C, [162, 0, 0, 0]) };
        frame = frame with { Memory = memory, Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool), Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        List<string> calls = [];
        NativeBattleDiablos325Inputs inputs = new(fixture.Packets, current => throw new InvalidOperationException())
        {
            PresentProgramme = (current, currentTask) =>
            {
                calls.Add("programme");
                return new(current, currentTask.WriteDword(0x268, currentTask.ReadDword(0x268) + 1));
            },
            PresentWell = (current, counter) =>
            {
                Assert.Equal((ushort)87, counter);
                calls.Add("well");
                return current;
            },
            DrawPuffs = current =>
            {
                calls.Add("puffs");
                return current;
            },
            PlayStream = (current, pan, volume) =>
            {
                calls.Add("stream");
                Assert.Equal(128u, pan);
                Assert.Equal(127u, volume);
            },
            PresentTail = (current, counter) =>
            {
                calls.Add("tail");
                return current;
            }
        };
        NativeBattleDiablos325Frame waiting = NativeBattleDiablos325Timeline.AdvanceOpening6545F0(frame, inputs);
        Assert.Equal(new[] { "programme", "well", "puffs" }, calls);
        NativeBattleModulePoolTask retained = waiting.Effect.Pools.Single(item => item.DescriptorAddress == 0x25051A0).Slots[0];
        Assert.Equal((ushort)87, retained.ReadWord(0xC));
        Assert.Equal(1u, retained.ReadDword(0x268));
        Assert.Equal((byte)0, retained.ReadByte(0xF));
        Assert.Equal(690161, waiting.Memory.SummonStream!.Number);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(retained).Payload, waiting.Memory.Arena!.Read(12, NativeBattleStorageSnapshots.Capture(retained).Payload.Length).ToImmutableArray());
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
        calls.Clear();
        waiting = waiting with { Memory = waiting.Memory with { Arena = waiting.Memory.Arena!.Write(0x4C14, [0, 0, 1, 1, 1]) } };
        NativeBattleDiablos325Frame ready = NativeBattleDiablos325Timeline.AdvanceOpening6545F0(waiting, inputs);
        Assert.Equal(new[] { "programme", "well", "puffs", "tail" }, calls);
        NativeBattleModulePoolTask advanced = ready.Effect.Pools.Single(item => item.DescriptorAddress == 0x25051A0).Slots[0];
        Assert.Equal((ushort)88, advanced.ReadWord(0xC));
        Assert.Equal(2u, advanced.ReadDword(0x268));
        Assert.Equal((byte)1, advanced.ReadByte(0xF));
        Assert.Equal(new NativeBattleSummonStreamState(690162, NativeBattleEffectNodeMemory.ArenaAddress + 0x1685Cu), ready.Memory.SummonStream);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, ready.Memory.Arena!.Read(0x4C14, 5));
    }
}
