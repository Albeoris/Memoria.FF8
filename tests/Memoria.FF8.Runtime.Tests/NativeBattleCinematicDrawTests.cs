using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicDrawTests
{
    [Theory]
    [InlineData(201, -1)]
    [InlineData(201, 1)]
    [InlineData(202, -1)]
    [InlineData(202, 1)]
    public void Draw_pass_reuses_ordering_across_slots_without_changing_packets_lighting_or_failed_publication(int effect, int failingSlot)
    {
        const uint manager = 0x2797690;
        NativeBattleWaterExecutionFrame original = NativeBattleIfritCallbackTests.Create();
        original = WaterSchedulerFixture.CoreDword(original, manager + 0x90, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        original = WaterSchedulerFixture.CoreDword(original, manager + 0x7C, 0x40);
        original = WaterSchedulerFixture.CoreDword(original, manager + 0xD8, 0x5000);
        original = original with { Core = original.Core.Write(0x2797554, [0, 1, 2, 0xFF]) };
        for (int index = 0; index < 3; index++)
        {
            original = original with { Arena = original.Arena.Write(0x201C + index * 0x100, [(byte)(index + 1)]).Write(0x20E1 + index * 0x100, [(byte)(index + 11)]) };
        }
        original = original with { Arena = original.Arena.Write(0x214C, [1]) };
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(20, new("20DFAB8", 0, 0x40), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        source = source.ClearRange45D550(17, 0x1000);
        string inputFrame = NativeBattleTestSnapshots.Json(original);
        string inputOrdering = NativeBattleTestSnapshots.Json(source);
        var scalar = Run(false);
        var batched = Run(true);
        Assert.Equal(NativeBattleTestSnapshots.Json(scalar.Frame), NativeBattleTestSnapshots.Json(batched.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(scalar.Memory), NativeBattleTestSnapshots.Json(batched.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(scalar.Ordering), NativeBattleTestSnapshots.Json(batched.Ordering));
        Assert.Equal(scalar.Calls, batched.Calls);
        Assert.Equal(inputFrame, NativeBattleTestSnapshots.Json(original));
        Assert.Equal(inputOrdering, NativeBattleTestSnapshots.Json(source));
        Assert.Null(batched.Ordering.Transaction);
        if (failingSlot >= 0)
        {
            Assert.Null(batched.Frame);
            Assert.Same(source, batched.Ordering);
        }
        else
        {
            Assert.Equal(0x68u, WaterSchedulerFixture.ReadCoreDword(batched.Frame!, manager + 0x7C));
            Assert.Equal(0x5014u, WaterSchedulerFixture.ReadCoreDword(batched.Frame!, manager + 0xD8));
            Assert.Equal(4, batched.Ordering.ProxyCursor);
            Assert.Equal(NativeBattleCinematicProfile.ForEffect(effect).Lit ? 6 : 3, batched.Calls.Length);
        }

        (NativeBattleWaterExecutionFrame? Frame, NativeBattleEffectMemory Memory, NativeBattleLogicalOrdering Ordering, string[] Calls) Run(bool batch)
        {
            NativeBattleWaterExecutionFrame input = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(original));
            NativeBattleLogicalOrdering inputOrdering = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
            using NativeBattleLogicalOrderingTransaction? transaction = batch ? new(inputOrdering) : null;
            NativeBattleLogicalOrdering ordering = transaction?.View ?? inputOrdering;
            NativeBattleEffectMemory memory = new(input.Arena, 0, 0, [], 0, 0, 0, 0);
            List<string> calls = [];
            int slot = 0;
            NativeBattleWaterExecutionFrame? drawn = null;
            try
            {
                drawn = NativeBattleCinematicDraw.Run(NativeBattleCinematicProfile.ForEffect(effect), input, (current, light) =>
                {
                    Assert.Equal(slot + 11, light);
                    calls.Add($"light{light}");
                    return current;
                }, (current, kind) =>
                {
                    Assert.Equal(slot + 1, kind);
                    calls.Add($"draw{kind}");
                    uint output = WaterSchedulerFixture.ReadCoreDword(current, manager + 0x7C);
                    Assert.Equal(slot == 1 ? 0x5000u : slot == 0 ? 0x40u : 0x54u, output);
                    ImmutableArray<uint> words = [0x04000000, 0x20010203u + kind, 1, 2, 3];
                    byte[] data = new byte[20];
                    for (int index = 0; index < words.Length; index++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
                    }
                    ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 20).ToImmutableArray();
                    NativeBattleGfGeometryBatch packets = new([new(0, data.ToImmutableArray(), mask, new(20 + slot, words, []))], 20);
                    if (slot++ == failingSlot)
                    {
                        packets = packets with { Attempts = [.. packets.Attempts, new(0, [], [], null)] };
                    }
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory with { Arena = current.Arena }, checked((int)output), ordering, packets);
                    memory = written.Memory;
                    ordering = written.Ordering;
                    current = current with { Arena = memory.Arena! };
                    return WaterSchedulerFixture.CoreDword(current, manager + 0x7C, output + 20);
                });
                ordering = transaction?.Freeze() ?? ordering;
            }
            catch (InvalidDataException) when (failingSlot >= 0)
            {
                ordering = source;
            }
            return (drawn, memory, ordering, calls.ToArray());
        }
    }
}
