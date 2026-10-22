using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshZantetsukenProfileTests
{
    [Fact]
    public void Zantetsuken_keeps_the_absent_attack_file_and_longer_camera()
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(327);
        Assert.Equal(0, profile.Variant);
        Assert.Equal(0x58D760u, profile.Entry);
        Assert.Equal(0x58D6E0u, profile.Initializer);
        Assert.Equal(0x2200FB4u, profile.TextureGlobal);
        Assert.Equal(0xCD05A0u, profile.CameraAddress);
        Assert.Equal(250, profile.CameraLength);
        Assert.Equal(0xCCFE54u, profile.VibrationAddress);
        Assert.Equal(326, profile.VibrationLength);
        Assert.Equal((short)60, profile.VibrationChannelCount);
        Assert.Equal(0xE9151Cu, profile.InitialStreamAddress);
        Assert.Equal(0x58DED0u, profile.AttackCallback);
        Assert.Equal(0x3AA9Cu, profile.ClosingSound);
        Assert.False(profile.HasAttackFile);
        Assert.Equal((ushort)0, profile.AttackFile);
        Assert.Throws<InvalidOperationException>(() => profile.AttackFileLength);
        Assert.Throws<InvalidOperationException>(() => profile.AttackFileName);
        Assert.Equal(new ushort[] { 745, 746, 747, 748, 749, 750, 751, 752, 753, 755 }, NativeBattleGilgameshResources.FileIdsForEffect(327));
    }

    [Theory]
    [InlineData(328)]
    [InlineData(329)]
    [InlineData(330)]
    public void Other_swords_keep_their_final_file_and_camera(int effect)
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect((ushort)effect);
        Assert.True(profile.HasAttackFile);
        Assert.Equal(234, profile.CameraLength);
        Assert.Equal(11, NativeBattleGilgameshResources.FileIdsForEffect((ushort)effect).Length);
    }
}
