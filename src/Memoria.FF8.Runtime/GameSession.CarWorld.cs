namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateCarGardenBody(WorldState world, NativeCarWorldRuntimeState car)
    {
        if (car.GardenObjects is not { } garden)
        {
            return;
        }
        NativeGardenWorldObject foot = garden.Objects[garden.Allocation.FootObjectIndex];
        if (foot.Position != new NativeWorldPathPosition(world.X, world.NativeHeight, -world.Z) || ((foot.RotationY % 4096 + 4096) % 4096) != world.BodyYaw)
        {
            throw new InvalidDataException("Car-allocation Garden foot object differs from the actual controlled body.");
        }
        NativeGardenWorldObject vehicle = garden.Objects[garden.Allocation.SavedCarIndex];
        NativeWorldPathPosition expected = world.NativeFootRuntime?.ParkedCar?.Position ?? new(world.X, world.NativeHeight, -world.Z);
        int yaw = world.NativeFootRuntime?.ParkedCar?.BodyYaw ?? world.BodyYaw;
        if (vehicle.Position != expected || ((vehicle.RotationY % 4096 + 4096) % 4096) != yaw || !NativeWorldForestState.ContentEquals(garden.Forest, world.NativeFootRuntime?.Forest ?? car.Forest))
        {
            throw new InvalidDataException("Car selected from Garden lost its actual parked/current vehicle or shared forest owner.");
        }
    }

    private static bool HasNativeCarArrivalAuthority(GameState state, WorldState world, NativeCarWorldRuntimeState car)
    {
        if (state.WorldArrival is not { } arrival)
        {
            return false;
        }
        if (car.Boarding is { } boarding && (boarding.OriginalPacket != arrival || boarding.SourceWorldFrame > world.Frame))
        {
            return false;
        }
        if (unchecked((byte)arrival.Mode) == car.EntityType)
        {
            return true;
        }
        return car.Boarding is not null;
    }

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
        if (!ReferenceEquals(departure, expectedDeparture) || !ReferenceEquals(departure, salary.Departure) || departure.Scene != SceneKind.WorldMap || departure.World is not WorldState live || live.NativeCarRuntime is not { } car || !HasNativeCarArrivalAuthority(departure, live, car) || departure.SavedWorld?.Slot4.ShapeId != car.ShapeId || departure.WorldControls is null)
        {
            throw new NotSupportedException("Native car-to-field entry requires its unchanged active original vehicle departure.");
        }
        receipt.Validate();
        Validate(departure with { World = finalWorld, WorldProcess = PrepareWorldProcess(departure, finalWorld) });
        if (transition.FieldId is < 0 or > ushort.MaxValue || receipt.LastFieldId != departure.FieldId || receipt.SourceWorldFrame != finalWorld.Frame || receipt.StoryWord != departure.Variables.GetValueOrDefault(256) || receipt.Pose != new NativeSavedWorldPose(finalWorld.X, finalWorld.Z, checked((short)finalWorld.NativeHeight), checked((ushort)finalWorld.BodyYaw)) || receipt.CameraYaw96 != finalWorld.CameraYaw || finalWorld.NativeCarRuntime is null || receipt.MapDisplayMode110 != (finalWorld.NativeCarRuntime.MapDisplayMode == 3 ? 0 : finalWorld.NativeCarRuntime.MapDisplayMode))
        {
            throw new InvalidOperationException("The prepared native car save differs from its current world action endpoint.");
        }
        GameState candidate = salary.ApplyTo(departure with { Scene = SceneKind.Field, FieldId = transition.FieldId, FieldLoadContext = NativeFieldLoadContext.FromWorldReturn(transition.FieldId), PreviousFieldId = receipt.LastFieldId, FieldName = transition.FieldId == 106 ? "bcgate_1" : $"field_{transition.FieldId}", EntryPoint = 0, X = transition.X, Y = transition.Y, EventState = null, DepartingFieldState = departure.DepartingFieldState ?? departure.EventState, DialogueDraft = null, FieldArrival = transition, FieldRequestPacket = (departure.FieldRequestPacket ?? new()).WithWorldReturn(transition), WorldArrival = null, World = null, DepartingWorldState = finalWorld, DebugView = null, SavedWorld = (finalWorld.NativeCarRuntime!.RagnarokObjects is { } parkedRagnarok ? departure.SavedWorld.WithRagnarokParking(parkedRagnarok) : finalWorld.NativeCarRuntime.GardenObjects is { } parkedGarden ? departure.SavedWorld.WithGardenParking(parkedGarden) : departure.SavedWorld).WithCarReturn(receipt), WorldControls = departure.WorldControls.WithNormalFieldLoad(), ExamTimer = AdvanceWorldTimer(departure, finalWorld), WorldProcess = PrepareWorldProcess(departure, finalWorld), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(finalWorld, departure.FieldId) }, finalWorld);
        candidate = candidate with { SavedWorld = PrepareOrdinaryWorldFieldEntry(candidate.SavedWorld!) };
        candidate = StageOriginalAchievementNativeMutations(candidate, salary.Payments > 0);
        candidate = AdvanceNativePlaytime(candidate);
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
