using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect283Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect283NodeTests
{
    [Theory]
    [InlineData(false, false, 100, 200, 2300, 0)]
    [InlineData(false, true, 100, 200, 2300, 4096)]
    [InlineData(true, false, 105, 210, 2315, 2048)]
    [InlineData(true, true, 105, 210, 2315, 4096)]
    public void Separate_translation_reads_actual_retained_E70_and_rotation_bypass(bool supplied, bool bypass, int x, int y, int z, int diagonal)
    {
        NativeBattleEffect283Frame frame = Frame();
        if (supplied)
        {
            NativeBattlePackedMatrix32 basis = NativeBattlePackedMatrix32.FromMatrix(new([2048, 0, 0, 0, 2048, 0, 0, 0, 2048], 0, 0, 0, 4096), null);
            frame = frame with { State = frame.State.Write(0x2507E70, basis.Bytes.AsSpan()) };
        }
        int calls = 0;
        Draw(frame, Node(0x1000u | (bypass ? 0x8000u : 0)), Caller(), Inputs((current, operation) =>
        {
            Assert.IsType<NativeBattleEffect283DrawWindow108>(operation);
            Assert.Equal(new int?[] { x, y, z }, current.Memory.GeometryRegisters!.Translation);
            short[] rotation = current.Memory.GeometryRegisters.Rotation20.Take(18).Select(value => value!.Value).Chunk(2).Select(bytes => BinaryPrimitives.ReadInt16LittleEndian(bytes)).ToArray();
            Assert.Equal(new short[] { (short)diagonal, 0, 0, 0, (short)diagonal, 0, 0, 0, (short)diagonal }, rotation);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Caller_depth_and_window_are_distinct_WORDs_and_only_geometry_one_initializes_window(int geometry)
    {
        NativeBattleEffectArena caller = Caller().Write(24, BitConverter.GetBytes((short)-123)).Write(26, BitConverter.GetBytes((ushort)96));
        Draw(Frame(), Node(0, geometry), caller, Inputs((current, operation) =>
        {
            NativeBattleEffect283DrawWindow108 draw = Assert.IsType<NativeBattleEffect283DrawWindow108>(operation);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 108);
            Assert.Equal(-123, BinaryPrimitives.ReadInt32LittleEndian(context.Read(24, 4)));
            Assert.Equal((ushort)0, Word(context, 0x20));
            Assert.Equal((ushort)(geometry == 1 ? 96 : 0), Word(context, 0x22));
            if (geometry == 1)
            {
                Assert.Equal(new ushort[] { 0, 0, 256, 256, 0, 0, 256, 128 }, Enumerable.Range(0, 8).Select(index => Word(context, 0x24 + index * 2)).ToArray());
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => context.Read(0x24, 16));
            }
            return current;
        }));
    }

    [Fact]
    public void All_zero_scale_skips_before_any_caller_read_but_one_zero_axis_draws()
    {
        NativeBattleEffect283Frame frame = Frame();
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        Array.Clear(bytes, 24, 6);
        Assert.Same(frame, Draw(frame, node with { Data = bytes.ToImmutableArray() }, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), Inputs((_, _) => throw new InvalidOperationException())));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        int calls = 0;
        Draw(frame, node with { Data = bytes.ToImmutableArray() }, Caller(), Inputs((current, _) =>
        {
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(4096, false)]
    [InlineData(2048, true)]
    public void Shape_endpoints_read_source_and_true_morph_writes_XYZ6_to_physical_overlapping_scratch(int ratio, bool morph)
    {
        NativeBattleEffect283Frame frame = Frame();
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 16).ToArray());
        frame = WriteBytes(frame, ArenaAddress + ScratchOffset, scratch, 16);
        NativeBattleEffect283Resources resources = Shapes();
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), (short)ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleEffect283Frame actual = DrawProgrammeNode67C8A0(frame, resources.MainProgramme, node with { Data = bytes.ToImmutableArray() }, Caller(), resources, Inputs((current, operation) =>
        {
            NativeBattleEffect283DrawWindow108 draw = Assert.IsType<NativeBattleEffect283DrawWindow108>(operation);
            uint expected = morph ? ArenaAddress + ScratchOffset : MainProgrammeAddress + 64 + 12 + (ratio == 4096 ? 16u : 0u);
            Assert.Equal(expected, draw.VertexAddress);
            return current;
        }));
        NativeBattleEffectArena after = ReadBytes(actual, ArenaAddress + ScratchOffset, 16);
        if (morph)
        {
            Assert.Equal(new short[] { 20, 30, 40 }, Enumerable.Range(0, 3).Select(axis => SignedWord(after, axis * 2)).ToArray());
            Assert.Equal(new short[] { 50, 60, 70 }, Enumerable.Range(0, 3).Select(axis => SignedWord(after, 8 + axis * 2)).ToArray());
            Assert.Equal((ushort)0xA5A5, Word(after, 6));
            Assert.Equal((ushort)0xA5A5, Word(after, 14));
            Assert.Equal(after.Read(8, 6), ReadBytes(actual, ArenaAddress + FirstPacketOffset, 6).Read(0, 6));
        }
        else
        {
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 16).ToArray(), after.Read(0, 16));
        }
        NativeBattleEffect283Frame restored = JsonSerializer.Deserialize<NativeBattleEffect283Frame>(JsonSerializer.Serialize(actual))!;
        NativeBattleEffect283Frame Next(NativeBattleEffect283Frame current) => DrawProgrammeNode67C8A0(current, resources.MainProgramme, node, Caller(), resources, Inputs((retained, _) => retained));
        Assert.Equal(JsonSerializer.Serialize(Next(actual)), JsonSerializer.Serialize(Next(restored)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static NativeBattleEffect283Frame Draw(NativeBattleEffect283Frame frame, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller, NativeBattleEffect283Inputs inputs)
    {
        NativeBattleEffect283Resources resources = NativeBattleEffect283StateTests.Resources();
        return DrawProgrammeNode67C8A0(frame, resources.MainProgramme, node, caller, resources, inputs);
    }

    private static NativeBattleEffect283Resources Shapes()
    {
        NativeBattleEffect283Resources resources = NativeBattleEffect283StateTests.Resources();
        byte[] bytes = resources.MainProgramme.Data.ToArray();
        Array.Clear(bytes, 64, 128);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 44);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(68), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(72), 2);
        short[][] points = [[10, 20, 30, 111], [40, 50, 60, 222], [30, 40, 50, 333], [60, 70, 80, 444]];
        for (int vertex = 0; vertex < points.Length; vertex++)
        {
            for (int axis = 0; axis < 4; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(76 + vertex * 8 + axis * 2), points[vertex][axis]);
            }
        }
        return resources with { MainProgramme = resources.MainProgramme with { Data = bytes.ToImmutableArray() } };
    }

    private static NativeBattleEffect283Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleOriginalEffectState effect = new(283, 282, new(283, Entry, Initializer), null, 0);
        return new(memory, effect, NativeBattleEffect283State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect283Inputs Inputs(Func<NativeBattleEffect283Frame, NativeBattleEffect283Operation, NativeBattleEffect283Frame> execute) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute, (_, _, _, _, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
    private static NativeBattleEffectArena Caller() => NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, BitConverter.GetBytes((short)100)).Write(2, BitConverter.GetBytes((short)200)).Write(4, BitConverter.GetBytes((short)300)).Write(6, BitConverter.GetBytes((short)91)).Write(20, new byte[4]).Write(24, new byte[4]).Write(28, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short SignedWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleGfAnimationNode Node(uint header, int geometry = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)geometry);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + axis * 2), (short)((axis + 1) * 10));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
