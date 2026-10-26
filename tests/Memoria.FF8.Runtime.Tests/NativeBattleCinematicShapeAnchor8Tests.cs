using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicShapeAnchor8Tests
{
    private const uint Basis = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Reference = Basis + 0x2100;

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2048, 1)]
    [InlineData(4095, 3)]
    [InlineData(4096, 3)]
    [InlineData(32767, 3)]
    public void Selects_an_actual_vertex_by_signed_factor_and_first_nearest_depth(int factor, int expected)
    {
        NativeBattleVector[] vertices = [new(10, 0, -100), new(20, 3, 80), new(30, 6, 120), new(40, 9, 200)];
        NativeBattleWaterExecutionFrame source = Fixture(factor, vertices);
        int resolutions = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShapeAnchor8.Update(source, (current, selector) =>
        {
            resolutions++;
            Assert.Equal((short)5050, selector);
            Assert.Equal(source.Random, current.Random);
            return Reference;
        });
        Assert.Equal(1, resolutions);
        Assert.Equal(vertices[expected], Position(result));
        Assert.Equal(source.Random, result.Random);
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(source, 0, 0x94));
        Assert.Equal(200u, Work(result, 0x44));
        Assert.Equal(4u, Work(result, 0x48));
        Assert.Equal(3u, Work(result, 0x50));
        Assert.Equal(0u, Work(result, 0x54));
        Assert.Equal(unchecked((uint)factor), Work(result, 0x58));
        Assert.Equal(Basis + 0x5100, Work(result, 0x74));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2048, 1)]
    [InlineData(4096, 0)]
    public void Retains_the_native_zero_maximum_and_first_index_for_all_negative_depths(int factor, int expected)
    {
        NativeBattleVector[] vertices = [new(1, 2, -200), new(3, 4, -100), new(5, 6, -300)];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShapeAnchor8.Update(Fixture(factor, vertices), (_, _) => Reference);
        Assert.Equal(vertices[expected], Position(result));
        Assert.Equal(0u, Work(result, 0x44));
        Assert.Equal(0u, Work(result, 0x50));
        Assert.Equal(2u, Work(result, 0x54));
    }

    [Fact]
    public void Nearest_search_preserves_the_signed_word_distance_sentinel()
    {
        NativeBattleVector[] vertices = [new(11, 12, short.MinValue), new(13, 14, -32767)];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShapeAnchor8.Update(Fixture(2048, vertices), (_, _) => Reference);
        Assert.Equal(vertices[0], Position(result));
    }

    [Fact]
    public void Uses_signed_reference_scales_with_division_toward_zero_and_reads_no_vector_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, [new(-3, 3, -1)]);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C, 128);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8E, 128);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x90, ushort.MaxValue);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicShapeAnchor8.Update(frame, (_, _) => Reference);
        Assert.Equal(new NativeBattleVector(-1, 1, 0), Position(result));
        Assert.DoesNotContain(true, result.Arena.ReadSnapshot(0x5106, 2).Known);
    }

    private static NativeBattleWaterExecutionFrame Fixture(int factor, NativeBattleVector[] vertices)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797624, Basis + 0x8000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xA0, Basis + 0x4000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x64, unchecked((uint)(factor << 16)) | 0xFFFF);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xD8, Basis + 0x5000);
        for (int axis = 0; axis < 3; axis++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C + axis * 2, 256);
        }
        NativeBattleEffectArena arena = frame.Arena.MarkUnknown(0x5000, 0x20).MarkUnknown(0x5100, vertices.Length * 8);
        arena = arena.Write(0x4000, BitConverter.GetBytes((short)5050)).Write(0x5014, BitConverter.GetBytes(0x100u)).Write(0x5018, BitConverter.GetBytes(vertices.Length));
        for (int index = 0; index < vertices.Length; index++)
        {
            byte[] position = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(position, vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(position.AsSpan(2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(position.AsSpan(4), vertices[index].Z);
            arena = arena.Write(0x5100 + index * 8, position);
        }
        return frame with { Arena = arena };
    }

    private static NativeBattleVector Position(NativeBattleWaterExecutionFrame frame) => new(unchecked((short)WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x94)), unchecked((short)WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x96)), unchecked((short)WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x98)));
    private static uint Work(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(0x8000 + offset, 4));
}
