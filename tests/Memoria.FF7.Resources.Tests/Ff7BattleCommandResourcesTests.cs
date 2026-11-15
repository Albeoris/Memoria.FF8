using System.Buffers.Binary;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattleCommandResourcesTests
{
    [Fact]
    public void NamesUseTheirNativeDirectoryAndDictionaryCodec()
    {
        byte[] section = Section();
        Ff7BattleCommandResources resources = Ff7BattleCommandResources.Read(section);
        Assert.Equal("ABCD", resources.Name(0));
        Assert.Equal("ABCD", resources.Name(1));
        Assert.Equal(string.Empty, resources.Name(31));
        section[64] = 0;
        Assert.Equal("ABCD", resources.Name(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.Name(32));
    }

    [Fact]
    public void WrongDirectoriesOffsetsAndDynamicNamesAreRejected()
    {
        byte[] section = Section();
        section[0] = 0;
        Assert.Throws<InvalidDataException>(() => Ff7BattleCommandResources.Read(section));
        Assert.Throws<InvalidDataException>(() => Ff7BattleCommandResources.Read(new byte[63]));
        section = Section();
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(6 * 2), 0);
        Assert.Throws<InvalidDataException>(() => Ff7BattleCommandResources.Read(section));
        section = Section();
        section[64] = 0xEC;
        Assert.Throws<NotSupportedException>(() => Ff7BattleCommandResources.Read(section));
        Assert.Throws<InvalidDataException>(() => Ff7BattleCommandResources.Read(Section()[..^1]));
    }

    [Fact]
    public void OriginalSenseAndManipulateLabelsRetainResourceSpelling()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for command labels.");
        Ff7BattleCommandResources resources = Ff7BattleCommandResources.Load(Ff7Installation.Discover(root));
        Assert.Equal("Attack", resources.Name(1));
        Assert.Equal("Sense", resources.Name(6));
        Assert.Equal("Manip.", resources.Name(0x0B));
        Assert.Equal(string.Empty, resources.Name(31));
    }

    private static byte[] Section()
    {
        byte[] section = new byte[73];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 64);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(2), 69);
        for (int index = 2; index < 32; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(index * 2), 72);
        }
        new byte[] { 0x21, 0x22, 0x23, 0x24, 255, 0xF9, 4, 255, 255 }.CopyTo(section, 64);
        return section;
    }
}
