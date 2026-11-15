using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsBookOpen => IsOpen && ScreenId == "book";

    private void RegisterBookScreen()
    {
        Register(new MenuScreenDefinition
        {
            Id = "book",
            Title = "Book",
            Help = "",
            BuildContent = (controller, panel) => panel.Children.Add(new UiImage { Id = "book-cover", Source = controller.Item?.BookCover })
        });
    }
}
