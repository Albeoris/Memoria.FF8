using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDuel274ResourcesTests
{
    [Fact]
    public void OriginalOpeningResourcesRetainActorDirectoryFiveSpriteEntriesAndMoveDurations()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleDuel274Resources resources = assets.ReadNativeDuel274Resources();
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe")));
        resources.Validate();

        Assert.Equal(original.Read(NativeBattleDuel274Resources.Address, checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))), resources.StaticData.ToArray());
        Assert.Equal(new uint[] { 0x10E778C, 0x10E7958, 0x10E7A34, 0x10E7ADC, 0x10E7C54 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(original.Read(0x10E2998, 0x4DF4), resources.ReadActorDat().ToArray());
        Assert.Equal(0x10, NativeBattleDatDirectory.Section(resources.ReadActorDat().AsSpan(), 0));
        Assert.Equal(0x4DC0, NativeBattleDatDirectory.Section(resources.ReadActorDat().AsSpan(), 1));
        Assert.Equal(0x4DF4, NativeBattleDatDirectory.Section(resources.ReadActorDat().AsSpan(), 2));
        Assert.Equal(new byte[] { 0, 2, 45, 45, 42, 42, 49, 80 }, resources.Read(0x110E3E0, 8).ToArray());
        Assert.Equal(new byte[2], original.ReadLoader(0x25084D8, 2));
        for (int index = 0; index < 5; index++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(checked(0x110E3B8u + (uint)index * 8), 4).AsSpan());
            Assert.Equal(resources.Sprites[index].Address, address);
        }
        foreach (uint address in new uint[] { 0x10FE310, 0x110198C })
        {
            Assert.Equal(original.Read(address, checked((int)(NativeBattleDuel274Resources.EndAddress - address))), resources.Hierarchy(address).Data.ToArray());
        }
        Assert.Throws<NotSupportedException>(() => resources.Read(NativeBattleDuel274Resources.EndAddress, 1));
    }
}
