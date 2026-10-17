using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMusicDecisionPresentationTests
{
    [Fact]
    public void Already_decoded_landing_identity_keeps_its_original_file_and_negative_identity_has_no_fallback()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "The original authenticated music catalog is required.");
        NativeMusicCatalog catalog = new(directory);
        Assert.Equal("017s-joriku.sgt", Path.GetFileName(catalog.MusicFileResolved(17)));
        Assert.NotEqual(catalog.MusicFile(17, 120), catalog.MusicFileResolved(17));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.MusicFileResolved(-1));
    }

    [Fact]
    public void Plain_play_uses_decoded_identity_and_prior_gain_then_resets_the_final_gain()
    {
        Assert.Equal(new[] { "stop0", "play0:99:63", "gain0:127" }, Trace(new(0, 99, true, true, false, 63, 127)));
    }

    [Fact]
    public void Same_song_does_not_stop_or_play_but_still_executes_the_native_final_gain()
    {
        Assert.Equal(new[] { "gain0:90" }, Trace(new(0, 17, false, false, false, 33, 90)));
    }

    [Fact]
    public void Cross_to_zero_keeps_the_actual_stop_without_inventing_a_play()
    {
        Assert.Equal(new[] { "stop0" }, Trace(new(0, 0, false, true, false, 42, null)));
    }

    [Fact]
    public void Dual_routes_the_second_physical_slot_without_an_extra_gain_reset()
    {
        Assert.Equal(new[] { "stop1", "play1:6:42" }, Trace(new(1, 6, true, true, false, 42, null)));
    }

    [Fact]
    public void World_initial_gain_precedes_stop_play_and_final_gain()
    {
        Assert.Equal(new[] { "gain0:0", "stop0", "play0:38:0", "gain0:127" }, Trace(new(0, 38, true, true, false, 0, 127), 0));
    }

    [Fact]
    public void Concert_and_its_native_early_return_cannot_become_ordinary_song43()
    {
        Assert.Equal(new[] { "stop0", "concert" }, Trace(new(0, 43, false, true, true, null, null)));
        Assert.Empty(Trace(new(0, null, false, false, false, null, null)));
    }

    [Fact]
    public void Signed_negative_decoded_identity_is_not_replaced_with_byte255()
    {
        Assert.Equal(new[] { "stop0", "play0:-1:60" }, Trace(new(0, -1, true, true, false, 60, null)));
    }

    [Fact]
    public void Missing_or_malformed_decisions_refuse_before_device_callbacks()
    {
        Assert.Throws<NotSupportedException>(() => Trace(null));
        Assert.Throws<InvalidDataException>(() => Trace(new(0, 5, true, true, false, null, 127)));
    }

    [Theory]
    [InlineData(1, true, "1")]
    [InlineData(-2, true, "0,1")]
    [InlineData(-1, false, "")]
    public void Stop_uses_the_retained_channel_not_the_script_slot(int channel, bool requested, string expected)
    {
        NativeMusicStop receipt = new(1, 100, 0, 0, 0, 0, 0, 0, channel, requested, GlobalSoundBackendStatus.Muted);
        List<int> stopped = [];
        FieldAudioPresentation.DispatchNativeMusicStop(receipt, stopped.Add);
        Assert.Equal(expected, string.Join(',', stopped));
    }

    [Theory]
    [InlineData(1, false, "1:42")]
    [InlineData(-1, true, "0:42,1:42")]
    public void Volume_routes_actual_channel_and_preserves_minus_one_broadcast(int channel, bool both, string expected)
    {
        NativeMusicVolume receipt = new(1, 100, 0, 0, 0, 0, 0, 0, channel, 42, 42, 0, 0, both, GlobalSoundBackendStatus.Muted);
        List<string> calls = [];
        FieldAudioPresentation.DispatchNativeMusicVolume(receipt, (slot, gain) => calls.Add($"{slot}:{gain}"));
        Assert.Equal(expected, string.Join(',', calls));
    }

    [Fact]
    public void Initialization_retains_ordered_calls_and_never_infers_minus_one_broadcast()
    {
        NativeSoundInitialization receipt = new(1, 100, 0, 0, 0, 0, 127, 127, 127, 0, 32, 1, 29, 64, GlobalSoundBackendStatus.Muted) { MusicGainChannels = ImmutableArray.Create(1, -2) };
        List<string> calls = [];
        FieldAudioPresentation.DispatchNativeMusicInitialization(receipt, (slot, gain) => calls.Add($"{slot}:{gain}"));
        Assert.Equal(new[] { "1:127", "0:127", "1:127" }, calls);
        Assert.Throws<InvalidDataException>(() => FieldAudioPresentation.DispatchNativeMusicInitialization(receipt with { MusicGainChannels = ImmutableArray.Create(-1) }, (_, _) => Assert.Fail("The skipped native channel cannot reach a device callback.")));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.DispatchNativeMusicInitialization(receipt with { MusicGainChannels = null }, (_, _) => Assert.Fail("An unknown receipt cannot reach a device callback.")));
    }

    private static List<string> Trace(NativeMusicPlaybackDecision? decision, int? initial = null)
    {
        List<string> calls = [];
        FieldAudioPresentation.DispatchNativeMusicPlayback(decision, slot => calls.Add($"stop{slot}"), (slot, music, gain) => calls.Add($"play{slot}:{music}:{gain}"), (slot, gain) => calls.Add($"gain{slot}:{gain}"), () => calls.Add("concert"), initial);
        return calls;
    }
}
