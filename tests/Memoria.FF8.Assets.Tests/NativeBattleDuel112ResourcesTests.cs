using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDuel112ResourcesTests
{
    [Fact]
    public void OriginalDuelResourcesRetainActualStaticBytesHierarchyAndLoaderColorGlobals()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleDuel112Resources resources = assets.ReadNativeDuel112Resources();
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe")));
        resources.Validate();
        Assert.Equal(original.Read(NativeBattleDuel112Resources.Address, checked((int)(NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address))), resources.StaticData.ToArray());
        Assert.Equal(original.ReadLoader(0x2521918, 4), resources.ColdGlobals.ToArray());
        Assert.Equal(new uint[] { 0x12C5650, 0x12C5794 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new uint[] { 0x12C5928, 0x12C7230 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(208, BinaryPrimitives.ReadInt32LittleEndian(resources.Read(0x12C592C, 4).AsSpan()));
        foreach (uint address in new uint[] { 0x12C7420, 0x12C8020, 0x12CA7B4, 0x12CB40C, 0x12D19F4, 0x12D4684, 0x12D8D28, 0x12D9DC0 })
        {
            NativeBattleGfAnimationSource source = resources.Hierarchy(address);
            Assert.Equal(address, source.Address);
            Assert.Equal(original.Read(address, checked((int)(NativeBattleDuel112Resources.EndAddress - address))), source.Data.ToArray());
        }
        Assert.Throws<NotSupportedException>(() => resources.Read(NativeBattleDuel112Resources.EndAddress, 1));
    }
}
