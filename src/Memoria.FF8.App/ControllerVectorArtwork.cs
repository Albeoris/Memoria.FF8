using SkiaSharp;

namespace Memoria.FF8;

internal static class ControllerVectorArtwork
{
    private static string Family(ControllerPresentation presentation) => presentation switch
    {
        ControllerPresentation.PlayStation => "sony",
        ControllerPresentation.Xbox => "xbox",
        _ => "unsupported"
    };

    internal static bool Supports(int icon, ControllerPresentation presentation) => ControllerVectorAtlas.Supports(icon, Family(presentation));
    internal static SKBitmap Render(int icon, ControllerPresentation presentation, int size) => ControllerVectorAtlas.Render(icon, Family(presentation), size);
}
