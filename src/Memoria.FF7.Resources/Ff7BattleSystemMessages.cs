using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed class Ff7BattleSystemMessages
{
    public const int ItemReceived = 0x52;
    public const int ItemStolen = 0x53;
    public const int GilReceived = 0x54;
    public const int GilStolen = 0x55;

    private sealed record Template(string Prefix, string Suffix);

    private readonly Dictionary<int, Template> templates;
    private readonly Dictionary<int, string> itemNames;

    private Ff7BattleSystemMessages(Dictionary<int, Template> templates, IReadOnlyDictionary<int, string> itemNames)
    {
        this.templates = templates;
        this.itemNames = itemNames.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static Ff7BattleSystemMessages Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        Ff7KernelTextResources names = Ff7KernelTextResources.Load(installation);
        IReadOnlyList<ReadOnlyMemory<byte>> sections = KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path));
        return Read(sections[16].Span, names.ItemNames);
    }

    /// <summary>Reads the original 128-entry battle system text category and its single-argument economy templates.</summary>
    public static Ff7BattleSystemMessages Read(ReadOnlySpan<byte> systemMessageSection, IReadOnlyDictionary<int, string> itemNames)
    {
        ArgumentNullException.ThrowIfNull(itemNames);
        if (systemMessageSection.Length < 256 || BinaryPrimitives.ReadUInt16LittleEndian(systemMessageSection) != 256)
        {
            throw new InvalidDataException("The battle system text category requires its original 128-entry directory.");
        }
        IReadOnlyList<ReadOnlyMemory<byte>> strings = KernelTextArchive.ReadExpandedStrings(systemMessageSection, 128);
        Dictionary<int, Template> templates = new();
        for (int index = ItemReceived; index <= GilStolen; index++)
        {
            templates.Add(index, ReadTemplate(strings[index].Span, index < GilReceived ? (byte)0xEB : (byte)0xEC));
        }
        return new(templates, itemNames);
    }

    public string FormatItemMessage(int index, ushort itemId)
    {
        if (index is not (ItemReceived or ItemStolen))
        {
            throw new NotSupportedException($"Battle system message {index} is not a supported item template.");
        }
        if (!itemNames.TryGetValue(itemId, out string? name))
        {
            throw new NotSupportedException($"Battle system item identity {itemId} has no supported kernel name.");
        }
        return Format(index, name);
    }

    public string FormatNumberMessage(int index, ushort number)
    {
        if (index is not (GilReceived or GilStolen))
        {
            throw new NotSupportedException($"Battle system message {index} is not a supported number template.");
        }
        return Format(index, number.ToString(CultureInfo.InvariantCulture));
    }

    private string Format(int index, string argument)
    {
        Template template = templates[index];
        return string.Concat(template.Prefix, argument, template.Suffix);
    }

    private static Template ReadTemplate(ReadOnlySpan<byte> source, byte expectedToken)
    {
        StringBuilder text = new();
        string? prefix = null;
        for (int position = 0; position < source.Length; position++)
        {
            byte code = source[position];
            if (code == 0xFF)
            {
                if (prefix is null)
                {
                    throw new InvalidDataException("A battle economy template must consume exactly one argument.");
                }
                return new(prefix, text.ToString());
            }
            if (code < 0xE0)
            {
                text.Append(FieldText.DecodeEnglishGlyph(code));
            }
            else if (code == 0xF8)
            {
                if (++position >= source.Length)
                {
                    throw new InvalidDataException("A battle system color token is truncated.");
                }
            }
            else if (code == expectedToken)
            {
                if (position + 2 >= source.Length || source[position + 1] != 0xFF || source[position + 2] != 0xFF || prefix is not null)
                {
                    throw new InvalidDataException("A battle economy template requires one unbound native WORD argument.");
                }
                prefix = text.ToString();
                text.Clear();
                position += 2;
            }
            else
            {
                throw new NotSupportedException($"Battle system text control {code:X2} has no supported economy substitution owner.");
            }
        }
        throw new InvalidDataException("A battle system template has no native terminator.");
    }
}
