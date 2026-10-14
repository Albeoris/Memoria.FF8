namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void CommitPreparedScriptedFieldEntry(GameState expectedDeparture, WorldState finalWorld, FieldTransition transition, NativeWorldSalaryState salary, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(finalWorld);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure) || departure.Scene != SceneKind.WorldMap || departure.World is not WorldState live || departure.WorldArrival is not WorldTransition arrival || departure.SavedWorld is null || departure.WorldControls is null || finalWorld.NativeScriptedRuntime is not NativeScriptedWorldRuntimeState scripted || arrival != scripted.Path.Packet)
        {
            throw new NotSupportedException("Native scripted-to-field entry requires its unchanged original scripted departure.");
        }
        scripted.Validate();
        NativeSavedWorldScriptedReturn receipt = scripted.Return ?? throw new NotSupportedException("Scripted departure has no executed endpoint save receipt.");
        NativeSavedWorldState? saved = PrepareScriptedWorldSave(departure, finalWorld);
        Validate(departure with { World = finalWorld, SavedWorld = saved });
        ValidatePreparedWorldActions(live, finalWorld);
        if (!scripted.Path.FieldTransitionReady || receipt.SourceWorldFrame + 16 != finalWorld.Frame || receipt.LastFieldId != departure.FieldId || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || transition != receipt.FieldEntry)
        {
            throw new InvalidOperationException("Prepared scripted field entry differs from its endpoint save and completed native fade.");
        }
        GameState candidate = salary.ApplyTo(departure with { Scene = SceneKind.Field, FieldId = transition.FieldId, PreviousFieldId = receipt.LastFieldId, FieldName = $"field_{transition.FieldId}", EntryPoint = 0, X = transition.X, Y = transition.Y, EventState = null, DepartingFieldState = departure.DepartingFieldState ?? departure.EventState, DialogueDraft = null, FieldArrival = transition, WorldArrival = null, World = null, DepartingWorldState = finalWorld, DebugView = null, SavedWorld = saved, WorldControls = departure.WorldControls.WithNormalFieldLoad(), ExamTimer = AdvanceWorldTimer(departure, finalWorld), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId) }, finalWorld);
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The scripted session changed during destination preparation.");
        }
        Dialog = null;
        Change(candidate);
    }

    private static NativeSavedWorldState? PrepareScriptedWorldSave(GameState departure, WorldState finalWorld)
    {
        if (finalWorld.NativeScriptedRuntime?.Return is not NativeSavedWorldScriptedReturn receipt)
        {
            return departure.SavedWorld;
        }
        NativeSavedWorldState saved = departure.SavedWorld ?? throw new NotSupportedException("Native scripted endpoint save has no original saved-world allocation.");
        if (receipt.LastFieldId != departure.FieldId || receipt.StoryWord != departure.Variables.GetValueOrDefault(256))
        {
            throw new InvalidDataException("Native scripted save metadata differs from its unchanged issuing field and story.");
        }
        if (saved.ScriptedReturn is not null)
        {
            if (saved.ScriptedReturn == receipt)
            {
                return saved;
            }
            if (departure.World?.NativeScriptedRuntime?.Return is not null || departure.DepartingWorldState?.NativeScriptedRuntime?.Return != saved.ScriptedReturn || !departure.DepartingWorldState.NativeScriptedRuntime.Path.FieldTransitionReady)
            {
                throw new InvalidDataException("Scripted departure changed its executed endpoint receipt without a completed preceding journey.");
            }
        }
        return saved.WithScriptedReturn(receipt);
    }

    private static NativeWorldConfiguration? PrepareScriptedWorldConfiguration(GameState departure, WorldState finalWorld)
    {
        return finalWorld.NativeScriptedRuntime?.Return is null ? departure.WorldConfiguration : NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId);
    }
}
