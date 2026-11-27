using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeShopMenuAbilities
{
    public const int CallShop = 95;
    public const int JunkShop = 96;

    public static ImmutableArray<int> CallableCatalogs(NativeShopHistory history, bool dreamWorld)
    {
        history.Validate();
        ImmutableArray<int>.Builder catalogs = ImmutableArray.CreateBuilder<int>();
        for (int catalog = 0; catalog < 20; catalog++)
        {
            bool dreamCatalog = catalog is >= 9 and <= 15;
            if (dreamCatalog == dreamWorld && BinaryPrimitives.ReadUInt16LittleEndian(history.Bytes.AsSpan(20 * catalog + 16, 2)) != 0)
            {
                catalogs.Add(catalog);
            }
        }
        return catalogs.ToImmutable();
    }

    public static bool CanUse(NativeCharacterVitals vitals, NativeShopHistory? history, int abilityId, int? catalog = null)
    {
        if (abilityId is not (CallShop or JunkShop) || (vitals.ShopAbilityMask() & (1u << (abilityId - 92))) == 0)
        {
            return false;
        }
        if (abilityId == JunkShop)
        {
            return !vitals.DreamEnabled && catalog is null;
        }
        if (history is null)
        {
            return false;
        }
        ImmutableArray<int> shops = CallableCatalogs(history, vitals.DreamEnabled);
        return catalog is int selected ? shops.Contains(selected) : !shops.IsEmpty;
    }
}
