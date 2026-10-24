using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Lower;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect287LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect287ProgrammeTests
{
    [Theory]
    [InlineData(0, 40100, 8192)]
    [InlineData(0x8000, 40100, 4096)]
    [InlineData(0x200, 20100, 4096)]
    public void Node_translation_uses_full_MAC_and_actual_header_rotation_policy(uint header, int expectedX, short expectedR)
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100 };
        NativeBattleEffect287Frame result = NativeBattleEffect287Programme.DrawNode(fixture.Frame, source, Node(header, 20000), Caller(matrix).MarkUnknown(36, 4), fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(unchecked((byte)expectedR), result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal(unchecked((byte)(expectedR >> 8)), result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Single(fixture.Operations);
    }

    [Fact]
    public void Intermediate_morph_preserves_each_W_byte_in_scratch_and_all_loaded_vectors()
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(true);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Common + 0x1A18;
        for (int index = 0; index < 3; index++)
        {
            fixture.Frame = WriteBytes(fixture.Frame, scratch + (uint)(index * 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, [(byte)(0xA0 + index)]), 8);
        }
        NativeBattleEffect287Frame result = NativeBattleEffect287Programme.DrawNode(fixture.Frame, source, Node(0x200, 0, 0, 1, 2048), Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(36, BitConverter.GetBytes(scratch)), fixture.Resources, fixture.Inputs());
        for (int index = 0; index < 3; index++)
        {
            (byte[] bytes, bool[] known) = ReadBytes(result, scratch + (uint)(index * 8), 8).ReadSnapshot(0, 8);
            Assert.Equal((byte)(0xA0 + index), bytes[6]);
            Assert.True(known[6]);
            Assert.False(known[7]);
        }
        Assert.True(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA0, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal((byte)0xA1, result.Memory.GeometryRegisters!.V1[6]);
        Assert.Null(result.Memory.GeometryRegisters.V1[7]);
        Assert.Equal((byte)0xA2, result.Memory.GeometryRegisters.V2[6]);
        Assert.Null(result.Memory.GeometryRegisters.V2[7]);
        Assert.Equal(0, Word287(ReadBytes(result, scratch, 8), 0));
        Assert.Equal(1000, Word287(ReadBytes(result, scratch, 8), 4));
        Assert.NotNull(Assert.Single(((NativeBattleEffect287DrawBatch)Assert.Single(fixture.Operations)).Batch.Attempts).Primitive);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(4096, 20)]
    public void Endpoint_selection_does_not_consume_unknown_morph_pointer(short factor, int selectedOffset)
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect287Frame result = NativeBattleEffect287Programme.DrawNode(fixture.Frame, source, Node(0x200, 0, 0, 1, factor), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(36, 4), fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 108);
        Assert.Equal(mesh.Address + (uint)selectedOffset, Dword287(temporary, 4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 128)]
    public void Phase_window_reads_selector_only_for_actual_selected_window(byte selector, short expectedY)
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(34, [selector, 32]);
        NativeBattleEffect287Frame result = NativeBattleEffect287Programme.DrawNode(fixture.Frame, source, Node(0x200, 0), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 108);
        Assert.Equal(32, Word287(temporary, 34));
        Assert.Equal(0, Word287(temporary, 32));
        Assert.Equal(256, Word287(temporary, 40));
        Assert.Equal(expectedY, Word287(temporary, 46));
        Assert.Equal(128, Word287(temporary, 50));
    }

    [Fact]
    public void Phase_zero_does_not_read_unknown_selector_or_overwrite_window_suffix()
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 36, Enumerable.Repeat((byte)0xBC, 16).ToArray()) };
        NativeBattleEffect287Frame result = NativeBattleEffect287Programme.DrawNode(fixture.Frame, source, Node(0x200, 0), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(34, 1), fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 108);
        Assert.All(temporary.Read(36, 16), value => Assert.Equal(0xBC, value));
    }

    private static NativeBattleEffectArena Caller(NativeBattleActionMatrix matrix) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes((short)-128)).MarkUnknown(34, 1).Write(35, [0]);
    private static NativeBattleGfAnimationNode Node(uint header, short x, short first = 0, short second = 0, short factor = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), factor);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), first);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) Source(bool polygon)
    {
        int count = polygon ? 3 : 1;
        int polygonOffset = 12 + count * 16;
        byte[] geometry = new byte[polygonOffset + 32 + (polygon ? 20 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, polygonOffset);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), count);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(8), 2);
        short[] xyz = [-100, -100, 1000, 100, -100, 1000, 0, 100, 1000];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(12 + (shape * count + vertex) * 8 + axis * 2), unchecked((short)(xyz[vertex * 3 + axis] + (axis == 0 ? shape * 200 : 0))));
                }
            }
        }
        if (polygon)
        {
            BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(polygonOffset + 16), 1);
            int face = polygonOffset + 20;
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(face), 0x30010203);
            BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(face + 8), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(face + 12), 0x00040506);
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(face + 16), 0x00070809);
        }
        byte[] bytes = new byte[16 + geometry.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        geometry.CopyTo(bytes, 16);
        NativeBattleGfAnimationSource source = new(0x13EF924, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, geometry.ToImmutableArray()));
    }
}
