using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

internal static class InventoryBookArtwork
{
    internal const int GirlNextDoorItemId = 163;
    internal const string GirlNextDoorPackageId = "Memoria/Girl Next Door";

    internal static ModFile? Find(ModCatalogSnapshot catalog, int itemId)
    {
        if (itemId != GirlNextDoorItemId)
        {
            return null;
        }

        return catalog.FindAll(ModFormat.Memoria, "GirlNextDoor.png").FirstOrDefault(file => file.PackageId.Equals(GirlNextDoorPackageId, StringComparison.OrdinalIgnoreCase));
    }
}
