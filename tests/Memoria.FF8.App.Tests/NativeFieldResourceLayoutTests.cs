using System.Reflection;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldResourceLayoutTests
{
    [Theory]
    [InlineData(95, 23_140)]
    [InlineData(104, 103_308)]
    [InlineData(263, 72_128)]
    [InlineData(264, 186_560)]
    [InlineData(265, 54_600)]
    public void OriginalFieldFilesRetainTheirNativePreModelAllocationCursor(int fieldId, int expectedCursor)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        MethodInfo method = typeof(GameAssets).GetMethod("ReadNativePreModelCursor", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(typeof(GameAssets).FullName, "ReadNativePreModelCursor");
        int cursor = Assert.IsType<int>(method.Invoke(assets, [fieldId]));
        Assert.Equal(expectedCursor, cursor);
    }

    [Fact]
    public void OriginalCurrentLoadSlabProvidesBothEmptyInstanceTerminalWords()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        IReadOnlyList<ushort?> terminals = assets.ReadNativeEmptyAnimationTerminals(264, [0, 1, 2, -1, -1]);
        Assert.Equal(5, terminals.Count);
        Assert.Null(terminals[0]);
        Assert.Null(terminals[1]);
        Assert.Null(terminals[2]);
        Assert.Equal((ushort?)257, terminals[3]);
        Assert.Equal((ushort?)57121, terminals[4]);
    }
}
