using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public bool HasPendingBattle => Events.PendingMenuBattle is not null || session.State.PendingBattleEncounter is not null || Events.PendingHostedBattle is not null || session.State.RandomEncounters?.Pending is not null;

    private void ConfigureRandomEncounterHost()
    {
        Events.HasPendingRandomBattle = () => session.State.RandomEncounters?.Pending is not null || session.State.PendingBattleEncounter is { ScriptedOwner: null, SourceScene: SceneKind.Field } && Events.PendingMenuBattle is null;
        Events.NativePlayerMovementProcessed = ProcessFieldRandomEncounters;
    }

    private void ApplyEncounterConfiguration(BattleInput input)
    {
        if (input.RandomEncounters is not bool enabled || session.State.Scene is not (SceneKind.Field or SceneKind.WorldMap or SceneKind.Movie))
        {
            throw new InvalidOperationException("Encounter policy requires an explicit setting in a running playthrough.");
        }
        RandomEncounterSessionState current = session.State.RandomEncounters ?? new(false, NativeRandomEncounterState.FromColdProcess(), null);
        session.SetRandomEncounters(current with { Enabled = enabled });
        if (enabled && session.State.Scene == SceneKind.WorldMap && !HasPendingBattle)
        {
            InitializeWorldRandomEncounters();
        }
    }

    private void ProcessFieldRandomEncounters(bool accepted)
    {
        if (session.State.FullBattlePlaythrough != true || session.State.RandomEncounters is not { Enabled: true, Pending: null } encounter || session.State.Scene != SceneKind.Field || HasPendingBattle || Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
        {
            return;
        }
        FieldAssets field = Field ?? throw new InvalidOperationException("Random encounters require the loaded field resources.");
        NativeFieldEncounterDefinition definition = field.RandomEncounters ?? throw new NotSupportedException("Random encounters require the original field RAT/MRT resources.");
        NativeWorldControlState controls = session.State.WorldControls ?? throw new NotSupportedException("Random field encounters require the canonical native control bytes.");
        NativeFieldEncounterInput input = new(accepted, unchecked((short)(Events.PlayerActor?.MovementSpeed ?? 0)), session.State.CharacterVitals!.GetEncounterAbilityFlags(), controls.MusicKeep207 != 0, session.IsMoviePlaying, 0, unchecked((ushort)Events.FieldFade!.Physical.Mode));
        NativeRandomEncounterResult result = NativeRandomEncounter.AdvanceField(encounter.Native, definition, assets.ReadNativeEncounterTables().FieldRandom.AsSpan(), input);
        RandomEncounterRequest? pending = result.Formation is ushort formation ? new(SceneKind.Field, Events.FieldId, Events.Frame, formation, Convert.ToHexString(SHA256.HashData(field.Jsm))) : null;
        session.SetRandomEncounters(encounter with { Native = result.State, Pending = pending });
    }

    private void InitializeWorldRandomEncounters()
    {
        if (session.State.FullBattlePlaythrough != true || session.State.RandomEncounters is not { Enabled: true } encounter || encounter.Native.WorldInitialized)
        {
            return;
        }
        uint clock = worldScene?.State.NativeFootRuntime?.Forest?.SeedCounter ?? (session.State.WorldProcess ?? throw new NotSupportedException("World encounters require their original process clock.")).MainLoopCounter;
        session.SetRandomEncounters(encounter with { Native = encounter.Native.InitializeWorld(unchecked((byte)clock)) });
    }

    private void ConfigureWorldRandomEncounters(WorldMapScene scene)
    {
        InitializeWorldRandomEncounters();
        scene.HasPendingBattle = () => session.State.RandomEncounters?.Pending is not null || session.State.PendingBattleEncounter is { SourceScene: SceneKind.WorldMap };
        scene.ProcessRandomEncounters = (worldState, movementMagnitude, currentEncounters) =>
        {
            if (session.State.FullBattlePlaythrough != true || currentEncounters is not { Enabled: true, Pending: null } encounter || worldState.NativeFootRuntime is null || worldState.NativeScriptedRuntime is not null || worldState.NativeTrainRuntime is not null)
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
            NativeWorldForestState? nextForest = forest is null ? null : forest with { RandomCursor = result.RandomCursor, RandomOffset = result.RandomOffset };
            WorldState next = nextForest is null ? worldState : worldState with
            {
                NativeFootRuntime = worldState.NativeFootRuntime! with { Forest = nextForest },
                NativeGardenRuntime = worldState.NativeGardenRuntime is { } garden ? garden with { Forest = nextForest } : null
            };
            return (next, encounter with { Native = result.State, Pending = pending });
        };
    }

    private bool TryApplyRandomBattleInput(BattleInput input, out int advanced, IReadOnlyDictionary<int, int>? guardianForceLearning = null)
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
        {
            if (input.Kind == BattleInputKind.StartEncounter)
            {
                if (battle is not null)
                {
                    throw new InvalidOperationException("A random battle is already active.");
                }
                ushort flags = request.Scene == SceneKind.Field ? Events.BeginNativeRandomFieldBattle(request) : (ushort)0;
                battle = StartNativeBattleSession(request.Formation, flags, input.NativeSeed!.Value, input.Mode);
            }
            else
            {
                BattleSession active = battle ?? throw new InvalidOperationException("The pending random encounter has not started.");
                long previous = active.Frame;
                if (input.Kind == BattleInputKind.Return)
                {
                    if (!active.CanReturnToField)
                    {
                        throw new InvalidOperationException("The completed battle does not permit return to its suspended exploration scene.");
                    }
                    CompleteBattle(active, guardianForceLearning);
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
        }
        return true;
    }

    private void ValidateRandomBattleHostState()
    {
        RandomEncounterRequest request = session.State.RandomEncounters!.Pending!;
        if (request.Scene != session.State.Scene || request.FieldId != session.State.FieldId || battle is null)
        {
            throw new InvalidDataException("Saved random combat has lost its exploration scene owner.");
        }
        ushort flags = 0;
        if (request.Scene == SceneKind.Field)
        {
            NativeRandomFieldBattleOwner owner = Events.Scene.NativeRandomFieldBattle ?? throw new InvalidDataException("Saved random field combat has no executed native entry owner.");
            if (owner.Request != request || owner.Result is not null)
            {
                throw new InvalidDataException("Saved random field combat disagrees with its actual suspended encounter.");
            }
            flags = owner.Flags;
        }
        BattleSnapshot saved = battle.Capture();
        BattleDefinition current = BindBattleEntryClock(CreateEncounterBattle(request.Formation, saved.Definition.NativeSeed, flags), saved, session.ExamTimer);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current), JsonSerializer.SerializeToElement(saved.Definition)))
        {
            throw new InvalidDataException("Saved random combat differs from its current installed formation and party.");
        }
    }

}
