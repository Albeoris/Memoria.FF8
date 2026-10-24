using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect319Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect319LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319ProgrammeTests
{
    [Theory]
    [InlineData(0, 40100, 8192)]
    [InlineData(0x8000, 40100, 4096)]
    [InlineData(0x200, 20100, 4096)]
    public void Header_branches_preserve_full_MAC_translation_and_selected_rotation_composition(uint header, int expectedX, short expectedR)
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100 };
        NativeBattleEffectArena caller = Caller(matrix).MarkUnknown(0x24, 4);
        NativeBattleEffect319Frame result = NativeBattleEffect319Programme.DrawNode(fixture.Frame, source, Node(header, 20000), caller, fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(unchecked((byte)expectedR), result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal(unchecked((byte)(expectedR >> 8)), result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.IsType<NativeBattleEffect319DrawBatch>(Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Intermediate_morph_writes_XYZ_only_into_actual_scratch_and_publishes_GTE_cue()
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Arena + 0xAEC;
        fixture.Frame = WriteBytes(fixture.Frame, scratch, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8);
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(0x24, BitConverter.GetBytes(scratch));
        NativeBattleGfAnimationNode node = Node(0x200, 0, 0, 1, 2048);
        NativeBattleEffect319Frame result = NativeBattleEffect319Programme.DrawNode(fixture.Frame, source, node, caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, scratch, 8);
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(row.Read(0, 2)));
        Assert.DoesNotContain(true, row.ReadSnapshot(6, 2).Known);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(scratch, NativeBattleEffect319Lower.Dword319(temporary, 4));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(4096, 20)]
    public void Endpoint_morph_selects_source_rows_without_consuming_unknown_scratch_pointer(short factor, int vertexOffset)
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect319Frame result = NativeBattleEffect319Programme.DrawNode(fixture.Frame, source, Node(0x200, 0, 0, 1, factor), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(0x24, 4), fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(mesh.Address + (uint)vertexOffset, NativeBattleEffect319Lower.Dword319(temporary, 4));
    }

    [Fact]
    public void Fully_faded_zero_color_node_does_not_allocate_or_read_unknown_caller()
    {
        Fixture fixture = new(0x65F2B0);
        NativeBattleGfAnimationNode node = Node(0, 0);
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        NativeBattleEffect319Frame result = NativeBattleEffect319Programme.DrawNode(fixture.Frame, null!, node with { Data = data.ToImmutableArray() }, NativeBattleEffectArena.Clear().MarkUnknown(0, 40), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }

    private static NativeBattleEffectArena Caller(NativeBattleActionMatrix matrix) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(-128));

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

    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) Source()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 3);
        for (int shape = 0; shape < 3; shape++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + shape * 8), (short)(100 + shape * 200));
        }
        NativeBattleGfAnimationSource source = new(0xFF24F8, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray()));
    }
}
