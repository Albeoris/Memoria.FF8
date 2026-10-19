using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMeshExtentTests
{
    private const uint Shape = NativeBattleEffectNodeMemory.ArenaAddress + 0x1000;

    [Fact]
    public void Extent_comes_from_current_headers_and_actual_record_sizes_including_normals_and_terminator()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = Dword(frame, 0x20, 3);
        frame = Dword(frame, 0x1C, 0x100);
        Assert.Equal(0x118, Measure(frame));
        frame = Dword(frame, 0x20, 0);
        Assert.Equal(0x9C, Measure(frame));
        frame = Dword(frame, 0x18, 30);
        Assert.Equal(0x130, Measure(frame));
    }

    [Fact]
    public void Count_minus_one_terminates_without_interpreting_the_type_or_consulting_its_record_size()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x1080, [0x34, 0x12, 0xFF, 0xFF]) };
        Assert.Equal(0x84, BattleSession.MeasureNativeCinematicMeshExtent(frame, Shape, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void Type_minus_one_with_positive_count_is_not_a_terminator()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x1080, [0xFF, 0xFF, 1, 0]) };
        Assert.Throws<NotSupportedException>(() => Measure(frame));
    }

    [Fact]
    public void Header_and_record_ranges_are_bounded_by_the_current_arena_without_dereferencing_vertex_or_normal_payloads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1040, 16).MarkUnknown(0x1100, 24) };
        Assert.Equal(0x9C, Measure(frame));
        frame = Dword(frame, 0x18, 0x20000);
        Assert.Throws<NotSupportedException>(() => Measure(frame));
    }

    [Fact]
    public void Malformed_native_counts_fail_instead_of_inventing_an_empty_mesh()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Throws<NotSupportedException>(() => Measure(Dword(frame, 0x18, 0)));
        Assert.Throws<NotSupportedException>(() => Measure(Dword(frame, 0x20, uint.MaxValue)));
        frame = frame with { Arena = frame.Arena.Write(0x1082, [0, 0]) };
        Assert.Throws<NotSupportedException>(() => Measure(frame));
    }

    [Fact]
    public void Independent_snapshot_and_current_primitive_size_are_the_only_extent_inputs()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(Measure(frame), Measure(restored));
        frame = frame with { Arena = frame.Arena.Write(0x109C, [0, 0, 0xFF, 0xFF]) };
        Assert.Equal(0xA0, BattleSession.MeasureNativeCinematicMeshExtent(frame, Shape, type => type == 8 ? (byte)24 : throw new InvalidOperationException()));
    }

    private static int Measure(NativeBattleWaterExecutionFrame frame) => BattleSession.MeasureNativeCinematicMeshExtent(frame, Shape, type => type == 8 ? (byte)20 : throw new InvalidOperationException());

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = Dword(frame, 8, 0x80);
        frame = Dword(frame, 0x14, 0x40);
        frame = Dword(frame, 0x18, 2);
        frame = Dword(frame, 0x1C, 0x100);
        frame = Dword(frame, 0x20, 0);
        return frame with { Arena = frame.Arena.Write(0x1080, [8, 0, 1, 0]).Write(0x1098, [0, 0, 0xFF, 0xFF]) };
    }

    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, int relative, uint value) => frame with { Arena = frame.Arena.Write(0x1000 + relative, BitConverter.GetBytes(value)) };
}
