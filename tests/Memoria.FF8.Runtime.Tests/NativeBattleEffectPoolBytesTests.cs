using System.Buffers.Binary;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectPoolBytesTests
{
    [Fact]
    public void DwordEqualityUsesKnownMismatchWithoutResolvingPaddingButRejectsAmbiguousMatches()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23C5910, 0x23C5920, 36, 100);
        (pool, _) = pool.Append(0x5E9650);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(20, 123).MarkPayloadUnknown(22, 2));
        uint address = pool.BankAddress + 20;
        Assert.False(NativeBattleEffectPoolBytes.EqualDword(pool, address, 124));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectPoolBytes.EqualDword(pool, address, 123));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectPoolBytes.Read(pool, address, 4));
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(22, 456));
        Assert.True(NativeBattleEffectPoolBytes.EqualDword(pool, address, 123u | 456u << 16));
        pool = pool.WriteSlot(0, pool.Slots[0].MarkPayloadUnknown(20, 2));
        Assert.False(NativeBattleEffectPoolBytes.EqualDword(pool, address, 123u | 457u << 16));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectPoolBytes.EqualDword(pool, address, 123u | 456u << 16));
    }

    [Fact]
    public void PhysicalDescriptorHeaderAndPayloadReadTheSameCanonicalPool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23C5910, 0x23C5920, 36, 100);
        (pool, _) = pool.Append(0x5E9650);
        (pool, _) = pool.Append(0x5EAAF0);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 42));
        Assert.Equal(pool.BankAddress, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectPoolBytes.Read(pool, pool.DescriptorAddress, 4)));
        Assert.Equal(pool.BankAddress + 36, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectPoolBytes.Read(pool, pool.BankAddress + 4, 4)));
        Assert.Equal(new byte[] { 0x5E, 0, 42, 0 }, NativeBattleEffectPoolBytes.Read(pool, pool.BankAddress + 10, 4));
        pool.Validate();
    }

    [Fact]
    public void CrossingAHeaderBoundaryUpdatesCallbackAndPayloadWithoutResolvingOtherUnknownBytes()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23C5910, 0x23C5920, 36, 100);
        (pool, _) = pool.Append(0x5E9650);
        pool = pool.WriteSlot(0, pool.Slots[0].MarkPayloadUnknown(12, 4));
        NativeBattleEffectTaskPool changed = NativeBattleEffectPoolBytes.Write(pool, pool.BankAddress + 10, [0x11, 0x22, 0x33, 0x44]);
        Assert.Equal(0x22119650u, changed.Slots[0].Callback);
        Assert.Equal(0x4433, changed.Slots[0].ReadWord(12));
        Assert.Throws<NotSupportedException>(() => changed.Slots[0].ReadWord(14));
        Assert.Equal(0x22119650u, pool.Slots[0].Callback);
        changed.Validate();
    }
}
