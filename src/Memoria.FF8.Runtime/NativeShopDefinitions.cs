using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeShopCatalogEntry([property: JsonRequired] byte ItemId, [property: JsonRequired] byte Availability);
public sealed record NativeShopPrice([property: JsonRequired] ushort Base, [property: JsonRequired] byte SellMultiplier, [property: JsonRequired] byte Byte3, [property: JsonRequired] string? Name);

public sealed record NativeShopDefinitions([property: JsonRequired] string ExecutableHash, [property: JsonRequired] string ShopHash, [property: JsonRequired] string PriceHash, [property: JsonRequired] string KernelHash, [property: JsonRequired] ImmutableArray<byte> ArgumentMap, [property: JsonRequired] ImmutableArray<ImmutableArray<NativeShopCatalogEntry>> Catalogs, [property: JsonRequired] ImmutableArray<NativeShopPrice> Prices)
{
    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || new[] { ShopHash, PriceHash, KernelHash }.Any(hash => hash is not { Length: 64 } || hash.Any(value => !Uri.IsHexDigit(value))) || ArgumentMap.IsDefault || ArgumentMap.Length != 256 || Catalogs.IsDefault || Catalogs.Length != 20 || Catalogs.Any(row => row.IsDefault || row.Length != 16 || row.Any(entry => entry is null || entry.ItemId >= 199)) || Prices.IsDefault || Prices.Length != 199 || Prices.Any(price => price is null))
        {
            throw new InvalidDataException("Native shop definitions require original argument bytes, twenty catalogs and199 price/name rows.");
        }
    }

    public byte ResolveCatalog(int rawArgument)
    {
        Validate();
        byte catalog = ArgumentMap[unchecked((byte)rawArgument)];
        if (catalog >= Catalogs.Length)
        {
            throw new NotSupportedException(catalog == 21 ? "Native weapon shop requires its separate recipe/upgrade contract." : "Native shop argument selects a catalog outside the proved original rows.");
        }
        return catalog;
    }

    public uint BuyPrice(int itemId, uint abilityMask)
    {
        NativeShopPrice price = ResolvePrice(itemId);
        return (uint)Math.Max(1, (abilityMask & 1) != 0 ? price.Base * 15 / 2 : price.Base * 10);
    }

    public uint SellPrice(int itemId, uint abilityMask)
    {
        NativeShopPrice price = ResolvePrice(itemId);
        return (uint)Math.Max(1, (abilityMask & 2) != 0 ? price.Base * price.SellMultiplier * 3 / 10 : price.Base * price.SellMultiplier / 5);
    }

    public NativeShopPrice ResolvePrice(int itemId)
    {
        if ((uint)itemId >= Prices.Length || Prices[itemId].Name is null)
        {
            throw new NotSupportedException("Native shop item requires its original retained price and name definition.");
        }
        return Prices[itemId];
    }
}
