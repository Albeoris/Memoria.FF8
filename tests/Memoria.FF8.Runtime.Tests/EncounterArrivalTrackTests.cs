using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterArrivalTrackTests
{
    [Theory]
    [InlineData(100, 8, 9, 0f, false)]
    [InlineData(110, 18, 19, 0.5f, false)]
    [InlineData(120, 28, 29, 1f, true)]
    [InlineData(200, 28, 29, 1f, true)]
    public void Uses_source_clock_and_translates_zero_based_source_frames(long callback, byte source, byte native, float opacity, bool complete)
    {
        EncounterArrivalTrack track = new("copy", 2, 8, 28, 100);
        Assert.Equal(new(2, source, native, opacity, complete), track.Sample(callback));
    }

    [Fact]
    public void Resume_reproduces_phase_without_mutating_track_or_restarting_arrival()
    {
        EncounterArrivalTrack track = new("copy", 2, 8, 28, 100, 2);
        EncounterArrivalTrack restored = JsonSerializer.Deserialize<EncounterArrivalTrack>(JsonSerializer.Serialize(track))!;
        Assert.Equal(track.Sample(121), restored.Sample(121));
        Assert.Equal(0.525f, restored.Sample(121).Opacity);
        Assert.False(restored.Sample(121).Complete);
        Assert.Throws<InvalidDataException>(() => track.Sample(99));
    }

    [Fact]
    public void Accepted_pause_callbacks_freeze_source_time_and_restore_keeps_the_last_phase()
    {
        EncounterArrivalState state = new(new("copy", 2, 8, 28, 100), new());
        state = state.Advance(100, false).Advance(101, false).Advance(102, true).Advance(103, true);
        Assert.Equal(1, state.ElapsedCallbacks);
        Assert.Equal((byte)9, state.Sample.SourceFrame);
        EncounterArrivalState restored = JsonSerializer.Deserialize<EncounterArrivalState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state.Advance(104, false).Sample, restored.Advance(104, false).Sample);
        Assert.Equal(2, restored.Advance(104, false).ElapsedCallbacks);
        Assert.Same(state, state.Advance(103, false));
    }

    [Fact]
    public void Final_source_pose_is_presented_before_returning_control_to_native_idle()
    {
        EncounterArrivalState state = new(new("copy", 2, 8, 28, 100), new());
        for (int callback = 100; callback <= 120; callback++)
        {
            state = state.Advance(callback, false);
        }
        Assert.True(state.Sample.Complete);
        Assert.Equal(1, state.Sample.Opacity);
        Assert.False(state.Complete);
        Assert.True(state.Advance(121, false).Complete);
    }
}
