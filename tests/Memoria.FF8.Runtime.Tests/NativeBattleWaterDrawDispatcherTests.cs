using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterDrawDispatcherTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x800, 8, 0x2000);

    [Fact]
    public void Scroll_reads_only_live_pixel_directory_and_publishes_each_ring_rectangle_before_enqueue()
    {
        var source = ScrollSource();
        NativeBattleWaterExecutionFrame initial = WaterSchedulerFixture.Create();
        initial = WaterSchedulerFixture.SlotDword(initial, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0x96, 1);
        initial = initial with { Arena = initial.Arena.Write(0x5000, new byte[] { 0, 0, 0, 2, 0, 1 }), Core = initial.Core.Write(0x27970AB, new byte[] { 0xFF }) };
        int fileOffset = source.Files[0].Data.Length;
        uint fileAddress = NativeBattleEffectNodeMemory.ArenaAddress + (uint)fileOffset;
        initial = initial with { Core = initial.Core.Write(0x2798A68, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress)).Write(0x2798A6C, BitConverter.GetBytes(fileAddress)) };
        initial.Arena.MarkUnknown(fileOffset, source.Files[1].Data.Length);
        initial.Arena.Write(fileOffset + 0x14, new byte[] { 0x40, 0, 0, 0 });
        initial.Arena.Write(fileOffset + 0x40, new byte[] { 0xC0, 0, 0, 0xFF });
        List<int> indices = [];
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWaterDrawServices services = UnusedServices() with
        {
            EnqueueScroll = (current, write) =>
            {
                Assert.Equal(write.NextCounter, current.Core.Read(0x27970AB, 1)[0]);
                Assert.Equal(write.RectangleBytes, current.Core.Read(0x27974D4 + (uint)(write.RingIndex * 8), 8).ToImmutableArray());
                Assert.Equal(1, write.Upload.SourceIndex);
                Assert.Equal(indices.Count == 0 ? 0x104 : 0x100, write.Upload.Offset);
                indices.Add(write.RingIndex);
                textures = textures.EnqueueArenaRectangle505DF0(write.Upload.Rectangle, current.Arena, fileOffset + write.Upload.Offset);
                return current.WriteTemporaryByte(0x10 + indices.Count, (byte)indices.Count);
            }
        };
        NativeBattleWaterDrawInputs input = new(new(source.Files, source.Profile), 0x155, 0, 0, 0);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(initial))!;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterDrawDispatcher.Draw(initial, 0x15, Bank, 0, input, services);
        Assert.Equal(new[] { 15, 0 }, indices);
        Assert.Equal((byte)1, result.Core.Read(0x27970AB, 1)[0]);
        Assert.Equal((byte)1, result.ReadTemporaryByte(0x11));
        Assert.Equal((byte)2, result.ReadTemporaryByte(0x12));
        Assert.Equal(new NativeCrtRandomState(1), result.Random);
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(0x279754C + 6, 2)));
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(0x27974D4 + 6, 2)));
        Assert.Equal(2, textures.Count);
        Assert.Throws<NotSupportedException>(() => textures.Drain505D20(result.Arena));
        byte[] pixels = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        result.Arena.Write(fileOffset + 0x100, pixels);
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(result.Arena);
        Assert.Equal(pixels.Skip(4), drain.Uploads[0].Bytes);
        Assert.Equal(pixels.Take(4), drain.Uploads[1].Bytes);

        indices.Clear();
        textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWaterExecutionFrame continued = NativeBattleWaterDrawDispatcher.Draw(restored, 0x15, Bank, 0, input, services);
        continued.Arena.Write(fileOffset + 0x100, pixels);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(continued));

        indices.Clear();
        continued.Arena.MarkUnknown(fileOffset + 0x40, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterDrawDispatcher.Draw(continued, 0x15, Bank, 0, input, services));
        Assert.Empty(indices);
    }

    [Fact]
    public void Rectangle_terminal_does_not_resolve_projection_packet_cursor_or_unrelated_owners()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD0, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xE4, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        frame = frame with { Arena = frame.Arena.Write(0x5000, new byte[] { 0, 0, 0, 0, 0, 4, 1, 0 }), Core = frame.Core.Write(0x2797554, new byte[] { 0, 0xFF }) };
        frame = frame.WriteTemporaryDword(0, 9);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterDrawDispatcher.Draw(frame, 5, Bank, 0, null!, UnusedServices());
        Assert.Equal((byte)0, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal((byte)0xFF, result.Core.Read(0x2797554, 1)[0]);
        Assert.Equal(8u, result.ReadTemporaryDword(0));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0xFF)]
    public void Unsupported_draw_kinds_fail_before_consuming_any_owner(byte kind)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterDrawDispatcher.Draw(null!, kind, null!, 0, null!, null!));
    }

    private static NativeBattleWaterDrawServices UnusedServices() => new(
        _ => throw new InvalidOperationException("Unexpected projection read."),
        (_, _) => throw new InvalidOperationException("Unexpected focal publication."),
        (_, _) => throw new InvalidOperationException("Unexpected shape extent read."),
        _ => throw new InvalidOperationException("Unexpected depth read."),
        (_, _) => throw new InvalidOperationException("Unexpected depth publication."),
        (_, _, _) => throw new InvalidOperationException("Unexpected packet submission."),
        (_, _) => throw new InvalidOperationException("Unexpected scroll enqueue."),
        _ => throw new InvalidOperationException("Unexpected actor read."),
        (_, _, _) => throw new InvalidOperationException("Unexpected actor publication."),
        new((_, _, _) => throw new InvalidOperationException("Unexpected local pose read."), (_, _, _, _) => throw new InvalidOperationException("Unexpected bone publication."), NativeBattleActorAnchorRegisterFixture.Services()),
        (_, _, _) => throw new InvalidOperationException("Unexpected metadata read."),
        _ => throw new InvalidOperationException("Unexpected ordering table read."));

    private static (NativeBattleWater222Profile Profile, ImmutableArray<NativeBattleEffectHeapAllocation> Files) ScrollSource()
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(4), 2);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(6), 4);
        tables[80] = 0x81;
        byte[] file = new byte[0x26258];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x40), 0xD0);
        return (new(new("tables", tables.ToImmutableArray()), new("initialization", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())), [new("current0", new byte[0x1799C].ToImmutableArray()), new("current1", file.ToImmutableArray())]);
    }
}
