using System.Globalization;

namespace Memoria.FF8.Mods;

public static class MoviePackagePaths
{
    public const string ComponentsDocument = "movies/components.json";

    private const string MovieDirectory = "movies/";
    private const string SubtitleExtension = ".subtitles.json";

    public static bool IsSubtitleDocument(string path) => path.StartsWith(MovieDirectory, StringComparison.OrdinalIgnoreCase) && path.EndsWith(SubtitleExtension, StringComparison.OrdinalIgnoreCase);

    public static (int Disc, int Movie) GetSubtitleAddress(string path)
    {
        if (IsSubtitleDocument(path) && TryReadMovieStem(path.AsSpan(MovieDirectory.Length, path.Length - MovieDirectory.Length - SubtitleExtension.Length), out int disc, out int movie))
        {
            return (disc, movie);
        }
        throw new InvalidDataException("A movie subtitle filename must be movies/discDD_II.subtitles.json, with a disc from 00 through 04 and a movie index from 00 through 255.");
    }

    public static bool IsNativeMovieVideo(string path)
    {
        string extension = Path.GetExtension(path);
        return (extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) && IsNativeMovieFile(path, isVideo: true);
    }

    public static bool IsNativeMovieCamera(string path) => path.EndsWith(".cam", StringComparison.OrdinalIgnoreCase) && IsNativeMovieFile(path, isVideo: false);

    private static bool IsNativeMovieFile(string path, bool isVideo)
    {
        int separator = path.LastIndexOf('/');
        ReadOnlySpan<char> directory = separator < 0 ? default : path.AsSpan(0, separator + 1);
        if (!directory.IsEmpty && !directory.Equals(MovieDirectory, StringComparison.OrdinalIgnoreCase) && !directory.Equals("data/movies/", StringComparison.OrdinalIgnoreCase) && !(directory.Length == 15 && directory.StartsWith("lang-", StringComparison.OrdinalIgnoreCase) && directory[7..].Equals("/movies/", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        ReadOnlySpan<char> stem = Path.GetFileNameWithoutExtension(path.AsSpan());
        if (isVideo)
        {
            if (!stem.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            stem = stem[..^1];
        }
        return TryReadMovieStem(stem, out _, out _);
    }

    public static bool TryGetMovieAddress(string stem, out (int Disc, int Movie) address)
    {
        bool valid = TryReadMovieStem(stem.AsSpan(), out int disc, out int movie);
        address = (disc, movie);
        return valid;
    }

    private static bool TryReadMovieStem(ReadOnlySpan<char> stem, out int disc, out int movie)
    {
        disc = movie = 0;
        return stem.Length is 9 or 10 && stem.StartsWith("disc", StringComparison.OrdinalIgnoreCase) && stem[6] == '_' &&
            int.TryParse(stem[4..6], NumberStyles.None, CultureInfo.InvariantCulture, out disc) && disc <= 4 &&
            int.TryParse(stem[7..], NumberStyles.None, CultureInfo.InvariantCulture, out movie) && movie <= byte.MaxValue &&
            stem[7..].Length == (movie >= 100 ? 3 : 2);
    }
}
