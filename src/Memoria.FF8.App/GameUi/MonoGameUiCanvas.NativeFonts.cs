using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas : INativeButtonTextMetrics
{
    internal NativeFontPresentation? NativeFont { get; set; }
    internal bool UseNativeFonts { get; set; }
    internal bool UseNativeLayout { get; set; }

    public bool UsesNativeButtonMetrics => UseNativeFonts;
    public float NativeButtonAdvance(string value, float size) => NativeTextAdvance(value, size, NativeContextFor(value));
    public UiSize NativeButtonMetrics(string value, float size) => MeasureNativeText(value, size, float.PositiveInfinity, NativeContextFor(value));
    private float NativeTextAdvance(string value, float size, NativeTextContext context) => (NativeFont?.Layout(value, context).FinalAdvance ?? 0) * size / (NativeFont?.LineHeightFor(context) ?? 16);

    private NativeTextContext NativeMenuTextContext => new(resources?.Source.Edition.ToString() ?? "Classic", "System", DictionaryLanguage: resources?.ResourceLanguage ?? "en");

    private NativeTextContext NativeContextFor(string value)
    {
        return NativeContextForBinding(LocalizationTextTrace.Find(value), NativeMenuTextContext);
    }

    internal static NativeTextContext NativeContextForBinding(LocalizationTextBinding? binding, NativeTextContext fallback)
    {
        string? category = binding?.Category;
        if (category is null && binding?.Fragments.Count > 0)
        {
            string?[] categories = binding.Fragments.Select(fragment => fragment.Binding.Category).Where(category => category is not null).Distinct(StringComparer.Ordinal).ToArray();
            category = categories.Length == 1 ? categories[0] : categories.Length > 1 ? "Mixed" : null;
        }
        string family = category switch
        {
            null => fallback.ResourceFamily,
            "System" => "System",
            "Draw points" => "World",
            "Magic" or "Items" or "Weapons" or "Characters" or "Guardians" or "Abilities" or "Information and SeeD exam" => "Menus",
            _ => category
        };
        return new(fallback.Edition, family, family == "Fields" ? binding?.File ?? fallback.Field : null, fallback.DictionaryLanguage);
    }

    private UiSize MeasureNativeText(string value, float size, float width, NativeTextContext? context = null)
    {
        context ??= NativeContextFor(value);
        float line = NativeFont?.LineHeightFor(context) ?? 16;
        float scale = size / line;
        NativeFontLayoutResult? layout = NativeFont?.Layout(value, context ?? NativeMenuTextContext, width / scale);
        return new((layout?.Width ?? 0) * scale, (layout?.Height ?? line) * scale);
    }

    private bool DrawNativeText(string value, float size, UiRect bounds, UiColor color, UiAlignment alignment, NativeTextContext? context = null)
    {
        if (!UseNativeFonts || NativeFont is null)
        {
            return false;
        }
        context ??= NativeContextFor(value);
        UiSize measured = MeasureNativeText(value, size, bounds.Width, context);
        float y = alignment is UiAlignment.Center or UiAlignment.End ? Math.Max(0, bounds.Height - measured.Height) * (alignment == UiAlignment.Center ? 0.5f : 1) : 0;
        float nativeScale = size / NativeFont.LineHeightFor(context ?? NativeMenuTextContext);
        bool drawn = NativeFont.Draw(batch, value, new(bounds.X, bounds.Y + y), nativeScale, ColorFor(color), context ?? NativeMenuTextContext, Rectangle.Intersect(RectangleFor(bounds), CurrentClip), bounds.Width / nativeScale, maximumHeight: bounds.Height / nativeScale);
        if (drawn)
        {
            float scale = nativeScale;
            NativeFontLayoutResult layout = NativeFont.Layout(value, context ?? NativeMenuTextContext, bounds.Width / scale);
            InspectNative(value, layout, new(bounds.X, bounds.Y + y), scale, Rectangle.Intersect(RectangleFor(bounds), CurrentClip), size, bounds.Width, NativeFont.ProfileFor(context ?? NativeMenuTextContext)?.Id, context);
        }
        if (!drawn)
        {
            Fill(new(bounds.X, bounds.Y, Math.Min(8, bounds.Width), Math.Min(8, bounds.Height)), new UiColor(255, 80, 100));
        }
        return true;
    }
}
