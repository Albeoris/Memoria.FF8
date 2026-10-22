using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshMasamuneProfileTests
{
    [Fact]
    public void Masamune_selects_its_real_director_resources_and_short_final_file()
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(328);
        Assert.Equal(1, profile.Variant);
        Assert.Equal(0x58D930u, profile.Entry);
        Assert.Equal(0x58D700u, profile.Initializer);
        Assert.Equal(0x2200FB0u, profile.TextureGlobal);
        Assert.Equal(0xCD069Cu, profile.CameraAddress);
        Assert.Equal(0xCCFF98u, profile.VibrationAddress);
        Assert.Equal(0x14C, profile.VibrationLength);
        Assert.Equal((short)66, profile.VibrationChannelCount);
        Assert.Equal(0xE8D208u, profile.InitialStreamAddress);
        Assert.Equal(0x590510u, profile.AttackCallback);
        Assert.Equal((ushort)754, profile.AttackFile);
        Assert.Equal("mag327_j.dat", profile.AttackFileName);
        Assert.Equal(33312, profile.AttackFileLength);
        Assert.Equal(0xCD0388u, profile.ClosingSoundAddress);
        Assert.Equal(0x3AA9Fu, profile.ClosingSound);
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshProfile.ForEffect(326));
    }

    [Theory]
    [InlineData(329, 2, 756, "mag328_j.dat", 0x14E)]
    [InlineData(330, 3, 757, "mag329_j.dat", 0x14A)]
    public void Existing_sword_profiles_keep_their_original_file_and_vibration_contracts(int effect, int variant, int file, string name, int vibrationLength)
    {
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect((ushort)effect);
        Assert.Equal(variant, profile.Variant);
        Assert.Equal(file, profile.AttackFile);
        Assert.Equal(name, profile.AttackFileName);
        Assert.Equal(99360, profile.AttackFileLength);
        Assert.Equal(vibrationLength, profile.VibrationLength);
    }
}
