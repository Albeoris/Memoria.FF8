using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlRibbonProducerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SecondaryBankUsesTheActualPairControlAndNarrowProducerOperands(int pair)
    {
        var (memory, effect) = Fixture();
        NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, checked(0x12AF800u + (uint)pair * 8), 0x12AF828, 44, 35, -17);
        byte[] task = spawned.Memory.Arena!.Read(0x3BA5C, 128);
        Assert.Equal(checked(0x12AF800u + (uint)pair * 8), BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(0x14)));
        Assert.Equal(0x12AF828u, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(0x18)));
        Assert.Equal((short)-17, BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(0x10)));
        Assert.Equal((byte)44, task[0x12]);
        Assert.Equal((byte)35, task[0x13]);
        Assert.Equal(0x20DFB7Cu, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(0x1C)));
    }

    [Fact]
    public void ProducerWritesEveryFinalByteWithoutReadingUnknownOldPayload()
    {
        var (memory, effect) = Fixture();
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Flags = 0xA500 }) };
        effect = effect with { Pools = [pool] };
        memory = memory with { Arena = memory.Arena!.Write(0x3BA5C, [0, 0xA5]).MarkUnknown(0x3BA5C + 2, 126) };
        NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, 1);
        byte[] task = spawned.Memory.Arena!.Read(0x3BA5C, 128);
        Assert.Equal((ushort)0xA501, BinaryPrimitives.ReadUInt16LittleEndian(task));
        Assert.Equal(0x6C8850u, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(8)));
        Assert.Equal(0x20DFB7Cu, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(0x1C)));
        Assert.Equal(task.AsSpan(12).ToArray(), spawned.Effect.Pools[0].Slots[0].Payload.ToArray());
    }

    [Fact]
    public void ProducerUsesNativeFirstFreeSlotPointersAndAllSixteenWidthColorRows()
    {
        var (memory, effect) = Fixture();
        NativeBattleQuezacotlRibbonSpawn first = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, 1);
        Assert.Equal(0, first.Slot);
        byte[] raw = first.Memory.Arena!.Read(0x3BA5C, 128);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(raw));
        Assert.Equal(0x6C8850u, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
        Assert.Equal(0x12AE7F8u, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x14)));
        Assert.Equal(0x12AE840u, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x18)));
        Assert.Equal(0x20DFB7Cu, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x1C)));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 1, 12, 7 }, raw.AsSpan(12, 8).ToArray());
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x20)));
        Assert.Equal((short)128, BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x22)));
        Assert.True(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x20 + 7 * 6)) > 250);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x20 + 15 * 6)));
        NativeBattleQuezacotlRibbonSpawn second = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(first.Memory, first.Effect, 2);
        Assert.Equal(1, second.Slot);
        Assert.Equal(0x211B594u, BinaryPrimitives.ReadUInt32LittleEndian(second.Memory.Arena!.Read(0x3BA60, 4)));
        Assert.Equal(0x211B514u, second.Memory.QuezacotlSecondaryHeads!.Value[0]);
        Assert.Equal(1, second.Effect.Pools[0].Slots[0].Next);
        Assert.Equal(second.Memory.Arena.Read(0x3BADC + 12, 116), second.Effect.Pools[0].Slots[1].Payload.ToArray());
    }

    [Fact]
    public void FullNativePoolReturnsNullAndPerformsNoProducerWrites()
    {
        var (memory, effect) = Fixture();
        for (int frame = 0; frame < 16; frame++)
        {
            NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, frame);
            memory = spawned.Memory;
            effect = spawned.Effect;
        }
        NativeBattleQuezacotlRibbonSpawn full = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, 16);
        Assert.Null(full.Slot);
        Assert.Same(memory, full.Memory);
        Assert.Same(effect, full.Effect);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(0x8C, [0x7C, 0xFB, 0x0D, 0x02]) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521728, 0x211B514, 0x80, 16);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool] };
        return (memory, effect);
    }
}
