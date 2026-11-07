namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223GridMemoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Scalar_access_preserves_little_endian_signed_values_and_packet_priority(int route)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleMeteor223GridMemory memory = new(frame, Services());
        int address = route switch
        {
            0 => checked((int)frame.TemporaryFrame.Address + 0x40),
            1 => checked((int)NativeBattleScriptedEffectMemory.Address + 0x100),
            _ => checked((int)NativeBattleEffectNodeMemory.ArenaAddress + 0x8000)
        };
        if (route == 3)
        {
            memory.BeginPackets(address, 2, 2);
        }
        memory.Dwords[address, 0] = unchecked((int)0x87654321);
        Assert.Equal(unchecked((int)0x87654321), memory.Dwords[address, 0]);
        Assert.Equal((ushort)0x8765, memory.Words[address, 2]);
        Assert.Equal(unchecked((short)0x8765), memory.SignedWords[address, 2]);
        Assert.Equal((byte)0x21, memory.Bytes[address, 0]);
        memory.Words[address, 1] = 0xFEDC;
        memory.Bytes[address, 3] = 0xAB;
        Assert.Equal(unchecked((int)0xABFEDC21), memory.Dwords[address, 0]);
        if (route == 3)
        {
            Assert.Equal(0u, frame.Arena.ReadUInt32(0x8000));
            Assert.Throws<NotSupportedException>(() => memory.Bytes[address, 4]);
        }
    }

    [Fact]
    public void Scalar_temporary_access_observes_private_writes_knownness_and_owner_lifetime()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame.TemporaryFrame.WriteUInt32(0x40, 0x11223344);
        frame.TemporaryFrame.MarkUnknown(0x44, 1);
        using NativeBattleWaterTemporaryFrame.PrivateWrite writes = frame.TemporaryFrame.BeginPrivateWrite();
        NativeBattleMeteor223GridMemory memory = new(frame with { TemporaryFrame = writes.View }, Services());
        int address = checked((int)frame.TemporaryFrame.Address + 0x40);
        Assert.Equal(0x11223344, memory.Dwords[address, 0]);
        memory.Bytes[address, 1] = 0xAA;
        Assert.Equal(0x1122AA44, memory.Dwords[address, 0]);
        Assert.Equal(0x11223344u, frame.TemporaryFrame.ReadUInt32(0x40));
        Assert.Throws<NotSupportedException>(() => memory.Bytes[address, 4]);
        writes.View.WriteByte(0x44, 0xCC);
        Assert.Equal((byte)0xCC, memory.Bytes[address, 4]);
        writes.Publish();
        Assert.Equal(0x1122AA44u, frame.TemporaryFrame.ReadUInt32(0x40));
        Assert.Throws<ObjectDisposedException>(() => memory.Bytes[address, 0]);
    }

    [Fact]
    public void Scalar_static_access_preserves_service_address_length_and_signed_values()
    {
        NativeBattleMeteor223GridMemory memory = new(WaterSchedulerFixture.Create(), Services((address, length) =>
        {
            Assert.Equal(0x1002u, address);
            Assert.Equal(2, length);
            return [0xFE, 0xFF];
        }));
        Assert.Equal((short)-2, memory.SignedWords[0x1000, 2]);
    }

    private static NativeBattleMeteor223GridServices Services(Func<uint, int, byte[]>? readStatic = null)
    {
        return new((_, _) => throw new InvalidOperationException(), (frame, _) => frame, (frame, _) => frame, _ => throw new InvalidOperationException(), (frame, _, _) => frame, readStatic ?? ((_, _) => throw new InvalidOperationException()));
    }
}
