namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void CommitPreparedBoatFieldEntry(GameState expectedDeparture, WorldState finalWorld, FieldTransition transition, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(finalWorld);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || departure.Scene != SceneKind.WorldMap || departure.World is not WorldState live || departure.WorldArrival is not WorldTransition arrival || departure.SavedWorld is null || departure.WorldControls is null || finalWorld.NativeBoatRuntime is not NativeBoatWorldRuntimeState boat || arrival != boat.Path.Packet)
        {
            throw new NotSupportedException("Native boat-to-field entry requires its unchanged original scripted departure.");
        }
        boat.Validate();
        NativeSavedWorldBoatReturn receipt = boat.Return ?? throw new NotSupportedException("Boat departure has no executed endpoint save receipt.");
        NativeSavedWorldState? saved = PrepareBoatWorldSave(departure, finalWorld);
        Validate(departure with { World = finalWorld, SavedWorld = saved });
        ValidatePreparedWorldActions(live, finalWorld);
        if (!boat.Path.FieldTransitionReady || receipt.SourceWorldFrame + 16 != finalWorld.Frame || receipt.LastFieldId != departure.FieldId || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || transition != receipt.FieldEntry)
        {
            throw new InvalidOperationException("Prepared boat field entry differs from its endpoint save and completed native fade.");
        }
        GameState candidate = departure with { Scene = SceneKind.Field, FieldId = transition.FieldId, PreviousFieldId = receipt.LastFieldId, FieldName = $"field_{transition.FieldId}", EntryPoint = 0, X = transition.X, Y = transition.Y, EventState = null, DepartingFieldState = departure.DepartingFieldState ?? departure.EventState, DialogueDraft = null, FieldArrival = transition, WorldArrival = null, World = null, DepartingWorldState = finalWorld, DebugView = null, SavedWorld = saved, WorldControls = departure.WorldControls.WithNormalFieldLoad(), ExamTimer = AdvanceWorldTimer(departure, finalWorld), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId) };
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The boat session changed during destination preparation.");
        }
        Dialog = null;
        Change(candidate);
    }

    private static NativeSavedWorldState? PrepareBoatWorldSave(GameState departure, WorldState finalWorld)
    {
        if (finalWorld.NativeBoatRuntime?.Return is not NativeSavedWorldBoatReturn receipt)
        {
            return departure.SavedWorld;
        }
        NativeSavedWorldState saved = departure.SavedWorld ?? throw new NotSupportedException("Native boat endpoint save has no original saved-world allocation.");
        if (receipt.LastFieldId != departure.FieldId || receipt.StoryWord != departure.Variables.GetValueOrDefault(256))
        {
            throw new InvalidDataException("Native boat save metadata differs from its unchanged issuing field and story.");
        }
        if (saved.BoatReturn is not null)
        {
            if (saved.BoatReturn != receipt)
            {
                throw new InvalidDataException("Boat departure changed the already executed endpoint save receipt during fade.");
            }
            return saved;
        }
        return saved.WithBoatReturn(receipt);
    }

    private static NativeWorldConfiguration? PrepareBoatWorldConfiguration(GameState departure, WorldState finalWorld)
    {
        return finalWorld.NativeBoatRuntime?.Return is null ? departure.WorldConfiguration : NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId);
    }
}
