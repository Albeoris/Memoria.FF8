using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMusicCommandsTests
{
    [Theory]
    [InlineData(0, 0x1CE075Cu)]
    [InlineData(1, 0x1CDC750u)]
    public void Initialization_selects_the_physical_buffer_without_clearing_unwritten_request_operands(byte bank, uint address)
    {
        NativeBattleMusicProcessState state = Create() with { ReturnBank = bank, Commands = new(true, 4, 300, 90, 0, true) };
        NativeBattleMusicProcessState initialized = state.InitializeCommands501C60();
        Assert.Equal(new NativeBattleMusicCommandState(false, 4, 300, 90, address, false), initialized.Commands);
        Assert.Same(state.Buffers, initialized.Buffers);
        Assert.Equal(bank, initialized.ReturnBank);
    }

    [Fact]
    public void Producers_overwrite_one_singleton_instead_of_appending_a_command_queue()
    {
        NativeBattleMusicProcessState state = Create().InitializeCommands501C60().QueueGain501B10(90, 300).QueueFade501B40(3).QueueStop501A70();
        Assert.Equal(new NativeBattleMusicCommandState(true, 1, 3, 300, 0x1CE075C, false), state.Commands);
        NativeGlobalSoundProcess sound = Sound(new(63, 42, 17, 3));
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(sound);
        Assert.Equal((byte)1, step.Receipt!.Command);
        Assert.Equal(new NativeMusicPlaybackState(63, 42, 0, 0) { Mode0 = NativeMusicPlaybackMode.Stopped, Mode1 = NativeMusicPlaybackMode.Stopped }, step.Sound.MusicPlayback);
        Assert.False(step.State.Commands!.Pending);
        Assert.Equal(3, step.State.Commands.Duration);
        Assert.Equal(300, step.State.Commands.TargetGain);
    }

    [Fact]
    public void Play_reads_the_selected_buffer_and_old_gain_then_publishes_the_actual_return_word()
    {
        NativeBattleMusicProcessState state = Create().InitializeCommands501C60().QueuePlay501A70();
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(Sound(new(63, 42, 5, 6)));
        Assert.Equal((ushort)0, step.State.ReturnChannel);
        Assert.Equal(new NativeMusicPlaybackDecision(0, 3, true, true, false, 63, null), step.Receipt!.Playback);
        Assert.True(step.Receipt.PauseOutput);
        Assert.False(step.Receipt.ResumeOutput);
        Assert.True(step.State.Commands!.Loading);
        Assert.Equal(63, step.Sound.MusicPlayback!.Gain0);
        Assert.Equal(3, step.Sound.MusicPlayback.CurrentMusicId0);
        Assert.Equal(NativeMusicPlaybackMode.Wave, step.Sound.MusicPlayback.Mode0);
        Assert.Same(state.Buffers, step.State.Buffers);
    }

    [Fact]
    public void Same_song_still_requests_pause_and_completion_but_does_not_restart_the_device()
    {
        NativeMusicPlaybackState playback = new(63, 42, 3, 6) { Mode0 = NativeMusicPlaybackMode.Wave };
        NativeBattleMusicCommandStep step = Create().InitializeCommands501C60().QueuePlay501A70().DrainCommands501B60(Sound(playback));
        Assert.Equal(new NativeMusicPlaybackDecision(0, 3, false, false, false, 63, null), step.Receipt!.Playback);
        Assert.True(step.Receipt.PauseOutput);
        Assert.Same(playback, step.Sound.MusicPlayback);
    }

    [Theory]
    [InlineData(339, 17)]
    [InlineData(340, 99)]
    public void Target_buffer_landing_ID_reads_the_current_field_only_on_its_original_branch(ushort field, int expected)
    {
        NativeBattleMusicProcessState state = Create() with { ReturnBank = 1 };
        state = state.InitializeCommands501C60().QueuePlay501A70();
        int reads = 0;
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(Sound(new(63, 42, 5, 6)), () =>
        {
            reads++;
            return field;
        });
        Assert.Equal(1, reads);
        Assert.Equal(expected, step.Receipt!.Playback!.MusicId);
        Assert.Throws<NotSupportedException>(() => state.DrainCommands501B60(Sound(new(63, 42, 5, 6))));
    }

    [Fact]
    public void Encoded_zero_still_commits_signed_minus_one_and_does_not_select_a_default_resource()
    {
        NativeBattleMusicProcessState state = Create();
        state = state with { Buffers = state.Buffers.LoadBuffer(0, new(0, NativeMusicBufferProducer.Field52CD10)) };
        NativeBattleMusicCommandStep step = state.InitializeCommands501C60().QueuePlay501A70().DrainCommands501B60(Sound(new(63, 42, 5, 6)), () => throw new InvalidOperationException("This branch does not read current field."));
        Assert.Equal(-1, step.Receipt!.Playback!.MusicId);
        Assert.Equal(-1, step.Sound.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(NativeMusicPlaybackMode.Midi, step.Sound.MusicPlayback.Mode0);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(49, 0, true)]
    [InlineData(50, 50, true)]
    [InlineData(256, 0, true)]
    [InlineData(-1, 255, true)]
    public void Gain_uses_the_full_duration_for_the_branch_then_narrows_only_the_device_duration(int duration, int effective, bool transition)
    {
        NativeBattleMusicProcessState state = Create().QueueGain501B10(duration, 300) with { ReturnChannel = ushort.MaxValue };
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(Sound(new(63, 42, 17, 3)));
        Assert.Equal(300, step.Sound.MusicPlayback!.Gain0);
        Assert.Equal(300, step.Sound.MusicPlayback.Gain1);
        Assert.Equal((ushort)65535, step.Receipt!.GainChannel);
        Assert.Equal(transition ? (int?)0 : null, step.Receipt.InitialGain);
        Assert.Equal(effective, step.Receipt.EffectiveDuration);
        Assert.True(step.Receipt.ResumeOutput);
        Assert.False(step.State.Commands!.Loading);
    }

    [Fact]
    public void Fade3_is_immediate_and_preserves_the_unwritten_loading_flag_and_second_gain()
    {
        NativeBattleMusicProcessState state = Create() with { Commands = NativeBattleMusicCommandState.Unknown() with { Loading = true } };
        NativeBattleMusicCommandStep step = state.QueueFade501B40(3).DrainCommands501B60(Sound(new(63, 42, 17, 3)));
        Assert.Equal(0, step.Sound.MusicPlayback!.Gain0);
        Assert.Equal(42, step.Sound.MusicPlayback.Gain1);
        Assert.Equal(0, step.Receipt!.EffectiveDuration);
        Assert.Null(step.Receipt.InitialGain);
        Assert.True(step.State.Commands!.Loading);
    }

    [Fact]
    public void No_pending_request_does_not_read_unknown_command_operands_or_create_playback_state()
    {
        NativeBattleMusicProcessState state = Create() with { Commands = NativeBattleMusicCommandState.Unknown() with { Pending = false } };
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.UnknownBufferless();
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(sound);
        Assert.Same(state, step.State);
        Assert.Same(sound, step.Sound);
        Assert.Null(step.Receipt);
        Assert.Throws<NotSupportedException>(() => (state with { Commands = null }).DrainCommands501B60(sound));
        Assert.Throws<NotSupportedException>(() => (state with { Commands = NativeBattleMusicCommandState.Unknown() }).DrainCommands501B60(sound));
    }

    [Fact]
    public void Native_default_command_clears_only_pending_and_does_not_read_music()
    {
        NativeBattleMusicProcessState state = Create() with { Commands = new(true, 255, null, null, null, null) };
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.UnknownBufferless();
        NativeBattleMusicCommandStep step = state.DrainCommands501B60(sound);
        Assert.Equal(state with { Commands = state.Commands! with { Pending = false } }, step.State);
        Assert.Same(sound, step.Sound);
        Assert.Equal((byte)255, step.Receipt!.Command);
    }

    [Fact]
    public void Missing_reached_operands_refuse_without_mutating_the_original_owners()
    {
        NativeBattleMusicProcessState state = Create().QueuePlay501A70();
        NativeGlobalSoundProcess sound = Sound(new(null, 42, 17, 3));
        string before = JsonSerializer.Serialize(state);
        Assert.Throws<NotSupportedException>(() => state.DrainCommands501B60(sound));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.Throws<NotSupportedException>(() => (state with { Commands = state.Commands! with { TargetBufferAddress = 0x1CE075C } }).DrainCommands501B60(sound));
        Assert.Throws<InvalidDataException>(() => (state with { Commands = state.Commands! with { TargetBufferAddress = 0 } }).DrainCommands501B60(sound));
        Assert.Throws<NotSupportedException>(() => (Create().QueueGain501B10(0, 127) with { ReturnChannel = null }).DrainCommands501B60(sound));
    }

    [Fact]
    public void Independent_json_resumes_the_same_single_pending_gain_without_replaying_a_previous_play()
    {
        NativeBattleMusicProcessState state = Create().InitializeCommands501C60().QueuePlay501A70().QueueGain501B10(60, 127);
        NativeBattleMusicProcessState restored = JsonSerializer.Deserialize<NativeBattleMusicProcessState>(JsonSerializer.Serialize(state))!;
        NativeGlobalSoundProcess sound = Sound(new(63, 42, 17, 3));
        NativeBattleMusicCommandStep first = state.DrainCommands501B60(sound);
        NativeBattleMusicCommandStep second = restored.DrainCommands501B60(JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(sound))!);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal((byte)3, first.Receipt!.Command);
        Assert.Null(first.Receipt.Playback);
        Assert.Null(first.State.DrainCommands501B60(first.Sound).Receipt);
    }

    private static NativeBattleMusicProcessState Create()
    {
        NativeMusicProcessState buffers = new(1, false, null, null, 0, 255) { Buffer0 = new(4, NativeMusicBufferProducer.Field52CD10), Buffer1 = new(18, NativeMusicBufferProducer.WorldWmset5306E0) };
        return new(buffers, 0, 0);
    }

    private static NativeGlobalSoundProcess Sound(NativeMusicPlaybackState playback)
    {
        return NativeGlobalSoundProcess.UnknownBufferless() with { MusicPlayback = playback };
    }
}
