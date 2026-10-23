using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid170ModelDrawingTests
{
    [Fact]
    public void Skin_publishes_XYZ_W0_and_body_packets_use_neutral_source_semibit_and_offsets()
    {
        var actual = Draw(Geometry(true), Context(0), 2000);
        Assert.Equal(new short[] { -400, -400, 2000, 0 }, Words(actual.VertexScratch.Read(0, 8)));
        Assert.Equal(72, actual.Batch.AllocationBytes);
        Assert.Equal(new int[] { 32, 40 }, actual.Batch.Attempts.Select(item => item.PacketLength));
        Assert.Equal(0x26112233u, actual.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x2E112233u, actual.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0x00DCBA98u, actual.Batch.Attempts[0].Primitive!.Words[5]);
        Assert.Equal(new int[] { 2000, 2000, 2000, 0 }, actual.Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(500u, Dword(actual.Temporary172, 0x4C));
        Assert.Equal(0u, Dword(actual.Temporary172, 0x50));
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), 2000, 24));
    }

    [Fact]
    public void Plane_entry_writes_only_two_LIGHT_DWORDs_and_BACK_X_even_without_objects()
    {
        var memory = Memory();
        var lighting = NativeBattleLightBackTransform.LoadBone(memory, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 77, Y = 88, Z = 99 }, 123)).ScriptedLighting!;
        memory = memory with { ScriptedLighting = lighting };
        var context = Context(0).Write(0x20, new byte[4]).Write(0x74, BitConverter.GetBytes(0xFEDC1234u)).Write(0x78, BitConverter.GetBytes(0xABCD5678u));
        var actual = Draw(Geometry(false), context, 2000, memory: memory);
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(context.Read(0x74, 8), actual.Memory.ScriptedLighting!.LightMatrix!.Bytes.Take(8));
        Assert.Equal(lighting.LightMatrix!.Bytes.Skip(8), actual.Memory.ScriptedLighting.LightMatrix.Bytes.Skip(8));
        Assert.Equal((int)unchecked((short)0xABCD), actual.Memory.ScriptedLighting.BackColor!.ReadLane(0));
        Assert.Equal(88, actual.Memory.ScriptedLighting.BackColor.ReadLane(1));
        Assert.Equal(99, actual.Memory.ScriptedLighting.BackColor.ReadLane(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Positive_plane_deformation_clamps_Y_and_only_primary_displaces_XZ(bool primary)
    {
        var context = Context(0).Write(0x74, BitConverter.GetBytes((short)0)).Write(0x76, BitConverter.GetBytes((short)4096)).Write(0x78, BitConverter.GetBytes((short)0)).Write(0x7A, BitConverter.GetBytes((short)1000)).Write(0x30, new byte[4]).Write(0x34, BitConverter.GetBytes(-4096)).Write(0x3C, BitConverter.GetBytes(1000)).Write(0x40, BitConverter.GetBytes(6250000)).Write(0x7C, new byte[12]).Write(0x88, new byte[2]).Write(0x8A, BitConverter.GetBytes(primary ? (ushort)1 : (ushort)0));
        byte[] geometry = Geometry(false);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(16 + vertex * 6), 100);
        }
        var actual = Draw(geometry, context, 0);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.VertexScratch.Read(2, 2)));
        short x = BinaryPrimitives.ReadInt16LittleEndian(actual.VertexScratch.Read(0, 2));
        Assert.Equal(primary, x < -400);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(actual.VertexScratch.Read(6, 2)));
        Assert.Equal(13u, Dword(actual.Temporary172, 0x38));
    }

    [Fact]
    public void Nonpositive_plane_does_not_consume_radial_denominators_or_saved_points()
    {
        var context = Context(0).MarkUnknown(0x30, 20).MarkUnknown(0x7C, 16);
        var actual = Draw(Geometry(false), context, 2000);
        Assert.Equal(32, actual.Batch.AllocationBytes);
        Assert.Equal(-10000, unchecked((int)Dword(actual.Temporary172, 0x38)));
        Assert.Throws<NotSupportedException>(() => actual.Temporary172.Read(0x34, 4));
    }

    [Fact]
    public void First_near_rejects_prefix_and_fourth_near_has_no_second_admission()
    {
        var near = Draw(Geometry(false), Context(0), 0);
        Assert.Null(Assert.Single(near.Batch.Attempts).Primitive);
        Assert.NotEqual(0u, Dword(near.Temporary172, 0x50) & 0x60000);
        byte[] geometry = Geometry(true);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(36), -1999);
        var fourth = Draw(geometry, Context(0), 2000);
        Assert.Equal(2, fourth.Batch.Attempts.Length);
        Assert.All(fourth.Batch.Attempts, item => Assert.NotNull(item.Primitive));
        Assert.Equal(new int[] { 2000, 2000, 2000, 1 }, fourth.Batch.Attempts[1].Primitive!.VertexPayload4);
    }

    [Fact]
    public void JSON_preserves_current_partial_LIGHT_and_next_deformation()
    {
        var first = Draw(Geometry(true), Context(0), 2000);
        var restored = JsonSerializer.Deserialize<NativeBattleAcid170ModelStep>(JsonSerializer.Serialize(first))!;
        NativeBattleAcid170ModelStep Next(NativeBattleAcid170ModelStep current) => Draw(Geometry(true), current.Temporary172, 2000, memory: current.Memory, scratch: current.VertexScratch);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    private static uint Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 } };

    private static NativeBattleAcid170ModelStep Draw(byte[] geometry, NativeBattleEffectArena context, int boneZ, int length = 32, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null)
    {
        memory ??= Memory();
        return NativeBattleAcid170ModelDrawing.DrawBody(memory, context, scratch ?? NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xCC, 40).ToArray()), (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1000000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Projection, memory.ScriptedGraphics!, 0x196B000, length);
    }

    private static NativeBattleEffectArena Context(int blend)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 172).ToArray());
        result = result.Write(4, BitConverter.GetBytes(0x196B000u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x74, new byte[6]).Write(0x7A, BitConverter.GetBytes((short)-10000));
        return result;
    }

    private static byte[] Geometry(bool quad)
    {
        byte[] data = new byte[quad ? 88 : 68];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 4);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 6), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(42), quad ? (ushort)1 : (ushort)0);
        Face(52, false);
        if (quad)
        {
            Face(68, true);
        }
        return data;

        void Face(int offset, bool four)
        {
            for (int corner = 0; corner < (four ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + corner * 2), (ushort)(0xF000 | corner));
            }
            if (!four)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 6), 0x8123);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 12), 0xFEDCBA98);
            if (four)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 16), 0xF3456789);
            }
        }
    }

    private static short[] Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
}
