using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private readonly byte[] itemIconKinds;

    public UiImageSource? ItemIcon(int itemId)
    {
        if (itemId <= 0 || itemId >= fieldItems.Length / 4)
        {
            return null;
        }
        int type = fieldItems[itemId * 4];
        if (type >= itemIconKinds.Length)
        {
            throw new InvalidDataException($"Item {itemId} has unsupported native icon type {type}.");
        }
        return new UiImageSource(UiImageKind.Icon, 223 + itemIconKinds[type]);
    }
}
