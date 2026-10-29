using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.GameUi;

internal interface IUiTextFragmentInspector
{
    void InspectTextFragment(string value, UiRect bounds);
    void InspectTextFragment(string value, UiRect bounds, float size, string? fontFamily) => InspectTextFragment(value, bounds);
}

internal sealed partial class MonoGameUiCanvas : IUiTextFragmentInspector
{
    private sealed record InspectionRaster(int Width, int Height, IReadOnlyList<PortableTextCluster> Clusters);
    private readonly Dictionary<(string Text, float Size, float Width, string Family), InspectionRaster> inspectionRasters = [];
    public Action<LocalizationTextBinding, UiRect>? InspectText { get; set; }
    public Action<IReadOnlyList<LocalizationTextHit>>? InspectTextBatch { get; set; }

    public void InspectTextFragment(string value, UiRect bounds)
    {
        InspectSemanticFragment(value, bounds, null, null);
    }

    public void InspectTextFragment(string value, UiRect bounds, float size, string? fontFamily)
    {
        InspectSemanticFragment(value, bounds, size, fontFamily);
    }

    private void InspectSemanticFragment(string value, UiRect bounds, float? size, string? family)
    {
        if (InspectTextBatch is null && InspectText is null || LocalizationTextTrace.Find(value) is not { } binding)
        {
            return;
        }
        Rectangle visible = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        PortableTextCluster cluster = new(Enumerable.Range(0, value.Length).ToArray(), new(bounds.X, bounds.Y, bounds.Right, bounds.Bottom));
        PublishInspection(LocalizationFragmentGeometry.Map(binding, [cluster], Vector2.Zero, 1, new(visible.X, visible.Y, visible.Width, visible.Height)), size, UseNativeFonts ? null : family, bounds.Width, UseNativeFonts ? NativeFont?.ProfileFor(NativeMenuTextContext)?.Id : null);
    }

    private void Inspect(string value, Vector2 position, float size, float maximumWidth, Rectangle clip, string? fontFamily = null, bool verticallyCentered = false)
    {
        if (InspectTextBatch is null && InspectText is null || LocalizationTextTrace.Find(value) is not { } binding)
        {
            return;
        }
        string family = fontFamily ?? text.FontFamily;
        var key = (value, size, maximumWidth, family);
        if (!inspectionRasters.TryGetValue(key, out InspectionRaster? raster))
        {
            PortableTextRasterResult mapped = SystemTextLayout.RasterizeMapped(value, size, maximumWidth, family);
            using (mapped.Bitmap)
            {
                raster = new(mapped.Bitmap.Width, mapped.Bitmap.Height, mapped.Clusters);
            }
            if (inspectionRasters.Count >= 128)
            {
                inspectionRasters.Clear();
            }
            inspectionRasters.Add(key, raster);
        }
        if (verticallyCentered)
        {
            position.Y -= raster.Height / (2f * SystemTextLayout.RasterScale);
        }
        UiRect visible = LocalizationFragmentGeometry.SystemRasterClip(position, raster.Width, raster.Height, clip);
        PublishInspection(LocalizationFragmentGeometry.Map(binding, raster.Clusters, position, 1, visible), size, SystemFontCatalog.ResolveFamily(family), maximumWidth);
    }

    private void InspectNative(string value, NativeFontLayoutResult layout, Vector2 origin, float scale, Rectangle clip, float size, float maximumWidth, string? profileId)
    {
        if (InspectTextBatch is null && InspectText is null || LocalizationTextTrace.Find(value) is not { } binding)
        {
            return;
        }
        PublishInspection(LocalizationFragmentGeometry.Map(binding, LocalizationFragmentGeometry.ClippedNativeClusters(layout, origin, scale, clip), Vector2.Zero, 1, new(clip.X, clip.Y, clip.Width, clip.Height)), size, null, maximumWidth, profileId);
    }

    private void PublishInspection(IReadOnlyList<LocalizationTextHit> hits, float? size = null, string? family = null, float? maximumWidth = null, string? nativeProfile = null)
    {
        hits = hits.Select(hit => hit with { Size = size, FontFamily = family, MaximumWidth = maximumWidth is float width && float.IsFinite(width) ? width : null, NativeProfileId = nativeProfile }).ToArray();
        if (InspectTextBatch is not null)
        {
            InspectTextBatch(hits);
        }
        else if (InspectText is not null)
        {
            foreach (LocalizationTextHit hit in hits)
            {
                InspectText(hit.Binding, hit.Bounds);
            }
        }
    }
}
