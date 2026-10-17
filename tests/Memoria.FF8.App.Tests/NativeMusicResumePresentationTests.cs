using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMusicResumePresentationTests
{
    [Fact]
    public void Stopped_modes_do_not_restart_track_zero_or_read_unneeded_gain_and_identity()
    {
        Assert.Empty(Trace(new(null, null, null, null) { Mode0 = NativeMusicPlaybackMode.Stopped, Mode1 = NativeMusicPlaybackMode.Stopped }));
    }

    [Theory]
    [InlineData(0, NativeMusicPlaybackMode.Midi)]
    [InlineData(17, NativeMusicPlaybackMode.Midi)]
    [InlineData(3, NativeMusicPlaybackMode.Wave)]
    public void Selected_primary_uses_the_retained_decoded_identity_and_full_gain(int id, NativeMusicPlaybackMode mode)
    {
        Assert.Equal(new[] { $"0:{id}:63" }, Trace(new(63, null, id, null) { Mode0 = mode, Mode1 = NativeMusicPlaybackMode.Stopped }));
    }

    [Fact]
    public void Distinct_native_backends_keep_both_physical_slots()
    {
        Assert.Equal(new[] { "0:17:63", "1:3:42" }, Trace(new(63, 42, 17, 3) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Wave }));
    }

    [Theory]
    [InlineData(NativeMusicPlaybackMode.Midi, 17, 41)]
    [InlineData(NativeMusicPlaybackMode.Wave, 3, 6)]
    public void Two_routes_to_the_same_shared_backend_do_not_choose_a_track_by_slot_order(NativeMusicPlaybackMode mode, int first, int second)
    {
        Assert.Throws<NotSupportedException>(() => Trace(new(63, 42, first, second) { Mode0 = mode, Mode1 = mode }));
    }

    [Fact]
    public void Legacy_missing_owner_or_mode_remains_unknown()
    {
        Assert.Throws<NotSupportedException>(() => Trace(null));
        Assert.Throws<NotSupportedException>(() => Trace(new(63, 0, 17, 0)));
        Assert.Throws<NotSupportedException>(() => Trace(new(63, 0, 17, 0) { Mode0 = NativeMusicPlaybackMode.Midi }));
    }

    [Theory]
    [InlineData(43, 63)]
    [InlineData(-1, 63)]
    [InlineData(117, 63)]
    [InlineData(17, 300)]
    public void Unowned_concert_selection_failed_resources_and_outside_table_gains_remain_explicit(int id, int gain)
    {
        Assert.Throws<NotSupportedException>(() => Trace(new(gain, 0, id, 0) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Stopped }));
    }

    [Fact]
    public void All_slots_are_preflighted_before_any_device_callback()
    {
        List<string> calls = [];
        NativeMusicPlaybackState malformed = new(63, 42, 17, 6) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Midi };
        Assert.Throws<NotSupportedException>(() => Dispatch(malformed, calls));
        Assert.Empty(calls);
        malformed = malformed with { Mode1 = NativeMusicPlaybackMode.Wave, Gain1 = null };
        Assert.Throws<NotSupportedException>(() => Dispatch(malformed, calls));
        Assert.Empty(calls);
        malformed = malformed with { Gain1 = 42, CurrentMusicId1 = 41 };
        Assert.Throws<InvalidDataException>(() => Dispatch(malformed, calls));
        Assert.Empty(calls);
    }

    private static List<string> Trace(NativeMusicPlaybackState? playback)
    {
        List<string> calls = [];
        Dispatch(playback, calls);
        return calls;
    }

    private static void Dispatch(NativeMusicPlaybackState? playback, List<string> calls)
    {
        FieldAudioPresentation.DispatchNativeMusicResume(playback, (slot, id, gain) => calls.Add($"{slot}:{id}:{gain}"));
    }
}
