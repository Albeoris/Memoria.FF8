namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static NativeSavedWorldState PrepareOrdinaryWorldFieldEntry(NativeSavedWorldState saved)
    {
        //544630 clears saved-world BYTE125 after549E80, preserving the116/120 flag bank.
        return saved.WithOrdinaryWorldFieldEntry();
    }

    private static NativeSavedWorldState PrepareGardenParkingForFootReturn(NativeSavedWorldState saved, WorldState finalWorld)
    {
        saved = finalWorld.NativeGardenRuntime is NativeGardenWorldRuntimeState garden ? saved.WithGardenParking(garden) : saved;
        return finalWorld.NativeRagnarokRuntime is NativeRagnarokWorldRuntimeState ragnarok ? saved.WithRagnarokParking(ragnarok) : saved;
    }

    public void CommitPreparedGardenFieldEntry(GameState expectedDeparture, WorldState finalWorld, FieldTransition transition, NativeSavedWorldGardenReturn receipt, NativeWorldSalaryState salary, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(finalWorld);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure) || departure.Scene != SceneKind.WorldMap || departure.World?.NativeGardenRuntime is null || departure.WorldArrival is null || departure.SavedWorld is null || departure.WorldControls is null || departure.WorldEntryMemory is null)
        {
            throw new NotSupportedException("Native Garden-to-field entry requires its unchanged actual world allocation and saved/control owners.");
        }
        receipt.Validate();
        Validate(departure with { World = finalWorld, WorldProcess = PrepareWorldProcess(departure, finalWorld) });
        NativeSavedWorldGardenReturn actual = NativeSavedWorldGardenReturn.Create(finalWorld, checked((ushort)departure.FieldId), departure.WorldEntryMemory.StoryWord, receipt.MapLocationId);
        if (transition.FieldId is < 0 or > ushort.MaxValue || receipt != actual)
        {
            throw new InvalidOperationException("The prepared Garden save differs from its actual world action endpoint.");
        }
        GameState candidate = salary.ApplyTo(departure with
        {
            Scene = SceneKind.Field,
            FieldId = transition.FieldId,
            PreviousFieldId = receipt.LastFieldId,
            FieldName = $"field_{transition.FieldId}",
            EntryPoint = 0,
            X = transition.X,
            Y = transition.Y,
            EventState = null,
            DepartingFieldState = departure.DepartingFieldState ?? departure.EventState,
            DialogueDraft = null,
            FieldArrival = transition,
            WorldArrival = null,
            World = null,
            DepartingWorldState = finalWorld,
            DebugView = null,
            SavedWorld = departure.SavedWorld.WithGardenReturn(receipt),
            WorldControls = departure.WorldControls.WithNormalFieldLoad(),
            WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId),
            ExamTimer = AdvanceWorldTimer(departure, finalWorld),
            WorldProcess = PrepareWorldProcess(departure, finalWorld)
        }, finalWorld);
        candidate = candidate with { SavedWorld = PrepareOrdinaryWorldFieldEntry(candidate.SavedWorld!) };
        candidate = AdvanceNativePlaytime(candidate);
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The Garden session changed during prepared destination validation.");
        }
        Dialog = null;
        Change(candidate);
    }
}
