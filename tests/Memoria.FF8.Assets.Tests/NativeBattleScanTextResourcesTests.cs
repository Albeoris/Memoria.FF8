using System.Buffers.Binary;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleScanTextResourcesTests
{
    [Fact]
    public void Installed_scan_descriptions_use_original_directory_and_bounded_bytecode()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var text = game.ReadNativeScanTextResources();
        Assert.Same(text, game.ReadNativeScanTextResources());
        NativeExecutableMemory memory = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        byte[] directory = memory.Read(0x1887474, 320);
        Assert.Equal(memory.Read(0x18875B4, 0x3A5F), text.DescriptionBytes.ToArray());
        for (int index = 0; index < 160; index++)
        {
            uint pointer = 0x18875B4u + BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(index * 2, 2));
            Assert.Equal(pointer, text.DescriptionPointer(index));
            Assert.Equal(memory.Read(pointer, 1)[0], text.ReadDescriptionByte(pointer));
        }
        Assert.Throws<NotSupportedException>(() => text.DescriptionPointer(160));
        Assert.Throws<NotSupportedException>(() => text.ReadDescriptionByte(0));
    }
}
