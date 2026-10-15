using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Dictionary<(byte Disc, int Movie), NativeMovieCameraFlags> movieCameraFlags = [];
    private readonly Dictionary<(byte Disc, int Movie), byte[]> movieCameraBytes = [];
    private FieldRenderer? movieRenderer;
    private FieldAssets? movieRendererField;

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
        FieldCamera camera = FieldCamera.Read(movieCameraBytes[(source.DiscIndex, movie)].AsSpan(8 + record * 44, 40).ToArray());
        //4767B0 submits actors before the late55A620 CAM flag update. BYTE490D (bit64)
        //and209A7A0 (bits1/32) therefore come from the previous consumed CAM record.
        byte previousFlags = flags.Flags[record - 1];
        IEnumerable<ActorVisual> actors = (previousFlags & 0x61) == 0 ? Events.Actors.Select(CreateVisual) : [];
        FieldMovieMask? mask = currentField.Information?.NativeMovieMaskFlag14 == 1 && playback.DrawValue == 0 ? currentField.MovieMask : null;
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
