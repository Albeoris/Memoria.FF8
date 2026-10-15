using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public bool HasPendingBattle => Events.PendingInteractiveBattle is not null || session.State.RandomEncounters?.Pending is not null;

    private void ConfigureRandomEncounterHost()
    {
        Events.HasPendingRandomBattle = () => session.State.RandomEncounters?.Pending is not null;
        Events.NativePlayerMovementProcessed = ProcessFieldRandomEncounters;
    }

    private void ApplyEncounterConfiguration(BattleInput input)
    {
        if (input.RandomEncounters is not bool enabled || HasPendingBattle || battle is not null || session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap))
        {
            throw new InvalidOperationException("Encounter policy requires an explicit setting outside combat in a running playthrough.");
        }
        RandomEncounterSessionState current = session.State.RandomEncounters ?? new(false, NativeRandomEncounterState.FromColdProcess(), null);
        if (enabled && !current.Enabled && session.ReplayJournal?.CompletedVisits.Any(visit => !visit.PhysicalEncounterHistory) == true)
        {
            // Earlier semantic visits may have discarded cadence that now seeds random outcomes.
            // Admit the toggle only when the actual current prefix independently reproduces its complete causal state.
            GameState source = session.State with { EventState = Events.Capture() };
            GameState recreated = RecreateSave(source, assets.GameDirectory);
            JsonObject expected = JsonSerializer.SerializeToNode(source)!.AsObject();
            JsonObject actual = JsonSerializer.SerializeToNode(recreated)!.AsObject();
            foreach (string presentation in new[] { "ReplayJournal", "DebugView" })
            {
                expected.Remove(presentation);
                actual.Remove(presentation);
            }
            if (!JsonNode.DeepEquals(expected, actual))
            {
                throw new NotSupportedException("Recreate this research save from its journal in the Load menu before enabling random encounters; its earlier compacted history does not reproduce the current movement and process-clock boundary.");
            }
        }
        session.SetRandomEncounters(current with { Enabled = enabled });
        if (enabled && session.State.Scene == SceneKind.WorldMap)
        {
            InitializeWorldRandomEncounters();
        }
    }

    private void ProcessFieldRandomEncounters(bool accepted)
    {
        if (session.State.RandomEncounters is not { Enabled: true, Pending: null } encounter || session.State.Scene != SceneKind.Field || HasPendingBattle || Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
        {
            return;
        }
        FieldAssets field = Field ?? throw new InvalidOperationException("Random encounters require the loaded field resources.");
        NativeFieldEncounterDefinition definition = field.RandomEncounters ?? throw new NotSupportedException("Random encounters require the original field RAT/MRT resources.");
        NativeFieldEncounterInput input = new(accepted, unchecked((short)(Events.PlayerActor?.MovementSpeed ?? 0)), session.State.CharacterVitals!.GetEncounterAbilityFlags(), !Events.Scene.RandomBattlesEnabled, session.IsMoviePlaying, 0, unchecked((ushort)Events.FieldFade!.Physical.Mode));
        NativeRandomEncounterResult result = NativeRandomEncounter.AdvanceField(encounter.Native, definition, assets.ReadNativeEncounterTables().FieldRandom.AsSpan(), input);
        RandomEncounterRequest? pending = result.Formation is ushort formation ? new(SceneKind.Field, Events.FieldId, Events.Frame, formation, Convert.ToHexString(SHA256.HashData(field.Jsm))) : null;
        session.SetRandomEncounters(encounter with { Native = result.State, Pending = pending });
    }

    private void InitializeWorldRandomEncounters()
    {
        if (session.State.RandomEncounters is not { Enabled: true } encounter || encounter.Native.WorldInitialized)
        {
            return;
        }
        uint clock = worldScene?.State.NativeFootRuntime?.Forest?.SeedCounter ?? (session.State.WorldProcess ?? throw new NotSupportedException("World encounters require their original process clock.")).MainLoopCounter;
        session.SetRandomEncounters(encounter with { Native = encounter.Native.InitializeWorld(unchecked((byte)clock)) });
    }

    private void ConfigureWorldRandomEncounters(WorldMapScene scene)
    {
        InitializeWorldRandomEncounters();
        scene.HasPendingBattle = () => session.State.RandomEncounters?.Pending is not null;
        scene.ProcessRandomEncounters = (worldState, movementMagnitude, currentEncounters) =>
        {
            if (currentEncounters is not { Enabled: true, Pending: null } encounter || worldState.NativeFootRuntime is null || worldState.NativeScriptedRuntime is not null || worldState.NativeTrainRuntime is not null)
            {
                return (worldState, currentEncounters);
            }
            NativeEncounterTables tables = assets.ReadNativeEncounterTables();
            byte ground = worldState.GroundType ?? throw new NotSupportedException("World encounters require the actual ground polygon.");
            int entityType = worldState.NativeFootRuntime?.EntityType ?? worldState.NativeCarRuntime?.EntityType ?? 10;
            NativeWorldEncounterDefinition? definition = scene.ReadRandomEncounterDefinition(worldState);
            NativeWorldEncounterInput input = new(movementMagnitude, entityType, ground, session.State.CharacterVitals!.GetEncounterAbilityFlags(), NativeWorldEntryControl.DelayAfterCallback(worldState.Frame, 0) == 0, false);
            NativeWorldForestState? forest = worldState.NativeFootRuntime?.Forest;
            NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(encounter.Native, definition, tables.WorldRandom.AsSpan(), tables.WorldWeights.AsSpan(), input, forest?.RandomCursor ?? 0, forest?.RandomOffset ?? 0);
            RandomEncounterRequest? pending = result.Formation is ushort formation ? new(SceneKind.WorldMap, session.State.FieldId, worldState.Frame, formation, scene.AllocationProgramSha256) : null;
            WorldState next = forest is null ? worldState : worldState with { NativeFootRuntime = worldState.NativeFootRuntime! with { Forest = forest with { RandomCursor = result.RandomCursor, RandomOffset = result.RandomOffset } } };
            return (next, encounter with { Native = result.State, Pending = pending });
        };
    }

    private bool TryApplyRandomBattleInput(BattleInput input, out int advanced)
    {
        advanced = 0;
        if (session.State.RandomEncounters is not { Pending: { } request } encounters)
        {
            return false;
        }
        if (input.Kind is BattleInputKind.ConfigureEncounters or BattleInputKind.ConfigurePlaythrough)
        {
            throw new InvalidOperationException("Battle policy cannot change during a pending encounter.");
        }
        long frame = request.Scene == SceneKind.Field ? Events.Frame : worldScene!.State.Frame;
        using (session.BeginReplayCapture())
        {
            if (input.Kind == BattleInputKind.StartEncounter)
            {
                if (battle is not null)
                {
                    throw new InvalidOperationException("A random battle is already active.");
                }
                battle = new BattleSession(CreateEncounterBattle(request.Formation, input.NativeSeed!.Value), input.Mode);
            }
            else
            {
                BattleSession active = battle ?? throw new InvalidOperationException("The pending random encounter has not started.");
                long previous = active.Frame;
                if (input.Kind == BattleInputKind.Return)
                {
                    if (active.Outcome != BattleOutcome.Victory)
                    {
                        throw new InvalidOperationException("Only victory can return to its suspended exploration scene.");
                    }
                    PublishBattleOutcome(active);
                    session.SetRandomEncounters(encounters with { Pending = null });
                    battle = null;
                }
                else
                {
                    ExecuteBattleCommand(active, input);
                }
                advanced = checked((int)(active.Frame - previous));
                if (advanced > 0)
                {
                    if (request.Scene == SceneKind.Field)
                    {
                        Events.AdvanceRandomBattleCallbacks(advanced);
                    }
                    else
                    {
                        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, advanced));
                        session.SetWorldProcess(session.State.WorldProcess!.AdvanceMainLoopCallbacks(advanced));
                    }
                }
            }
            session.SetBattle(battle?.Capture());
        }
        if (request.Scene == SceneKind.WorldMap && Field is not null)
        {
            // The field VM is dormant, but its checkpoint projects the current process-owned globals.
            session.SetEventState(Events.Capture());
        }
        session.RecordReplayEntry(new ReplayJournalEntry(ReplayEntryKind.BattleInput, request.Scene, request.FieldId, frame) { BattleInput = input, Value = request.Formation, NativeSourceHash = request.SourceHash, Advanced = advanced, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = frame, Trigger = (int)PlayerTriggerKind.Battle, ResultTrigger = (int)(input.Kind == BattleInputKind.Return ? NextTrigger.Kind : PlayerTriggerKind.Battle) });
        return true;
    }

    private void ValidateRandomBattleHostState()
    {
        RandomEncounterRequest request = session.State.RandomEncounters!.Pending!;
        if (request.Scene != session.State.Scene || request.FieldId != session.State.FieldId || battle is null)
        {
            throw new InvalidDataException("Saved random combat has lost its exploration scene owner.");
        }
        BattleDefinition current = CreateEncounterBattle(request.Formation, battle.Capture().Definition.NativeSeed);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current), JsonSerializer.SerializeToElement(battle.Capture().Definition)))
        {
            throw new InvalidDataException("Saved random combat differs from its current installed formation and party.");
        }
    }

    private void ValidateRecordedBattleOwner(ReplayJournalEntry entry)
    {
        if (entry.BattleInput is { Kind: BattleInputKind.StartEncounter, NativeSeed: null })
        {
            throw new NotSupportedException("Battle reconstruction requires its actually sampled entry seed; regenerate the checkpoint from current ordinary inputs.");
        }
        if (entry.BattleInput?.Kind is BattleInputKind.ConfigurePlaythrough or BattleInputKind.ConfigureEncounters)
        {
            return;
        }
        if (session.State.RandomEncounters?.Pending is { } random)
        {
            if (entry.Scene != random.Scene || entry.FieldId != random.FieldId || entry.Value != random.Formation || entry.NativeSourceHash != random.SourceHash || entry.ObjectId is not null || entry.ScriptId is not null || entry.Operation is not null)
            {
                throw new InvalidDataException("Recorded random battle differs from its current encounter owner.");
            }
            return;
        }
        NativeBattleCompletion request = Events.PendingInteractiveBattle ?? throw new InvalidDataException("Recorded battle input has no suspended encounter owner.");
        if (entry.ObjectId != request.ObjectId || entry.ScriptId != request.ScriptId || entry.Operation != request.Operation || entry.Code != request.Flags || entry.Value != request.Encounter || entry.NativeSourceHash != request.ProgramHash)
        {
            throw new InvalidDataException("Recorded battle input differs from its current native issuing site.");
        }
    }
}
