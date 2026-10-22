using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public enum MenuPresentation
{
    Vector,
    Classic
}

/// <summary>Owns screen composition and drawing style; the controller owns navigation and game actions.</summary>
public interface IMenuPresentation
{
    string PreferredFontFamily => SystemFontCatalog.DefaultFamily;
    UiViewportMode ViewportMode => UiViewportMode.Fit;
    float DesignWidth(MenuController controller) => 960;
    UiThickness Padding(MenuController controller) => new(20);
    float DesignHeight(MenuController controller);
    UiElement Compose(MenuController controller);
    UiTheme CreateTheme(float textScale);
}

public sealed class ClassicMenuPresentation : IMenuPresentation
{
    public UiThickness Padding(MenuController controller) => controller.ClassicRootPadding;
    public float DesignHeight(MenuController controller) => controller.ScreenId == "magazine" ? 720 : 672;
    public UiElement Compose(MenuController controller) => controller.BuildClassicContent();
    public UiTheme CreateTheme(float textScale) => new() { FontSize = 16 * textScale };
}

public sealed class VectorMenuPresentation : IMenuPresentation
{
    public string PreferredFontFamily => "Georgia";
    public UiViewportMode ViewportMode => UiViewportMode.Expand;
    public UiThickness Padding(MenuController controller) => new(controller.ScreenId == "magazine" ? 0 : 26);
    public float DesignHeight(MenuController controller) => controller.UsesVectorComposition ? 540 : controller.ScreenId == "magazine" ? 720 : 672;
    public UiElement Compose(MenuController controller) => controller.BuildVectorContent();
    public UiTheme CreateTheme(float textScale) => new VectorUiTheme { FontSize = 16 * textScale };
}
