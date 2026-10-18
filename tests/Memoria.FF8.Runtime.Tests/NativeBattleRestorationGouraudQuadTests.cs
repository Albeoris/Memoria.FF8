using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationGouraudQuadTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Esuna_G4_wrapper_preserves_normalized_operands_fade_and_rejected_partial_writes(bool fade, bool reject)
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x38808080);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x102030);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x405060);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x708090);
        NativeBattleModuleProjectedVertex[] vertices = [new(XY(100, 100), 4096, reject ? 0x20000u : 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(fade ? 0x20F0u : 0x2030u, fade ? 0x123456u : null, fade ? 2048 : null, -256, 0, 0, full, full, 17, 2);
        NativeBattleEffectMeshPacketContext normalized = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, -256, 17, 2, new(0, 0, full, full));
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleGfGeometryBatch actual = NativeBattleRestorationMeshPackets.EmitG4_88F470(face, Read, context, colors, 96);
        NativeBattleGfGeometryBatch expected = NativeBattleEffectMeshPackets.EmitGouraud(face, Read, true, normalized, colors, 96);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(reject ? 0 : 36, actual.AllocationBytes);
        Assert.Equal(96, Assert.Single(actual.Attempts).ByteOffsetRelative);
        if (reject)
        {
            Assert.Null(actual.Attempts[0].Primitive);
            Assert.Contains(actual.Attempts[0].WriteMask, item => item == NativeBattleGfPacketByteState.Untouched);
        }
        else
        {
            Assert.Equal(209, actual.Attempts[0].Primitive!.Bucket);
        }

        NativeBattleModuleProjectedVertex Read(int index) => reject && index == 3 ? throw new InvalidOperationException("Fourth corner must follow native near rejection.") : vertices[index];
    }

    [Theory]
    [InlineData(-2048, 17)]
    [InlineData(256, 337)]
    [InlineData(int.MaxValue, 17)]
    public void G4_supplied_depth_adds_unchecked_before_signed_zero_clamp_and_shift(int bias, int expectedBucket)
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x38808080);
        NativeBattleModuleProjectedVertex[] vertices = [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(0x2030, null, null, bias, 0, 0, full, full, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitG4_88F470(face, index => vertices[index], context);
        Assert.Equal(expectedBucket, Assert.Single(batch.Attempts).Primitive!.Bucket);
        NativeBattleFullLifeMeshContext withoutBias = new(0x30, null, null, null, null, null, 17, 2);
        Assert.Equal(273, NativeBattleRestorationMeshPackets.EmitG4_885050(face, index => vertices[index], withoutBias).Attempts[0].Primitive!.Bucket);
    }

    private static uint XY(short x, short y) => (ushort)x | (uint)(ushort)y << 16;
}
