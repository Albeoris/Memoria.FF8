namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void CommitPreparedRagnarokFieldEntry(GameState expectedDeparture, WorldState finalWorld, FieldTransition transition, NativeSavedWorldRagnarokReturn receipt, NativeWorldSalaryState salary, Action<GameState> validatePrepared)
    {
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure) || departure.Scene != SceneKind.WorldMap || departure.World?.NativeRagnarokRuntime is null || departure.WorldArrival is null || departure.SavedWorld is null || departure.WorldControls is null || departure.WorldEntryMemory is null)
        {
            throw new NotSupportedException("Ragnarok field entry requires the unchanged actual world allocation and saved/control owners.");
        }
        receipt.Validate();
        Validate(departure with { World = finalWorld, WorldProcess = PrepareWorldProcess(departure, finalWorld) });
        NativeSavedWorldRagnarokReturn actual = NativeSavedWorldRagnarokReturn.Create(finalWorld, checked((ushort)departure.FieldId), departure.WorldEntryMemory.StoryWord, receipt.MapLocationId);
        if (transition.FieldId is < 0 or > ushort.MaxValue || receipt != actual)
        {
            throw new InvalidOperationException("Prepared Ragnarok save differs from the actual physical action endpoint.");
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
            SavedWorld = departure.SavedWorld.WithRagnarokReturn(receipt),
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
            throw new InvalidOperationException("Ragnarok session changed during prepared destination validation.");
        }
        Dialog = null;
        Change(candidate);
    }
}
