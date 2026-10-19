using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleXatmDeath276ResourcesTests
{
    [Fact]
    public void OriginalBreakdownResourcesKeepAllFiveEventsAndBothDifferentLengthTables()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleXatmDeath276Resources resources = game.ReadNativeXatmDeath276Resources();
        Assert.Same(resources, game.ReadNativeXatmDeath276Resources());
        Assert.Equal(new uint[] { 0x10D56A8, 0x10D55A8, 0x10D5528, 0x10D5628, 0x10D5728 }, resources.Events.Select(item => item.Address));
        Assert.All(resources.Events, table =>
        {
            Assert.Equal(128, table.Data.Length);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(table.Data.AsSpan().Slice(0x78)) * 24, table.Triangles.Length);
            Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(table.Data.AsSpan().Slice(12)));
        });
        Assert.Equal(512, resources.ArcLengths.Length);
        Assert.Equal(16384, resources.ArcDistances.Length);
        Assert.Equal(256, resources.TriangleLengths.Length);
        Assert.False(Enumerable.Range(0, 64).All(index => resources.TriangleLengths[index] == BinaryPrimitives.ReadUInt16LittleEndian(resources.ArcLengths.AsSpan().Slice(index * 2))));
        Assert.All(resources.EffectCamera.Rotation, value => Assert.Equal((short)0, value));
        Assert.Equal(0, resources.EffectCamera.X);
        Assert.Equal(0, resources.EffectCamera.Y);
        Assert.Equal(0, resources.EffectCamera.Z);
        Assert.All(resources.Sprites, sprite => Assert.NotEmpty(sprite.ProjectedFrame571C80(0).Quads));
    }
}

