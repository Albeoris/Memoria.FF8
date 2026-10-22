using System.Diagnostics;
using Memoria.FF8.Media;

namespace Memoria.FF8;

internal sealed record MovieCompletionFrame(byte[] Pixels, int Width, int Height, int FrameIndex)
{
    public static MovieCompletionFrame Read(string toolsDirectory, string path, MovieInformation information, double seconds)
    {
        int frame = MovieFrameTimeline.FrameAt(seconds, information);
        Stopwatch timeout = Stopwatch.StartNew();
        bool recovering = false;
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            using FfmpegMovieDecoder decoder = new(toolsDirectory, path, frame / information.FramesPerSecond, information);
            MovieCompletionFrame? last = null;
            while (timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                while (decoder.TryGetAudio(out _))
                {
                }
                while (timeout.Elapsed < TimeSpan.FromSeconds(10) && decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? decoded))
                {
                    last = new(decoded!.Rgba, information.Width, information.Height, checked(frame + (int)decoded.Index));
                    if (!recovering)
                    {
                        return last;
                    }
                }
                if (decoder.Error is string error)
                {
                    throw new InvalidDataException(error);
                }
                if (decoder.VideoFinished)
                {
                    if (last is not null)
                    {
                        return last;
                    }
                    if (information.FrameCount > 0 || frame == 0)
                    {
                        throw new InvalidDataException("Movie completion seek produced no decoded frame.");
                    }
                    // Container duration can outlast video. An empty presentation seek
                    // backs up until actual images can establish the final video frame.
                    frame /= 2;
                    recovering = true;
                    break;
                }
                Thread.Sleep(1);
            }
        }
        throw new TimeoutException("Movie completion frame did not arrive within its presentation budget.");
    }
}
