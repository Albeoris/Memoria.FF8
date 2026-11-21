using System.Text.Json;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    internal sealed record MovieSubtitlePublication(IReadOnlyDictionary<(int Disc, int Movie), MovieSubtitleTrack> Tracks, IReadOnlyList<string> PackageIds);

    private MovieSubtitlePublication movieSubtitles = new(new Dictionary<(int, int), MovieSubtitleTrack>(), []);

    public MovieSubtitleTrack? ResolveMovieSubtitles(int discIndex, int movieIndex) => movieSubtitles.Tracks.GetValueOrDefault((discIndex, movieIndex));

    internal static bool TryPrepareMovieSubtitles(ModCatalogSnapshot catalog, out MovieSubtitlePublication publication, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<(int Disc, int Movie), MovieSubtitleTrack> tracks = [];
        HashSet<(int Disc, int Movie)> selected = [];
        HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
        List<ModDiagnostic> failures = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && MoviePackagePaths.IsSubtitleDocument(file.RelativePath)))
        {
            try
            {
                var address = MoviePackagePaths.GetSubtitleAddress(file.RelativePath);
                if (!selected.Add(address))
                {
                    continue;
                }
                if (file.ContentStamp?.Length > MovieSubtitleTrack.MaximumDocumentBytes)
                {
                    throw new InvalidDataException($"A movie subtitle document cannot exceed {MovieSubtitleTrack.MaximumDocumentBytes} bytes.");
                }
                tracks.Add(address, MovieSubtitleTrack.Parse(file.ReadBytes()));
                packageIds.Add(file.PackageId);
            }
            catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
            {
                failures.Add(new("Memoria.InvalidMovieSubtitles", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        publication = failures.Count == 0 ? new(tracks, packageIds.ToArray()) : new(new Dictionary<(int, int), MovieSubtitleTrack>(), []);
        diagnostics = failures.AsReadOnly();
        return failures.Count == 0;
    }

    internal static ModFile? FindFileReplacement(ModCatalogSnapshot catalog, string relative)
    {
        bool moviePath = IsMovieDirectoryPath(relative);
        bool camera = moviePath && relative.EndsWith(".cam", StringComparison.OrdinalIgnoreCase);
        if (camera)
        {
            ModFile? video = FindFileReplacement(catalog, Path.ChangeExtension(relative, null) + "h.avi");
            if (video is not null)
            {
                // A lower-priority video's CAM may have a different frame cadence.
                return FindMoviePath(catalog.Files.Where(file => file.Format == video.Format && file.PackageId.Equals(video.PackageId, StringComparison.OrdinalIgnoreCase)), relative);
            }
            return FindMoviePath(catalog.Files.Where(file => file.Format == ModFormat.Junction || file.Format == ModFormat.Memoria && MoviePackagePaths.IsNativeMovieCamera(file.RelativePath)), relative);
        }
        if (!moviePath || !relative.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
        {
            return catalog.FindAll(ModFormat.Junction, relative).FirstOrDefault();
        }
        Dictionary<string, ModPackage> packages = catalog.Packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        IEnumerable<ModFile> videoFiles = catalog.Files.Where(file => file.Format == ModFormat.Memoria && MoviePackagePaths.IsNativeMovieVideo(file.RelativePath))
            .Concat(catalog.Files.Where(file => file.Format == ModFormat.Junction));
        foreach (IGrouping<string, ModFile> files in videoFiles.GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            ModPackage package = packages[files.Key];
            ModFile? selected = package.Format == ModFormat.Junction
                ? FindMoviePath(files, Path.ChangeExtension(relative, package.JunctionMetadata?.VideoExtension ?? "avi"))
                : FindMoviePath(files, relative, Path.ChangeExtension(relative, "mp4"));
            if (selected is not null)
            {
                return selected;
            }
        }
        return null;
    }

    private static bool IsMovieDirectoryPath(string path)
    {
        return path.StartsWith("movies/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("data/movies/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("lang-", StringComparison.OrdinalIgnoreCase) && path.IndexOf("/movies/", StringComparison.OrdinalIgnoreCase) == 7;
    }

    private static ModFile? FindMoviePath(IEnumerable<ModFile> files, string relative, string? alternate = null)
    {
        ModFile[] candidates = files.ToArray();
        string name = Path.GetFileName(relative);
        string? alternateName = Path.GetFileName(alternate);
        return candidates.FirstOrDefault(file => file.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(file => file.RelativePath.Equals(alternate, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(file => file.RelativePath.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(file => file.RelativePath.Equals(alternateName, StringComparison.OrdinalIgnoreCase));
    }
}
