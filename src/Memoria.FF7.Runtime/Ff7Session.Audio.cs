using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<int, Ff7NativeMusicTimeline> nativeMusicTimelines = [];

    private Ff7NativeMusicTimeline ReadNativeMusicTimeline(int track)
    {
        if (!nativeMusicTimelines.TryGetValue(track, out Ff7NativeMusicTimeline? timeline))
        {
            timeline = Ff7NativeMusicTimeline.Load(installation, track, executable);
            nativeMusicTimelines.Add(track, timeline);
        }
        return timeline;
    }

    public void AdvanceAudioClock(TimeSpan elapsed, bool paused = false)
    {
        Host.AdvanceAudioClock(elapsed, paused);
    }

    private void AdvanceAudio()
    {
        if (Field.Name.Equals("md1stin", StringComparison.OrdinalIgnoreCase) && Host.Process.Movie is { Frame: 1760 })
        {
            // NG-021: The original opening field starts native track 2 at movie frame 1760.
            Host.PlayNativeMusic(2);
        }
    }
}
