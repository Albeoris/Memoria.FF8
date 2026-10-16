using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActionCameraTests
{
    [Fact]
    public void InstalledExecutableAndModelsSupplyTheOriginalCameraInputs()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleActionCameraRules rules = game.ReadNativeActionCameraRules();
        Assert.Equal(new ushort[] { 0, 16, 32, 64, 256 }, rules.Increment);
        Assert.Equal(new ushort[] { 0, 49, 97, 193, 0 }, rules.RandomRange);
        Assert.Equal(new ushort[] { 0, 96, 128, 192, 256 }, rules.EnemyThreshold);
        Assert.Equal((byte)2, NativeBattleActionCameraRules.Frequency(game.ReadNewGameInputConfiguration().Options));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionCameraRules.Frequency(0x50000));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionCameraRulesReader.Read(new byte[128]));
        using BattleAssets battle = new(game.GameDirectory);
        NativeBattleGraphicsResource ifrit = battle.ReadEnemyNativeGraphicsResource(61);
        Assert.True(ifrit.HasActionCamera);
        Assert.NotNull(ifrit.ActionCameraBank);
        Assert.NotEmpty(ifrit.ActionCameraBank.Data);
        Assert.Equal(760, ifrit.ActionCameraBank.Data.Length);
        Assert.Equal((byte)1, ifrit.ActionCameraBank.Data[0]);
        NativeBattleGraphicsResource squall = battle.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        Assert.True(squall.HasActionCamera);
        Assert.Equal(1720, squall.ActionCameraBank!.Data.Length);
        Assert.Equal(squall.HasActionCamera == true, squall.ActionCameraBank is not null);
    }
}
