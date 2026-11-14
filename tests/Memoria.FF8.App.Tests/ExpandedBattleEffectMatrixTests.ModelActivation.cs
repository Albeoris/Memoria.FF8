using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void Installed_formation63_activates_its_authored_model_through_the_death_action()
    {
        string directory = TestResources.GameDirectory;
        using GameWorld world = StartBattle(directory, PrepareField(directory), 63, useAreaEnemyRoster: true);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.ReadDefinition().UsesOriginalEnemySlots);
        BattleCombatant carrier = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 68);
        BattleCombatant reserve = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 77);
        Assert.True(reserve.IsActive && reserve.Hidden && reserve.Suspended && reserve.Untargetable);
        Assert.Contains((ushort)335, battle.GetCombatantDefinition(reserve.Id).NativeRenzokukenMagicIds);
        BattleScenePresentation scene = new(world.BattleResources, battle.ReadDefinition(), true);
        Assert.True(AdvanceAuthoredModelActivation(world, battle, out var presentation, scene));
        Assert.True(presentation.Descriptor);
        Assert.True(presentation.Overlay);
        Assert.True(presentation.RenderedTint);
        Assert.True(reserve.IsActive && !reserve.Hidden && !reserve.Suspended && !reserve.Untargetable);
        Assert.Single(battle.Events, entry => entry.Kind == "Activated" && entry.ActorId == reserve.Id);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == carrier.Id && entry.TargetId == carrier.Id && entry.Amount == 0);
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.False(restored.Combatants.Single(actor => actor.Id == reserve.Id).Suspended);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Installed_expanded_Renzokuken335_reaches_the_original_activated_model()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedRenzokuken(directory, PrepareField(directory), game, assets, 335);
        Assert.True(status == "logical", detail);
    }

    private static bool AdvanceAuthoredModelActivation(GameWorld world, BattleSession battle, out (bool Descriptor, bool Overlay, bool RenderedTint) presentation, BattleScenePresentation? scene = null)
    {
        BattleCombatant carrier = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 68);
        BattleCombatant reserve = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 77);
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        attacker.Atb = BattleSession.AtbMaximum;
        carrier.CurrentHp = 1;
        world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = attacker.Id, TargetId = carrier.Id, Ability = BattleAbility.Attack });

        bool invocation = false;
        presentation = default;
        for (int callback = 0; callback < 720; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.ExpandedEffect is { } effect)
            {
                Assert.Equal((ushort)20, effect.Graphics.OriginalEffect!.Binding.Effect);
                Assert.Equal((byte)93, effect.Graphics.Models[6].ModelId4);
                invocation = true;
                presentation.Descriptor |= effect.Graphics.Descriptors.Queue.Any(slot => effect.Graphics.Descriptors.Packets[slot].Command == 113);
                presentation.Overlay |= effect.Overlay?.LastCommands.Length > 0;
                if (scene is not null && !presentation.RenderedTint && effect.StageColor.Depth != 0 && effect.CinematicCamera is { FocalLength: > 0 })
                {
                    Assert.NotEmpty(scene.Render(battle, 640, 448));
                    presentation.RenderedTint = true;
                }
            }
            if (invocation && !reserve.Suspended && battle.ExpandedEffect is null)
            {
                return true;
            }
        }
        return false;
    }
}
