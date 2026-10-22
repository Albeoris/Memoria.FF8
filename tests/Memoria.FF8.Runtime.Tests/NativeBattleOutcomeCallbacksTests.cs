using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOutcomeCallbacksTests
{
    [Fact]
    public void OpeningFadeUsesZeroTo128AndInvokesTheTimerAtNativeElapsedFiveWithoutExitPublication()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendOpeningFade501CE0(15);
        List<byte> colors = [];
        int timer = 0;
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ApplyScreenFade501D10 = (color, publish) =>
            {
                colors.Add(color);
                Assert.False(publish);
            },
            InitializeTimerDisplay47D910 = () =>
            {
                Assert.Equal(6, colors.Count);
                timer++;
            }
        };
        for (int frame = 0; frame <= 15; frame++)
        {
            state = state.Advance(inputs).State;
        }
        Assert.Equal((byte)0, colors[0]);
        Assert.Equal((byte)128, colors[^1]);
        Assert.Equal(1, timer);
        Assert.Null(state.Head);
    }

    [Fact]
    public void DeferredCompletionClearsItsCallerBeforeTheNextSharedAuxiliaryCallbackPublishes255()
    {
        List<(uint Address, byte Value)> stores = [];
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendDeferredCompletion5086C0(0x1D96D26, (address, value) => stores.Add((address, value)));
        Assert.Equal(new[] { (0x1D96D26u, (byte)0) }, stores);
        state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        NativeBattleAuxiliaryInputs inputs = new() { WriteCompletionByte = (address, value) => stores.Add((address, value)) };
        state = state.Advance(inputs).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new[] { (0x1D96D26u, (byte)0), (0x1D96D26u, (byte)255) }, stores);
        Assert.Null(state.Head);
        NativeBattleGraphicsModel protectedModel = new(3, 2) { Flags8 = 0x1000, Kind5 = 0, ResourceFlags44 = 0 };
        Assert.True(NativeBattleActionGraphics.IsModelBusy(protectedModel, 0x40));
        Assert.False(NativeBattleActionGraphics.IsModelBusy(protectedModel, 0x40, 0x101A));
    }

    [Fact]
    public void Fade_packet_is_the_native_320_by_216_quad_followed_by_exact_draw_mode_in_bucket1()
    {
        var packets = NativeBattleOutcomeCallbacks.FadePackets501D10(0);
        Assert.All(packets, packet => Assert.Equal(1, packet.Bucket));
        Assert.Equal(new uint[] { 0x05000000, 0x2A808080, 0, 320, 216u << 16, (216u << 16) | 320 }, packets[0].Words);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000040 }, packets[1].Words);
        Assert.Empty(packets[0].VertexPayload4);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packets[1].VertexPayload4);
        Assert.Equal(32, packets.Sum(packet => packet.Words.Length * 4));
        Assert.Equal(0x2A000000u, NativeBattleOutcomeCallbacks.FadePackets501D10(128)[0].Words[1]);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1, 30)]
    [InlineData(2, 40)]
    [InlineData(3, 60)]
    public void Fade_styles_establish_native_countdown_and_delayed_47E030_publication(byte style, byte duration)
    {
        Assert.Equal(duration, NativeBattleOutcomeCallbacks.Duration47DFC0(style));
        (NativeBattleScriptTasks tasks, int slot) = NativeBattleScriptTasks.Clear().Allocate(0x47E030);
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, tasks.Slots[slot].WriteWord(8, (ushort)(duration - 15))) };
        int publications = 0;
        for (int callback = 0; callback < duration - 15; callback++)
        {
            tasks = tasks.Advance((state, index) => state with { Slots = state.Slots.SetItem(index, NativeBattleOutcomeCallbacks.Advance47E030(state.Slots[index], () => publications++)) });
            Assert.Equal(0, publications);
        }
        tasks = JsonSerializer.Deserialize<NativeBattleScriptTasks>(JsonSerializer.Serialize(tasks))!;
        tasks = tasks.Advance((state, index) => state with { Slots = state.Slots.SetItem(index, NativeBattleOutcomeCallbacks.Advance47E030(state.Slots[index], () => publications++)) });
        Assert.Equal(1, publications);
        Assert.Equal(ushort.MaxValue, tasks.Slots[slot].ReadWord(8));
        Assert.False(tasks.Allocated(slot));
    }

    [Fact]
    public void Unknown_or_unestablished_fade_style_is_not_an_invented_duration()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleOutcomeCallbacks.Duration47DFC0(null));
        Assert.Throws<NotSupportedException>(() => NativeBattleOutcomeCallbacks.Duration47DFC0(4));
    }

    [Theory]
    [InlineData(255, 255)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(60, 59)]
    public void Outer_frame_countdown_preserves_ff_and_stops_at_zero(byte before, byte after)
    {
        Assert.Equal(after, NativeBattleOutcomeCallbacks.AdvanceCountdown47CCB0(before));
    }

    [Fact]
    public void Descriptor4_fade_uses_shared_auxiliary_slots_and_fourteen_native_callbacks()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendOutcomeFade501E10(13);
        List<byte> colors = [];
        List<bool> completed = [];
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ApplyScreenFade501D10 = (color, done) =>
            {
                colors.Add(color);
                completed.Add(done);
            }
        };
        for (int frame = 0; frame <= 13; frame++)
        {
            if (frame == 7)
            {
                state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
            }
            state = state.Advance(inputs).State;
        }
        Assert.Equal(14, colors.Count);
        Assert.Equal((byte)128, colors[0]);
        Assert.Equal((byte)0, colors[^1]);
        Assert.All(completed.Take(13), value => Assert.False(value));
        Assert.True(completed[^1]);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.DoesNotContain(state.Slots, task => (task.Flags & 1) != 0);
    }
}
