using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private (long Generation, long? Request, int Id, byte Disc)? nativeMovieCameraIdentity;
    private Ff7MovieResources? nativeMovieResources;
    private Ff7MovieCameraTrack? nativeMovieCameraTrack;

    public byte NativeMovieDisc => Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0);

    public Ff7MovieResources ReadMovieResources(int movieId)
    {
        return Ff7MovieResources.Load(installation, movieId, executable, NativeMovieDisc);
    }

    private Ff7MovieResources NativeMovieResources()
    {
        Ff7Movie movie = Host.Process.Movie is { Playing: true } active ? active : throw new InvalidOperationException("Native movie camera resources require an active owner.");
        var identity = (FieldGeneration, movie.RequestId, movie.PreparedId, NativeMovieDisc);
        if (nativeMovieResources is null || nativeMovieCameraIdentity != identity)
        {
            Ff7MovieResources resources = ReadMovieResources(movie.PreparedId);
            nativeMovieResources = resources;
            nativeMovieCameraTrack = new(resources.Cameras ?? []);
            nativeMovieCameraIdentity = identity;
        }
        return nativeMovieResources;
    }

    private void ReleaseUnloadedMovieResources()
    {
        if (Host.Process.Movie is null && nativeMovieResources is not null)
        {
            nativeMovieResources = null;
            nativeMovieCameraTrack = null;
            nativeMovieCameraIdentity = null;
        }
    }

    private int ReadNativeMovieCameraRecordCount() => NativeMovieResources().Cameras?.Count ?? 0;

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
