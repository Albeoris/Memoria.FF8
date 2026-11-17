using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsItemImageOpen => IsOpen && ScreenId == "item-image";

    private void RegisterItemImageScreen()
    {
        Register(new MenuScreenDefinition
        {
            Id = "item-image",
            Title = "",
            Help = "",
            BuildContent = (controller, panel) => panel.Children.Add(new UiImage { Id = "item-image-content", Source = controller.Item?.UseImage })
        });
    }
}
