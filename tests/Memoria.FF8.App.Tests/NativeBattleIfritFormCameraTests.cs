using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleIfritFormCameraTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_form_jump_attack_restores_idle_camera_after_completion_and_Json_continuation(bool restoreDuringAttack)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with { GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()] };
        definition = definition with { Combatants = [definition.Combatants[0] with { Speed = 80 }, definition.Combatants[1] with { NativeCombatantFlags = 8 }] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        WaitFor(() => battle.CanRequestGuardianForm("party", 2));
        battle.RequestGuardianForm("party", 2);
        WaitFor(() => battle.GetGuardianForm("party")?.Phase == BattleGuardianFormPhase.Active);
        WaitFor(() => battle.CanChoosePlayerCommand("party"));
        NativeBattleCameraPose idle = battle.NativeCamera!.CurrentPose!;
        battle.QueueGuardianFormAction("party", "enemy", BattleGuardianFormAction.Attack);
        WaitFor(() => battle.NativeGraphics!.GuardianFormPresentation!.IdleCameraRestorePending && battle.NativeCamera!.CurrentPose is { } current && (current.Eye != idle.Eye || current.Target != idle.Target));
        Assert.NotNull(battle.ActiveAction);
        if (restoreDuringAttack)
        {
            BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
            Assert.True(snapshot.NativeGraphics!.GuardianFormPresentation!.IdleCameraRestorePending);
            battle = BattleSession.Restore(snapshot);
        }
        WaitFor(() => battle.Events.Any(item => item.Kind == "Damage" && item.ActorId == "party") && battle.ActiveAction is null && !battle.NativeGraphics!.GuardianFormPresentation!.IdleCameraRestorePending);
        AssertIdlePose();
        for (int callback = 0; callback < 5; callback++)
        {
            battle.Tick();
            AssertIdlePose();
        }

        void AssertIdlePose()
        {
            NativeBattleCameraPose actual = battle.NativeCamera!.CurrentPose!;
            Assert.Equal(idle.Eye, actual.Eye);
            Assert.Equal(idle.Target, actual.Target);
            Assert.Equal(idle.Roll, actual.Roll);
            Assert.Equal(idle.FocalLength, actual.FocalLength);
            Assert.Equal(checked((ushort)actual.FocalLength!.Value), battle.NativeCamera.Projection!.FocalLength);
        }

        void WaitFor(Func<bool> ready)
        {
            for (int callback = 0; callback < 1000 && !ready(); callback++)
            {
                if (battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(ready());
        }
    }
}
