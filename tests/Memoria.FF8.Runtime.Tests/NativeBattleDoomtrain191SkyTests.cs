using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191SkyTests
{
    [Fact]
    public void Sky_factory_consumes_exact_sixteen_CRT_draws_and_preserves_unwritten_task_suffix()
    {
        NativeBattleDoomtrain191Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 16; index++)
        {
            random = random.Draw(out _);
        }
        NativeBattleDoomtrain191Frame result = NativeBattleDoomtrain191Sky.Spawn640390(frame);
        NativeBattleModulePoolTask first = result.Effect.Pools[1].Slots[1];
        NativeBattleModulePoolTask second = result.Effect.Pools[1].Slots[2];
        Assert.Equal(random, result.Random);
        Assert.Equal((ushort)41, first.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)-7), first.ReadWord(0x12));
        Assert.Equal((ushort)2238, first.ReadWord(0x14));
        Assert.Equal((ushort)2, first.ReadWord(0x16));
        Assert.Equal((ushort)42, first.ReadWord(0x22));
        Assert.Equal((ushort)2944, first.ReadWord(0x1C));
        Assert.Equal((ushort)3072, second.ReadWord(0x1C));
        Assert.Equal((ushort)1769, second.ReadWord(0x20));
        Assert.Equal((ushort)0xA5A5, first.ReadWord(0xE));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleDoomtrain191Sky.Spawn640390(JsonSerializer.Deserialize<NativeBattleDoomtrain191Frame>(JsonSerializer.Serialize(frame))!)));
    }

    [Fact]
    public void Sky_morph_uses_signed_low_DWORD_SAR_rounding_and_preserves_unknown_vertex_padding_before_draw()
    {
        NativeBattleDoomtrain191Frame frame = SkyTask(0);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x37000, Enumerable.Repeat((byte)0xA5, 16).ToArray()).MarkUnknown(0x37006, 2).MarkUnknown(0x3700E, 2);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int draws = 0;
        var result = NativeBattleDoomtrain191Sky.Advance640580(frame, 0, Resources(), new(() => 0, (current, draw) =>
        {
            draws++;
            Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(current.Memory.Arena!.Read(0x37000, 2)));
            Assert.Equal((short)-2, BinaryPrimitives.ReadInt16LittleEndian(current.Memory.Arena.Read(0x37008, 2)));
            Assert.Equal(0x404040u, draw.Tint);
            Assert.Equal(4096, draw.Fade);
            Assert.Equal(0x20C3u, draw.Flags);
            Assert.Equal(288, draw.Matrix.Z);
            return current;
        }));
        Assert.Equal(1, draws);
        var padding = result.Frame.Memory.Arena!.ReadSnapshot(0x37006, 2);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, padding.Data);
        Assert.All(padding.Known, value => Assert.False(value));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena.Read(0x37006, 2));
        Assert.Equal((ushort)1, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
    }

    [Theory]
    [InlineData(0u, 2, 388)]
    [InlineData(1u, 0, 387)]
    public void Final_sky_draw_precedes_pause_or_completion_and_updates_only_active_WORD_counters(uint flags, int expectedResult, int expectedAge)
    {
        NativeBattleDoomtrain191Frame frame = SkyTask(387);
        int draws = 0;
        var step = NativeBattleDoomtrain191Sky.Advance640580(frame, 0, Resources(), new(() => flags, (current, draw) =>
        {
            draws++;
            Assert.Equal(3840, draw.Fade);
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(expectedResult, step.Result);
        Assert.Equal((ushort)expectedAge, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
    }

    [Fact]
    public void Empty_sky_groups_publish_exact_source_cursor_flags_and_tint_without_fabricating_clip_scratch()
    {
        NativeBattleDoomtrain191SkyResources resources = Resources();
        NativeBattleEffectNodeScratch scratch = NativeBattleEffectNodeScratch.Unknown();
        NativeBattleDoomtrain191SkyDraw draw = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 288, 4096), 0x123456, 0, 0x20C3);
        var result = NativeBattleDoomtrain191Sky.Emit640830(resources, NativeBattleEffectArena.Clear(), draw, new(null, 288, 160, 108), scratch, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x20C3u, BinaryPrimitives.ReadUInt32LittleEndian(result.Scratch.ReadPrefix(0, 4)));
        Assert.Equal(0x123456u, BinaryPrimitives.ReadUInt32LittleEndian(result.Scratch.ReadPrefix(4, 4)));
        Assert.Equal(resources.Source.Address + 56, BinaryPrimitives.ReadUInt32LittleEndian(result.Scratch.ReadPrefix(8, 4)));
        Assert.Throws<NotSupportedException>(() => result.Scratch.ReadPrefix(12, 4));
        Assert.Equal((byte)0, result.Scratch.KnownMask);
    }

    private static NativeBattleDoomtrain191Frame Fixture() => NativeBattleDoomtrain191Tests.Initialize(NativeBattleDoomtrain191Tests.Fixture());

    private static NativeBattleDoomtrain191Frame SkyTask(ushort age)
    {
        NativeBattleDoomtrain191Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[0] with { Callback = 0x640580 };
        task = task.WriteWord(0xC, age).WriteWord(0x10, 0).WriteWord(0x12, 3).WriteWord(0x14, 0).WriteWord(0x16, 5).WriteWord(0x18, 0).WriteWord(0x1A, 1).WriteWord(0x1C, 4096).WriteWord(0x1E, 0).WriteWord(0x20, 0).WriteWord(0x22, 36);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }

    private static NativeBattleDoomtrain191SkyResources Resources()
    {
        byte[] source = new byte[56];
        BinaryPrimitives.WriteInt32LittleEndian(source, 24);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 2);
        byte[] target = new byte[16];
        BinaryPrimitives.WriteInt16LittleEndian(target, -1);
        BinaryPrimitives.WriteInt16LittleEndian(target.AsSpan(8), -3);
        return new(new(0, 0xE3496C, source.ToImmutableArray()), new("target", target.ToImmutableArray()), Enumerable.Repeat(0x808080u, 32).ToImmutableArray());
    }
}
