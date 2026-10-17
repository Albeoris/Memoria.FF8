using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDrawCastCameraTests
{
    [Fact]
    public void Saved_before_entry_Ifrit_runs_Draw_Cast_Fire_with_native_camera_and_JSON_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_CAST_ENTRY_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A standalone before-entry Ifrit component is required.");
        string directory = TestResources.GameDirectory;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_CAST_EVIDENCE");
        string id = "draw-cast-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        GameSession session = new(GameSession.Load(source!));
        Assert.Null(session.State.Battle);
        NativeCharacterVitals vitals = session.CharacterVitals!;
        int drawAbility = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6) + 20;
        Assert.True(drawAbility >= 20);
        session.SetCharacterVitals(vitals.SetJunctionAbility(0, 1, drawAbility, true));
        JsonNode eventState = JsonNode.Parse(session.State.EventState!.Value.GetRawText())!;
        eventState["CharacterVitals"] = JsonSerializer.SerializeToNode(session.CharacterVitals);
        session.SetEventState(JsonSerializer.SerializeToElement(eventState));
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-before-entry-draw-assigned", session.State);
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, directory);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 7));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal(94, battle.Capture().Definition.EncounterId);
        Assert.NotNull(battle.NativeGraphics);
        BattleCombatant? caster = null;
        for (int callback = 0; callback < 2000; callback++)
        {
            caster = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.Draw));
            if (battle.NativeEntryReady && caster is not null)
            {
                break;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(battle.NativeEntryReady);
        Assert.NotNull(caster);
        BattleCombatant target = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        using BattleAssets resources = new(directory);
        byte information = resources.ReadEnemyNativeInformation246(target.ModelId);
        Assert.Equal(information, battle.Capture().Definition.Combatants.Single(actor => actor.Id == target.Id).NativeTemplate!.Information246);
        byte expected = (information & 8) != 0 ? (byte)5 : (information & 4) != 0 ? (byte)4 : unchecked((byte)(3 - information));
        Assert.Equal(expected, Assert.Single(battle.NativeGraphics!.Models, model => model.ModelId4 == target.ModelId + 16).SequenceByte145);
        Assert.Contains(target.NativeDrawSlots, slot => slot.Id == 1);
        string stocks = JsonSerializer.Serialize(caster.MagicStocks);
        int initialCompletions = battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        Save(world, session, evidence, id + "-before-draw");
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.Draw, ActionId: 1, DrawCast: true) { DrawCastTargetId = target.Id });
        bool restored = false;
        bool fire = false;
        bool draw = false;
        GameWorld? clone = null;
        try
        {
            for (int callback = 0; callback < 2000; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                clone?.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.ActiveAction is { Ability: BattleAbility.Draw } action)
                {
                    draw = true;
                    Assert.False(action.NativeDraw!.Failed);
                    if (!restored)
                    {
                        Save(world, session, evidence, id + "-mid-draw");
                        GameSession independent = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
                        clone = NativeBattleProductionEffectAdmissionTests.RestoreWorld(independent, directory);
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Battle!.Capture()));
                        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(world), NativeBattleProductionEffectAdmissionTests.RenderBattle(clone));
                        restored = true;
                    }
                }
                if (clone is not null && callback % 8 == 0)
                {
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Battle!.Capture()));
                }
                fire |= battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 2 };
                if (battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id) > initialCompletions)
                {
                    break;
                }
            }
            Assert.True(draw);
            Assert.True(restored);
            Assert.True(fire);
            Assert.Equal(initialCompletions + 1, battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id));
            Assert.Equal(stocks, JsonSerializer.Serialize(caster.MagicStocks));
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone!.Battle!.Capture()));
            Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(world), NativeBattleProductionEffectAdmissionTests.RenderBattle(clone));
            Save(world, session, evidence, id + "-after");
        }
        catch
        {
            Save(world, session, evidence, id + "-unsupported");
            throw;
        }
        finally
        {
            clone?.Dispose();
        }
    }

    private static void Save(GameWorld world, GameSession session, string? evidence, string name)
    {
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, name, session.State);
    }
}
