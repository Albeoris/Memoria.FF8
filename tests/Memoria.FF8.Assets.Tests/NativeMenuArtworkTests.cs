using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMenuArtworkTests
{
    [Fact]
    public void OriginalPcPortraitsKeepEveryNativeCharacterAndGuardianForceIdentity()
    {
        string gameDirectory = InstalledGameDirectory();
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(gameDirectory);
        HashSet<string> hashes = [];
        for (int id = 0; id < NativeMenuArtwork.CharacterCount; id++)
        {
            PixelImage image = artwork.GetCharacterPortrait(id);
            Assert.Equal(64, image.Width);
            Assert.Equal(96, image.Height);
            Assert.All(image.Pixels, pixel => Assert.Equal(byte.MaxValue, pixel.A));
            Assert.True(hashes.Add(Hash(image)), $"Character portrait {id} repeats another native identity.");
        }
        for (int id = 0; id < NativeMenuArtwork.GuardianForceCount; id++)
        {
            PixelImage image = artwork.GetGuardianForcePortrait(id);
            Assert.Equal(64, image.Width);
            Assert.Equal(96, image.Height);
            Assert.All(image.Pixels, pixel => Assert.Equal(byte.MaxValue, pixel.A));
            Assert.True(hashes.Add(Hash(image)), $"GF portrait {id} repeats another native identity.");
        }
        Assert.Equal("54222106922A80B428255505B5747876544DDDA8FDA55F073106A6331AEF0DC6", Hash(artwork.GetCharacterPortrait(0)));
        Assert.Equal("EAF55493F5D8FBB3228918CED8AE82646EFF32F055AA61DC49F57D4101D3935E", Hash(artwork.GetCharacterPortrait(4)));
        Assert.Equal("34A3E658D8194110A5865DC9B4813289114AB41A3D4DDEF0BEAA182AE7A2DE0E", Hash(artwork.GetCharacterPortrait(8)));
        Assert.Equal("6B9B40C996407B597FFC6205B34B0A3C630E011DD0FF1C3090AEA407D803FB25", Hash(artwork.GetGuardianForcePortrait(0)));
        Assert.Equal("E0C89E495ED7386F0CCDF5D5D1D88C94A5ABE88E070E60DB08C6DD155B1CEAC4", Hash(artwork.GetGuardianForcePortrait(4)));
        Assert.Equal("55F67E28CF631E87CFC2B8385CA2A864D0D6F09B2176CA55A23246A5CE81FE5C", Hash(artwork.GetGuardianForcePortrait(8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetCharacterPortrait(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetCharacterPortrait(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetGuardianForcePortrait(16));
    }

    [Fact]
    public void OriginalIconPartsKeepTheirEmbeddedPaletteAndSignedCompositionOffsets()
    {
        string gameDirectory = InstalledGameDirectory();
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(gameDirectory);
        NativeMenuPointer pointer = NativeMenuPointer.Read(gameDirectory);
        Assert.Equal(329, artwork.IconCount);
        Assert.Equal(Hash(pointer.Image), Hash(artwork.GetIcon(0)));
        Assert.Same(artwork.GetIcon(0), artwork.GetIcon(0));
        PixelImage negativeOffsetParts = artwork.GetIcon(90);
        Assert.Equal(64, negativeOffsetParts.Width);
        Assert.Equal(8, negativeOffsetParts.Height);
        PixelImage separatedParts = artwork.GetIcon(68);
        Assert.Equal(112, separatedParts.Width);
        Assert.Equal(16, separatedParts.Height);
        Assert.Contains(separatedParts.Pixels, pixel => pixel.A == 0);
        for (int id = 0; id < artwork.IconCount; id++)
        {
            if (id is >= 128 and < 140)
            {
                Assert.Throws<NotSupportedException>(() => artwork.GetIcon(id));
            }
            else
            {
                Assert.NotEmpty(artwork.GetIcon(id).Pixels);
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetIcon(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetIcon(artwork.IconCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetIcon(0, 16));
    }

    [Fact]
    public void Native_countdown_digits_format_original_parts_without_font_or_battle_ot_substitution()
    {
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(InstalledGameDirectory());
        HashSet<string> packetHashes = [];
        for (int digit = 0; digit < 10; digit++)
        {
            var opaque = artwork.EncodeNativeIconPackets(0x18 + digit, -12, 19, 0xFFFFFF);
            var semi = artwork.EncodeNativeIconPackets(0x18 + digit, -12, 19, 0x04FFFFFF);
            Assert.NotEmpty(opaque);
            Assert.Equal(0, opaque.Length % 24);
            Assert.Equal(opaque.Length, semi.Length);
            Assert.True(packetHashes.Add(Convert.ToHexString(SHA256.HashData(opaque.AsSpan()))));
            for (int offset = 0; offset < opaque.Length; offset += 24)
            {
                Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(opaque.AsSpan(offset, 4)));
                Assert.Equal(0xE100041Eu, BinaryPrimitives.ReadUInt32LittleEndian(opaque.AsSpan(offset + 4, 4)));
                Assert.Equal(0x64FFFFFFu, BinaryPrimitives.ReadUInt32LittleEndian(opaque.AsSpan(offset + 8, 4)));
                Assert.Equal(0x66FFFFFFu, BinaryPrimitives.ReadUInt32LittleEndian(semi.AsSpan(offset + 8, 4)));
                Assert.True(opaque.AsSpan(offset + 12, 12).SequenceEqual(semi.AsSpan(offset + 12, 12)));
            }
            Assert.NotEmpty(artwork.GetIcon(0x18 + digit).Pixels);
        }
        Assert.Throws<NotSupportedException>(() => artwork.EncodeNativeIconPackets(128, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.EncodeNativeIconPackets(artwork.IconCount, 0, 0, 0));
    }

    private static string InstalledGameDirectory()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs")), "Installed original menu resources are required.");
        return gameDirectory;
    }

    private static string Hash(PixelImage image)
    {
        return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan())));
    }
}
