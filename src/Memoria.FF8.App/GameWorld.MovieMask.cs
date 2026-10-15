using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Dictionary<int, NativeMovieCameraFlags> movieCameraFlags = [];

    private NativeMovieCameraFlags ReadNativeMovieCameraFlags(int movie)
    {
        if (!movieCameraFlags.TryGetValue(movie, out NativeMovieCameraFlags? flags))
        {
            string path = Path.Combine(assets.GameDirectory, "Data", "movies", $"disc00_{movie:00}.cam");
            flags = NativeMovieCameraReader.Read(File.ReadAllBytes(path));
            movieCameraFlags.Add(movie, flags);
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
        Events.RequireSupportedMovieMaskRender(inf14, frame);
    }
}
