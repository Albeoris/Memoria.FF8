using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoneAnchor67E550Tests
{
    [Fact]
    public void Current_translation_stays_DWORD_through_ordered_metadata_reads_and_FPU_rotation()
    {
        List<string> reads = [];
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(12, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, new byte[] { 0xEF });
        NativeBattleActionMatrix current = NativeBattleCuragaMatrices.Identity8DD770() with { X = 65537, Y = -65538, Z = 32769 };
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 100, Y = -200, Z = 300 };
        NativeBattleBoneAnchor67E550Step result = NativeBattleBoneAnchor67E550.Query(0xF1, temporary, point, selector =>
        {
            Assert.Equal(0xF1, selector);
            reads.Add("alias");
            return 3;
        }, bone =>
        {
            Assert.Equal(3, bone);
            reads.Add("parent");
            return 2;
        }, bone =>
        {
            Assert.Equal(3, bone);
            reads.Add("current");
            return NativeBattlePackedMatrix32.FromMatrix(current, null);
        }, () =>
        {
            reads.Add("world");
            return NativeBattlePackedMatrix32.FromMatrix(world, null);
        });
        Assert.Equal(0, result.Result);
        Assert.Equal(new[] { "alias", "parent", "current", "world" }, reads);
        Assert.Equal(new int[] { 32769, -65538, -65537 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(result.Temporary20.Read(axis * 4, 4))));
        Assert.Equal(new short[] { -32667, -202, 299 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(result.Point8.Read(axis * 2, 2))));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), result.Temporary20.Read(12, 8));
        Assert.Equal((byte)0xEF, result.Point8.Read(6, 1)[0]);
        Assert.False(result.Point8.ReadSnapshot(7, 1).Known[0]);
        Assert.False(temporary.ReadSnapshot(0, 1).Known[0]);
        Assert.False(point.ReadSnapshot(0, 1).Known[0]);
    }
}
