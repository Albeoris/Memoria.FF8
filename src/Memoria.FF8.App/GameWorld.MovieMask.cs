using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Dictionary<(byte Disc, int Movie), NativeMovieCameraFlags> movieCameraFlags = [];
    private readonly Dictionary<(byte Disc, int Movie), byte[]> movieCameraBytes = [];
    private readonly Dictionary<string, MoviePresentationCamera> presentationCameras = new(StringComparer.OrdinalIgnoreCase);
    private FieldRenderer? movieRenderer;
    private FieldAssets? movieRendererField;

    private (NativeMovieSource Source, NativeMovieMaskPlayback Playback, NativeMovieCameraFlags Flags) RequireNativeMovieCameraPhase()
    {
        NativeMovieSource source = MovieClock.RequireSource(session.State);
        NativeMovieMaskPlayback playback = Events.Scene.NativeMovieMask?.Playback ?? throw new NotSupportedException("Native movie camera requires its executed CAM phase.");
        NativeMovieCameraFlags flags = ReadNativeMovieCameraFlags(source.DiscIndex, source.MovieIndex);
        if (playback.MovieIndex != source.MovieIndex || playback.CameraSha256 != flags.SourceSha256 || (uint)playback.AppliedRecord >= flags.Flags.Length)
        {
            throw new InvalidDataException("Native movie camera differs from its authenticated CAM phase.");
        }
        return (source, playback, flags);
    }

    private FieldCamera ReadNativeFootstepCamera(FieldAssets field, bool beforeCameraSelection)
    {
        if (!session.IsMoviePlaying)
        {
            return field.GetCamera(Events.Scene.CameraIndex);
        }
        (NativeMovieSource source, NativeMovieMaskPlayback playback, _) = RequireNativeMovieCameraPhase();
        //529FF0 uses the focal retained from the preceding selection. MOVIE's
        //record0 presentation precedes the first CAM selection in4767B0.
        if (beforeCameraSelection && playback.AppliedRecord == 0)
        {
            return field.GetCamera(Events.Scene.CameraIndex);
        }
        int record = playback.AppliedRecord + (beforeCameraSelection ? 0 : 1);
        return ReadNativeMovieCamera(movieCameraBytes[(source.DiscIndex, source.MovieIndex)], record);
    }

    internal static FieldCamera ReadNativeMovieCamera(ReadOnlySpan<byte> bytes, int record)
    {
        if (record < 0 || 8L + (record + 1L) * 44 > bytes.Length)
        {
            throw new InvalidDataException("Native movie camera record is outside its original CAM bank.");
        }
        return FieldCamera.Read(bytes.Slice(8 + record * 44, 40).ToArray());
    }

    private NativeMovieCameraFlags ReadNativeMovieCameraFlags(byte disc, int movie)
    {
        if (!movieCameraFlags.TryGetValue((disc, movie), out NativeMovieCameraFlags? flags))
        {
            string path = MovieClock.GetCameraPath(assets.GameDirectory, disc, movie);
            byte[] bytes = File.ReadAllBytes(path);
            flags = NativeMovieCameraReader.Read(bytes);
            movieCameraFlags.Add((disc, movie), flags);
            movieCameraBytes.Add((disc, movie), bytes);
        }
        return flags;
    }

    internal void RequireSupportedMovieMaskRender()
    {
        if (!session.IsMoviePlaying)
        {
            return;
        }
        byte inf14 = Field?.Information?.NativeMovieMaskFlag14 ?? throw new NotSupportedException("Native movie rendering requires its actual field INF byte14.");
        int frame = session.State.MoviePlayback?.FrameIndex ?? Events.Scene.NativeMovieMask?.Playback?.DrawRecord ?? throw new NotSupportedException("Native movie rendering requires its decoded or initial native draw frame.");
        if (Events.RequireSupportedMovieMaskRender(inf14, frame) && Field?.MovieMask is null)
        {
            throw new NotSupportedException("Native movie rendering requires its original INF-selected MSK triangle bank.");
        }
    }

    internal byte[] RenderMovieFrame(ReadOnlySpan<byte> decodedPixels, int decodedWidth, int decodedHeight)
    {
        RequireSupportedMovieMaskRender();
        FieldAssets currentField = Field ?? throw new NotSupportedException("Movie field compositing requires the allocated field.");
        NativeMovieSource source = MovieClock.RequireSource(session.State);
        int movie = source.MovieIndex;
        NativeMovieCameraFlags flags = ReadNativeMovieCameraFlags(source.DiscIndex, movie);
        NativeMovieMaskPlayback playback = Events.Scene.NativeMovieMask?.Playback ?? throw new NotSupportedException("Movie field compositing requires its current CAM draw receipt.");
        int record = playback.DrawRecord ?? throw new NotSupportedException("Movie field compositing requires its current CAM draw record.");
        if (playback.CameraSha256 != flags.SourceSha256 || (uint)record >= flags.Flags.Length || record < 1)
        {
            throw new InvalidDataException("Movie field compositing differs from its authenticated CAM phase.");
        }
        byte[] cameraBytes = movieCameraBytes[(source.DiscIndex, movie)];
        byte drawValue = playback.DrawValue ?? throw new InvalidDataException("Movie rendering has no retained draw-control phase.");
        string originalPath = MovieClock.GetCameraPath(assets.GameDirectory, source.DiscIndex, movie);
        string presentationPath = movieClock.CameraPath;
        if (!string.Equals(Path.GetFullPath(originalPath), Path.GetFullPath(presentationPath), StringComparison.OrdinalIgnoreCase))
        {
            if (!presentationCameras.TryGetValue(presentationPath, out MoviePresentationCamera? presentation))
            {
                presentation = new MoviePresentationCamera(File.ReadAllBytes(presentationPath), flags, movieClock.Prepare(), movieClock.PreparePresentation());
                presentationCameras.Add(presentationPath, presentation);
            }
            Memoria.FF8.Media.MovieInformation nativeInformation = movieClock.Prepare();
            Memoria.FF8.Media.MovieInformation mediaInformation = movieClock.PreparePresentation();
            int mediaFrame = movieClock.PresentationFrame((session.State.MoviePlayback?.FrameIndex ?? record) / nativeInformation.FramesPerSecond, mediaInformation);
            record = presentation.DrawRecord(mediaFrame);
            cameraBytes = presentation.Bytes;
            flags = presentation.Flags;
            drawValue = presentation.DrawValue(record);
        }
        FieldCamera camera = ReadNativeMovieCamera(cameraBytes, record);
        //4767B0 submits actors before the late55A620 CAM flag update. BYTE490D (bit64)
        //and209A7A0 (bits1/32) therefore come from the previous consumed CAM record.
        byte previousFlags = flags.Flags[record - 1];
        IEnumerable<ActorVisual> actors = (previousFlags & 0x61) == 0 ? Events.Actors.Select(CreateVisual) : [];
        FieldMovieMask? mask = currentField.Information?.NativeMovieMaskFlag14 == 1 && drawValue == 0 ? currentField.MovieMask ?? throw new NotSupportedException("Movie presentation requires its original INF-selected MSK triangle bank.") : null;
        if (movieRenderer is null || movieRendererField != currentField || movieRenderer.OutputWidth != renderWidth || movieRenderer.OutputHeight != renderHeight)
        {
            movieRenderer = new FieldRenderer(currentField, renderWidth, renderHeight, nativeMovieViewport: true);
            movieRendererField = currentField;
        }
        //4767B0's movie branch returns before47B980 submits the field fade packets.
        //The retained ordinary-field sample is state for the next field callback,
        //not a movie overlay (the opening legitimately retains a black sample).
        return movieRenderer.RenderMovieFrame(decodedPixels, decodedWidth, decodedHeight, camera, actors, mask);
    }
}
