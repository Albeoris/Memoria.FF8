using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanModelSelectionResult(uint SelectedPhysicalModel, byte Category, ImmutableArray<uint> SelectionFlags);

public static class NativeBattleScanModelSelection
{
    public const uint ModelBase = 0x1D972C0;
    public const int ModelStride = 0x9C;

    public static NativeBattleScanModelSelectionResult Select84D320(ImmutableArray<NativeBattleScanByteOwner> models, short actor, Func<uint, byte> readExternalByte, Func<int, byte>? readResourceSequence = null)
    {
        Validate(models);
        uint source = Address(actor);
        uint selected = Minimum501FF0(models, source);
        byte category = Category84D410(models, actor, readExternalByte, readResourceSequence);
        HashSet<uint> included = [];
        if (category == 0x54)
        {
            for (int index = 0; index < 7; index++)
            {
                if (models[index].ReadByte(4) is 0x54 or 0x60 or 0x61)
                {
                    included.Add(Address(index));
                }
            }
        }
        else
        {
            uint current = source;
            HashSet<uint> visited = [];
            do
            {
                if (!visited.Add(current))
                {
                    throw new InvalidDataException("Native Scan model ring does not return to its actual source.");
                }
                NativeBattleScanByteOwner model = models[Index(current)];
                if ((model.ReadByte(0) & 2) != 0)
                {
                    included.Add(current);
                }
                current = model.ReadDword(0x8C);
            }
            while (current != 0 && current != source);
        }
        return new(selected, category, Enumerable.Range(0, 7).Select(index => included.Contains(Address(index)) ? 1u : 0u).ToImmutableArray());
    }

    public static uint Minimum501FF0(ImmutableArray<NativeBattleScanByteOwner> models, uint source)
    {
        Validate(models);
        return Address(Minimum501FF0(Index(source), index =>
        {
            uint next = models[index].ReadDword(0x8C);
            return next != 0 ? Index(next) : null;
        }));
    }

    public static int Minimum501FF0(ImmutableArray<NativeBattleGraphicsModel> models, int source)
    {
        if (models.IsDefault || models.Length != 7)
        {
            throw new InvalidDataException("Native501FF0 requires its seven physical graphics models.");
        }
        _ = Address(source);
        return Minimum501FF0(source, index => models[index].ReadLinkedModelIndex140());
    }

    private static int Minimum501FF0(int source, Func<int, int?> readNext)
    {
        int? next = readNext(source);
        int minimum = source;
        int visited = 1 << source;
        while (next != source)
        {
            if (next is null)
            {
                return source;
            }
            if ((visited & (1 << next.Value)) != 0)
            {
                throw new InvalidDataException("Native501FF0 model ring does not return to its actual source.");
            }
            visited |= 1 << next.Value;
            minimum = Math.Min(minimum, next.Value);
            next = readNext(next.Value);
        }
        return minimum;
    }

    public static byte Category84D410(ImmutableArray<NativeBattleScanByteOwner> models, short actor, Func<uint, byte> readExternalByte, Func<int, byte>? readResourceSequence = null)
    {
        Validate(models);
        ArgumentNullException.ThrowIfNull(readExternalByte);
        NativeBattleScanByteOwner model = models[Index(Address(actor))];
        byte category = model.ReadByte(4);
        return category switch
        {
            0x24 or 0x25 => model.ReadDword(0x7C) == uint.MaxValue ? (byte)0xA0 : category,
            0x2B => Sequence() != 1 ? (byte)0xA1 : category,
            0x77 => Sequence() != 1 ? (byte)0xA2 : category,
            0x3B => model.ReadWord(0x9A) switch { 0x1800 => 0xA3, 0x2000 => 0xA4, _ => category },
            0x4B => Sequence() != 1 ? (byte)0xA5 : category,
            _ => category
        };

        byte Sequence() => readResourceSequence is not null ? readResourceSequence(actor) : readExternalByte(model.ReadDword(0x74));
    }

    private static uint Address(int actor)
    {
        if ((uint)actor >= 7)
        {
            throw new NotSupportedException("Native Scan actor leaves its actual seven physical model records.");
        }
        return NativeBattleGraphicsModelAddress.Encode(actor);
    }

    private static int Index(uint address)
    {
        if (!NativeBattleGraphicsModelAddress.TryDecode(address, out int row))
        {
            throw new NotSupportedException("Native Scan model link leaves its actual physical bank.");
        }
        return row;
    }

    private static void Validate(ImmutableArray<NativeBattleScanByteOwner> models)
    {
        if (models.IsDefault || models.Length != 7 || models.Any(model => model is null))
        {
            throw new InvalidDataException("Native Scan requires its seven current physical model records.");
        }
        foreach (NativeBattleScanByteOwner model in models)
        {
            model.RequireLength(ModelStride);
        }
    }
}
