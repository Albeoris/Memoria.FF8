using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLevelBoundsTests
{
    [Fact]
    public void Bounds_visit_all_body_objects_and_bones_with_signed_WORD_wrap_and_actual_adjacent_input()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleRawMesh first = Mesh(0, new(5, -7, 10), 0x1234);
        NativeBattleRawMesh second = Mesh(1, new(32767, 8, -20), 0x2345);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 32).ToArray());
        NativeBattleLevelBoundsStep actual = NativeBattleLevelBounds.Measure(memory, temporary, [identity, identity with { X = 1 }], [first, second]);
        Assert.Equal(new NativeBattleVector(-32768, -7, -20), actual.Minimum);
        Assert.Equal(new NativeBattleVector(5, 8, 10), actual.Maximum);
        Assert.Equal((short)-32768, BinaryPrimitives.ReadInt16LittleEndian(actual.Temporary32.Read(12, 2)));
        Assert.Equal((short)0x2345, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6, 2)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 4), actual.Temporary32.Read(0, 4));
    }

    private static NativeBattleRawMesh Mesh(ushort bone, NativeBattleVector vertex, short adjacent)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vertex.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vertex.Z);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), adjacent);
        return new([new(vertex, bone)], []) { PackedVertexGroups = [new(bone, 1, bytes.ToImmutableArray())] };
    }
}
