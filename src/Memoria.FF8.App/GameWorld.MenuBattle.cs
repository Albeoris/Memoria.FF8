using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private PlayerActionResult ApplyMenuBattleItem(PlayerAction action, NativeFieldItem item)
    {
        if (action.Quantity != 0 || action.Text is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Battle items accept only their inventory identity and target0.", nameof(action));
        }
        if (session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap) || HasPendingBattle || Events.CurrentDialogue is not null || worldScene?.CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, NextTrigger, 0, "The current interaction suspends battle item use.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Battle items require the saved character owner.");
        NativeEconomy economy = session.State.Economy ?? throw new NotSupportedException("Battle items require the saved inventory owner.");
        bool blocked = Events.PartyMenu?.TimerDisplayBeforeMenu ?? session.ExamTimer?.DisplayEnabled ?? throw new NotSupportedException("Battle items require the actual menu entry timer context.");
        var next = item.BeginBattle(vitals, economy, action.Value, blocked);
        SceneKind sourceScene = session.State.Scene;
        long sourceFrame = sourceScene == SceneKind.Field ? Events.Frame : worldScene!.State.Frame;
        string sourceHash = sourceScene == SceneKind.Field ? Events.Capture().GetProperty("ProgramHash").GetString()! : worldScene!.AllocationProgramSha256;
        ushort flags = Events.Scene.NativeBattleFlagWrites is { Count: > 0 } writes ? writes[^1].ActiveFlags : throw new NotSupportedException("Menu battle entry requires its retained active battle flags.");
        Events.ValidateNativeMenuBattleEntry(action.Value, next.Encounter, sourceScene, sourceFrame, sourceHash, flags);
        _ = CreateEncounterBattle(next.Encounter, activeFlags: flags);
        _ = Events.PrepareNativeBattleMusicEntry(next.Encounter, flags, BattleMusicEntryResources, session.State.NativeBattleMemory?.MusicProcess?.ReturnChannel);
        BattleEncounterOccurrence occurrence = new(Guid.NewGuid().ToString("N"), next.Encounter, BattleResources.FormationSourceHash(next.Encounter), sourceScene, session.State.FieldId, sourceFrame, sourceHash, null);
        if (sourceScene == SceneKind.WorldMap)
        {
            WorldMapScene scene = worldScene!;
            WorldState world = scene.State;
            if (world.NativeFootRuntime is not { EntityType: 0 or 1 or 2 } || world.NativeScriptedRuntime is not null || world.NativeTrainRuntime is not null)
            {
                throw new NotSupportedException("World menu battle departure requires the normal foot resource and saved pose contract.");
            }
            NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(world, checked((ushort)session.State.FieldId), WorldEntryVariables.Read(session.State).StoryWord, scene.PlaceId);
            session.RecordNativeMenuWorldBattleDeparture(occurrence, receipt);
        }
        Events.BeginNativeMenuBattle(action.Value, next.Encounter, sourceScene, sourceFrame, sourceHash, flags);
        session.SetNativePartyMenuOwners(next.Vitals, next.Economy, session.State.ShotAvailability!, session.State.SalaryPresentation!);
        session.SetPendingBattleEncounter(occurrence);
        session.SetEventState(Events.Capture());
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.Battle, session.State.FieldId, sourceFrame, null, null), 0);
    }

    private bool TryApplyMenuBattleInput(BattleInput input, out int advanced)
    {
        advanced = 0;
        if (Events.PendingMenuBattle is not { } request)
        {
            return false;
        }
        if (input.Kind == BattleInputKind.StartEncounter)
        {
            if (battle is not null)
            {
                throw new InvalidOperationException("A menu battle is already active.");
            }
            battle = StartNativeBattleSession(request.Formation, request.Flags, input.NativeSeed!.Value, input.Mode);
        }
        else
        {
            BattleSession active = battle ?? throw new InvalidOperationException("The menu battle has not started.");
            long previous = active.Frame;
            if (input.Kind == BattleInputKind.Return)
            {
                if (!active.CanReturnToField)
                {
                    throw new InvalidOperationException("The menu battle does not permit return to exploration yet.");
                }
                CompleteBattle(active);
                battle = null;
            }
            else
            {
                ExecuteBattleCommand(active, input);
            }
            advanced = checked((int)(active.Frame - previous));
        }
        PublishBattleSnapshot(battle);
        CompleteNativeDefeatIfReady();
        session.SetEventState(Events.Capture());
        return true;
    }

    private void ValidateMenuBattleHostState()
    {
        var request = Events.PendingMenuBattle ?? throw new InvalidDataException("Saved menu battle has no original menu return owner.");
        BattleEncounterOccurrence occurrence = session.State.PendingBattleEncounter ?? throw new InvalidDataException("Saved menu battle lost its encounter occurrence.");
        if (occurrence.ScriptedOwner is not null || occurrence.SourceScene != request.SourceScene || occurrence.SourceFrame != request.SourceFrame || occurrence.SourceHash != request.SourceHash || occurrence.Formation != request.Formation || session.State.Scene != request.SourceScene || session.State.RandomEncounters?.Pending is not null)
        {
            throw new InvalidDataException("Saved menu battle differs from its actual exploration and item owner.");
        }
        if (battle is null)
        {
            return;
        }
        BattleSnapshot snapshot = battle.Capture();
        BattleDefinition current = BindBattleEntryClock(CreateEncounterBattle(request.Formation, snapshot.Definition.NativeSeed, request.Flags), snapshot, session.ExamTimer);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current), JsonSerializer.SerializeToElement(snapshot.Definition)))
        {
            throw new InvalidDataException("Saved menu combat differs from the installed encounter and saved party.");
        }
    }
}
