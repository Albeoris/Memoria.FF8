using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Explicit synthetic page metadata for resource-independent menu construction and drawing tests.</summary>
internal static class MagazineMenuFixture
{
    public static MenuItem AddOwnedPage(MenuController controller)
    {
        MenuItem item = new()
        {
            Id = "synthetic-magazine",
            Name = "Synthetic magazine",
            Quantity = 1,
            NativeMagazinePages =
            [
                new NativeMagazinePage(177, 0, 255, 255, 255, ["Synthetic magazine page"])
                {
                    Layout = new NativeMagazineLayout(0, 0, 320, 240, [new NativeMagazineText(16, 16, "Synthetic magazine page")], [], null)
                }
            ]
        };
        controller.Data.Items.Insert(0, item);
        return item;
    }
}
