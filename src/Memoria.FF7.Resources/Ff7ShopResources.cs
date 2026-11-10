using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public enum Ff7ShopStockKind
{
    Item,
    Materia,
    Special
}

public sealed record Ff7ShopStockEntry(int Index, Ff7ShopStockKind Kind, int NativeId, string Name, string Description, uint Price, bool CanSell);
public sealed record Ff7ShopDefinition(int Id, int Category, int TextGroup, IReadOnlyList<Ff7ShopStockEntry> Stock);

public sealed class Ff7ShopResources
{
    private readonly byte[] definitions;
    private readonly uint[] prices;
    private readonly ushort[] itemRestrictions;
    private readonly Ff7KernelTextResources items;
    private readonly IReadOnlyList<string> materiaNames;
    private readonly IReadOnlyList<string> materiaDescriptions;
    private readonly byte[] materiaFlags;

    private Ff7ShopResources(byte[] definitions, uint[] prices, ushort[] itemRestrictions, Ff7KernelTextResources items, IReadOnlyList<string> materiaNames, IReadOnlyList<string> materiaDescriptions, byte[] materiaFlags)
    {
        this.definitions = definitions;
        this.prices = prices;
        this.itemRestrictions = itemRestrictions;
        this.items = items;
        this.materiaNames = materiaNames;
        this.materiaDescriptions = materiaDescriptions;
        this.materiaFlags = materiaFlags;
    }

    public static Ff7ShopResources Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var native = Ff7PcExecutableData.LoadShopData(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        if (kernel.Count <= 8 || kernel[4].Data.Length != 128 * 28 || kernel[5].Data.Length != 128 * 44 || kernel[6].Data.Length != 32 * 36 || kernel[7].Data.Length != 32 * 16 || kernel[8].Data.Length != 96 * 20)
        {
            throw new InvalidDataException("The native shop requires the original item and materia kernel record directories.");
        }
        ushort[] restrictions = new ushort[320];
        foreach (var (section, first, count, stride, offset) in new[] { (4, 0, 128, 28, 10), (5, 128, 128, 44, 42), (6, 256, 32, 36, 32), (7, 288, 32, 16, 14) })
        {
            for (int index = 0; index < count; index++)
            {
                restrictions[first + index] = BinaryPrimitives.ReadUInt16LittleEndian(kernel[section].Data.Span.Slice(index * stride + offset));
            }
        }
        IReadOnlyList<ReadOnlyMemory<byte>> text = KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path));
        byte[] materiaFlags = new byte[96];
        for (int id = 0; id < materiaFlags.Length; id++)
        {
            materiaFlags[id] = (byte)((native.MateriaClasses[kernel[8].Data.Span[id * 20 + 13] & 15] == 10 ? 1 : 0) | (id == 44 ? 2 : 0));
        }
        return new(native.Definitions, native.Prices, restrictions, Ff7KernelTextResources.Load(installation), KernelTextArchive.ReadPlainStrings(text[14].Span, 96), KernelTextArchive.ReadPlainStrings(text[6].Span, 96), materiaFlags);
    }

    public Ff7ShopDefinition Get(int shopId)
    {
        if (shopId is < 0 or >= 80)
        {
            throw new NotSupportedException("The native shop index is outside the recognized executable directory.");
        }
        ReadOnlySpan<byte> row = definitions.AsSpan(shopId * 84, 84);
        int count = BinaryPrimitives.ReadInt16LittleEndian(row[2..]);
        if (count is < 0 or > 10)
        {
            throw new InvalidDataException("The native shop stock count is invalid.");
        }
        List<Ff7ShopStockEntry> stock = new(count);
        for (int index = 0; index < count; index++)
        {
            int kind = BinaryPrimitives.ReadInt16LittleEndian(row[(4 + index * 8)..]);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(row[(8 + index * 8)..]);
            if (kind is < 0 or > 2 || id > int.MaxValue)
            {
                throw new InvalidDataException("The native shop stock entry is invalid.");
            }
            if (kind == 0)
            {
                stock.Add(DescribeItem((int)id) with { Index = index });
            }
            else if (kind == 1 && id < 96)
            {
                stock.Add(DescribeMateria((int)id) with { Index = index });
            }
            else
            {
                throw new NotSupportedException("This native shop stock kind requires its original resource owner.");
            }
        }
        return new(shopId, unchecked((sbyte)row[0]), unchecked((sbyte)row[1]), stock.AsReadOnly());
    }

    public Ff7ShopStockEntry DescribeItem(int itemId)
    {
        if (itemId is < 0 or >= 320)
        {
            throw new NotSupportedException("The native shop item has no supported original item namespace entry.");
        }
        return new(-1, Ff7ShopStockKind.Item, itemId, items.ItemNames[itemId], items.ItemDescriptions[itemId], prices[itemId], (itemRestrictions[itemId] & 1) == 0);
    }

    public Ff7ShopStockEntry DescribeMateria(int materiaId)
    {
        if (materiaId is < 0 or >= 96)
        {
            throw new NotSupportedException("The native shop materia has no original kernel entry.");
        }
        return new(-1, Ff7ShopStockKind.Materia, materiaId, materiaNames[materiaId], materiaDescriptions[materiaId], prices[384 + materiaId], true);
    }

    public uint MateriaSalePrice(int materiaId, uint abilityPoints)
    {
        uint price = DescribeMateria(materiaId).Price;
        if (abilityPoints > 0xFFFFFF)
        {
            throw new InvalidDataException("The native materia AP value exceeds24 bits.");
        }
        return price == 1 ? 1 : abilityPoints == 0xFFFFFF ? unchecked(price * 70) : abilityPoints;
    }

    public byte MateriaAcquisitionFlags(int materiaId)
    {
        _ = DescribeMateria(materiaId);
        return materiaFlags[materiaId];
    }
}
