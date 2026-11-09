using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeMovieCamera
{
    public static PcFieldCamera? ResolvePresentationCamera(Ff7FieldProcess process, PcFieldCamera fieldCamera, Func<int, long, PcMovieCamera?>? snapshot)
    {
        if (process.Movie is not { Playing: true } movie || movie.CameraDisabled || process.MovieCameraDisabled)
        {
            return fieldCamera;
        }
        Ff7MovieCameraConsumption consumption = movie.CameraConsumption ?? throw new InvalidOperationException("Native model presentation requires its saved movie camera owner.");
        if (movie.Frame < consumption.Frame)
        {
            throw new InvalidDataException("Native model presentation cannot read before its saved movie camera owner.");
        }
        long words = consumption.Frame == movie.Frame ? consumption.WordsBeforeRead : consumption.WordsAfterRead;
        PcMovieCamera? camera = (snapshot ?? throw new NotSupportedException("Native model presentation requires a read-only original movie camera source."))(movie.Frame, words);
        return camera?.Camera ?? throw new NotSupportedException("An enabled movie camera without an original camera member requires its native missing-resource contract.");
    }
}
