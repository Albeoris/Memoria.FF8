using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native shop's200-byte count cache and local gil, distinct from saved inventory until4EB9F0 commits.</summary>
public sealed record NativeShopTransaction([property: JsonRequired] int RawArgument, [property: JsonRequired] byte Catalog, [property: JsonRequired] NativeShopDefinitions Definitions, [property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Counts, [property: JsonRequired] uint Gil, [property: JsonRequired] bool Past, [property: JsonRequired] uint AbilityMask, [property: JsonRequired] ImmutableArray<byte> AvailableItems)
{
    public static NativeEconomy NormalizeInventory(NativeEconomy economy)
    {
        NativeEconomy result = economy.DeepCopy();
        for (int slot = 0; slot < result.Items.Length; slot++)
        {
            NativeItemStock stock = result.Items[slot];
            if (stock.Count == 0 || stock.ItemId == 0)
            {
                result.Items[slot] = new NativeItemStock(0, 0);
            }
        }
        return result;
    }

    public static NativeShopTransaction Begin(int rawArgument, NativeShopDefinitions definitions, NativeEconomy economy, NativeCharacterVitals vitals, NativeShopHistory history)
    {
        definitions.Validate();
        economy.Validate();
        vitals.Validate();
        history.Validate();
        if (economy.InitOutSha256 != vitals.InitOutSha256 || history.InitOutSha256 != vitals.InitOutSha256 || definitions.KernelHash != vitals.KernelSha256)
        {
            throw new InvalidDataException("Native shop owners have different original initialization/kernel identities.");
        }
        byte catalog = definitions.ResolveCatalog(rawArgument);
        ImmutableArray<byte> counts = CreateCounts(economy);
        ImmutableArray<byte> available = definitions.Catalogs[catalog].Select((entry, slot) => (entry, slot)).Where(item => item.entry.ItemId != 0 && history.Bytes[20 * catalog + item.slot] != 0).Select(item => item.entry.ItemId).ToImmutableArray();
        NativeShopTransaction result = new(rawArgument, catalog, definitions, economy.InitOutSha256, counts, vitals.DreamEnabled ? economy.PastGil : economy.Gil, vitals.DreamEnabled, vitals.ShopAbilityMask(), available);
        result.Validate();
        return result;
    }

    public static ImmutableArray<byte> CreateCounts(NativeEconomy economy)
    {
        economy.Validate();
        byte[] counts = new byte[200];
        foreach (NativeItemStock stock in economy.Items)
        {
            if (stock.ItemId >= counts.Length)
            {
                throw new NotSupportedException("Native shop inventory would write outside its retained200-byte count cache.");
            }
            if (stock.ItemId != 0)
            {
                counts[stock.ItemId] = stock.Count;
            }
        }
        return ImmutableArray.CreateRange(counts);
    }

    public void Validate()
    {
        if (Definitions is null || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || Counts.IsDefault || Counts.Length != 200 || AvailableItems.IsDefault)
        {
            throw new InvalidDataException("Native shop transaction lacks its complete current local cache, gil and definitions.");
        }
        Definitions.Validate();
        if (Catalog != Definitions.ResolveCatalog(RawArgument) || AvailableItems.Any(id => id == 0 || id >= Definitions.Prices.Length || !Definitions.Catalogs[Catalog].Any(entry => entry.ItemId == id)))
        {
            throw new InvalidDataException("Native shop transaction changed its actual catalog or available item identities.");
        }
    }

    public uint Price(int itemId) => Definitions.BuyPrice(itemId, AbilityMask);

    public int MaximumPurchase(int itemId)
    {
        Validate();
        if ((uint)itemId >= Counts.Length || !AvailableItems.Contains(unchecked((byte)itemId)) || Counts[itemId] >= 100)
        {
            return 0;
        }
        return (int)Math.Min(100u - Counts[itemId], Gil / Price(itemId));
    }

    public NativeShopTransaction Buy(int itemId, int quantity, out bool accepted)
    {
        int maximum = MaximumPurchase(itemId);
        accepted = quantity > 0 && quantity <= maximum;
        if (!accepted)
        {
            return this;
        }
        uint cost = Price(itemId) * (uint)quantity;
        return this with { Gil = unchecked(Gil - cost), Counts = Counts.SetItem(itemId, unchecked((byte)(Counts[itemId] + quantity))) };
    }

    public NativeShopTransaction Sell(int itemId, int quantity, out bool accepted)
    {
        Validate();
        int maximum = itemId > 0 && itemId < Definitions.Prices.Length ? Counts[itemId] : 0;
        // Native quantity-dialog comparisons sign-extend both BYTEs; counts>=128
        // cannot increment its initial quantity1 through the original controls.
        if (maximum >= 128)
        {
            maximum = 1;
        }
        accepted = quantity > 0 && quantity <= maximum;
        if (!accepted)
        {
            return this;
        }
        uint income = Definitions.SellPrice(itemId, AbilityMask) * (uint)quantity;
        return this with { Gil = Math.Min(unchecked(Gil + income), NativeEconomy.GilCeiling), Counts = Counts.SetItem(itemId, unchecked((byte)(Counts[itemId] - quantity))) };
    }

    public NativeEconomy Commit(NativeEconomy economy)
    {
        Validate();
        return CommitCounts(economy, InitOutSha256, Counts, Gil, Past);
    }

    public static NativeEconomy CommitCounts(NativeEconomy economy, string initOutSha256, ImmutableArray<byte> cache, uint gil, bool past)
    {
        if (cache.IsDefault || cache.Length != 200)
        {
            throw new InvalidDataException("Native shop commit requires its actual complete two-hundred-byte local cache.");
        }
        NativeEconomy result = economy.DeepCopy();
        if (economy.InitOutSha256 != initOutSha256)
        {
            throw new InvalidDataException("Native shop cannot commit to another inventory initialization.");
        }
        byte[] counts = cache.ToArray();
        for (int slot = 0; slot < result.Items.Length; slot++)
        {
            byte id = result.Items[slot].ItemId;
            if (id >= counts.Length)
            {
                throw new NotSupportedException("Native shop commit reads an item outside the retained count cache.");
            }
            byte count = counts[id];
            result.Items[slot] = new NativeItemStock(count == 0 ? (byte)0 : id, count);
            counts[id] = 0;
        }
        int destination = 0;
        for (int id = 0; id < counts.Length; id++)
        {
            if (counts[id] == 0)
            {
                continue;
            }
            while (destination < result.Items.Length && result.Items[destination].ItemId != 0 && result.Items[destination].Count != 0)
            {
                destination++;
            }
            if (destination >= result.Items.Length)
            {
                throw new NotSupportedException("Native shop commit would write beyond the retained198 physical item pairs.");
            }
            result.Items[destination] = new NativeItemStock((byte)id, counts[id]);
            result = result.UpdateItemOrder(id);
        }
        return past ? result with { PastGil = gil } : result with { Gil = gil };
    }
}
