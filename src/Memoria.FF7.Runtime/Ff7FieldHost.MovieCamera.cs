namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public Func<int>? NativeMovieCameraRecordCount { get; set; }

    public void PublishCurrentNativeMovieCameraFrame()
    {
        if (Process.Movie is { Playing: true, CameraDisabled: false } movie && !Process.MovieCameraDisabled && NativeMovieCameraRecordCount is { } count)
        {
            PublishMovieCameraFrame(movie.Frame, count());
        }
    }

    private void InitializeMovieCameraConsumption()
    {
        if (Process.Movie is Ff7Movie { CameraConsumption: null } movie)
        {
            long precedingWords = Process.MovieCameraDisabled || movie.CameraDisabled ? 0 : checked((long)movie.Frame * 10);
            Process = Process with { Movie = movie with { CameraConsumption = new(-1, precedingWords, precedingWords) } };
            if (movie.Playing && movie.Frame > 0)
            {
                Console.Error.WriteLine("FFVII compatibility: legacy movie camera consumption was inferred from the saved frame and camera gate; video position, progress and script PCs are preserved.");
            }
        }
    }

    public long? PublishMovieCameraFrame(int frame, int cameraRecordCount)
    {
        if (frame < 0 || cameraRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(frame < 0 ? nameof(frame) : nameof(cameraRecordCount));
        }
        if (Process.Movie is not Ff7Movie { Playing: true } movie || movie.CameraDisabled || Process.MovieCameraDisabled)
        {
            return null;
        }
        Ff7MovieCameraConsumption consumption = movie.CameraConsumption ?? throw new InvalidOperationException("Movie camera consumption must be initialized before presentation.");
        if (frame < consumption.Frame)
        {
            throw new InvalidOperationException("Movie camera frames must advance monotonically.");
        }
        if (frame == consumption.Frame)
        {
            return consumption.WordsBeforeRead;
        }
        long before = consumption.WordsAfterRead;
        long after = before > cameraRecordCount * 10L - 2 ? before : checked(before + 10);
        Process = Process with { Movie = movie with { CameraConsumption = new(frame, before, after) } };
        return before;
    }
}
