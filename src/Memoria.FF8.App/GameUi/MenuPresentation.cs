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
    float DesignHeight(MenuController controller);
    UiElement Compose(MenuController controller);
    UiTheme CreateTheme(float textScale);
}

public sealed class ClassicMenuPresentation : IMenuPresentation
{
    public float DesignHeight(MenuController controller) => controller.ScreenId == "magazine" ? 720 : 672;
    public UiElement Compose(MenuController controller) => controller.BuildClassicContent();
    public UiTheme CreateTheme(float textScale) => new() { FontSize = 16 * textScale };
}

public sealed class VectorMenuPresentation : IMenuPresentation
{
    public float DesignHeight(MenuController controller) => controller.UsesVectorComposition ? 540 : controller.ScreenId == "magazine" ? 720 : 672;
    public UiElement Compose(MenuController controller) => controller.BuildVectorContent();
    public UiTheme CreateTheme(float textScale) => new VectorUiTheme { FontSize = 16 * textScale };
}
