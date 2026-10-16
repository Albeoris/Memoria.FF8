using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private LiveMenuCatalog? fieldItemCatalog;
    private LiveMenuCatalog FieldItemCatalog => fieldItemCatalog ??= LiveMenuCatalog.ReadInstalled(assets);
}
