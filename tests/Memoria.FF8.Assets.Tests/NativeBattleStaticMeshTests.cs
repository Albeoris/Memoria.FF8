using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStaticMeshTests
{
    [Theory]
    [InlineData(1, 0xC7EFD0u, 24, 3, 12, 520)]
    [InlineData(7, 0xC800F0u, 60, 5, 48, 1672)]
    public void InstalledCastingMeshesRetainTheirOriginalPrimitiveDirectories(byte id, uint address, int vertices, int group, int faces, int length)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        var mesh = Assert.Single(game.ReadNativeEffectMeshes(), mesh => mesh.Id == id);
        Assert.Equal(address, mesh.Address);
        Assert.Equal(length, mesh.Bytes.Length);
        Assert.Equal((uint)vertices, BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
        int cursor = 8 + vertices * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int current = 0; current < 8; current++)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            Assert.Equal((uint)(current == group ? faces : 0), count);
            cursor += 4 + checked((int)count) * strides[current];
        }
        Assert.Equal(length, cursor);
        Assert.Equal(game.ReadNativeEffectMeshes(), game.ReadNativeEffectMeshes());
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectStaticResourcesReader.ReadMesh(new byte[128], id));
    }
}
