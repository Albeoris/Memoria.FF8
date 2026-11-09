using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

/// <summary>Original movie identity and optional native camera member; video and PCM decoding belong to the presentation backend.</summary>
public sealed record Ff7MovieResources(string Path, IReadOnlyList<PcMovieCamera>? Cameras)
{
    public static Ff7MovieResources Load(Ff7Installation installation, int movieId, Ff7PcExecutableData? native = null)
    {
        ArgumentNullException.ThrowIfNull(installation);
        native ??= Ff7PcExecutableData.Load(installation);
        if ((uint)movieId >= native.MovieFiles.Count)
        {
            throw new InvalidDataException("The FFVII movie index is outside its original execution-data table.");
        }
        string name = native.MovieFiles[movieId];
        string path = System.IO.Path.Combine(installation.GetRequired(Ff7ResourceRole.Movies).Path, name);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MovieCameraArchive).Path);
        IReadOnlyList<LgpEntry> members = archive.Find(System.IO.Path.ChangeExtension(name, ".cam"));
        IReadOnlyList<PcMovieCamera>? cameras = members.Count switch
        {
            0 => null,
            1 => PcMovieCameras.Read(archive.ReadEntry(members[0])),
            _ => throw new InvalidDataException("The FFVII movie identity has ambiguous native camera members.")
        };
        return new(path, cameras);
    }
}
