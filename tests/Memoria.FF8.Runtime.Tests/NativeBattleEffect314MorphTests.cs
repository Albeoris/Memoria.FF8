using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314MorphTests
{
    [Theory]
    [InlineData(15, true)]
    [InlineData(19, false)]
    public void Quad_consumed_marker_uses_the_shared_texture_page_high_byte(int markerOffset, bool consumed)
    {
        byte[] geometry = new byte[44];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(14), 1);
        geometry[24 + markerOffset] = 0x80;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x1000u)).Write(0x1C, BitConverter.GetBytes(1u));
        NativeBattleEffect314MorphBodyStep Draw() => NativeBattleEffect314Morph.DrawBody(memory, temporary, NativeBattleEffectArena.Clear(), (address, length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan(checked((int)(address - 0x2000)), length)), 0x2000, [], NativeBattleEffectArena.Clear(), null!, null!, 0x1000, 0);
        if (consumed)
        {
            NativeBattleEffect314MorphBodyStep result = Draw();
            Assert.Empty(result.Batch.Attempts);
            Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary156.Read(10, 2)));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Draw());
        }
    }
}
