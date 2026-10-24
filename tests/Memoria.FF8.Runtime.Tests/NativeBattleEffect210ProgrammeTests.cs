using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210ProgrammeTests
{
    [Theory]
    [InlineData(0u, 102, 203, 304)]
    [InlineData(0x1000u, 97, 202, 304)]
    public void Header1000_selects_normalized_rotation_but_keeps_actual_caller_translation(uint header, int x, int y, int z)
    {
        NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(0);
        NativeBattleActionMatrix normalized = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 9000, 8000, 7000, 4096);
        frame = frame with { State = frame.State.WriteView(ViewAddress, PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null)), 32) };
        NativeBattleEffect210ControllerTests.Harness harness = new();
        NativeBattleEffect210Inputs inputs = harness.Inputs();
        (NativeBattleEffect210Resources resources, NativeBattleGfAnimationSource source) = Resources();
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(new(0, 0, 0)) with { X = 100, Y = 200, Z = 300 };
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(SelectorsAddress)).Write(36, BitConverter.GetBytes((short)-128));
        NativeBattleGfAnimationNode node = Node(header);
        uint temporary = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffect210Frame result = NativeBattleEffect210Programme.DrawNode(frame, source, node, caller, resources, inputs);
        Assert.Equal(new int?[] { x, y, z }, result.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(Context(result, temporary, 108).Read(24, 4)));
        Assert.Equal(0x2030u, BinaryPrimitives.ReadUInt32LittleEndian(Context(result, temporary, 108).Read(28, 4)));
        Assert.DoesNotContain(true, Context(result, temporary, 108).ReadSnapshot(8, 4).Known);
        Assert.Equal(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData((byte)'2', 0, 127, 0, 128, 256, 128, -128)]
    [InlineData((byte)'3', 85, 0, 0, 0, 128, 256, -128)]
    [InlineData((byte)'9', 0, 0, 0, 0, 256, 256, 128)]
    public void Selected_ASCII_dispatch_preserves_actual_phase_bytes_and_depth(byte selector, int u, int v, int beforeX, int beforeY, int width, int height, int depth)
    {
        NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(0);
        (NativeBattleEffect210Resources resources, NativeBattleGfAnimationSource source) = Resources(selector);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null)).Write(32, BitConverter.GetBytes(SelectorsAddress)).Write(36, BitConverter.GetBytes((short)-128)).Write(38, BitConverter.GetBytes((ushort)0x12FF)).Write(40, BitConverter.GetBytes((ushort)0x3455));
        uint temporary = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffect210Frame result = NativeBattleEffect210Programme.DrawNode(frame, source, Node(0), caller, resources, new NativeBattleEffect210ControllerTests.Harness().Inputs());
        NativeBattleEffectArena context = Context(result, temporary, 108);
        Assert.Equal(u, Word(context, 32));
        Assert.Equal(v, Word(context, 34));
        if (selector != '9')
        {
            Assert.Equal(beforeX, Word(context, 44));
            Assert.Equal(beforeY, Word(context, 46));
            Assert.Equal(width, Word(context, 48));
            Assert.Equal(height, Word(context, 50));
        }
        Assert.Equal(depth, BinaryPrimitives.ReadInt32LittleEndian(context.Read(24, 4)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Morph_publishes_only_XYZ_and_retains_current_common_W_knownness(bool knownW)
    {
        NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(0);
        (NativeBattleEffect210Resources resources, NativeBattleGfAnimationSource source) = Resources(morph: true);
        uint destination = frame.State.ReadDword(ArenaGlobal) + 0xAB10;
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF));
        if (!knownW)
        {
            row = row.MarkUnknown(6, 2);
        }
        frame = WriteBytes(frame, destination, row, 8);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null)).Write(32, BitConverter.GetBytes(SelectorsAddress)).Write(36, BitConverter.GetBytes((short)0)).Write(44, BitConverter.GetBytes(destination));
        byte[] output = Node(0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(42), 1);
        NativeBattleEffect210Frame result = NativeBattleEffect210Programme.DrawNode(frame, source, new(0, 0, output.ToImmutableArray()), caller, resources, new NativeBattleEffect210ControllerTests.Harness().Inputs());
        NativeBattleEffectArena actual = ReadBytes(result, destination, 8);
        Assert.Equal(new NativeBattleVector(200, 300, 400), Vector(actual));
        Assert.Equal(knownW, actual.ReadSnapshot(6, 2).Known[0]);
        if (knownW)
        {
            Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Read(6, 2)));
        }
    }

    [Fact]
    public void Initializer_overwrites_unknown_prefix_and_retains_the_physical_suffix()
    {
        NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(0);
        (NativeBattleEffect210Resources resources, NativeBattleGfAnimationSource source) = Resources();
        uint workspace = 0x20DFAB8u + 0xA750 + 0x168;
        frame = WriteBytes(frame, workspace, NativeBattleEffectArena.Clear().MarkUnknown(0, 0xE0), 0xE0);
        NativeBattleEffectArena suffix = NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).MarkUnknown(6, 2);
        frame = WriteBytes(frame, workspace + 0xE0, suffix, 8);
        NativeBattleEffect210Frame result = NativeBattleEffect210Programme.Initialize(frame, workspace, source.Address, 0xE0, resources);
        Assert.Equal(source.Address, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(result, workspace, 4).Read(0, 4)));
        Assert.All(ReadBytes(result, workspace, 0xE0).ReadSnapshot(0, 0xE0).Known, value => Assert.True(value));
        Assert.Equal(suffix.ReadSnapshot(0, 8).Data, ReadBytes(result, workspace + 0xE0, 8).ReadSnapshot(0, 8).Data);
        Assert.Equal(suffix.ReadSnapshot(0, 8).Known, ReadBytes(result, workspace + 0xE0, 8).ReadSnapshot(0, 8).Known);
    }

    [Fact]
    public void Invisible_node_has_no_allocation_or_geometry_side_effect()
    {
        NativeBattleEffect210Frame frame = NativeBattleEffect210ControllerTests.Frame(0);
        NativeBattleGfAnimationNode node = new(0, 0, new byte[44].ToImmutableArray());
        NativeBattleEffect210Frame result = NativeBattleEffect210Programme.DrawNode(frame, null!, node, null!, null!, null!);
        Assert.Same(frame, result);
    }

    private static NativeBattleGfAnimationNode Node(uint header)
    {
        byte[] output = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), header);
        foreach ((int offset, short value) in new (int, short)[] { (8, 2), (10, 3), (12, 4), (24, 4096), (26, 4096), (28, 4096) })
        {
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(offset), value);
        }
        return new(0, 0, output.ToImmutableArray());
    }

    private static (NativeBattleEffect210Resources Resources, NativeBattleGfAnimationSource Source) Resources(byte selector = (byte)'0', bool morph = false)
    {
        const uint address = 0x146D8A4;
        uint geometry = address + 16;
        byte[] mesh = new byte[morph ? 60 : 52];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, morph ? 28 : 20);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(8), morph ? 2 : 1);
        if (morph)
        {
            foreach ((int offset, short value) in new (int, short)[] { (12, 100), (14, 200), (16, 300), (18, 777), (20, 300), (22, 400), (24, 500), (26, 888) })
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset), value);
            }
        }
        byte[] bytes = new byte[16 + mesh.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        mesh.CopyTo(bytes, 16);
        NativeBattleGfAnimationSource source = new(address, bytes.ToImmutableArray());
        NativeBattleEffect210Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(address, source), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(geometry, new(0, geometry, mesh.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(SelectorsAddress, ImmutableArray.Create(selector)));
        return (resources, source);
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
