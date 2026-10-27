namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect9Tests
{
    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(0, 1, 1, false)]
    [InlineData(1, 1, 3, false)]
    [InlineData(1, 1, 2, true)]
    [InlineData(-1, -1, 0, true)]
    public void ControlRequestRequiresAcknowledgedSignedPredecessor(int actual, int requested, int next, bool accepted)
    {
        uint control = NativeBattleEffect9State.ControlAddress;
        NativeBattleEffect9State state = NativeBattleEffect9State.FromColdProcess().WriteWord(control + 0x40, unchecked((ushort)actual)).WriteWord(control + 0x44, unchecked((ushort)requested));
        NativeBattleEffect9State? result = NativeBattleEffect9Controller.RequestControl(state, next);
        Assert.Equal(accepted, result is not null);
        Assert.Equal(unchecked((ushort)(accepted ? next : requested)), state.ReadWord(control + 0x44));
        if (result is not null)
        {
            Assert.Equal(unchecked((ushort)next), result.ReadWord(control + 0x44));
            Assert.Equal(unchecked((ushort)actual), result.ReadWord(control + 0x40));
        }
    }

    [Theory]
    [InlineData(0, 100, 2048)]
    [InlineData(100, 0, 3072)]
    [InlineData(-100, 0, 1024)]
    [InlineData(0, -100, 0)]
    public void InitialControlRetainsPackedTargetAndUsesSignedCoordinateWords(int x, int z, int expectedHeading)
    {
        uint control = NativeBattleEffect9State.ControlAddress;
        uint xy = 0x9ABC0000u | unchecked((ushort)x);
        uint zw = 0x76540000u | unchecked((ushort)z);
        NativeBattleEffect9State state = NativeBattleEffect9State.FromColdProcess().WriteDword(control - 4, 0xDEADBEEF).WriteDword(control + NativeBattleEffect9State.ControlLength, 0xCAFEBABE);
        state = NativeBattleEffect9Controller.InitializeControl(state, xy, zw);
        Assert.Equal(xy, state.ReadDword(control + 8));
        Assert.Equal(zw, state.ReadDword(control + 12));
        Assert.Equal(expectedHeading, state.ReadWord(control + 0x4A));
        Assert.Equal(new byte[8], state.Read(control, 8));
        Assert.Equal(new byte[0x30], state.Read(control + 0x10, 0x30));
        Assert.Equal(0xDEADBEEFu, state.ReadDword(control - 4));
        Assert.Equal(0xCAFEBABEu, state.ReadDword(control + NativeBattleEffect9State.ControlLength));
    }
}
