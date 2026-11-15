using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private readonly byte[] itemIconKinds;
    internal Func<int, ModFile?>? FindBookArtwork { get; set; }

    internal UiImageSource? ItemBookCover(int itemId) => FindBookArtwork?.Invoke(itemId) is not null ? new UiImageSource(UiImageKind.BookCover, itemId) : null;

    internal UiImageSource BookIcon(int itemId) => ItemTypeIcon(itemId, 9);

    public UiImageSource? ItemIcon(int itemId)
    {
        if (itemId <= 0 || itemId >= fieldItems.Length / 4)
        {
            return null;
        }
        return ItemTypeIcon(itemId, fieldItems[itemId * 4]);
    }

    private UiImageSource ItemTypeIcon(int itemId, int type)
    {
        if (type >= itemIconKinds.Length)
        {
            throw new InvalidDataException($"Item {itemId} has unsupported native icon type {type}.");
        }
        return new UiImageSource(UiImageKind.Icon, 223 + itemIconKinds[type]);
    }
}
