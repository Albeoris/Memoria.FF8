using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeMusicBattleFirstLoadTests
{
    [Fact]
    public void First_native_worker_consumes_the_authenticated_process_flag_once_without_changing_playback()
    {
        NativeMusicPlaybackState before = NativeMusicPlaybackState.FromColdProcess().PlayEncoded46B530(0, 4, 63);
        NativeMusicPlaybackState after = before.ConsumeBattleFirstLoad501A70(out bool toggle);
        Assert.True(toggle);
        Assert.Equal(before with { BattleFirstLoadB8B7D4 = false }, after);
        Assert.Same(after, after.ConsumeBattleFirstLoad501A70(out toggle));
        Assert.False(toggle);
        Assert.False(after.IsColdProcess);
    }

    [Fact]
    public void Stop_gain_and_other_song_selection_do_not_reinitialize_the_process_flag()
    {
        NativeMusicPlaybackState state = NativeMusicPlaybackState.FromColdProcess().ConsumeBattleFirstLoad501A70(out _);
        state = state.Stop46B800(2).SetGain46BB40(0, 127).PlayEncoded46B530(0, 4, 63);
        Assert.False(state.BattleFirstLoadB8B7D4);
        state = state.ConsumeBattleFirstLoad501A70(out bool toggle);
        Assert.False(toggle);
    }

    [Fact]
    public void Old_or_unknown_owner_does_not_acquire_a_cold_first_load_from_known_gains_or_modes()
    {
        NativeMusicPlaybackState state = new(0, 0, 0, 0) { Mode0 = NativeMusicPlaybackMode.Stopped, Mode1 = NativeMusicPlaybackMode.Stopped };
        Assert.False(state.IsColdProcess);
        Assert.Throws<NotSupportedException>(() => state.ConsumeBattleFirstLoad501A70(out _));
        Assert.Throws<NotSupportedException>(() => NativeMusicPlaybackState.Unknown().ConsumeBattleFirstLoad501A70(out _));
        Assert.DoesNotContain("BattleFirstLoadB8B7D4", JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Independent_json_preserves_the_next_actual_worker_decision(bool flag)
    {
        NativeMusicPlaybackState state = new(63, 42, 17, 3) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Wave, BattleFirstLoadB8B7D4 = flag };
        NativeMusicPlaybackState restored = JsonSerializer.Deserialize<NativeMusicPlaybackState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state.ConsumeBattleFirstLoad501A70(out bool first), restored.ConsumeBattleFirstLoad501A70(out bool second));
        Assert.Equal(flag, first);
        Assert.Equal(first, second);
        JsonObject legacy = JsonSerializer.SerializeToNode(state)!.AsObject();
        legacy.Remove("BattleFirstLoadB8B7D4");
        Assert.Null(legacy.Deserialize<NativeMusicPlaybackState>()!.BattleFirstLoadB8B7D4);
    }
}
