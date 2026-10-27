using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFinalStage142Tests
{
    [Fact]
    public void Cold_state_and_initializer_zero_preserve_retained_counters_and_private_bank_bytes()
    {
        NativeBattleFinalStage142State cold = NativeBattleFinalStage142State.FromColdProcess();
        Assert.Equal(4096, cold.CurrentRatio);
        Assert.Equal(4096, cold.TargetRatio);
        Assert.Equal(0x00808080u, NativeBattleFinalStage142.InitializerColors.Color44);
        Assert.Equal(0x32808080u, NativeBattleFinalStage142.LoadedColors.Color44);
        Assert.All(cold.ReadOverlay(0x1D9A020, 1024), value => Assert.Equal((byte)0, value));
        NativeBattleFinalStage142State retained = cold with { CurrentRatio = 1024, TargetRatio = 2, FlashCountdown = 3, AlternatorA = 1, TransitionB = 5, SavedRotation = 17 };
        retained = retained.WriteOverlay(0x1D9A024, [9], [NativeBattleGfPacketByteState.Known]);
        NativeBattleFinalStage142State reset = NativeBattleFinalStage142.Initialize0(retained);
        Assert.Equal(4096, reset.TargetRatio);
        Assert.Equal(retained with { TargetRatio = 4096 }, reset);
        Assert.Equal((byte)9, reset.ReadOverlay(0x1D9A024, 1)[0]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    public void Every_callback_resets_only_cursor_and_phase_zero_defers_all_helpers(short phase, byte buffer)
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TransitionA = 1, TargetRatio = 0, FlashCountdown = 3, OverlayCursor = 123 };
        NativeBattleFinalStage142Step result = NativeBattleFinalStage142.Advance(state, Stage(), phase, buffer, 0);
        Assert.Equal(0x1D9A020u + ((uint)buffer << 10), result.State.OverlayCursor);
        Assert.Equal(phase == 0 ? (short)1 : phase, result.Phase);
        Assert.Equal(3, result.State.FlashCountdown);
        Assert.Empty(result.Commands);
    }

    [Fact]
    public void Flash_triggers_without_overlay_then_runs_five_decremented_frames_and_commits_captured_target()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TargetRatio = 2048 };
        NativeBattleFinalStage142Step trigger = NativeBattleFinalStage142.Advance(state, Stage(), 1, 0, 0);
        Assert.Equal(5, trigger.State.FlashCountdown);
        Assert.Equal(2048, trigger.State.CapturedTarget);
        Assert.DoesNotContain(trigger.Commands, command => command.Kind == NativeBattleFinalStage142CommandKind.Overlay);
        state = trigger.State with { TargetRatio = 1024 };
        for (int remaining = 4; remaining >= 0; remaining--)
        {
            NativeBattleFinalStage142Step frame = NativeBattleFinalStage142.Advance(state, Stage(), 1, 1, 0);
            NativeBattleFinalStage142Command overlay = Assert.Single(frame.Commands.Where(command => command.Kind == NativeBattleFinalStage142CommandKind.Overlay));
            int t = (remaining << 12) / 5;
            Assert.Equal(4096 - (2048 * t >> 12) - 2048, overlay.X);
            Assert.Equal(64, overlay.Y);
            Assert.Equal(3, overlay.Z);
            Assert.Equal(0x1D9A420u, overlay.PacketAddress);
            Assert.Equal(remaining, frame.State.FlashCountdown);
            Assert.Equal(remaining == 0 ? 2048 : 4096, frame.State.CurrentRatio);
            state = frame.State;
        }
        NativeBattleFinalStage142Step retrigger = NativeBattleFinalStage142.Advance(state, Stage(), 1, 0, 0);
        Assert.Equal(5, retrigger.State.FlashCountdown);
        Assert.Equal(1024, retrigger.State.CapturedTarget);
    }

    [Fact]
    public void Ratio_increase_does_not_synchronize_or_start_a_flash()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { CurrentRatio = 1024, TargetRatio = 2048 };
        NativeBattleFinalStage142Step result = NativeBattleFinalStage142.Advance(state, Stage(), 1, 0, 0);
        Assert.Equal(1024, result.State.CurrentRatio);
        Assert.Equal(0, result.State.FlashCountdown);
    }

    [Fact]
    public void Transitions_execute_A_then_B_preserve_high_rotation_word_and_do_not_consume_busy_shake_alternator()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TransitionA = 1, TransitionB = 1 };
        NativeBattleStageState stage = Stage() with { Rotation = unchecked((int)0xCAFEFFFF) };
        NativeBattleFinalStage142Step result = NativeBattleFinalStage142.Advance(state, stage, 1, 0, 0);
        Assert.Equal(unchecked((int)0xCAFE0056), result.Stage.Rotation);
        Assert.Equal((short)-1, result.CameraShake);
        Assert.Equal(1, result.State.AlternatorA);
        Assert.Equal(0, result.State.AlternatorB);
        Assert.Equal(2, result.State.TransitionA);
        Assert.Equal(2, result.State.TransitionB);
        Assert.Equal(new[] { NativeBattleFinalStage142CommandKind.FarColor, NativeBattleFinalStage142CommandKind.Overlay, NativeBattleFinalStage142CommandKind.PcRenderRequest, NativeBattleFinalStage142CommandKind.Texture0, NativeBattleFinalStage142CommandKind.StandardStage }, result.Commands.Select(command => command.Kind));
        Assert.Equal(0x1D9A068u, result.State.OverlayCursor);
    }

    [Fact]
    public void Stage_flag_eight_blocks_transitions_but_not_HP_flash_and_flag_two_blocks_only_texture_motion()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TransitionA = 1, TransitionB = 1, FlashCountdown = 1, CapturedTarget = 2048 };
        NativeBattleFinalStage142Step result = NativeBattleFinalStage142.Advance(state, Stage() with { Flags = 10 }, 1, 0, 0);
        Assert.Equal(1, result.State.TransitionA);
        Assert.Equal(1, result.State.TransitionB);
        Assert.Equal(2048, result.State.CurrentRatio);
        Assert.Equal(new[] { NativeBattleFinalStage142CommandKind.FarColor, NativeBattleFinalStage142CommandKind.Overlay, NativeBattleFinalStage142CommandKind.StandardStage }, result.Commands.Select(command => command.Kind));
    }

    [Fact]
    public void Transition_B_shake_alternates_only_when_zero_and_counter_saturates_at_five()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TransitionB = 5 };
        NativeBattleFinalStage142Step first = NativeBattleFinalStage142.Advance(state, Stage(), 1, 0, 0);
        Assert.Equal((short)-2, first.CameraShake);
        Assert.Equal(1, first.State.AlternatorB);
        Assert.Equal(5, first.State.TransitionB);
        NativeBattleFinalStage142Step busy = NativeBattleFinalStage142.Advance(first.State, Stage(), 1, 0, 3);
        Assert.Equal((short)3, busy.CameraShake);
        Assert.Equal(1, busy.State.AlternatorB);
        NativeBattleFinalStage142Step next = NativeBattleFinalStage142.Advance(busy.State, Stage(), 1, 0, 0);
        Assert.Equal((short)1, next.CameraShake);
        Assert.Equal(0, next.State.AlternatorB);
    }

    [Fact]
    public void Controls_preserve_alternators_and_saved_rotation_and_A_stop_emits_one_overlay()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess() with { TransitionA = 5, TransitionB = 4, AlternatorA = 1, SavedRotation = 0xFEDC, OverlayCursor = 0x1D9A420 };
        NativeBattleFinalStage142Step stopA = NativeBattleFinalStage142.Control(state, Stage() with { Rotation = 0x12345678 }, 1, 3, 101);
        Assert.Equal(0x1234FEDC, stopA.Stage.Rotation);
        Assert.Equal(0, stopA.State.TransitionA);
        Assert.Equal(1, stopA.State.AlternatorA);
        NativeBattleFinalStage142Command overlay = Assert.Single(stopA.Commands);
        Assert.Equal(NativeBattleFinalStage142CommandKind.Overlay, overlay.Kind);
        Assert.Equal((0, 32, 16), (overlay.X, overlay.Y, overlay.Z));
        NativeBattleFinalStage142Step stopB = NativeBattleFinalStage142.Control(stopA.State, stopA.Stage, 1, 3, 103);
        Assert.Empty(stopB.Commands);
        Assert.Equal(0, stopB.State.TransitionB);
    }

    [Fact]
    public void HP_target_uses_first_enabled_identity8C_descriptor_in_actor_order_and_signed_quantization()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess();
        ImmutableArray<NativeBattleFinalStage142Actor> actors = [new(6, 2, 0x8C, 1, 4), new(3, 4, 0x8C, 1, 4), new(5, 2, 0x8C, 3, 4), new(4, 2, 0x8C, 1, 3)];
        NativeBattleFinalStage142State sampled = NativeBattleFinalStage142.UpdateTarget3(state, actors);
        Assert.Equal(1536, sampled.TargetRatio);
        sampled = NativeBattleFinalStage142.UpdateTarget3(state, actors.Select(actor => actor with { Identity = 1 }).ToImmutableArray());
        Assert.Equal(state.TargetRatio, sampled.TargetRatio);
    }

    [Fact]
    public void HP_sampling_preserves_signed_division_and_fails_on_native_zero_divisor()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess();
        ImmutableArray<NativeBattleFinalStage142Actor> actors = Enumerable.Range(3, 4).Select(index => new NativeBattleFinalStage142Actor(index, 2, 0x8C, -1, 3)).ToImmutableArray();
        Assert.Equal(0xFC00, NativeBattleFinalStage142.UpdateTarget3(state, actors).TargetRatio);
        Assert.Throws<NotSupportedException>(() => NativeBattleFinalStage142.UpdateTarget3(state, actors.SetItem(0, actors[0] with { MaximumHp = 0 })));
    }

    [Fact]
    public void Overlay_banks_preserve_untouched_and_unknown_bytes_across_snapshot_and_reentry()
    {
        NativeBattleFinalStage142State state = NativeBattleFinalStage142State.FromColdProcess().WriteOverlay(0x1D9A020, [1, 2, 3], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Unknown]);
        Assert.Equal(new byte[] { 1, 0 }, state.ReadOverlay(0x1D9A020, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadOverlay(0x1D9A022, 1));
        NativeBattleFinalStage142State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFinalStage142State>(NativeBattleTestSnapshots.Json(state))!;
        Assert.Equal(new byte[] { 1, 0 }, restored.ReadOverlay(0x1D9A020, 2));
        Assert.Equal(new byte[] { 0, 0 }, restored.ReadOverlay(0x1D9A420, 2));
        Assert.Throws<InvalidDataException>(() => restored.ReadOverlay(0x1D9A41F, 2));
    }

    private static NativeBattleStageState Stage() => NativeBattleStageState.FromColdProcess() with { TextureAnimations = ImmutableArray<byte>.Empty };
}
