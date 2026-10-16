using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePlayerCommandTests
{
    [Fact]
    public void Installed_additional_commands_read_actual_target_flags_not_the_menu_mode_byte()
    {
        using BattleAssets assets = InstalledAssets();

        foreach ((byte command, byte flags) in new (byte, byte)[] { (7, 0x50), (23, 0x18), (24, 0x08), (25, 0x14), (26, 0x14), (29, 0x50) })
        {
            BattleNativeCommandBinding binding = assets.ReadNativeCommand(command);
            Assert.Equal(flags, binding.TargetFlags);
            BattleNativeAction action = assets.NativeActions.Single(action => action.Command == command && action.ActionId == binding.ActionId);
            Assert.Equal(flags, action.TargetFlags);
            Assert.Equal(0, action.SupplementalRecord[7]);
            Assert.Equal(flags, action.SupplementalRecord[6]);
        }
    }

    [Fact]
    public void Blue_Magic_invocation_uses_owned_learned_flags_and_the_selected_crisis_record()
    {
        using BattleAssets assets = InstalledAssets();
        NativeLimitProgress progress = new(new string('A', 64), 1, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray());
        BattleSession battle = new(new BattleDefinition { NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, MaximumHp = 100, CurrentHp = 10, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(3) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("quistis", 4)] });
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("quistis", true));

        Assert.Equal((15 << 16) | 0, choice.Id);
        Assert.Equal((byte)4, choice.NativeAction!.CrisisLevel);
        Assert.Equal((byte)0x50, choice.NativeAction.TargetFlags);
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("quistis", "enemy", 15, 1));
        Assert.Equal(BattleSession.AtbMaximum, battle.Combatants[0].Atb);
        battle.QueueAction("quistis", "enemy", BattleAbility.LimitBreak, choice.Id);

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(choice.NativeAction, queued.NativeAction);
        Assert.Equal((ushort)0x0008, queued.NativeTargetMask);
        Assert.Equal(0, battle.GetNativeLimitLevel("quistis"));
    }

    [Fact]
    public void Guest_limit_cannot_select_another_characters_shared_kernel_row()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = new(new BattleDefinition { NativeActions = assets.NativeActions.ToArray(), NativeLimitsDisabled = false, Combatants = [new() { Id = "laguna", Name = "Laguna", IsPlayer = true, ModelId = 8, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(8) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("laguna", 2)] });
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("laguna", true));

        Assert.Equal((20 << 16) | 2, choice.Id);
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("laguna", "enemy", 20, 1));
        battle.QueueNativePlayerCommand("laguna", "enemy", 20, 2);
        Assert.Equal((ushort)2, Assert.Single(battle.QueuedActions).NativeAction!.ActionId);
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
