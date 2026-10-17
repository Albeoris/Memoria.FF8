using System.Diagnostics;
using Memoria.FF8.Media;

namespace Memoria.FF8;

internal sealed record MovieCompletionFrame(byte[] Pixels, int Width, int Height, int FrameIndex)
{
    public static MovieCompletionFrame Read(string toolsDirectory, string path, MovieInformation information, double seconds)
    {
        int frame = MovieFrameTimeline.FrameAt(seconds, information);
        using FfmpegMovieDecoder decoder = new(toolsDirectory, path, frame / information.FramesPerSecond, information);
        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? decoded))
            {
                return new(decoded!.Rgba, information.Width, information.Height, frame);
            }
            if (decoder.Error is string error)
            {
                throw new InvalidDataException(error);
            }
            if (decoder.VideoFinished)
            {
                throw new InvalidDataException("Movie completion seek produced no decoded frame.");
            }
            Thread.Sleep(1);
        }
        throw new TimeoutException("Movie completion frame did not arrive within its presentation budget.");
    }
}
