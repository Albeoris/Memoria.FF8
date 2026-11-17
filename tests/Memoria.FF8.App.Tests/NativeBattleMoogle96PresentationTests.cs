using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMoogle96PresentationTests
{
    [Fact]
    public void EquippedMiniMogRunsMoogleDanceToCompletionAndRestoresDuringTheEffect()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition original = NativeBattleTestDefinitions.CreateGuestLimitDefinition(game, assets, 2, 96, out NativeBattleGraphicsDefinition graphicsResources);
        byte[] pocket = game.ReadNewGamePocketStation().Raw.ToArray();
        pocket[0] = 0x0D;
        pocket[1] = 20;
        BattleCombatantDefinition actor = original.Combatants[0] with { EquipmentIds = [0], NativeCommands = [assets.ReadNativeCommand(38)] };
        uint price = checked((uint)(100 * actor.Level));
        BattleDefinition definition = original with
        {
            Combatants = [actor, original.Combatants[1]],
            GuardianForces = [new(0, 500, 1000)],
            NativeCommandNames = assets.ReadNativeCommandNames(),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativePocketStation = game.ReadNewGamePocketStation() with { Raw = pocket.ToImmutableArray() },
            NativeGil = new(price, 123)
        };
        BattleSession battle = NativeBattleTypedFixture.CreateBattle(definition, BattleAtbMode.TurnBased, graphicsResources);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("party"));
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("party", false));
        battle.QueueAction("party", "party", choice.Ability, choice.Id);

        BattleSession? restored = null;
        bool completed = false;
        for (int callback = 0; callback < 1024; callback++)
        {
            battle.Tick();
            restored?.Tick();
            if (restored is null && battle.Presentation!.Graphics.OriginalEffect is { Binding.Effect: 96, ReturnedDescriptor: > 0 })
            {
                restored = battle.ForkForContinuation();
            }
            if (restored is not null && battle.ActiveAction is null && battle.Presentation!.Graphics.OriginalEffect is { ReturnedDescriptor: 0 })
            {
                completed = true;
                break;
            }
        }
        Assert.True(completed);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored!.Capture()));
        Assert.Equal(new NativeBattleGil(0, 123), battle.Capture().NativeGil);
        Assert.Equal((ushort)700, Assert.Single(battle.GuardianForces).HitPoints);
        Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceHealing");
    }
}
