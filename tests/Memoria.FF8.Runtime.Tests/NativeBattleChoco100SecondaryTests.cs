using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100SecondaryTests
{
    [Fact]
    public void Seven_phases_keep_published_actor_world_and_cleanup_without_resetting_consumed_timer()
    {
        NativeBattleChoco100SecondaryFrame frame = Frame(0, 0);
        frame = frame with { Task = frame.Task.WriteByte(0x28, 1).WriteWord(0x24, ushort.MaxValue) };
        NativeBattleActionMatrix world = frame.Actor.GeometryWorldMatrix!;
        int draws = 0;
        int callbacks = 0;
        HashSet<byte> phases = [];
        while (frame.Task.ReadByte(0x29) != 6 && callbacks < 256)
        {
            phases.Add(frame.Task.ReadByte(0x29));
            NativeBattleChoco100SecondaryStep next = Advance(frame, false);
            NativeBattleChoco100SecondaryFrame cold = JsonSerializer.Deserialize<NativeBattleChoco100SecondaryFrame>(JsonSerializer.Serialize(frame))!;
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Advance(cold, false)));
            if ((next.Frame.Task.ReadByte(0x26) & 8) != 0)
            {
                draws++;
                Assert.Equal((ushort)((draws * 0x180) & 0xFFF), next.Frame.Task.ReadWord(0xC0));
                Assert.Equal((ushort)0x806, next.Frame.Actor.Flags0);
            }
            Assert.Equal(JsonSerializer.Serialize(world), JsonSerializer.Serialize(next.Frame.Actor.GeometryWorldMatrix));
            Assert.False(next.ReadyToComplete);
            frame = next.Frame;
            callbacks++;
        }
        Assert.Equal(75, callbacks);
        Assert.Equal(70, draws);
        Assert.Equal(Enumerable.Range(0, 6).Select(value => (byte)value), phases.Order());
        Assert.Equal((ushort)0xFFFF, frame.Task.ReadWord(0xCC));
        Assert.Equal((ushort)0x802, frame.Actor.Flags0);
        Assert.Equal((short)0, frame.Actor.Rotation12);
        Assert.Equal((ushort)0, frame.Task.ReadWord(0xC4));
        Assert.Equal((ushort)0, frame.Task.ReadWord(0xC6));
        Assert.True(Advance(frame with { Task = frame.Task.WriteByte(0x28, 0) }, true).ReadyToComplete);
        Assert.Equal(0x1D8E054u, frame.Task.ReadDword(0xB8));
        Assert.Equal(0x1D8E054u, frame.Task.ReadDword(0xBC));
        Assert.Equal(Work, frame.Settings!.WorkAddress());

        NativeBattleChoco100SecondaryStep Advance(NativeBattleChoco100SecondaryFrame current, bool dormant) => NativeBattleChoco100Secondary.Advance716C20(current, Work, drawn =>
        {
            Assert.False(dormant);
            Assert.True((drawn.Task.ReadByte(0x26) & 8) != 0);
            return drawn;
        });
    }

    [Theory]
    [InlineData(300, 1024, 8)]
    [InlineData(1280, 1280, 8)]
    [InlineData(1281, 1281, 7)]
    [InlineData(1793, 1793, 6)]
    [InlineData(2305, 2305, 5)]
    [InlineData(2817, 2817, 4)]
    [InlineData(3329, 3329, 3)]
    [InlineData(3841, 3841, 2)]
    [InlineData(5000, 4096, 2)]
    public void Height_clamp_and_frequency_use_independent_original_height(int height, int amplitude, int frequency)
    {
        NativeBattleChoco100SecondaryFrame frame = Frame(1, 1);
        frame = frame with { Actor = frame.Actor with { GeometryBounds52 = new(new(0, 0, 0), new(0, (short)height, 0)) } };
        NativeBattleChoco100SecondaryFrame next = NativeBattleChoco100Secondary.Advance716C20(frame, Work, value => value).Frame;
        Assert.Equal((ushort)amplitude, next.Task.ReadWord(0xC2));
        Assert.Equal((ushort)frequency, next.Task.ReadWord(0xC8));
        Assert.Equal((ushort)0x180, next.Task.ReadWord(0xC0));
        Assert.Equal((ushort)45, next.Task.ReadWord(0xCC));
        Assert.Equal((short)0xC00, next.Actor.Rotation12);
    }

    [Fact]
    public void Height_subtraction_wraps_word_and_settings_preserve_warm_or_unknown_unwritten_bytes()
    {
        NativeBattleChoco100SecondaryFrame frame = Frame(1, 1);
        frame = frame with { Actor = frame.Actor with { GeometryBounds52 = new(new(0, -30000, 0), new(0, 30000, 0)) } };
        NativeBattleChoco100SecondaryFrame next = NativeBattleChoco100Secondary.Advance716C20(frame, Work, value => value).Frame;
        Assert.Equal((ushort)1024, next.Task.ReadWord(0xC2));
        Assert.Equal((ushort)8, next.Task.ReadWord(0xC8));
        NativeBattleChoco100SecondarySettings unknown = NativeBattleChoco100SecondarySettings.Unknown();
        Assert.Throws<NotSupportedException>(() => unknown.WorkAddress());
        unknown = unknown.Initialize717450(Work);
        Assert.All(unknown.Known.Take(4), value => Assert.False(value));
        Assert.All(unknown.Known.Skip(8).Take(12), value => Assert.False(value));
        Assert.False(unknown.Known[0x1F]);
        Assert.Equal(Work, unknown.WorkAddress());
        NativeBattleChoco100SecondarySettings warm = new(Enumerable.Repeat((byte)0xA5, 0x26).ToImmutableArray(), Enumerable.Repeat(true, 0x26).ToImmutableArray());
        warm = warm.Initialize717450(Work);
        Assert.Equal((byte)0xA5, warm.Bytes[0]);
        Assert.Equal((byte)0xA5, warm.Bytes[0x1F]);
        Assert.All(warm.Known, value => Assert.True(value));
        Assert.Equal((byte)0x80, warm.Bytes[0x1C]);
        Assert.All(NativeBattleChoco100SecondarySettings.FromColdProcess().Bytes, value => Assert.Equal(0, value));
    }

    private const uint Work = 0x20DFAB8 + 0x30000;
    private static NativeBattleChoco100SecondaryFrame Frame(byte phase, ushort timer)
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x716C20, new byte[0xD0 - 12].ToImmutableArray()).WriteByte(0x29, phase).WriteByte(0x2D, 0).WriteWord(0xCC, timer);
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleGraphicsModel actor = new(0, 0x802) { Rotation12 = 0, GeometryWorldMatrix = world, GeometryBounds52 = new(new(-10, -2048, -10), new(10, 2048, 10)) };
        return new(task, actor, null);
    }
}
