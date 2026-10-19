using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedMeshMorphTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private static readonly NativeBattleScriptedMeshMorphServices Services = new((_, selector) => selector == -3 ? Base + 0x4000 : throw new InvalidDataException(), (_, id) => id switch { -7 => Base + 0x10000, 100 => Base + 0x20000, _ => throw new InvalidDataException() });

    [Fact]
    public void Missing_tuple_does_not_read_sources_or_invoke_resolvers()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x4C, 0);
        NativeBattleScriptedMeshMorphServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Same(frame, NativeBattleScriptedMeshMorph.Morph(frame, services));
    }

    [Fact]
    public void Position_morph_uses_start_count_actual_offsets_signed_factors_and_preserves_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(182, 2);
        frame = Word(frame, 0x4094, -256);
        frame = Word(frame, 0x4096, 128);
        frame = Word(frame, 0x4098, 512);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20018, 4) };
        for (int index = 0; index < 182; index++)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x10070 + index * 8 + 6, 2).MarkUnknown(0x20090 + index * 8 + 6, 2).MarkUnknown(0x50000 + index * 8 + 6, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, Services);
        Assert.Equal((short)-10, Read(result, 0x50000));
        Assert.Equal((short)6, Read(result, 0x50002));
        Assert.Equal((short)22, Read(result, 0x50004));
        Assert.Equal((short)171, Read(result, 0x50000 + 181 * 8));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x50006, 2));
        Assert.Equal(Base + 0x50000, result.ReadTemporaryDword(0x74));
        Assert.Equal(Base + 0x10070, result.ReadTemporaryDword(0xF0));
        Assert.Equal(Base + 0x20090, result.ReadTemporaryDword(0xF4));
        Assert.Equal(182u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(unchecked((uint)-256), result.ReadTemporaryDword(0xE0));
    }

    [Fact]
    public void Position_pipeline_preloads_Y_before_X_store_and_Z_before_Y_store()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 2).WriteTemporaryDword(0x7C, Base + 0x10072);
        for (int lane = 0; lane < 3; lane++)
        {
            frame = Word(frame, 0x4094 + 2 * lane, 128);
        }
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, Services);
        Assert.Equal(new short[] { 5, 6, 7 }, Enumerable.Range(0, 3).Select(lane => Read(result, 0x10072 + 2 * lane)));
    }

    [Fact]
    public void Normal_morph_truncates_negative_products_while_position_uses_arithmetic_shift()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 6);
        frame = Dword(frame, 0x1001C, 0x100);
        frame = Dword(frame, 0x2001C, 0x100);
        frame = Dword(frame, 0x10020, 1);
        for (int lane = 0; lane < 3; lane++)
        {
            frame = Word(frame, 0x4094 + 2 * lane, 129);
            frame = Word(frame, 0x10070 + 2 * lane, 0);
            frame = Word(frame, 0x20090 + 2 * lane, -1);
            frame = Word(frame, 0x10100 + 2 * lane, 0);
            frame = Word(frame, 0x20100 + 2 * lane, -1);
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x60006, 2).MarkUnknown(0x10106, 2).MarkUnknown(0x20106, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, Services);
        Assert.Equal((short)-1, Read(result, 0x50000));
        Assert.Equal((short)0, Read(result, 0x60000));
        Assert.Equal(Base + 0x60000, result.ReadTemporaryDword(0x78));
        Assert.Equal(Base + 0x10070, result.ReadTemporaryDword(0xF0));
        Assert.Equal(1u, result.ReadTemporaryDword(0xF8));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x60006, 2));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleScriptedMeshMorph.Morph(restored, Services)));
    }

    [Fact]
    public void Resolver_order_precedes_factor_reads_and_combined_flags_ignore_unselected_branches()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 2).WriteTemporaryDword(0xE0, 0);
        frame = Dword(frame, 0x20000, 4);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x10014, 0x10).MarkUnknown(0x20014, 0x10) };
        List<string> order = [];
        NativeBattleScriptedMeshMorphServices services = new((current, selector) =>
        {
            order.Add($"bone:{selector}");
            return Services.ResolveBone(current, selector);
        }, (current, id) =>
        {
            order.Add($"shape:{id}");
            Assert.Equal(0u, current.ReadTemporaryDword(0xE0));
            return Services.ResolveShape(current, id);
        });
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, services);
        Assert.Equal(new[] { "bone:-3", "shape:-7", "shape:100" }, order);
        Assert.Equal(256u, result.ReadTemporaryDword(0xE0));
        Assert.Equal(frame.ReadTemporaryDword(0x74), result.ReadTemporaryDword(0x74));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normal_only_nonpositive_count_updates_source_pointer_without_consuming_records_or_position_scratch(int count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 4).WriteTemporaryDword(0xF0, 0x11111111).WriteTemporaryDword(0xF4, 0x22222222).WriteTemporaryDword(0xF8, 0x33333333);
        frame = Dword(frame, 0x1001C, 0x100);
        frame = Dword(frame, 0x2001C, 0x100);
        frame = Dword(frame, 0x10020, unchecked((uint)count));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x10014, 8).MarkUnknown(0x20014, 8).MarkUnknown(0x10100, 8).MarkUnknown(0x20100, 8) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, Services);
        Assert.Equal(Base + 0x60000, result.ReadTemporaryDword(0x78));
        Assert.Equal(0x11111111u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(0x22222222u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(0x33333333u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(JsonSerializer.Serialize(frame.Arena), JsonSerializer.Serialize(result.Arena));
    }

    [Fact]
    public void Normal_headers_are_read_after_position_stores_alias_the_start_header()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 6).WriteTemporaryDword(0x7C, Base + 0x1001C);
        for (int lane = 0; lane < 3; lane++)
        {
            frame = Word(frame, 0x4094 + 2 * lane, 0);
        }
        frame = Word(frame, 0x10070, 0x100);
        frame = Word(frame, 0x10072, 0);
        frame = Word(frame, 0x10074, 1);
        frame = Dword(frame, 0x1001C, 0x900);
        frame = Dword(frame, 0x10020, 0);
        frame = Dword(frame, 0x2001C, 0x100);
        frame = Word(frame, 0x10100, 123);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x10900, 8) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedMeshMorph.Morph(frame, Services);
        Assert.Equal((short)123, Read(result, 0x60000));
        Assert.Equal(0x100u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x1001C, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x10020, 4)));
    }

    [Fact]
    public void Position_nonpositive_count_fails_at_its_unconditional_iteration_boundary()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedMeshMorph.Morph(Fixture(0, 2), Services));
    }

    private static NativeBattleWaterExecutionFrame Fixture(int count, uint flags)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x4C, Base + 0x3000).WriteTemporaryDword(0x7C, Base + 0x50000).WriteTemporaryDword(0x80, Base + 0x60000);
        frame = Word(frame, 0x3000, -3);
        frame = Word(frame, 0x3002, -7);
        frame = Word(frame, 0x3004, 100);
        frame = Dword(frame, 0x10000, flags);
        frame = Dword(frame, 0x20000, flags);
        frame = Dword(frame, 0x10014, 0x70);
        frame = Dword(frame, 0x20014, 0x90);
        frame = Dword(frame, 0x10018, unchecked((uint)count));
        for (int lane = 0; lane < 3; lane++)
        {
            frame = Word(frame, 0x4094 + 2 * lane, 256);
        }
        for (int index = 0; index < count; index++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                frame = Word(frame, 0x10070 + index * 8 + lane * 2, (short)(index + lane));
                frame = Word(frame, 0x20090 + index * 8 + lane * 2, (short)(index + lane + 10));
            }
        }
        return frame;
    }

    private static short Read(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static NativeBattleWaterExecutionFrame Word(NativeBattleWaterExecutionFrame frame, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
}
