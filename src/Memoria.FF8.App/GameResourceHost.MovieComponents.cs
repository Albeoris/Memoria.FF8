using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    internal sealed record MovieComponent(ModFile Video, ModFile Camera, MovieSubtitleTrack Track, int Priority);

    private sealed record MovieComponentDeclaration(string Video, int Priority);
    private sealed record MovieComponentDocument(int Version, MovieComponentDeclaration[] Components);

    private static readonly JsonSerializerOptions MovieComponentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    private static IReadOnlyDictionary<(int Disc, int Movie), MovieComponent> PrepareMovieComponents(ModCatalogSnapshot catalog, List<ModDiagnostic> failures)
    {
        Dictionary<(int Disc, int Movie), MovieComponent> winners = [];
        foreach (ModFile manifest in catalog.FindAll(ModFormat.Memoria, MoviePackagePaths.ComponentsDocument))
        {
            try
            {
                MovieComponentDocument? document = JsonSerializer.Deserialize<MovieComponentDocument>(ReadMovieDocument(manifest), MovieComponentOptions);
                if (document is not { Version: 1, Components.Length: > 0 and <= 256 })
                {
                    throw new InvalidDataException("A version 1 document must declare between 1 and 256 movie components.");
                }
                Dictionary<string, ModFile> files = catalog.Files.Where(file => file.PackageId.Equals(manifest.PackageId, StringComparison.OrdinalIgnoreCase)).ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
                HashSet<(int Disc, int Movie)> declared = [];
                foreach (MovieComponentDeclaration component in document.Components)
                {
                    if (component?.Video is not { } path || !path.StartsWith("movies/", StringComparison.OrdinalIgnoreCase)
                        || !MoviePackagePaths.IsNativeMovieVideo(path) || !MoviePackagePaths.TryGetMovieAddress(Path.GetFileNameWithoutExtension(path)[..^1], out var address) || !declared.Add(address))
                    {
                        throw new InvalidDataException("Each component needs a unique movie address and a video path of movies/discDD_IIh.avi or movies/discDD_IIh.mp4.");
                    }
                    string stem = FormattableString.Invariant($"movies/disc{address.Disc:00}_{address.Movie:00}");
                    ModFile video = RequireMovieMember(files, path);
                    ModFile camera = RequireMovieMember(files, stem + ".cam");
                    ModFile subtitles = RequireMovieMember(files, stem + ".subtitles.json");
                    MovieSubtitleTrack track = MovieSubtitleTrack.Parse(ReadMovieDocument(subtitles));
                    if (!winners.TryGetValue(address, out MovieComponent? previous) || component.Priority > previous.Priority)
                    {
                        winners[address] = new(video, camera, track, component.Priority);
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
            {
                failures.Add(new("Memoria.InvalidMovieComponents", exception.Message, manifest.PackageId, manifest.RelativePath));
            }
        }
        return winners;
    }

    private static ModFile RequireMovieMember(Dictionary<string, ModFile> files, string path)
    {
        if (!files.TryGetValue(path, out ModFile? file) || file.ContentStamp?.Length == 0)
        {
            throw new InvalidDataException($"Movie component requires its own nonempty '{path}'.");
        }
        return file;
    }

    private static byte[] ReadMovieDocument(ModFile file)
    {
        if (file.ContentStamp?.Length > MovieSubtitleTrack.MaximumDocumentBytes)
        {
            throw new InvalidDataException($"'{file.RelativePath}' exceeds {MovieSubtitleTrack.MaximumDocumentBytes} bytes.");
        }
        byte[] bytes = file.ReadBytes();
        if (bytes.Length > MovieSubtitleTrack.MaximumDocumentBytes)
        {
            throw new InvalidDataException($"'{file.RelativePath}' exceeds {MovieSubtitleTrack.MaximumDocumentBytes} bytes.");
        }
        return bytes;
    }

    private static bool TryGetMovieComponentAddress(string relative, out (int Disc, int Movie) address)
    {
        address = default;
        if (!IsMovieDirectoryPath(relative))
        {
            return false;
        }
        string stem = Path.GetFileNameWithoutExtension(relative);
        if (relative.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) && stem.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            return MoviePackagePaths.TryGetMovieAddress(stem[..^1], out address);
        }
        return relative.EndsWith(".cam", StringComparison.OrdinalIgnoreCase) && MoviePackagePaths.TryGetMovieAddress(stem, out address);
    }
}
