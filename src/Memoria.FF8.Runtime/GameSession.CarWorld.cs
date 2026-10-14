namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetSavedWorldState(NativeSavedWorldState saved)
    {
        saved.Validate();
        State = State with { SavedWorld = saved };
    }

    public void CommitPreparedCarFieldEntry(GameState expectedDeparture, WorldState finalWorld, FieldTransition transition, NativeSavedWorldCarReturn receipt, NativeWorldSalaryState salary, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(finalWorld);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure) || departure.Scene != SceneKind.WorldMap || departure.World is not WorldState live || departure.WorldArrival is not WorldTransition arrival || unchecked((sbyte)arrival.Mode) != 33 || departure.SavedWorld?.Slot4.ShapeId != 73 || departure.WorldControls is null)
        {
            throw new NotSupportedException("Native car-to-field entry requires its unchanged active original vehicle departure.");
        }
        receipt.Validate();
        Validate(departure with { World = finalWorld });
        ValidatePreparedWorldActions(live, finalWorld);
        if (transition.FieldId is < 0 or > ushort.MaxValue || receipt.LastFieldId != departure.FieldId || receipt.SourceWorldFrame != finalWorld.Frame || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || receipt.Pose != new NativeSavedWorldPose(finalWorld.X, finalWorld.Z, checked((short)finalWorld.NativeHeight), checked((ushort)finalWorld.BodyYaw)) || receipt.CameraYaw96 != finalWorld.CameraYaw || finalWorld.NativeCarRuntime is null || receipt.MapDisplayMode110 != finalWorld.NativeCarRuntime.MapDisplayMode)
        {
            throw new InvalidOperationException("The prepared native car save differs from its current world action endpoint.");
        }
        GameState candidate = salary.ApplyTo(departure with { Scene = SceneKind.Field, FieldId = transition.FieldId, PreviousFieldId = receipt.LastFieldId, FieldName = transition.FieldId == 106 ? "bcgate_1" : $"field_{transition.FieldId}", EntryPoint = 0, X = transition.X, Y = transition.Y, EventState = null, DepartingFieldState = departure.DepartingFieldState ?? departure.EventState, DialogueDraft = null, FieldArrival = transition, WorldArrival = null, World = null, DepartingWorldState = finalWorld, DebugView = null, SavedWorld = departure.SavedWorld.WithCarReturn(receipt), WorldControls = departure.WorldControls.WithNormalFieldLoad(), ExamTimer = AdvanceWorldTimer(departure, finalWorld), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId) }, finalWorld);
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The car session changed during destination preparation.");
        }
        Dialog = null;
        Change(candidate);
    }
}
