namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97ModeTests
{
    [Theory]
    [InlineData(0, 0, 1, 1, true)]
    [InlineData(0, 1, 1, 1, false)]
    [InlineData(2, 2, 4, 2, false)]
    [InlineData(65535, 65535, 0, 0, true)]
    [InlineData(32767, 32767, -32768, 32767, false)]
    [InlineData(32768, 32768, -32767, 32769, true)]
    public void Request_checks_current_and_requested_owners_and_signed_predecessor(int current, int requested, int mode, int expected, bool accepted)
    {
        Assert.Equal(((ushort)expected, accepted), NativeBattleChoco97CameraTask.RequestMode730EC0((ushort)current, (ushort)requested, (short)mode));
    }

    [Theory]
    [InlineData(65535, 0, false)]
    [InlineData(65535, -1, true)]
    [InlineData(32768, 0, false)]
    [InlineData(6, 6, true)]
    public void Mode_comparison_is_signed(int current, int mode, bool expected)
    {
        Assert.Equal(expected, NativeBattleChoco97CameraTask.ModeAtLeast730E80((ushort)current, (short)mode));
    }
}
