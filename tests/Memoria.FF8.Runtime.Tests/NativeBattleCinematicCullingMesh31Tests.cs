using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCullingMesh31Tests
{
    [Theory]
    [InlineData(4096, 0)]
    [InlineData(0, 2)]
    [InlineData(2048, 1)]
    [InlineData(-4096, 3)]
    [InlineData(4095, 1)]
    public void Local_depth_uses_signed_strict_comparison_and_toward_zero_threshold(short factor, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(factor, [-4, 0, 2, 4]);
        List<uint> projected = [];
        NativeBattleCinematicCullingMesh31Services services = Services((current, source, _) =>
        {
            projected.Add(source);
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Assert.Equal(expected, projected.Count);
        Assert.Equal(4u, result.ReadTemporaryDword(0x44));
        Assert.Equal(Address(0x5020), result.ReadTemporaryDword(0x40));
        Assert.Equal(0x400u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void Negative_local_depth_keeps_zero_maximum_and_never_reads_hidden_vertex_coordinates()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, [-8, -1]);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3100, 4).MarkUnknown(0x3108, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCullingMesh31.Draw(frame, Services((_, _, _) => throw new InvalidOperationException()));
        Assert.Equal(0u, result.ReadTemporaryDword(0x44));
        Assert.Equal(new byte[] { 255, 255 }, result.Arena.Read(0x5010, 2));
    }

    [Fact]
    public void Normal_work_offsets_the_masks_without_reading_normal_storage_or_projected_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, [1]);
        byte[] count = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(count, 3);
        frame = frame with { Arena = frame.Arena.Write(0x3020, count).MarkUnknown(0x3200, 24) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCullingMesh31.Draw(frame, Services(Project));
        Assert.Equal(Address(0x5008), result.ReadTemporaryDword(0x80));
        Assert.Equal(Address(0x5020), result.ReadTemporaryDword(0x40));
        Assert.Equal(new byte[] { 0 }, result.Arena.Read(0x5020, 1));
        Assert.Equal(new byte[] { 2, 0 }, result.Arena.Read(0x5004, 2));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5006, 2));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x5008, 24));
    }

    [Fact]
    public void Lazy_initialization_reads_only_selector_word_and_retains_the_cached_shape()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(4096, [1]);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xFE112233);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3200, 2).Write(0x3202, new byte[] { 7, 0 }) };
        int resolves = 0;
        NativeBattleCinematicCullingMesh31Services services = Services((_, _, _) => throw new InvalidOperationException()) with
        {
            ResolveShape = (_, selector) =>
            {
                resolves++;
                Assert.Equal(7, selector);
                return Address(0x3000);
            }
        };
        frame = NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xBC));
        Assert.Equal(0x02112233u, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xCC));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3202, 2) };
        NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Assert.Equal(1, resolves);
    }

    [Fact]
    public void G4_links_before_draw_mode_and_shades_each_corner_without_backface_rejection()
    {
        NativeBattleWaterExecutionFrame frame = Quad(0);
        List<uint> colors = [];
        List<(uint Output, uint Ordering, NativeBattleGfGeometryBatch Batch)> packets = [];
        NativeBattleCinematicCullingMesh31Services services = Services(Project) with
        {
            Shade = (current, color) =>
            {
                colors.Add(color);
                return new(current, color | 0xAA000000);
            },
            Append = (current, output, ordering, batch) =>
            {
                packets.Add((output, ordering, batch));
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, colors);
        Assert.Equal(2, packets.Count);
        Assert.Equal(0x400u, packets[0].Output);
        Assert.Equal(0x424u, packets[1].Output);
        Assert.Equal(0x1004u, packets[0].Ordering);
        Assert.Equal(packets[0].Ordering, packets[1].Ordering);
        Assert.Equal(36, packets[0].Batch.AllocationBytes);
        Assert.Equal(12, packets[1].Batch.AllocationBytes);
        Assert.Equal(new uint[] { 0x02000000, 0xE1000060, 0 }, Assert.Single(packets[1].Batch.Attempts).Primitive!.Words);
        var words = Assert.Single(packets[0].Batch.Attempts).Primitive!.Words;
        Assert.Equal(0x08000000u, words[0]);
        Assert.Equal(0x3A000001u, words[1]);
        Assert.Equal(0xAA000002u, words[3]);
        Assert.Equal(0x430u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(0x430u, result.ReadTemporaryDword(0x60));
    }

    [Fact]
    public void Ordering_base_is_read_from_the_callback_temporary_owner_not_the_shared_core_window()
    {
        NativeBattleWaterExecutionFrame frame = Quad(0);
        List<uint> orderings = [];
        NativeBattleCinematicCullingMesh31.Draw(frame, Services(Project) with
        {
            Append = (current, _, ordering, _) =>
            {
                orderings.Add(ordering);
                return current;
            }
        });
        Assert.Equal(new uint[] { 0x1004, 0x1004 }, orderings);
        Assert.Throws<InvalidDataException>(() => frame.Core.Read(frame.TemporaryFrame.Address + 0x14C, 4));
    }

    [Fact]
    public void Any_hidden_corner_rejects_face_before_colors_or_hidden_work_are_read()
    {
        NativeBattleWaterExecutionFrame frame = Quad(2048);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3044, 16) };
        int appends = 0;
        NativeBattleCinematicCullingMesh31Services services = Services(Project) with
        {
            Shade = (_, _) => throw new InvalidOperationException(),
            Append = (current, _, _, _) =>
            {
                appends++;
                return current;
            }
        };
        NativeBattleCinematicCullingMesh31.Draw(frame, services);
        Assert.Equal(0, appends);
    }

    private static NativeBattleWaterExecutionFrame Project(NativeBattleWaterExecutionFrame current, uint source, uint output)
    {
        int index = checked((int)(source - Address(0x3100)) / 8);
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, unchecked(0x00200010u + (uint)index));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), checked((short)(2 + index * 2)));
        return current with { Arena = current.Arena.Write(checked((int)(output - Address(0))), bytes) };
    }

    private static NativeBattleCinematicCullingMesh31Services Services(Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> project)
    {
        return new((_, _) => throw new InvalidOperationException(), current => current, project, (current, _) => current, (current, color) => new(current, color), kind => kind == 17 ? (byte)24 : throw new InvalidOperationException(), (current, _, _, _) => current);
    }

    private static NativeBattleWaterExecutionFrame Quad(short factor)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(factor, [1, 2, 3, 4]);
        byte[] group = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(group, 17);
        BinaryPrimitives.WriteInt16LittleEndian(group.AsSpan(2), 1);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(group.AsSpan(4 + index * 4), (uint)(index + 1));
            BinaryPrimitives.WriteUInt16LittleEndian(group.AsSpan(20 + index * 2), (ushort)(index * 8));
        }
        BinaryPrimitives.WriteInt16LittleEndian(group.AsSpan(30), -1);
        return frame with { Arena = frame.Arena.Write(0x3040, group) };
    }

    private static NativeBattleWaterExecutionFrame Fixture(short factor, short[] depths)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, uint.MaxValue);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3200));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02102030);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)factor));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0x60);
        byte[] shape = new byte[0x44];
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(8), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x14), 0x100);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x18), (uint)depths.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x1C), 0x200);
        BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x42), -1);
        byte[] vertices = new byte[depths.Length * 8];
        for (int index = 0; index < depths.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 4), depths[index]);
        }
        return frame with { Arena = frame.Arena.Write(0x3000, shape).Write(0x3100, vertices).MarkUnknown(0x5000, 0x100) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
