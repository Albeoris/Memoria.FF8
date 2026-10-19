using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicScrollTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Header = 0x2797068;
    private const uint Manager = 0x2797690;

    [Theory]
    [InlineData(false, 1, 0, 0, 0)]
    [InlineData(true, 0, 256, 1024, 1024)]
    [InlineData(true, 0, -256, 1024, 1024)]
    public void Row_scroll_allocates_once_rotates_native_WORDs_and_publishes_final_scratch_and_ring(bool sineRows, short shift, short amplitude, short angle, short step)
    {
        var fixture = Fixture(0, 2, 255);
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(fixture.Frame, Manager + 0x74, Base + 0x9000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)shift));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, unchecked((ushort)amplitude));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, unchecked((ushort)angle));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, unchecked((ushort)step));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5061, 1) };
        int uploads = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicRowScroll.Draw(current, fixture.Resources, sineRows, (advanced, rectangle, source) =>
        {
            uploads++;
            Assert.Equal(new NativeBattleEffectTextureRectangle(-2, 32766, 2, 2), rectangle);
            Assert.Equal(Base + 0x9000, source);
            Assert.Equal(rectangle, Rectangle(advanced.Core.Read(0x279754C, 8)));
            return advanced;
        });
        NativeBattleWaterExecutionFrame result = Run(frame);
        var actual = result.Arena.ReadSnapshot(0x9000, 8);
        Assert.Equal(sineRows ? new byte[] { 2, 3, 0, 1, 4, 5, 6, 7 } : new byte[] { 2, 3, 0, 1, 6, 7, 4, 5 }, actual.Data);
        Assert.False(actual.Known[3]);
        Assert.Equal(7, actual.Known.Count(known => known));
        Assert.Equal(Base + 0x9008, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Equal(Base + 0x9008, result.ReadTemporaryDword(0x60));
        Assert.Equal(Base + 0x5068, result.ReadTemporaryDword(0x64));
        Assert.Equal(0u, result.ReadTemporaryDword(0x7C));
        Assert.Equal(0, result.Core.Read(Header + 0x43, 1)[0]);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
        Assert.Equal(2, uploads);
        NativeBattleWaterExecutionFrame warm = Run(result);
        Assert.Equal(Base + 0x9008, WaterSchedulerFixture.ReadCoreDword(warm, Manager + 0x74));
    }

    [Theory]
    [InlineData(0, 8, 0, 1)]
    [InlineData(3, 8, 3, 2)]
    [InlineData(-1, 8, 7, 2)]
    [InlineData(6, 6, 4, 2)]
    public void Current_scroll_uses_signed_bitmask_row_exact_source_addresses_and_full_ring_RECT_stores(short scroll, short height, int row, int count)
    {
        var fixture = Fixture(scroll, height, 0xFF);
        List<(NativeBattleEffectTextureRectangle Rectangle, uint Address)> uploads = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScroll.Draw(fixture.Frame, fixture.Resources, (current, rectangle, address) =>
        {
            int index = uploads.Count == 0 ? 15 : 0;
            byte[] actual = current.Core.Read(0x27974D4 + (uint)(index * 8), 8);
            Assert.Equal(rectangle, Rectangle(actual));
            Assert.Equal(unchecked((byte)uploads.Count), current.Core.Read(Header + 0x43, 1)[0]);
            uploads.Add((rectangle, address));
            return WaterSchedulerFixture.CoreDword(current, Manager + 0xC0, (uint)uploads.Count);
        });
        Assert.Equal(count, uploads.Count);
        Assert.Equal(new NativeBattleEffectTextureRectangle(-2, 32766, 2, (short)(height - row)), uploads[0].Rectangle);
        Assert.Equal(Base + 0x5060u + (uint)(4 * row), uploads[0].Address);
        if (row != 0)
        {
            Assert.Equal(new NativeBattleEffectTextureRectangle(-2, unchecked((short)(32766 + height - row)), 2, (short)row), uploads[1].Rectangle);
            Assert.Equal(Base + 0x5060, uploads[1].Address);
        }
        Assert.Equal(unchecked((byte)(0xFF + count)), result.Core.Read(Header + 0x43, 1)[0]);
        Assert.Equal((uint)count, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0xC0));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Equal(Base + 0x5060, result.ReadTemporaryDword(0x64));
        Assert.Equal((uint)row, result.ReadTemporaryDword(0x70));
        Assert.Equal(2u, result.ReadTemporaryDword(0x78));
        Assert.Equal((uint)height, result.ReadTemporaryDword(0x7C));
        Assert.Equal(0x1000D0u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(0xFBu, result.ReadTemporaryDword(0xF4));
        Assert.Equal(0x80u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(Base + 0x5060, result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void Scroll_never_reads_IO_busy_or_VM_transfer_budget_and_honors_current_streamed_source_pointer_and_header()
    {
        var fixture = Fixture(1, 8, 0);
        NativeBattleWaterExecutionFrame frame = fixture.Frame with { Core = fixture.Frame.Core.MarkUnknown(0x2798219, 1).MarkUnknown(Manager + 0xD2, 1), Arena = fixture.Frame.Arena.Write(0x6000, fixture.Frame.Arena.Read(0x5000, 0x200)) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68 + 9 * 4, Base + 0x6000);
        frame = frame with { Arena = frame.Arena.Write(0x6014, BitConverter.GetBytes(0x80u)).Write(0x60D8, BitConverter.GetBytes(0xA0000020u)) };
        List<uint> addresses = [];
        NativeBattleCinematicScroll.Draw(frame, fixture.Resources, (current, _, address) =>
        {
            addresses.Add(address);
            return current;
        });
        Assert.Equal(new[] { Base + 0x60A4, Base + 0x60A0 }, addresses);
    }

    [Fact]
    public void Direct_arena_queue_and_independent_snapshot_consume_actual_late_source_bytes()
    {
        var fixture = Fixture(1, 8, 0);
        NativeBattleWaterExecutionFrame frame = fixture.Frame with { Arena = fixture.Frame.Arena.Write(0x3002, BitConverter.GetBytes((short)10)).Write(0x3004, BitConverter.GetBytes((short)20)) };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        frame = NativeBattleCinematicScroll.Draw(frame, fixture.Resources, (current, rectangle, address) =>
        {
            textures = textures.EnqueueArenaRectangle505DF0(rectangle, current.Arena, checked((int)(address - Base)));
            return current;
        });
        Assert.Equal(2, textures.Count);
        Assert.All(textures.Slots.Take(2), slot => Assert.Null(slot!.Source));
        frame = frame with { Arena = frame.Arena.Write(0x5064, [0xAB, 0xCD]) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffectTextureState restoredTextures = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(textures))!;
        NativeBattleEffectTextureDrain live = textures.Drain505D20(frame.Arena);
        NativeBattleEffectTextureDrain cold = restoredTextures.Drain505D20(restored.Arena);
        Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(cold));
        Assert.Equal(new byte[] { 0xAB, 0xCD }, live.Uploads[0].Bytes.Take(2));
        Assert.Equal((short)20, live.Uploads[0].Y);
        Assert.Equal((short)27, live.Uploads[1].Y);
    }

    [Fact]
    public void Shared_geometry_preserves_native_DWORD_source_wrap_and_explicit_malformed_extent_boundary()
    {
        List<uint> addresses = [];
        NativeBattleCinematicScroll.EmitGeometry(new(0, 0, 2, 8), new(0, 1, 2), uint.MaxValue - 1, 1, (_, address) => addresses.Add(address));
        Assert.Equal(new[] { 2u, uint.MaxValue - 1 }, addresses);
        var fixture = Fixture(0, 0, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicScroll.Draw(fixture.Frame, fixture.Resources, (_, _, _) => throw new InvalidOperationException()));
    }

    private static (NativeBattleWaterExecutionFrame Frame, NativeBattleCinematicResources Resources) Fixture(short scroll, short height, byte counter)
    {
        NativeBattleCinematicResources resources = NativeBattleCinematicTextureMetadataTests.Resources();
        byte[] data = resources.StaticData.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20 + 22 * 8 + 4), 2);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20 + 22 * 8 + 6), height);
        resources = resources with { StaticData = resources.StaticData with { Data = data.ToImmutableArray() } };
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicTextureMetadataTests.Frame().WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, Header);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Base + 0x3000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)scroll));
        frame = frame with { Core = frame.Core.Write(Header + 0x43, [counter]), Arena = frame.Arena.Write(0x3000, BitConverter.GetBytes((short)22)).Write(0x3002, BitConverter.GetBytes((short)-2)).Write(0x3004, BitConverter.GetBytes((short)32766)).Write(0x5060, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()) };
        return (frame, resources);
    }

    private static NativeBattleEffectTextureRectangle Rectangle(byte[] bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
}
