using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed class Ff7MovieCameraTrack(IReadOnlyList<PcMovieCamera> cameras)
{
    private static readonly PcMovieCamera empty = new(new PcFieldCamera(default, default, default, 0, default, 0, 0), 0);
    private int publishedFrame = -1;
    private long consumedWords;

    public void Publish(int frame, long wordsBeforeRead)
    {
        if (frame < 0 || frame < publishedFrame || wordsBeforeRead < 0 || wordsBeforeRead % 10 != 0)
        {
            throw new InvalidDataException("FFVII movie camera timeline must advance monotonically.");
        }
        if (frame == publishedFrame && wordsBeforeRead == consumedWords)
        {
            return;
        }
        // Native camera ownership supplies the saved read budget independently of the absolute movie frame.
        consumedWords = wordsBeforeRead;
        publishedFrame = frame;
    }

    public PcMovieCamera CameraAt(int frame)
    {
        if (frame != publishedFrame)
        {
            throw new InvalidOperationException("FFVII camera lookup requires its published logical movie frame.");
        }
        return SnapshotAt(frame, consumedWords);
    }

    public PcMovieCamera SnapshotAt(int frame, long wordsBeforeRead)
    {
        if (frame < 0 || wordsBeforeRead < 0 || wordsBeforeRead % 10 != 0)
        {
            throw new InvalidDataException("A movie camera snapshot requires its native frame and whole-record read budget.");
        }
        long lastReadableWords = cameras.Count * 10L - 2;
        return wordsBeforeRead > lastReadableWords || (uint)frame >= cameras.Count ? empty : cameras[frame];
    }
}
