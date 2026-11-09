using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private (long Generation, long? Request, int Id)? nativeMovieCameraIdentity;
    private Ff7MovieResources? nativeMovieResources;
    private Ff7MovieCameraTrack? nativeMovieCameraTrack;

    private Ff7MovieResources NativeMovieResources()
    {
        Ff7Movie movie = Host.Process.Movie is { Playing: true } active ? active : throw new InvalidOperationException("Native movie camera resources require an active owner.");
        var identity = (FieldGeneration, movie.RequestId, movie.PreparedId);
        if (nativeMovieResources is null || nativeMovieCameraIdentity != identity)
        {
            Ff7MovieResources resources = Ff7MovieResources.Load(installation, movie.PreparedId, executable);
            nativeMovieResources = resources;
            nativeMovieCameraTrack = resources.Cameras is null ? null : new(resources.Cameras);
            nativeMovieCameraIdentity = identity;
        }
        return nativeMovieResources;
    }

    private int ReadNativeMovieCameraRecordCount() => NativeMovieResources().Cameras?.Count ?? throw new NotSupportedException("An enabled movie camera requires the native behavior of its missing original member.");

    public PcFieldCamera? ReadNativeMovieCamera()
    {
        if (Host.Process.Movie is not { Playing: true } movie || movie.CameraDisabled || Host.Process.MovieCameraDisabled)
        {
            return Field.Cameras[0];
        }
        _ = NativeMovieResources();
        return Ff7NativeMovieCamera.ResolvePresentationCamera(Host.Process, Field.Cameras[0], nativeMovieCameraTrack is null ? null : nativeMovieCameraTrack.SnapshotAt);
    }
}
