using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleScanTextResourcesTests
{
    [Fact]
    public void Effect_materialization_carries_the_byte_category_camera_sources_and_encoded_text_resources()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var binding = game.ReadNativeEffectCatalogForEffects([40]).Single(effect => effect.Effect == 40);
        Assert.Same(game.ReadNativeScanTextResources(), binding.ScanTextResources);
        var profiles = binding.ScanCameraProfiles!.Value;
        Assert.Equal(256, profiles.Length);
        Assert.Same(game.ReadNativeScanCameraProfile(0xA5), profiles[0xA5]);
        Assert.Equal((ushort)255, profiles[255].Category);
    }

    [Theory]
    [InlineData((ushort)0x24)]
    [InlineData((ushort)0x54)]
    [InlineData((ushort)0xA0)]
    [InlineData((ushort)0xA5)]
    public void Installed_camera_profile_reads_only_the_requested_actual_sixteen_byte_category_row(ushort category)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var profile = game.ReadNativeScanCameraProfile(category);
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        uint address = unchecked(0x15D0B40u + (uint)(unchecked((short)category) * 16));
        Assert.Equal(category, profile.Category);
        Assert.Equal(address, profile.Address);
        Assert.Equal(16, profile.Bytes.Length);
        Assert.Equal(original.Read(address, 16), profile.Bytes.ToArray());
        Assert.Same(profile, game.ReadNativeScanCameraProfile(category));
    }

    [Fact]
    public void Installed_kernel_misc_text_preserves_original_WORD_directory_and_encoded_result_page_bytes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var text = game.ReadNativeScanTextResources();
        using IFlArchive main = game.OpenResourceArchive("main");
        using Stream original = main.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("kernel.bin", StringComparison.OrdinalIgnoreCase)).OpenForRead();
        using MemoryStream content = new();
        original.CopyTo(content);
        byte[] kernel = content.ToArray();
        int directory = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 30 * 4));
        int directoryEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 31 * 4));
        int bytes = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 55 * 4));
        Assert.Equal(directory, text.KernelMiscDirectoryFileOffset);
        Assert.Equal(bytes, text.KernelMiscTextFileOffset);
        Assert.Equal(kernel.AsSpan(bytes).ToArray(), text.KernelMiscBytes.ToArray());
        Assert.Equal((directoryEnd - directory) / 2, text.KernelMiscOffsets.Length);
        for (int id = 0; id < text.KernelMiscOffsets.Length; id++)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(directory + id * 2));
            Assert.Equal(offset, text.KernelMiscOffsets[id]);
            if (offset == ushort.MaxValue)
            {
                Assert.Null(text.MiscTextOffset(id));
                Assert.Null(text.ReadEncodedMiscText(id));
                continue;
            }
            Assert.Equal((int?)offset, text.MiscTextOffset(id));
            int length = kernel.AsSpan(bytes + offset).IndexOf((byte)0);
            Assert.True(length >= 0);
            Assert.Equal(kernel.AsSpan(bytes + offset, length + 1).ToArray(), text.ReadEncodedMiscText(id)!.Value.ToArray());
            Assert.Equal(kernel[bytes + offset], text.ReadMiscTextByte(offset));
        }
        int[] resultIds = [0x0B, 0x21, 0x22, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x63, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x71, 0x72];
        Assert.All(resultIds, id =>
        {
            var encoded = text.ReadEncodedMiscText(id) ?? throw new InvalidDataException("Original Scan result text ID has no encoded kernel entry.");
            Assert.Equal((byte)0, encoded[^1]);
        });
        Assert.Throws<NotSupportedException>(() => text.MiscTextOffset(text.KernelMiscOffsets.Length));
        Assert.Throws<NotSupportedException>(() => text.ReadMiscTextByte(text.KernelMiscBytes.Length));
    }

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
