using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeShotInvocationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(7, false)]
    public void Installed_shot_actions_share_the_effect_and_read_only_the_firing_target_flags(int ammunition, bool group)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleNativeAction opening = Assert.Single(assets.NativeActions, action => action.Command == 14 && action.ActionId == ammunition);
        BattleNativeAction shot = Assert.Single(assets.NativeActions, action => action.Command == 237 && action.ActionId == ammunition);
        BattleNativeAction ending = Assert.Single(assets.NativeActions, action => action.Command == 238 && action.ActionId == ammunition);

        Assert.Equal(opening.MagicId, shot.MagicId);
        Assert.Equal(shot.MagicId, ending.MagicId);
        Assert.Equal(group, (shot.TargetFlags!.Value & 0x10) == 0);
        Assert.Equal(new byte[] { 14, 237, 238 }, new[] { opening.Command, shot.Command, ending.Command });
    }
}
