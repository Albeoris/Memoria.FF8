using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Media;

public sealed record MovieSubtitleTrack
{
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions DocumentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    public required int Version { get; init; }
    public required IReadOnlyList<MovieSubtitleCue> Cues { get; init; }

    public static MovieSubtitleTrack Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException($"A movie subtitle document cannot exceed {MaximumDocumentBytes} bytes.");
        }
        if (utf8.StartsWith("\uFEFF"u8))
        {
            utf8 = utf8[3..];
        }
        MovieSubtitleTrack? track = JsonSerializer.Deserialize<MovieSubtitleTrack>(utf8, DocumentOptions);
        if (track is null || track.Version != 1)
        {
            throw new InvalidDataException("A movie subtitle document must specify version 1.");
        }
        if (track.Cues is null || track.Cues.Count > 4096)
        {
            throw new InvalidDataException("A movie subtitle document must contain a cues array with at most 4096 entries.");
        }

        MovieSubtitleCue[] cues = new MovieSubtitleCue[track.Cues.Count];
        for (int index = 0; index < cues.Length; index++)
        {
            MovieSubtitleCue cue = track.Cues[index] ?? throw new InvalidDataException($"Subtitle cue {index + 1} is null.");
            try
            {
                cues[index] = ValidateCue(cue);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Subtitle cue {index + 1}: {exception.Message}", exception);
            }
        }
        ValidateOverlap(cues);
        return track with { Cues = Array.AsReadOnly(cues) };
    }

    private static void ValidateOverlap(IEnumerable<MovieSubtitleCue> cues)
    {
        var boundaries = cues.SelectMany(cue => new[] { (Time: cue.StartSeconds, Change: 1), (Time: cue.EndSeconds, Change: -1) });
        int active = 0;
        foreach (var boundary in boundaries.OrderBy(boundary => boundary.Time).ThenBy(boundary => boundary.Change))
        {
            active += boundary.Change;
            if (active > 16)
            {
                throw new InvalidDataException(FormattableString.Invariant($"At most 16 subtitle cues can overlap. This limit is exceeded at {boundary.Time:G17} seconds."));
            }
        }
    }

    private static MovieSubtitleCue ValidateCue(MovieSubtitleCue cue)
    {
        if (string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Length > 1024 || cue.Text.Count(character => character == '\n') > 15 || cue.Text.Any(character => char.IsControl(character) && character is not ('\n' or '\r')) || cue.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Contains('\r'))
        {
            throw new InvalidDataException("Text must contain 1–1024 characters and at most 16 lines, with LF or CRLF line breaks.");
        }
        if (!double.IsFinite(cue.StartSeconds) || !double.IsFinite(cue.EndSeconds) || cue.StartSeconds < 0 || cue.EndSeconds <= cue.StartSeconds)
        {
            throw new InvalidDataException("Cue times must satisfy 0 <= startSeconds < endSeconds.");
        }
        MovieSubtitleStyle style = cue.Style ?? throw new InvalidDataException("An explicit style cannot be null.");
        if (string.IsNullOrWhiteSpace(style.FontFamily) || style.FontFamily.Length > 128 || style.FontFamily.Any(char.IsControl))
        {
            throw new InvalidDataException("fontFamily must name a system font family.");
        }
        if (style.Color is not { Length: 7 or 9 } || style.Color[0] != '#' || !style.Color[1..].All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("color must use #RRGGBB or #RRGGBBAA.");
        }
        ValidateRange(style.FontSize, 0.005f, 0.25f, "fontSize");
        ValidateRange(style.X, 0, 1, "x");
        ValidateRange(style.Y, 0, 1, "y");

        if (cue.Keyframes is null || cue.Keyframes.Count > 128)
        {
            throw new InvalidDataException("keyframes must be an array with at most 128 entries.");
        }
        double duration = cue.EndSeconds - cue.StartSeconds;
        double previous = -1;
        for (int index = 0; index < cue.Keyframes.Count; index++)
        {
            MovieSubtitleKeyframe frame = cue.Keyframes[index] ?? throw new InvalidDataException($"Keyframe {index + 1} is null.");
            if (!double.IsFinite(frame.Seconds) || frame.Seconds < 0 || frame.Seconds > duration + 1e-9 || frame.Seconds <= previous)
            {
                throw new InvalidDataException($"Keyframe {index + 1} seconds must increase strictly within the cue duration.");
            }
            previous = frame.Seconds;
            ValidateRange(frame.Opacity, 0, 1, "opacity");
            ValidateRange(frame.OffsetX, -2, 2, "offsetX");
            ValidateRange(frame.OffsetY, -2, 2, "offsetY");
            ValidateRange(frame.ScaleX, 0, 8, "scaleX");
            ValidateRange(frame.ScaleY, 0, 8, "scaleY");
            ValidateRange(frame.Blur, 0, 2, "blur");
            ValidateRange(frame.Reveal, 0, 1, "reveal");
            ValidateRange(frame.Scatter, 0, 8, "scatter");
            ValidateRange(frame.Ripple, 0, 8, "ripple");
            ValidateRange(frame.Noise, 0, 1, "noise");
            ValidateRange(frame.Brightness, 0, 4, "brightness");
            ValidateRange(frame.SplitX, -2, 2, "splitX");
            ValidateRange(frame.SplitY, -2, 2, "splitY");
        }
        return cue with { Keyframes = Array.AsReadOnly(cue.Keyframes.ToArray()) };
    }

    private static void ValidateRange(float? value, float minimum, float maximum, string property)
    {
        if (value is float number && (!float.IsFinite(number) || number < minimum || number > maximum))
        {
            throw new InvalidDataException(FormattableString.Invariant($"{property} must be in [{minimum}, {maximum}]."));
        }
    }
}

public sealed record MovieSubtitleCue
{
    public required string Text { get; init; }
    public required double StartSeconds { get; init; }
    public required double EndSeconds { get; init; }
    public int Layer { get; init; }
    public MovieSubtitleStyle Style { get; init; } = new();
    public IReadOnlyList<MovieSubtitleKeyframe> Keyframes { get; init; } = [];
}

public sealed record MovieSubtitleStyle
{
    public string FontFamily { get; init; } = "Arial";
    public float FontSize { get; init; } = 0.05f;
    public float X { get; init; } = 0.5f;
    public float Y { get; init; } = 0.9f;
    public string Color { get; init; } = "#FFFFFF";
    public bool Bold { get; init; }
}

public sealed record MovieSubtitleKeyframe
{
    public required double Seconds { get; init; }
    public float? Opacity { get; init; }
    public float? OffsetX { get; init; }
    public float? OffsetY { get; init; }
    public float? ScaleX { get; init; }
    public float? ScaleY { get; init; }
    public float? Blur { get; init; }
    public float? Reveal { get; init; }
    public float? Scatter { get; init; }
    public float? Ripple { get; init; }
    public float? Noise { get; init; }
    public float? Brightness { get; init; }
    public float? SplitX { get; init; }
    public float? SplitY { get; init; }
}
