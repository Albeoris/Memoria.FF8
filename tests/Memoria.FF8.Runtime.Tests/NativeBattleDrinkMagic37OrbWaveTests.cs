using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37OrbWaveTests
{
    [Fact]
    public void Wave_preserves_faces_uses_signed_truncation_and_marks_the_physically_written_stack_W_unknown()
    {
        var fixture = Fixture();
        NativeBattleDrinkMagic37OrbWaveStep step = NativeBattleDrinkMagic37OrbWave.Transform(fixture.Shape, fixture.Mesh, 1, 0, fixture.Registers);
        for (int vertex = 0; vertex < 276; vertex++)
        {
            int offset = 8 + vertex * 8;
            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(step.Mesh.Read(offset, 2)));
            Assert.Equal((short)3072, BinaryPrimitives.ReadInt16LittleEndian(step.Mesh.Read(offset + 2, 2)));
            Assert.Equal((short)-4097, BinaryPrimitives.ReadInt16LittleEndian(step.Mesh.Read(offset + 4, 2)));
            Assert.All(step.Mesh.ReadSnapshot(offset + 6, 2).Known, known => Assert.False(known));
        }
        Assert.Equal(fixture.Mesh.Read(2216, 8992 - 2216), step.Mesh.Read(2216, 8992 - 2216));
        Assert.Equal(fixture.Mesh.Read(0, 8), step.Mesh.Read(0, 8));
        Assert.Equal(new int?[] { 11, 22, 33 }, step.Registers.Geometry.Translation);
        Assert.Null(step.Registers.Geometry.Rotation20[18]);
        Assert.Null(step.Registers.Geometry.Rotation20[19]);
        Assert.Equal(new int?[] { 0, 0, -4097 }, step.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 0, 0, -4097 }, step.Registers.Colors.Ir);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(step.Registers.Colors.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Equal(new short[] { 0, 3072, -4097, 0, 3072, -4097 }, NativeBattleDrinkMagic37OrbWave.Bounds(step.Mesh));
    }

    [Fact]
    public void A_proved_stack_W_is_copied_into_every_vertex_and_unknown_mesh_roundtrips_without_hydration()
    {
        var fixture = Fixture();
        NativeBattleDrinkMagic37OrbWaveStep unknown = NativeBattleDrinkMagic37OrbWave.Transform(fixture.Shape, fixture.Mesh, 1, 0, fixture.Registers);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(unknown.Mesh))!;
        NativeBattleDrinkMagic37OrbWaveStep step = NativeBattleDrinkMagic37OrbWave.Transform(fixture.Shape, restored, 1, 0, unknown.Registers, unchecked((short)0xABCD));
        for (int vertex = 0; vertex < 276; vertex++)
        {
            Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(step.Mesh.Read(14 + 8 * vertex, 2)));
        }
        Assert.Equal(JsonSerializer.Serialize(NativeBattleDrinkMagic37OrbWave.Transform(fixture.Shape, unknown.Mesh, 1, 0, unknown.Registers)), JsonSerializer.Serialize(NativeBattleDrinkMagic37OrbWave.Transform(fixture.Shape, restored, 1, 0, unknown.Registers)));
    }

    private static (ImmutableArray<byte> Shape, NativeBattleEffectArena Mesh, NativeBattleGeometryRegisterStep Registers) Fixture()
    {
        byte[] shape = new byte[2216];
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(4), 276);
        for (int vertex = 0; vertex < 276; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(12 + vertex * 8), -4097);
            BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(14 + vertex * 8), 0xBEEF);
        }
        byte[] mesh = Enumerable.Repeat((byte)0xA5, 8992).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 276);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown() with { Translation = [11, 22, 33] }, NativeBattleScriptedColorRegisters.Unknown());
        return (shape.ToImmutableArray(), NativeBattleEffectArena.Clear().Write(0, mesh), registers);
    }
}
