using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112GeometryTests
{
    [Fact]
    public void DeformationUsesNestedSignedSinesAndPreservesEveryDestinationPaddingWord()
    {
        byte[] mesh = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 3);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + 8 * index), 7);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + 8 * index), (short)(100 - 100 * index));
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12 + 8 * index), 50);
        }
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x548, Enumerable.Repeat((byte)0xCC, 24).ToArray());
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!;
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleEffectArena result = NativeBattleDuel112Geometry.Deform6CD7F0(mesh, arena, 0x548, matrix, 100, 100, 200);
        for (int index = 0; index < 3; index++)
        {
            byte[] vertex = result.Read(0x548 + 8 * index, 8);
            Assert.Equal(7, BinaryPrimitives.ReadInt16LittleEndian(vertex));
            Assert.Equal(100 - 100 * index, BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(2)));
            Assert.Equal(50 - 200 * index, BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(4)));
            Assert.Equal(new byte[] { 0xCC, 0xCC }, vertex.AsSpan(6).ToArray());
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleDuel112Geometry.Deform6CD7F0(mesh, restored, 0x548, matrix, 100, 100, 200)));
        Assert.Throws<DivideByZeroException>(() => NativeBattleDuel112Geometry.Deform6CD7F0(mesh, arena, 0x548, matrix, 100, 0, 200));
    }

    [Fact]
    public void MidpointReadsBothActualBonesInOrderAndRoundsNegativeOddSumsDown()
    {
        List<byte> bones = [];
        NativeBattleVector result = NativeBattleDuel112Geometry.Midpoint6CD8B0(0x1D972C0, (actor, bone, fraction) =>
        {
            Assert.Equal(0x1D972C0u, actor);
            Assert.Equal((short)0, fraction);
            bones.Add(bone);
            return bone == 21 ? new(-1, 32767, -32768) : new(0, 32766, -32767);
        });
        Assert.Equal(new byte[] { 21, 22 }, bones);
        Assert.Equal(new NativeBattleVector(-1, 32766, -32768), result);
    }
}
