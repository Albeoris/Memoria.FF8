using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGuardianBoostProjectionTests
{
    [Fact]
    public void Boost_uses_the_selected_guardians_learned_modifier_flag_and_original_kernel_timers()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using BattleAssets resources = new(assets.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        int bit = Assert.Single(Enumerable.Range(19, 9), index => (vitals.Definitions.GuardianForceModifiers[index - 19].Flags & 1) != 0);
        NativeGuardianForceVital gf = vitals.GuardianForces[0];
        vitals.GuardianForces[0] = gf with { LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High & ~(1UL << bit) } };
        BattleActionDefinition before = Assert.Single(BattlePartyProjection.CreateActions(vitals, resources.ActionDefinitions), action => action.Ability == BattleAbility.GuardianForce && action.Id == 0);
        Assert.False(before.NativeGuardianBoostAvailable);
        vitals.GuardianForces[0] = gf with { LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << bit) } };
        BattleActionDefinition after = Assert.Single(BattlePartyProjection.CreateActions(vitals, resources.ActionDefinitions), action => action.Ability == BattleAbility.GuardianForce && action.Id == 0);
        Assert.True(after.NativeGuardianBoostAvailable);
        BattleNativeAction native = after.NativeAction!;
        NativeBattleGuardianBoost widget = NativeBattleGuardianBoost.Register(native.RawRecord[128], native.RawRecord[129], after.NativeGuardianBoostAvailable!.Value);
        Assert.Equal(15 * native.RawRecord[128], widget.WindowRemaining);
        Assert.Equal(15 * native.RawRecord[129], widget.Remaining);
        Assert.True(widget.Enabled);
        Assert.Equal(before.NativeAction, after.NativeAction);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(gf), System.Text.Json.JsonSerializer.Serialize(assets.ReadNewGameCharacterVitals().GuardianForces[0]));
    }
}
