using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlParticleProducerTests
{
    private const int Row = NativeBattleQuezacotlParticleProducer.Rows;

    [Fact]
    public void ProducerDoesNotReadUnknownOldTaskBytesThatAllNativeStoresOverwrite()
    {
        var (memory, effect) = Fixture();
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Flags = 0xA500 }) };
        effect = effect with { Pools = [pool] };
        memory = memory with { Arena = Word(memory.Arena!, 0x3C27C, unchecked((short)0xA500)).MarkUnknown(0x3C27C + 2, 58) };
        NativeBattleQuezacotlParticleSpawn spawned = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, new(new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12), -1, 0, 256, 250, 0, 3));
        Assert.Equal(0, spawned.Slot);
        byte[] task = spawned.Memory.Arena!.Read(0x3C27C, 60);
        Assert.Equal((ushort)0xA501, BinaryPrimitives.ReadUInt16LittleEndian(task));
        Assert.Equal(new byte[] { 10, 0, 11, 0, 12, 0, 0xFF, 0xFF }, task.AsSpan(0x30, 8).ToArray());
        Assert.Equal((ushort)1, spawned.Effect.Quezacotl!.Random17A0);
        Assert.Equal(task.AsSpan(12).ToArray(), spawned.Effect.Pools[0].Slots[0].Payload.ToArray());
    }

    [Fact]
    public void RowAllocationUsesCachedPointerBeforeFirstFreeAndPreservesUnwrittenVelocityAndScreen()
    {
        var (memory, _) = Fixture();
        int cached = Row + 3 * 40;
        memory = memory with { Arena = Dword(memory.Arena!, NativeBattleQuezacotlParticleProducer.CachedRow, NativeBattleQuezacotlParticleProducer.ArenaAddress + (uint)cached).Write(cached + 8, Enumerable.Repeat((byte)0xA5, 6).ToArray()).MarkUnknown(cached + 0x10, 4) };
        NativeBattleQuezacotlParticleRow allocated = NativeBattleQuezacotlParticleProducer.Allocate6C7E30(memory, new(1, -2, 3), 250);
        Assert.Equal(cached, allocated.Row);
        Assert.Equal(new byte[] { 1, 0, 0xFE, 0xFF, 3, 0, 250, 0 }, allocated.Memory.Arena!.Read(cached, 8));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 6), allocated.Memory.Arena.Read(cached + 8, 6));
        Assert.Throws<NotSupportedException>(() => allocated.Memory.Arena.Read(cached + 0x10, 4));
        Assert.Equal((short)-1, I16(allocated.Memory, Row + 6));
        Assert.Equal(0u, U32(allocated.Memory, NativeBattleQuezacotlParticleProducer.CachedRow));
        Assert.Equal((short)2, I16(allocated.Memory, cached + 0xE));
        Assert.Equal((short)512, I16(allocated.Memory, cached + 0x20));
        Assert.Equal((short)768, I16(allocated.Memory, cached + 0x22));
    }

    [Fact]
    public void TaskProducerUsesActualOperandsAndThreePrivateDrawsOnlyForNullVelocity()
    {
        var (memory, effect) = Fixture();
        NativeBattleQuezacotlParticleArguments args = new(new(9, 8, 7), new(1, 2, 3), new(4, 5, 6), null, 0, 0, 256, 250, 0, 3);
        NativeBattleQuezacotlParticleSpawn spawned = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, args);
        Assert.Equal(0, spawned.Slot);
        Assert.Equal(Row, spawned.Row);
        Assert.Equal((ushort)10951, spawned.Effect.Quezacotl!.Random17A0);
        byte[] task = spawned.Memory.Arena!.Read(0x3C27C, 60);
        Assert.Equal(0x6C7EA0u, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(8)));
        Assert.Equal(new byte[] { 11, 0, 109, 0, 71, 0 }, task.AsSpan(0x30, 6).ToArray());
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, task.AsSpan(0xC, 4).ToArray());
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(0x1E)));
        Assert.Equal((short)256, BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(0x26)));
        Assert.Equal(new byte[] { 9, 0, 8, 0, 7, 0 }, task.AsSpan(0x28, 6).ToArray());
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(0x2E)));
        Assert.Equal(new byte[6], spawned.Memory.Arena.Read(Row + 8, 6));
        Assert.Equal(0x211C4B4u, U32(spawned.Memory, 0x3C28C));
        Assert.Equal(0x211BD34u, spawned.Memory.QuezacotlSecondaryHeads!.Value[1]);
        memory = memory with { Arena = memory.Arena!.Write(Row + 8, Enumerable.Repeat((byte)0xA5, 6).ToArray()) };
        NativeBattleQuezacotlParticleSpawn explicitVelocity = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, args with { TrailVelocity = new(100, -200, 300), VelocityScale = -1 });
        Assert.Equal((ushort)1, explicitVelocity.Effect.Quezacotl!.Random17A0);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 6), explicitVelocity.Memory.Arena!.Read(Row + 8, 6));
        Assert.Equal((short)-1, I16(explicitVelocity.Memory, 0x3C27C + 0x36));
    }

    [Fact]
    public void FullTaskBankReleasesItsReservedRowWithoutDrawingRandomOrClearingOtherStores()
    {
        var (memory, effect) = Fixture();
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        for (int index = 0; index < 32; index++)
        {
            (pool, _) = pool.Append(0x6C7EA0);
            memory = memory with { Arena = Word(memory.Arena!, 0x3C27C + index * 60, 1) };
        }
        effect = effect with { Pools = [pool] };
        NativeBattleQuezacotlParticleSpawn failed = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory, effect, new(new(0, 0, 0), new(10, 20, 30), new(0, 0, 0), null, 0, 0, 256, 250, 0, 3));
        Assert.Null(failed.Slot);
        Assert.Null(failed.Row);
        Assert.Same(effect, failed.Effect);
        Assert.Equal((short)-1, I16(failed.Memory, Row + 6));
        Assert.Equal((short)10, I16(failed.Memory, Row));
        Assert.Equal((short)512, I16(failed.Memory, Row + 0x20));
        Assert.Equal((ushort)1, failed.Effect.Quezacotl!.Random17A0);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        for (int index = 0; index < 256; index++)
        {
            memory = memory with { Arena = Word(memory.Arena!, Row + index * 40 + 6, -1) };
        }
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521718, 0x211BD34, 0x3C, 32);
        return (memory, new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool], Quezacotl = new(1, 0, 3690) });
    }

    private static short I16(NativeBattleEffectMemory memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Arena!.Read(offset, 2));
    private static uint U32(NativeBattleEffectMemory memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Arena!.Read(offset, 4));

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, short value)
    {
        byte[] raw = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(raw, value);
        return arena.Write(offset, raw);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
        return arena.Write(offset, raw);
    }
}
