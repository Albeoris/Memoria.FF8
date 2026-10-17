namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void RecordNativeWorldBattleDeparture(RandomEncounterRequest request, NativeSavedWorldFootReturn receipt)
    {
        receipt.RequireCompleteFootReload();
        if (State.Scene != SceneKind.WorldMap || State.World is not { } world || State.SavedWorld is not { } saved || State.RandomEncounters?.Pending != request || request.Scene != SceneKind.WorldMap || request.Frame != world.Frame || receipt.SourceWorldFrame != world.Frame || receipt.WorldMode != 3 || State.PendingBattleEncounter is not null || State.Battle is not null)
        {
            throw new InvalidOperationException("World combat departure requires its current unconsumed random request and actual saved foot pose.");
        }
        State = State with { SavedWorld = saved.WithFootReturn(receipt), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(world, State.FieldId) };
    }

    public void CommitPreparedWorldBattleReturn(BattleEncounterOccurrence occurrence, WorldState departureWorld, NativeSavedWorldState departureSaved, GameState prepared, Action<GameState> validatePrepared)
    {
        if (State.Scene != SceneKind.WorldMap || State.World != departureWorld || State.SavedWorld != departureSaved || State.PendingBattleEncounter is not null || !State.AppliedBattleResultIds.Contains(occurrence.OccurrenceId, StringComparer.Ordinal) || occurrence.SourceScene != SceneKind.WorldMap || prepared.Scene != SceneKind.WorldMap || prepared.World?.NativeFootRuntime?.Rule != NativeFootWorldRuntimeState.BattleReloadRule || prepared.SavedWorld != departureSaved || prepared.WorldArrival != State.WorldArrival)
        {
            throw new InvalidOperationException("Prepared world combat reload no longer matches its applied encounter and physical saved owner.");
        }
        RandomEncounterSessionState encounters = State.RandomEncounters ?? throw new NotSupportedException("World combat reload requires its original encounter process.");
        GameState candidate = State with { Battle = null, World = prepared.World, SavedWorld = prepared.SavedWorld, EventMemory = prepared.EventMemory, WorldEntryMemory = prepared.WorldEntryMemory, RandomEncounters = encounters with { Native = encounters.Native with { WorldInitialized = false } } };
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, candidate.EventMemory);
        ValidateBattleFactsCheckpoint(candidate);
        validatePrepared(candidate);
        State = candidate;
    }

    public void PublishNativeWorldBattleMusic(NativeMusicProcessState buffers)
    {
        buffers.Validate();
        if (State.Scene != SceneKind.WorldMap || State.Battle is not null || State.NativeBattleMemory?.MusicProcess is not { } music)
        {
            throw new InvalidOperationException("World music publication requires the completed world combat reload.");
        }
        NativeBattleRetainedMemory memory = State.NativeBattleMemory with { MusicProcess = music with { Buffers = buffers } };
        memory.Validate();
        State = State with { NativeBattleMemory = memory };
    }
}
