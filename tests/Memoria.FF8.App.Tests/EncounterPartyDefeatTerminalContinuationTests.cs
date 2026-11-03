using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPartyDefeatTerminalContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartyWipeRunsTheBoundNativeTerminalAndRestoresBeforeItsActualMarker(bool petrified)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_COPY_DEATH_FIXTURE");
        Assert.SkipUnless(Directory.Exists(directory), "Configured installed native resources are required.");
        Assert.SkipUnless(fixture is not null && File.Exists(fixture), "Configure a standalone mod encounter before its genuine terminal action.");
        GameSession session = new(GameSession.Load(fixture!));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        BattleSnapshot source = world.Battle!.Capture();
        Assert.Equal(EncounterBindingResolver.Disc1Edea, source.Definition.EncounterMod!.Encounter);
        Assert.Null(source.ActiveAction);
        BattleSnapshot wipe = source with
        {
            Mode = BattleAtbMode.TurnBased,
            Combatants = source.Combatants.Select(actor => source.Definition.Combatants.Any(definition => definition.Id == actor.Id && definition.IsPlayer) ? actor with { CurrentHp = petrified ? actor.CurrentHp : 0, NativeStatus = actor.NativeStatus! with { Saved = (ushort)(actor.NativeStatus.Saved | (petrified ? 4 : 1)) } } : actor).ToArray(),
            NativePartyPresentation = petrified ? source.NativePartyPresentation : source.NativePartyPresentation! with { Rows = source.NativePartyPresentation.Rows.Select(row => row is null ? null : row with { HitPoints = 0, DisplayedHitPointsQ12 = 0 }).ToImmutableArray() }
        };
        BattleSession Attach(BattleSnapshot snapshot) => (BattleSession)typeof(GameWorld).GetMethod("AttachNativeEffectResources", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [BattleSession.Restore(snapshot)])!;
        BattleSession battle = Attach(wipe);
        FieldInfo clockOwner = typeof(BattleSession).GetField("nativeBattleClock", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void CheckCompletionOwner(BattleSession current)
        {
            if (current.ActiveAction is { NativeHeaderIndex: int header } active && header >= current.NativeActionPackets.ActionCount && clockOwner.GetValue(current) is NativeBattleClockState { ActionPause: 0 })
            {
                Assert.Fail(JsonSerializer.Serialize(new { current.Frame, active.ActorId, active.ActionId, Header = header, current.NativeActionPackets.ActionCount, Scratch = current.Capture().NativeActionScratch, current.EncounterPolicies }));
            }
        }
        for (int callback = 0; callback < 20 && battle.EncounterPolicies?.TerminalStarted != true; callback++)
        {
            battle.Tick();
            CheckCompletionOwner(battle);
        }
        Assert.True(battle.EncounterPolicies!.TerminalStarted);
        Assert.False(battle.EncounterPolicies.TerminalReached);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.All(battle.Combatants.Where(actor => actor.IsPlayer), actor => Assert.NotEqual(0, BattleSession.NativeSavedStatusBits(actor) & 5));
        Assert.DoesNotContain(battle.Events.Skip(source.Events.Length), entry => entry.Kind == "EncounterPolicy");
        BattleSession restored = Attach(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int callback = 0; callback < 2000 && battle.Outcome == BattleOutcome.Active; callback++)
        {
            battle.Tick();
            restored.Tick();
            CheckCompletionOwner(battle);
            CheckCompletionOwner(restored);
        }
        Assert.True(battle.Outcome == BattleOutcome.Victory, JsonSerializer.Serialize(new
        {
            battle.Frame,
            battle.Outcome,
            battle.EncounterPolicies,
            Active = battle.ActiveAction is { } active ? new { active.ActorId, active.ActionId, active.NativeHeaderIndex } : null,
            Queued = battle.QueuedActions.Select(action => new { action.ActorId, action.ActionId, action.NativeQueuedCommand, action.NativeAiCallbackStage, action.NativeHeaderIndex }),
            battle.NativeActionPackets.ActionCount,
            battle.NativeActionPackets.ContactCount,
            Original = battle.NativeGraphics!.Models[battle.Combatants.Single(actor => actor.Id == source.Definition.EncounterMod.OriginalActorId).NativeIndex].SequenceState,
            Clock = battle.Capture().NativeClock,
            Scratch = battle.Capture().NativeActionScratch,
            Events = battle.Events.TakeLast(12).Select(entry => new { entry.Frame, entry.Kind, entry.ActorId, entry.Amount })
        }));
        Assert.Equal((byte)4, battle.NativeResult);
        Assert.True(battle.EncounterPolicies.TerminalReached);
        Assert.True(battle.EncounterPolicies.CleanedUp);
        Assert.Contains(battle.Events.Skip(source.Events.Length), entry => entry.Kind == "Queued" && entry.ActorId == source.Definition.EncounterMod.OriginalActorId && entry.Amount == 207);
        Assert.All(battle.Combatants.Where(actor => actor.IsPlayer), actor => Assert.Equal(wipe.Combatants.Single(saved => saved.Id == actor.Id).CurrentHp, actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
