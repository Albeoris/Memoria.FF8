using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDrawCastCameraTests
{
    [Fact]
    public void Saved_before_entry_Ifrit_runs_Draw_Cast_Fire_with_native_camera_and_checkpoint_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_CAST_ENTRY_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A standalone before-entry Ifrit component is required.");
        string directory = TestResources.GameDirectory;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_CAST_EVIDENCE");
        string id = "draw-cast-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        GameSession session = new(GameSession.Load(source!));
        Assert.Null(session.State.Battle);
        using GameAssets game = new(directory);
        if (session.State.FieldId == 999)
        {
            // The synthetic field fixture supplies the before-entry script and party.
            // Allocate this independent battle component with current process state.
            session.Restore(session.State with { NativeBattleMemory = game.ReadColdProcessNativeBattleMemory() });
        }
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
        Assert.NotNull(battle.Presentation?.Graphics);
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
        Assert.Equal(expected, Assert.Single(ReadOriginalModelCells(battle.Presentation!.Graphics), model => model.ModelId4 == target.ModelId + 16).SequenceByte145);
        Assert.Contains(target.NativeDrawSlots, slot => slot.Id == 1);
        string stocks = JsonSerializer.Serialize(caster.MagicStocks);
        int initialCompletions = battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: BattleAbility.Draw, ActionId: 1, DrawCast: true) { DrawCastTargetId = target.Id });
        bool fire = false;
        bool draw = false;
        BattleSession? restored = null;
        BattleScenePresentation presentation = new(resources, battle.Capture().Definition);
        try
        {
            for (int callback = 0; callback < 2000; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                restored?.Tick();
                if (battle.ActiveAction is { Ability: BattleAbility.Draw } action && battle.Presentation?.Graphics.ActionGraphics?.Header is { } header)
                {
                    draw = true;
                    Assert.False(action.NativeDraw!.Failed);
                    if (restored is null)
                    {
                        BattleSnapshot checkpoint = battle.CaptureForContinuation();
                        Assert.Equal((byte)0, checkpoint.ReadFixturePresentation().NativeActionPackets!.ReadHeader(header).ReadRepeatModeMinusOne());
                        Assert.Equal(new BattleEnemyActorModel(new(target.Id)), Assert.Single(checkpoint.ReadFixturePresentation().NativeActionPackets!.ReadHeaderContacts(header)).Primary.Recipient);
                        NativeBattleRestorationPresentationTests.Publish(evidence, id + "-mid-draw", checkpoint, null);
                        restored = battle.ForkForContinuation();
                        Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(restored.Capture()));
                        Assert.Equal(presentation.Render(battle, 320, 224).ToArray(), presentation.Render(restored, 320, 224).ToArray());
                    }
                }
                if (restored is not null && callback % 8 == 0)
                {
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                }
                fire |= battle.Presentation!.Graphics.OriginalEffect is { Binding.Effect: 2 };
                if (battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id) > initialCompletions)
                {
                    break;
                }
            }
            Assert.True(draw);
            Assert.NotNull(restored);
            Assert.True(fire);
            Assert.Equal(initialCompletions + 1, battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id));
            Assert.Equal(stocks, JsonSerializer.Serialize(caster.MagicStocks));
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            byte[] pixels = presentation.Render(battle, 320, 224).ToArray();
            Assert.Equal(pixels, presentation.Render(restored, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(evidence, id + "-after", battle.Capture(), pixels);
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(evidence, id + "-unsupported", battle.Capture(), null);
            throw;
        }
    }
}
