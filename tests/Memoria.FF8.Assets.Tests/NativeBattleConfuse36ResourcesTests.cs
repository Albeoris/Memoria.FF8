using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleConfuse36ResourcesTests
{
    [Fact]
    public void OriginalActorDirectoryIsAlreadyRelocatedAndRetainsBackwardResourcePointers()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleConfuse36Resources resources = game.ReadNativeConfuse36Resources();
        Assert.Same(resources, game.ReadNativeConfuse36Resources());
        Assert.Equal(0xBE78, resources.Executable.Length);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.AsSpan().Slice(0xBD74)));
        Assert.Equal(0x15E3FF0u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.AsSpan().Slice(0xBD78)));
        Assert.Equal(new uint[] { 0x7F7F7F, 0x3F3F3F, 0xFF000000 }, resources.Overlay);
        Assert.Equal(new short[] { 16, -12, 8, -4, 0x7F }, resources.Shake);
        Assert.Equal(0xD08, resources.Animation.Data.Length);
        Assert.Equal(0x2BBC, resources.ConstantChannels.Length);
        Assert.All(resources.ConstantChannels, value => Assert.Equal((byte)0, value));
        Assert.Equal(new byte[] { 0, 0 }, resources.ReadConstantChannel(0x26AC77E, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadConstantChannel(0x26AC77F, 2));
    }
}
