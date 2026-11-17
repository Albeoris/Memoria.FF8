using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record MenuItemDefinition(byte Type, NativeFieldItem NativeUse, ShowImageItemUse? Use);

public sealed partial class LiveMenuCatalog
{
    private readonly byte[] itemIconKinds;
    internal Func<int, ItemOverride?>? FindItemOverride { get; set; }

    public MenuItemDefinition? ItemDefinition(int itemId)
    {
        if (FieldItem(itemId) is not { } original)
        {
            return null;
        }

        ItemOverride? change = FindItemOverride?.Invoke(itemId);
        return new(change?.Type is { } type ? (byte)type : original.Type, original, change?.Use);
    }

    public UiImageSource? ItemIcon(int itemId)
    {
        return ItemDefinition(itemId) is { } item ? ItemTypeIcon(itemId, item.Type) : null;
    }

    internal UiImageSource ItemTypeIcon(int itemId, int type)
    {
        if (type >= itemIconKinds.Length)
        {
            throw new InvalidDataException($"Item {itemId} has unsupported native icon type {type}.");
        }
        return new UiImageSource(UiImageKind.Icon, 223 + itemIconKinds[type]);
    }
}
