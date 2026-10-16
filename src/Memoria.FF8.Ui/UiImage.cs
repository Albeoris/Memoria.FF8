namespace Memoria.FF8.Ui;

public enum UiImageKind
{
    CharacterPortrait,
    GuardianForcePortrait,
    Card,
    Icon,
    CardBack
}

/// <summary>Stable artwork identity, independent of translated or player-assigned display names.</summary>
public readonly record struct UiImageSource(UiImageKind Kind, int Id, int Palette = 0);

/// <summary>Fits artwork inside the arranged grid or anchor bounds while preserving its native aspect ratio.</summary>
public sealed class UiImage : UiElement
{
    public UiImageSource? Source { get; set; }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (Source is { } source && !Bounds.IsEmpty)
        {
            canvas.Image(source, Bounds);
        }
    }

    public static UiRect Fit(UiRect bounds, float imageWidth, float imageHeight)
    {
        if (!float.IsFinite(imageWidth) || !float.IsFinite(imageHeight) || imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image dimensions must be finite and positive.");
        }
        float scale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
        float width = imageWidth * scale;
        float height = imageHeight * scale;
        return new UiRect(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}
