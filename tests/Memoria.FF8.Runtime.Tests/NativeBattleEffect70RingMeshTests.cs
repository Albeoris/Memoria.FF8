using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70RingMeshTests
{
    [Fact]
    public void Ring_flag10_rejects_negative_G4_area_before_fourth_vertex_and_retains_partial_packet()
    {
        const uint address = 0x1324ED8;
        const int directory = 32;
        byte[] bytes = new byte[directory + 32 + 24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        short[] vertices = [-400, -400, 0, 1, -400, 400, 0, 2, 400, -400, 0, 3];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), vertices[index]);
        }
        int face = directory + 5 * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face), 1);
        face += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x38112233);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), ushort.MaxValue);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(address)).Write(0x18, BitConverter.GetBytes(-256)).Write(0x1C, BitConverter.GetBytes(0x10u));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleShockwavePulsar260MeshStep step = NativeBattleShockwavePulsar260Mesh.Draw(memory, new(address, bytes.ToImmutableArray()), 0, context, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(address + 8, U32(step.Context88, 4));
        Assert.Equal(0, step.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.True(unchecked((int)U32(step.Context88, 0x24)) < 0);
        Assert.Throws<NotSupportedException>(() => step.Context88.Read(0x2C, 4));
    }

    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
}
