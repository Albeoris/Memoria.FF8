namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldMapControlTests
{
    [Theory]
    [InlineData(0, 0, 1, 0)]
    [InlineData(1, 0, 2, 0)]
    [InlineData(2, 0, 3, 12)]
    [InlineData(3, 12, 0, 0)]
    [InlineData(2, 1, 0, 1)]
    [InlineData(2, 2, 0, 2)]
    [InlineData(2, 3, 0, 3)]
    [InlineData(2, 4, 0, 4)]
    [InlineData(2, 13, 0, 13)]
    public void SelectCyclesOnlyThroughAdmittedNativeModes(byte mode, byte scriptMode, byte expected, byte expectedScript)
    {
        NativeWorldMapDisplay result = NativeWorldMapControl.Select(mode, scriptMode, Input(0x100, 0), false, false);
        Assert.Equal(new NativeWorldMapDisplay(expected, expectedScript), result);
    }

    [Theory]
    [InlineData(0x100, 0x100)]
    [InlineData(0, 0x100)]
    [InlineData(2, 0)]
    public void HeldAndUnrelatedButtonsDoNotCycle(ushort current, ushort previous)
    {
        Assert.Equal(new NativeWorldMapDisplay(1, 0), NativeWorldMapControl.Select(1, 0, Input(current, previous), false, false));
    }

    [Fact]
    public void DialogueAndUnadmittedProcessModesBlockMapChanges()
    {
        Assert.Equal(new NativeWorldMapDisplay(1, 0), NativeWorldMapControl.Select(1, 0, Input(0x100, 0), true, false));
        Assert.Equal(new NativeWorldMapDisplay(1, 6), NativeWorldMapControl.Select(1, 6, Input(0x100, 0), false, false));
        Assert.Equal(new NativeWorldMapDisplay(0, 0), NativeWorldMapControl.Select(2, 0, Input(0x100, 0), false, true));
    }

    [Fact]
    public void MissingPreviousInputAndInvalidModeAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldMapControl.Select(0, 0, new(0x100, null, 2) { BufferIndex = 1 }, false, false));
        Assert.Throws<InvalidDataException>(() => NativeWorldMapControl.Select(4, 0, Input(0x100, 0), false, false));
    }

    private static NativeWorldInputHistory Input(ushort current, ushort previous) => new(current, previous, 2) { BufferIndex = 0 };
}
