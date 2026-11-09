namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
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
