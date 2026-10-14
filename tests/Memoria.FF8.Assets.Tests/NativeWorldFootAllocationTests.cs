using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldFootAllocationTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 34, 0)]
    [InlineData(0, 33, 0, 0, 34, 0)]
    [InlineData(1, 34, 1, 34, 205, 4)]
    [InlineData(1, 204, 1, 34, 205, 4)]
    [InlineData(0, 205, 2, 205, 290, 0)]
    [InlineData(0, 289, 2, 205, 290, 0)]
    public void InstalledOriginalClausesSelectOnlyTheirNativeControlledFootShape(byte entity, ushort story, int clause, ushort minimum, ushort maximum, byte shape)
    {
        using var assets = new WorldAssets(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        Assert.Equal(new NativeWorldFootAllocation(clause, minimum, maximum, entity, shape, new NativeSavedWorldPose(0, 0, 0, 0)), assets.ReadFootAllocation(entity, story));
    }

    [Theory]
    [InlineData(0, 34)]
    [InlineData(0, 204)]
    [InlineData(0, 290)]
    [InlineData(1, 33)]
    [InlineData(1, 205)]
    [InlineData(2, 205)]
    public void MismatchedEntityAndLaterMultiObjectClausesCannotReuseTheFootExclusion(byte entity, ushort story)
    {
        using var assets = new WorldAssets(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        Assert.Throws<NotSupportedException>(() => assets.ReadFootAllocation(entity, story));
    }
}
