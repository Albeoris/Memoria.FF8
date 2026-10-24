using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect285Lower;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect285LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285ProgrammeTests
{
    [Theory]
    [InlineData(0, 40300, 700, 8192)]
    [InlineData(0x200, 40200, 700, 4096)]
    [InlineData(0x1000, 40400, 300, 0)]
    [InlineData(0x8000, 40300, 700, 4096)]
    public void Header_translation_branches_use_full_MAC_and_selected_rotation_basis(uint header, int expectedX, int expectedZ, short expectedR00)
    {
        Fixture fixture = new();
        fixture.View = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100, Y = 200, Z = 300 }, null);
        NativeBattlePackedMatrix32 normalized = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [0, 0, 4096, 0, 4096, 0, -4096, 0, 0] }, null);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x2507D70, PackedArena(normalized), 32) };
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffectArena caller = Point(20000, 0, 100).MarkUnknown(6, 2).MarkUnknown(12, 4);
        NativeBattleEffect285Frame result = NativeBattleEffect285Programme.DrawNode(fixture.Frame, source, Node(header, new(100, 0, 300)), caller, fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(expectedZ, result.Memory.GeometryRegisters.Translation[2]);
        Assert.Equal(unchecked((byte)expectedR00), result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal(unchecked((byte)(expectedR00 >> 8)), result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.IsType<NativeBattleEffect285DrawBatch>(Assert.Single(fixture.Operations));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(32, 0, true)]
    [InlineData(32, 1, false)]
    public void Window_fields_are_written_only_for_nonzero_phase_and_geometry_zero(short phase, ushort geometry, bool window)
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 0x24, 16) };
        NativeBattleEffectArena caller = Point(0, 0, 0).MarkUnknown(6, 2).MarkUnknown(12, 4).Write(8, BitConverter.GetBytes((short)-256)).Write(10, BitConverter.GetBytes(phase));
        NativeBattleEffect285Frame result = NativeBattleEffect285Programme.DrawNode(fixture.Frame, source, Node(0x200, new(0, 0, 0), geometry: geometry), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 108);
        Assert.Equal(-256, unchecked((int)Dword285(context, 0x18)));
        Assert.Equal(0, Word285(context, 0x20));
        Assert.Equal(window ? phase : 0, Word285(context, 0x22));
        if (window)
        {
            Assert.Equal(256, Word285(context, 0x28));
            Assert.Equal(128, Word285(context, 0x2C));
            Assert.Equal(128, Word285(context, 0x32));
        }
        else
        {
            Assert.DoesNotContain(true, context.ReadSnapshot(0x24, 16).Known);
        }
    }

    [Fact]
    public void Interior_morph_preserves_actual_row_W_and_the_following_IO_owner()
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Arena + 0x1AB8;
        fixture.Frame = WriteBytes(fixture.Frame, scratch, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 8);
        fixture.Frame = WriteBytes(fixture.Frame, scratch + 896, NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4]), 4);
        NativeBattleEffectArena caller = Point(0, 0, 0).MarkUnknown(6, 2).Write(12, BitConverter.GetBytes(scratch));
        NativeBattleEffect285Frame result = NativeBattleEffect285Programme.DrawNode(fixture.Frame, source, Node(0x200, new(0, 0, 0), first: 0, second: 1, factor: 2048), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, scratch, 8);
        Assert.Equal(200, Word285(row, 0));
        Assert.DoesNotContain(true, row.ReadSnapshot(6, 2).Known);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, ReadBytes(result, scratch + 896, 4).Read(0, 4));
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Fully_faded_node_does_not_allocate_or_read_unknown_caller()
    {
        Fixture fixture = new();
        NativeBattleGfAnimationNode node = Node(0, new(0, 0, 0));
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        NativeBattleEffect285Frame result = NativeBattleEffect285Programme.DrawNode(fixture.Frame, null!, node with { Data = data.ToImmutableArray() }, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector point, ushort geometry = 0, short first = 0, short second = 0, short factor = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), geometry);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), point.Z);
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
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 3);
        for (int shape = 0; shape < 3; shape++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + shape * 8), (short)(100 + shape * 200));
        }
        NativeBattleGfAnimationSource source = new(0x1084BBC, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray()));
    }
}
