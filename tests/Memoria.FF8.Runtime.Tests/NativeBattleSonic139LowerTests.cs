using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSonic139Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSonic139LowerTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Geometry = 0x2517B5C;
    private static readonly NativeBattleSonic139Resources Resources = new(null!, null!, null!, null!, null!, [], [], Enumerable.Repeat((short)0, 4096).ToImmutableArray(), [], 0);

    [Fact]
    public void XY_morph_wraps_differences_and_publishes_cumulative_third_IR_and_FIFO()
    {
        NativeBattleSonic139Frame frame = Frame();
        NativeBattleEffectArena a = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(2)).Write(8, BitConverter.GetBytes((ushort)65530)).Write(16, new byte[4]).Write(12, BitConverter.GetBytes(0x12345678u)).Write(20, BitConverter.GetBytes(0x87654321u));
        NativeBattleEffectArena b = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(2)).Write(8, BitConverter.GetBytes((ushort)10)).Write(10, BitConverter.GetBytes((ushort)65530)).Write(16, BitConverter.GetBytes((ushort)7));
        frame = WriteBytes(WriteBytes(frame, Base + 0x3000, a, 24), Base + 0x3100, b, 24);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Ir = [99, 88, 8], Rgb = [0x11000000, 0x22000000, 0x33000000], Material = 0xAB000000 } } };
        NativeBattleSonic139Frame actual = NativeBattleSonic139Morph.Morph6A9BB0(frame, Base + 0x3000, Base + 0x3100, Base + 0x3200, 2048, Inputs(), Resources);
        NativeBattleEffectArena output = ReadBytes(actual, Base + 0x3200, 16);
        Assert.Equal((ushort)2, Word(output, 0));
        Assert.Equal((ushort)65533, Word(output, 2));
        Assert.Equal(0x12345678u, Dword(output, 4));
        Assert.Equal((ushort)4, Word(output, 8));
        Assert.Equal(0x87654321u, Dword(output, 12));
        Assert.Equal(new int?[] { 4, 0, 2 }, actual.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new uint?[] { 0x33000000, 0xAB040008, 0xAB020004 }, actual.Memory.ScriptedColorRegisters.Rgb);
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(0xAB000000u, actual.Memory.ScriptedColorRegisters.Material);
        NativeBattleSonic139Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSonic139Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleSonic139Morph.Morph6A9BB0(actual, Base + 0x3000, Base + 0x3100, Base + 0x3200, 2048, Inputs(), Resources)), NativeBattleTestSnapshots.Json(NativeBattleSonic139Morph.Morph6A9BB0(restored, Base + 0x3000, Base + 0x3100, Base + 0x3200, 2048, Inputs(), Resources)));
    }

    [Fact]
    public void XY_morph_preserves_unknown_metadata_and_requires_inherited_IR3()
    {
        NativeBattleSonic139Frame frame = Frame();
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(1)).MarkUnknown(12, 4);
        frame = WriteBytes(WriteBytes(frame, Base + 0x3000, source, 16), Base + 0x3100, source, 16);
        NativeBattleSonic139Frame actual = NativeBattleSonic139Morph.Morph6A9BB0(frame, Base + 0x3000, Base + 0x3100, Base + 0x3200, unchecked((int)0xFFFFF800), Inputs(), Resources);
        Assert.Throws<NotSupportedException>(() => ReadBytes(actual, Base + 0x3204, 4).Read(0, 4));
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Ir = [0, 0, null] } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleSonic139Morph.Morph6A9BB0(frame, Base + 0x3000, Base + 0x3100, Base + 0x3200, 0, Inputs(), Resources));
    }

    [Fact]
    public void Screen_records_preserve_native_old_flag_OR_and_asymmetric_UV_clamp()
    {
        NativeBattleSonic139Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(Geometry + 4, 1) };
        frame = WriteBytes(frame, Base + 0xF3C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)).Write(2, BitConverter.GetBytes((short)300)), 8);
        frame = WriteBytes(frame, Base + 0x1584, NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes((ushort)0xAB10)).MarkUnknown(10, 2), 12);
        NativeBattleSonic139Frame actual = NativeBattleSonic139Warp.BuildScreenRecords6A9260(frame, Geometry, Base + 0xF3C, 0, 0, Resources);
        NativeBattleEffectArena record = ReadBytes(actual, Base + 0x1584, 12);
        Assert.Equal((ushort)0xAB19, Word(record, 8));
        Assert.Equal((ushort)0, Word(record, 4));
        Assert.Equal((ushort)300, Word(record, 6));
        Assert.Throws<NotSupportedException>(() => record.Read(10, 2));
        Assert.Equal((byte)7, actual.Memory.ScriptedColorRegisters!.NormalInput!.Value[4]);
        Assert.False(actual.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void Screen_transform_consumes_retained_nonzero_Z_but_does_not_consume_W()
    {
        NativeBattleSonic139Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(Geometry + 4, 1) };
        frame = WriteBytes(frame, Base + 0xF3C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)10)).Write(2, BitConverter.GetBytes((short)20)), 8);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [4096, 0, 4096, 0, 4096, 0, 0, 0, 4096] };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        NativeBattleSonic139Frame actual = NativeBattleSonic139Warp.BuildScreenRecords6A9260(frame, Geometry, Base + 0xF3C, 0, 0, Resources);
        Assert.Equal((ushort)17, Word(ReadBytes(actual, Base + 0x1584, 12), 0));
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { NormalInputKnown = [true, true, true, true, false, false, false, false] } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleSonic139Warp.BuildScreenRecords6A9260(frame, Geometry, Base + 0xF3C, 0, 0, Resources));
    }

    [Fact]
    public void Warp_preserves_physical_packet_reservations_masks_context_and_color_state()
    {
        NativeBattleSonic139Frame frame = Scene();
        List<NativeBattleSonic139AppendPacket> packets = [];
        NativeBattleSonic139Inputs inputs = Inputs((current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleSonic139AppendPacket>(operation));
            return current;
        });
        NativeBattleSonic139Frame actual = NativeBattleSonic139Warp.Draw6A9260(frame, Geometry, Base + 0xF3C, 17, 2048, 0, 0, inputs, Resources);
        Assert.Equal(new[] { 12, 28, 8, 32, 64, 64 }, packets.Select(packet => packet.Allocation));
        Assert.Equal(new[] { 12, 28, 8, 32, 24, 36 }, packets.Select(packet => packet.Written));
        Assert.Equal(new ushort[] { 0, 1, 0, 1, 0, 0 }, packets.Select(packet => packet.Flags));
        Assert.Equal(new[] { 17, 17, 17, 17, 17, 4385 }, packets.Select(packet => packet.Bucket));
        Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(packets[0].Data.AsSpan(8)));
        Assert.Equal((byte)0x32, packets[1].Data[7]);
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(packets[2].Data.AsSpan(4)));
        Assert.Equal((byte)0x26, packets[3].Data[7]);
        foreach (int index in new[] { 14, 15, 30, 31 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[3].WriteMask[index]);
        }
        Assert.All(packets[0].WriteMask.Take(3), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(packets[4].WriteMask.Skip(24), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(Base + 0x1EF0 + 208u, actual.State.ReadDword(0x2520BF4));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(0x00302010u, actual.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(Geometry + 0x48, Dword(Context(actual, NativeBattleTemporaryMemory.Address, 76), 20));
        Assert.Equal(0x1D8E058u + 17 * 4, Dword(Context(actual, NativeBattleTemporaryMemory.Address, 76), 24));
    }

    [Fact]
    public void FT3_rejects_only_a_common_clip_bit_and_does_not_advance_packet_cursor()
    {
        NativeBattleSonic139Frame frame = Scene();
        for (int index = 0; index < 3; index++)
        {
            frame = WriteBytes(frame, Base + 0x1584 + (uint)index * 12, NativeBattleEffectArena.Clear().Write(8, new byte[] { 2 }), 12);
        }
        (frame, uint temporary) = Allocate(frame, 76);
        NativeBattleEffectArena context = Context(frame, temporary, 76).Write(4, BitConverter.GetBytes(Base + 0x1584)).Write(12, BitConverter.GetBytes(576)).Write(16, BitConverter.GetBytes(256)).Write(20, BitConverter.GetBytes(Geometry + 0x30)).Write(24, BitConverter.GetBytes(0x1D8E09Cu));
        frame = PublishContext(frame, temporary, context, 76);
        NativeBattleSonic139Frame actual = NativeBattleSonic139Warp.DrawTextured6A9640(frame, temporary, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(frame.State.ReadDword(0x2520BF4), actual.State.ReadDword(0x2520BF4));
        Assert.Equal(Geometry + 0x48, Dword(Context(actual, temporary, 76), 20));
    }

    private static NativeBattleSonic139Frame Scene()
    {
        NativeBattleSonic139Frame frame = Frame();
        NativeBattleEffectArena geometry = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x20)).Write(4, BitConverter.GetBytes(3)).Write(0x30, BitConverter.GetBytes(1)).Write(0x34, BitConverter.GetBytes(0x00102030u)).Write(0x38, BitConverter.GetBytes((ushort)0)).Write(0x3A, BitConverter.GetBytes((ushort)3)).Write(0x3C, BitConverter.GetBytes((ushort)6)).Write(0x40, BitConverter.GetBytes(0x00201030u)).Write(0x44, BitConverter.GetBytes(0x00302010u));
        frame = WriteBytes(frame, Geometry, geometry, 0x48);
        for (int index = 0; index < 3; index++)
        {
            frame = WriteBytes(frame, Base + 0xF3C + (uint)index * 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)(10 + index * 10))).Write(2, BitConverter.GetBytes((short)(20 + index * 10))), 8);
        }
        return frame;
    }

    private static NativeBattleSonic139Frame Frame()
    {
        NativeBattleScriptedColorRegisters colors = new([1, 2, 3], [0, 0, 8], [11, 22, 33], 0) { Material = 0, Ir0 = 0, NormalInput = [0, 0, 0, 0, 7, 0, 0, 0], NormalInputKnown = [true, true, true, true, true, true, false, false] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = colors, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleSoftwarePrimitiveState software = memory.PrimitiveProcess.Software!;
        NativeSoftwareDrawingEnvironmentApplication applied = software.EnvironmentBuffers!.Apply45B660(0, software.Environment);
        memory = memory with { PrimitiveProcess = memory.PrimitiveProcess with { Software = software with { EnvironmentBuffers = applied.Buffers, Environment = applied.Environment } } };
        NativeBattleSonic139State state = NativeBattleSonic139State.FromColdProcess().WriteDword(0x2517B58, Base).WriteDword(0x2520BF4, Base + 0x1EF0);
        return new(memory, new(139, 138, new(139, 0x6A8830, 0x6A8840), null, 0), new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleSonic139Inputs Inputs(Func<NativeBattleSonic139Frame, NativeBattleSonic139Operation, NativeBattleSonic139Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute ?? ((current, _) => current));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
}
