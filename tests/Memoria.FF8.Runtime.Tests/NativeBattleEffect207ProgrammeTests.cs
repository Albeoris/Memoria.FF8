using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect207ProgrammeTests
{
    [Theory]
    [InlineData(0, 0x2030u)]
    [InlineData(512, 0x20F0u)]
    public void Selected_translation_rotates_caller_point_with_full_MAC_then_adds_node_and_view_translation(short cue, uint flags)
    {
        NativeBattleEffect207Frame frame = NativeBattleEffect207ControllerTests.Frame(0);
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleEffect207Inputs inputs = new NativeBattleEffect207ControllerTests.Harness().Inputs() with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(view, null), ReadFlags = _ => throw new InvalidOperationException("No selected pause read exists.") };
        (NativeBattleEffect207Resources resources, NativeBattleGfAnimationSource source, uint geometry) = Resources();
        byte[] output = new byte[44];
        Put(output, 4, 0x200u);
        Word(output, 8, 5);
        Word(output, 10, 6);
        Word(output, 12, 7);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            Word(output, offset, 4096);
        }
        Word(output, 36, cue);
        NativeBattleGfAnimationNode node = new(0, 0, output.ToImmutableArray());
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)2)).Write(2, BitConverter.GetBytes((short)3)).Write(4, BitConverter.GetBytes((short)4)).Write(8, BitConverter.GetBytes(SelectorsAddress)).Write(12, BitConverter.GetBytes((short)-128));
        uint temporary = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffect207Frame drawn = NativeBattleEffect207Programme.DrawNode41E0(frame, source, node, caller, resources, inputs);
        NativeBattleGeometryRegisters registers = drawn.Memory.GeometryRegisters!;
        Assert.Equal(new int?[] { 102, 208, 311 }, registers.Translation.ToArray());
        Assert.Equal(-3, drawn.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal(2, drawn.Memory.ScriptedColorRegisters.Mac[1]);
        Assert.Equal(4, drawn.Memory.ScriptedColorRegisters.Mac[2]);
        Assert.Equal(4096, BinaryPrimitives.ReadInt16LittleEndian(registers.Rotation20.Take(2).Select(value => value!.Value).ToArray()));
        NativeBattleEffectArena context = Context(drawn, temporary, 108);
        Assert.Equal(geometry, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4)));
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(28, 4)));
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(context.Read(24, 4)));
        Assert.Equal(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4), drawn.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Morph_writes_XYZ6_and_retains_destination_W_knownness(bool knownW)
    {
        NativeBattleEffect207Frame frame = NativeBattleEffect207ControllerTests.Frame(0);
        (NativeBattleEffect207Resources resources, NativeBattleGfAnimationSource source, uint geometry) = Resources();
        uint destination = 0x20DFAB8 + 0xCE8;
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF));
        if (!knownW)
        {
            retained = retained.MarkUnknown(6, 2);
        }
        frame = WriteBytes(frame, destination, retained, 8);
        NativeBattleEffect207Frame result = NativeBattleEffect207Programme.Morph701390(frame, source, geometry, 0, 1, 2048, destination, new NativeBattleEffect207ControllerTests.Harness().Inputs());
        NativeBattleEffectArena row = ReadBytes(result, destination, 8);
        Assert.Equal(new NativeBattleVector(200, 300, 400), Vector(row));
        Assert.Equal(knownW, row.ReadSnapshot(6, 2).Known[0]);
        if (knownW)
        {
            Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(row.Read(6, 2)));
        }
    }

    [Fact]
    public void Unselected_translation_branch_fails_before_allocating_or_reading_invented_parent_state()
    {
        NativeBattleEffect207Frame frame = NativeBattleEffect207ControllerTests.Frame(0);
        byte[] output = new byte[44];
        Word(output, 24, 4096);
        NativeBattleGfAnimationNode node = new(0, 0, output.ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect207Programme.DrawNode41E0(frame, null!, node, null!, null!, new NativeBattleEffect207ControllerTests.Harness().Inputs()));
    }

    private static (NativeBattleEffect207Resources Resources, NativeBattleGfAnimationSource Source, uint Geometry) Resources()
    {
        uint geometry = ProgrammeAddress + 16;
        byte[] data = new byte[76];
        Put(data, 8, 16);
        Put(data, 16, 28);
        Put(data, 20, 1);
        Put(data, 24, 2);
        Word(data, 28, 100);
        Word(data, 30, 200);
        Word(data, 32, 300);
        Word(data, 34, 1234);
        Word(data, 36, 300);
        Word(data, 38, 400);
        Word(data, 40, 500);
        Word(data, 42, 5678);
        NativeBattleGfAnimationSource source = new(ProgrammeAddress, data.ToImmutableArray());
        NativeBattleStaticMeshResource mesh = new(0, geometry, data.AsSpan(16).ToArray().ToImmutableArray());
        NativeBattleEffect207Resources resources = new(null!, null!, source, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(geometry, mesh), null!, [], [(byte)'0'], 0, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(SelectorsAddress, new byte[] { (byte)'0' }.ToImmutableArray()));
        return (resources, source, geometry);
    }

    private static void Put(byte[] target, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);
    private static void Word(byte[] target, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(target.AsSpan(offset), unchecked((short)value));
}
