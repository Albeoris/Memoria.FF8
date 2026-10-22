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
    public UiThickness Padding(MenuController controller) => controller.UsesVectorComposition ? new(26) : controller.ClassicRootPadding;
    public float DesignHeight(MenuController controller) => controller.UsesVectorComposition ? 540 : controller.ScreenId == "magazine" ? 720 : 672;
    public UiElement Compose(MenuController controller)
    {
        UiElement content = controller.BuildVectorContent();
        ReserveCaptions(content);
        return content;
    }

    private static void ReserveCaptions(UiElement element)
    {
        if (element is UiPanel { Caption: { Length: > 0 } } panel)
        {
            panel.CaptionHeight = Math.Max(panel.CaptionHeight, panel.CaptionFontSize * 1.5f + 12);
        }
        foreach (UiElement child in element.Children)
        {
            ReserveCaptions(child);
        }
    }
    public UiTheme CreateTheme(float textScale) => new VectorUiTheme { FontSize = 16 * textScale };
}
