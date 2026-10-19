using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorRotation72Tests
{
    [Fact]
    public void Signed_actor_rotation_WORDs_publish_fixed_point_then_republish_current_scale_without_operands()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(255, 0x12340000);
        List<uint> reads = [];
        short[] rotation = [short.MinValue, -1, short.MaxValue];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorRotation72.Execute(frame, (current, address, length) =>
        {
            int lane = reads.Count;
            Assert.Equal(2, length);
            Assert.Equal(0x1234000Cu + (uint)(lane * 2), address);
            for (int previous = 0; previous < lane; previous++)
            {
                Assert.Equal(unchecked((uint)(rotation[previous] << 16)), WaterSchedulerFixture.ReadSlotDword(current, 0, 0x50 + previous * 4));
            }
            reads.Add(address);
            return BitConverter.GetBytes(rotation[lane]);
        });
        Assert.Equal(3, reads.Count);
        for (int lane = 0; lane < 3; lane++)
        {
            Assert.Equal(unchecked((uint)(rotation[lane] << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50 + lane * 4));
            Assert.Equal(unchecked((ushort)rotation[lane]), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8C + lane * 2));
        }
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Actor_source_alias_reads_each_current_WORD_after_the_previous_fixed_point_store()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, Address(0x2042));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x4E, 0xFFFE);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, 0x12345678);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorRotation72.Execute(frame, (_, _, _) => throw new InvalidOperationException("Source is the current arena owner."));
        Assert.Equal(0xFFFE0000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50));
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x54));
        Assert.Equal(0xFFFE0000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x58));
        Assert.Equal(0xFFFE, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8C));
        Assert.Equal(0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8E));
        Assert.Equal(0xFFFE, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x90));
    }

    [Fact]
    public void Canonical_core_actor_source_and_snapshot_repeat_keep_unrelated_slot_words()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 0x2797800);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x279780C, 0x0102);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x279780E, 0x8000);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797810, 0xFFFF);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame a = NativeBattleCinematicActorRotation72.Execute(frame, (_, _, _) => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame b = NativeBattleCinematicActorRotation72.Execute(restored, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(0x01020000u, WaterSchedulerFixture.ReadSlotDword(a, 0, 0x50));
        Assert.Equal(0x80000000u, WaterSchedulerFixture.ReadSlotDword(a, 0, 0x54));
        Assert.Equal(0xFFFF0000u, WaterSchedulerFixture.ReadSlotDword(a, 0, 0x58));
        Assert.Equal(0xA5A5, WaterSchedulerFixture.ReadSlotWord(a, 0, 0x92));
    }

    private static NativeBattleWaterExecutionFrame Fixture(byte actor, uint pointer)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797068 + 0x60 + (uint)(actor * 4), pointer);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x1B, actor);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0xA5A5);
        return frame;
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
