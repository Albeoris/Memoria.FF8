using System.Buffers.Binary;
using Memoria.FF7.Formats;
using static Memoria.FF7.Resources.Ff7BattleSystemMessages;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattleSystemMessagesTests
{
    [Fact]
    public void NativeTokensBindUnsignedWordsAndKeepResourceGlyphsAndDictionaryExpansion()
    {
        Dictionary<int, string> names = new() { [0] = "Sample", [319] = "Accessory" };
        byte[] section = Section([0x21, 0xEB, 0xFF, 0xFF, 0x22, 0xFF], [0xF9, 5, 0x23, 0xFF], [0x24, 0xEC, 0xFF, 0xFF, 0x25, 0xFF], [0xEC, 0xFF, 0xFF, 0x26, 0xFF]);
        Ff7BattleSystemMessages messages = Ff7BattleSystemMessages.Read(section, names);
        names[0] = "Changed";
        Assert.Equal("ASampleB", messages.FormatItemMessage(ItemReceived, 0));
        Assert.Equal("AAccessoryC", messages.FormatItemMessage(ItemStolen, 319));
        Assert.Equal("D65535E", messages.FormatNumberMessage(GilReceived, ushort.MaxValue));
        Assert.Equal("0F", messages.FormatNumberMessage(GilStolen, 0));
        Assert.Equal(new byte[] { 0x21, 0xEB, 0xFF, 0xFF, 0x23, 0xFF }, KernelTextArchive.ReadExpandedStrings(section, 128)[ItemStolen].ToArray());
        Assert.Throws<NotSupportedException>(() => messages.FormatItemMessage(52, 0));
        Assert.Throws<NotSupportedException>(() => messages.FormatNumberMessage(55, 0));
        Assert.Throws<NotSupportedException>(() => messages.FormatItemMessage(GilReceived, 0));
        Assert.Throws<NotSupportedException>(() => messages.FormatNumberMessage(ItemStolen, 0));
        Assert.Throws<NotSupportedException>(() => messages.FormatNumberMessage(0x56, 0));
        Assert.Throws<NotSupportedException>(() => messages.FormatItemMessage(ItemReceived, ushort.MaxValue));
    }

    [Fact]
    public void EachTemplateRequiresExactlyOneUnboundTokenOfItsProvedKind()
    {
        Dictionary<int, string> names = new() { [0] = "Sample" };
        foreach (byte[] invalid in new byte[][] { [0xFF], [0xEB, 0, 0xFF, 0xFF], [0xEB, 0xFF, 0xFF, 0xEB, 0xFF, 0xFF, 0xFF] })
        {
            Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(Section(itemReceived: invalid), names));
        }
        foreach (byte control in new byte[] { 0xEA, 0xEC, 0xED, 0xF0, 0xF1, 0xE0 })
        {
            Assert.Throws<NotSupportedException>(() => Ff7BattleSystemMessages.Read(Section(itemReceived: [control, 0xFF, 0xFF, 0xFF]), names));
        }
        Assert.Throws<NotSupportedException>(() => Ff7BattleSystemMessages.Read(Section(numberReturned: [0xEB, 0xFF, 0xFF, 0xFF]), names));
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(Section(numberStolen: [0xEC])[..^(127 - GilStolen)], names));
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(Section(numberStolen: [0xEC, 0xFF, 0xFF])[..^(127 - GilStolen)], names));
    }

    [Fact]
    public void SharedExpansionRejectsCorruptDirectoriesReferencesAndOversizedStrings()
    {
        Dictionary<int, string> names = new();
        byte[] badDirectory = Section();
        badDirectory[1] = 0;
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(badDirectory, names));
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(new byte[255], names));
        byte[] badOffset = Section();
        BinaryPrimitives.WriteUInt16LittleEndian(badOffset.AsSpan(ItemReceived * 2), 0);
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(badOffset, names));
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(Section(numberStolen: [0xF9])[..^(127 - GilStolen)], names));
        Assert.Throws<InvalidDataException>(() => Ff7BattleSystemMessages.Read(Section(numberStolen: [0xF9, 0]), names));
        Assert.Throws<InvalidDataException>(() => KernelTextArchive.ReadExpandedStrings(Section(), 128, maximumStringBytes: 3));
        Assert.Throws<NotSupportedException>(() => KernelTextArchive.ReadPlainStrings(Section(), 128));
    }

    [Fact]
    public void OriginalEconomyMessagesUseNativeTemplatesAndKernelItemNames()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the battle system message check.");
        Ff7BattleSystemMessages messages = Ff7BattleSystemMessages.Load(Ff7Installation.Discover(root));
        Assert.Equal("Received Potion.", messages.FormatItemMessage(ItemReceived, 0));
        Assert.Equal("Ether was stolen.", messages.FormatItemMessage(ItemStolen, 3));
        Assert.Equal("Received 65535 gil.", messages.FormatNumberMessage(GilReceived, ushort.MaxValue));
        Assert.Equal("0 gil was stolen.", messages.FormatNumberMessage(GilStolen, 0));
    }

    private static byte[] Section(byte[]? itemReceived = null, byte[]? itemStolen = null, byte[]? numberReturned = null, byte[]? numberStolen = null)
    {
        List<byte> data = new(new byte[256]);
        for (int index = 0; index < 128; index++)
        {
            byte[] text = index switch
            {
                ItemReceived => itemReceived ?? [0xEB, 0xFF, 0xFF, 0xFF],
                ItemStolen => itemStolen ?? [0xEB, 0xFF, 0xFF, 0xFF],
                GilReceived => numberReturned ?? [0xEC, 0xFF, 0xFF, 0xFF],
                GilStolen => numberStolen ?? [0xEC, 0xFF, 0xFF, 0xFF],
                _ => [0xFF]
            };
            ushort offset = checked((ushort)data.Count);
            data[index * 2] = (byte)offset;
            data[index * 2 + 1] = (byte)(offset >> 8);
            data.AddRange(text);
        }
        return data.ToArray();
    }
}
