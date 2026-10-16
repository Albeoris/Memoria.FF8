using Memoria.FF8.Media;

namespace Memoria.FF8;

/// <summary>Maps presentation timestamps onto the original movie callback clock without changing saved native frame ownership.</summary>
internal sealed class MovieFrameTimeline
{
    private readonly MovieInformation information;
    private bool published;

    public MovieFrameTimeline(MovieInformation information, int frame)
    {
        this.information = information;
        Frame = Math.Clamp(frame, 0, information.FrameCount - 1);
    }

    public int Frame { get; private set; }

    public IEnumerable<int> AdvanceTo(double seconds)
    {
        int first = published ? Frame + 1 : Frame;
        int target = FrameAt(seconds, information);
        for (int frame = first; frame <= target; frame++)
        {
            Frame = frame;
            published = true;
            yield return frame;
        }
    }

    public static int FrameAt(double seconds, MovieInformation information)
    {
        if (!double.IsFinite(seconds) || !double.IsFinite(information.FramesPerSecond) || information.FramesPerSecond <= 0 || information.FrameCount < 1)
        {
            throw new InvalidDataException("Movie timing requires finite timestamps, positive detected FPS and an actual frame count.");
        }
        return Math.Clamp((int)Math.Floor(seconds * information.FramesPerSecond + 1e-9), 0, information.FrameCount - 1);
    }

}
