using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325WingGateTests
{
    [Fact]
    public void Unacknowledged_stream_keeps_programme_and_wing_updates_then_plays_and_queues_exact_next_file_once()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, new byte[0x3890].ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0])) { Sounds = [240248, 240251] };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25051A0, NativeBattleEffectNodeMemory.ArenaAddress, 0x994, 1);
        (pool, int slot) = pool.Append(0x6545F0);
        NativeBattleModulePoolTask task = (pool.Slots[slot] with { Payload = new byte[0x994 - 12].ToImmutableArray() }).WriteWord(0xC, 91);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 0x4C14);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x534150 }, Diablos325 = memory.Diablos325! with { Mode51F0 = 0 } };
        frame = frame with { Memory = memory, Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool), Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
        string original = JsonSerializer.Serialize(frame);
        List<string> calls = [];
        NativeBattleDiablos325Inputs inputs = new(fixture.Packets, current => throw new InvalidOperationException())
        {
            PresentProgramme = (current, currentTask) =>
            {
                calls.Add("programme");
                return new(current, currentTask.WriteDword(0x268, currentTask.ReadDword(0x268) + 1));
            },
            PresentWings = (current, currentTask) =>
            {
                calls.Add("wings");
                return new(current, currentTask.WriteDword(0x14, 0x20DFAB8u + 0x3505C));
            },
            PresentWell = (current, counter) => throw new InvalidOperationException(),
            DrawPuffs = current => throw new InvalidOperationException(),
            PlayStream = (current, pan, volume) =>
            {
                calls.Add("stream");
                Assert.Equal(128u, pan);
                Assert.Equal(127u, volume);
            },
            PlaySound = (sound, flags, pan) =>
            {
                calls.Add("sfx");
                Assert.Equal(240251u, sound);
                Assert.Equal(1, flags);
                Assert.Equal(128, pan);
            },
            QueueFile = (current, request) =>
            {
                calls.Add("queue");
                Assert.Equal(new NativeBattleEffectFileRequest(732, 0x20DFAB8u + 0x1685C, 0x534150), request);
                return current;
            },
            PresentTail = (current, counter) =>
            {
                calls.Add("tail");
                return current;
            }
        };
        NativeBattleDiablos325Frame waiting = NativeBattleDiablos325Timeline.AdvanceOpening6545F0(frame, inputs);
        Assert.Equal(new[] { "programme", "wings" }, calls);
        NativeBattleEffectTaskPool retainedPool = waiting.Effect.Pools.Single(item => item.DescriptorAddress == 0x25051A0);
        NativeBattleModulePoolTask retained = retainedPool.Slots[0];
        Assert.Equal((ushort)91, retained.ReadWord(0xC));
        Assert.Equal(1u, retained.ReadDword(0x268));
        Assert.Equal(0x20DFAB8u + 0x3505C, retained.ReadDword(0x14));
        Assert.Equal(retained.Payload, waiting.Memory.Arena!.Read(12, retained.Payload.Length).ToImmutableArray());
        Assert.Equal(original, JsonSerializer.Serialize(frame));
        calls.Clear();
        NativeBattleEffectTaskPool acknowledged = retainedPool with { Slots = retainedPool.Slots.SetItem(0, retained.WriteByte(0xF, 1)) };
        waiting = waiting with { Memory = waiting.Memory with { Arena = waiting.Memory.Arena!.Write(15, [1]) }, Effect = waiting.Effect with { Pools = waiting.Effect.Pools.Select(item => item.DescriptorAddress == 0x25051A0 ? acknowledged : item).ToImmutableArray() } };
        NativeBattleDiablos325Frame ready = NativeBattleDiablos325Timeline.AdvanceOpening6545F0(waiting, inputs);
        Assert.Equal(new[] { "programme", "wings", "stream", "queue", "sfx", "tail" }, calls);
        NativeBattleModulePoolTask advanced = ready.Effect.Pools.Single(item => item.DescriptorAddress == 0x25051A0).Slots[0];
        Assert.Equal((ushort)92, advanced.ReadWord(0xC));
        Assert.Equal(2u, advanced.ReadDword(0x268));
        Assert.Equal(new byte[] { 1, 0, 1, 1, 0 }, ready.Memory.Arena!.Read(0x4C14, 5));
    }
}
