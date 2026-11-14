using System.Numerics;
using System.Buffers.Binary;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldVehicleState(int[] RideStack, int CancelHeldTicks = 0, int DismountPhase = 0, int BuggyAnimation = 0, int FlightSpeed = 0, int FlightState = 0, int FlightProgress = 0, int FlightVelocity = 0, int FlightMaxHeight = 0, int FlightSavedHeading = 0, int FlightPitch = 0, int FlightRoll = 0)
{
    public bool TinyBroncoFlightEnabled { get; init; }
    public int FlightPreviousView { get; init; }
    public int? FlightBasePitch { get; init; }
    public int? FlightBaseDistance { get; init; }
    public int HighwindPropellerFrame { get; init; }
    public int ChocoboFloatOffset { get; init; }
    public int BuggyTiltX { get; init; }
    public int BuggyTiltZ { get; init; }
}

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldVehicleState? vehicle;

    private Ff7WorldEntity CreateWorldEntity(int model)
    {
        Ff7WorldEntity created = new(model) { FrameDivisor = model switch { 5 or SubmarineModel => 0, 10 => 128, _ => 32 } };
        for (int i = 0; i < 6; i++)
        {
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(persistent.AsSpan(0x3B8 + i * 8));
            uint second = BinaryPrimitives.ReadUInt32LittleEndian(persistent.AsSpan(0x3BC + i * 8));
            if ((first >> 19 & 31) == model)
            {
                int x = (int)(first & 0x7FFFF);
                int y = unchecked((int)second) >> 18;
                int z = (int)(second & 0x3FFFF);
                int direction = (int)(first >> 20 & 0xFF0);
                return created with { X = x, Y = y, Z = z, PreviousX = x, PreviousY = y, PreviousZ = z, Direction = direction, TargetDirection = direction };
            }
        }
        return created;
    }

    private static Ff7WorldVehicleState? CopyVehicleState(Ff7WorldVehicleState? state)
    {
        return state is null ? null : state with { RideStack = state.RideStack.ToArray() };
    }

    private void PushWorldRide(int model)
    {
        vehicle ??= new([]);
        if (vehicle.RideStack.Length < 8)
        {
            vehicle = vehicle with { RideStack = [.. vehicle.RideStack, model] };
        }
    }

    private int PopWorldRide()
    {
        if (vehicle is not { RideStack.Length: > 0 })
        {
            return 0;
        }
        int model = vehicle.RideStack[^1];
        vehicle = vehicle with { RideStack = vehicle.RideStack[..^1] };
        return model;
    }

    private bool ExecuteVehicleOperation(WorldHostRequest request)
    {
        int[] arguments = request.Command.Arguments;
        switch (request.Command.Operation)
        {
            case WorldOperation.StartLeaveVehicle:
                vehicle ??= new([]);
                vehicle = vehicle with { DismountPhase = 1 };
                return true;
            case WorldOperation.RestoreAttachedRider:
                Ff7WorldEntity carrier = RequireEntity(controlledModel);
                if (carrier.AttachedModel is int attached)
                {
                    Ff7WorldEntity attachedEntity = RequireEntity(attached);
                    Machine.ReattachModel(attached);
                    UpdateEntity(attachedEntity with { X = carrier.X, Y = carrier.Y, Z = carrier.Z, PreviousX = carrier.PreviousX, PreviousY = carrier.PreviousY, PreviousZ = carrier.PreviousZ, Direction = carrier.Direction, TargetDirection = carrier.TargetDirection, TurnOffset = carrier.TurnOffset, Flags = attachedEntity.Flags | 2, PresentationEpoch = attachedEntity.PresentationEpoch + 1 });
                    UpdateEntity(carrier with { AttachedModel = null });
                    SelectControlledWorldModel(attached);
                    PresentationCameraEpoch++;
                }
                return true;
            case WorldOperation.EnableTinyBroncoFlight:
                vehicle ??= new([]);
                vehicle = vehicle with { TinyBroncoFlightEnabled = arguments[0] != 0 };
                return true;
            case WorldOperation.Surface:
                BeginSubmarineMapTransfer(0);
                return true;
        }
        if (request.Command.Operation == WorldOperation.AttachRider)
        {
            Ff7WorldEntity rider = RequireEntity(arguments[0]);
            int model = arguments[1];
            Ff7WorldEntity transport = entities.GetValueOrDefault(model) ?? CreateWorldEntity(model);
            if (transport.AttachedModel is not null)
            {
                throw new InvalidDataException("FFVII world transport already has an attached model.");
            }
            PushWorldRide(rider.Model);
            entities[model] = transport with { AttachedModel = rider.Model, Flags = transport.Flags | 2 };
            return true;
        }
        if (request.Command.Operation == WorldOperation.ChangeModelIdentity)
        {
            Ff7WorldEntity previous = RequireEntity(request.ActiveModel);
            int replacement = arguments[0];
            entities.Remove(previous.Model);
            entities.Add(replacement, previous with { Model = replacement, PresentationEpoch = previous.PresentationEpoch + 1 });
            if (controlledModel == previous.Model)
            {
                controlledModel = replacement;
            }
            foreach (Ff7WorldEntity entity in entities.Values.Where(entity => entity.AttachedModel == previous.Model).ToArray())
            {
                UpdateEntity(entity with { AttachedModel = replacement });
            }
            return true;
        }
        return false;
    }

    private void BoardSurfaceVehicle(int model, int scriptWord)
    {
        Ff7WorldEntity transport = RequireEntity(model);
        if (model is not (3 or 5 or 6 or SubmarineModel))
        {
            throw new NotSupportedException($"FFVII world model {model} has no supported transport contract at word {scriptWord:X4}.");
        }
        if (vehicle is null && chocobo?.RiderModel is int previousRider)
        {
            PushWorldRide(previousRider);
        }
        PushWorldRide(controlledModel);
        int? removed = controlledModel;
        HashSet<int> removedModels = [];
        while (removed is int previous && removedModels.Add(previous))
        {
            Ff7WorldEntity previousEntity = RequireEntity(previous);
            removed = previousEntity.AttachedModel ?? (chocobo?.Model == previous ? chocobo.RiderModel : null);
            Machine.RemoveModel(previous);
            entities.Remove(previous);
        }
        if (chocobo is not null && removedModels.Contains(chocobo.Model))
        {
            chocobo = null;
        }
        if (transport.AttachedModel is int targetAttachment && removedModels.Contains(targetAttachment))
        {
            UpdateEntity(transport with { AttachedModel = null });
        }
        SelectControlledWorldModel(model);
        input = input with { PreviousX = transport.X, PreviousY = transport.Y, PreviousZ = transport.Z };
        SynchronizeFreeDirection(transport);
        PresentationCameraEpoch++;
        if (model == 6)
        {
            PlayPrioritizedWorldSound(0x1EC);
        }
        if (model == 3 || model == 5 && vehicle?.TinyBroncoFlightEnabled == true)
        {
            BeginHighwindFlight(1);
            if (model == 3)
            {
                EmitHighwindTakeoffEffect();
            }
        }
    }

    private void AdvanceSurfaceVehicleTransport()
    {
        if (controlledModel is not (3 or 5 or 6 or SubmarineModel) || Resources.World != 0)
        {
            return;
        }
        vehicle ??= new([]);
        Ff7WorldEntity transport = RequireEntity(controlledModel);
        bool cancel = (input.Held & 0x40) != 0;
        int heldTicks = vehicle.CancelHeldTicks;
        int terrain = transport.Terrain & 31;
        if (vehicle.DismountPhase == 0 && movementEnabled && !cancel && heldTicks is > 0 and < 15 && (controlledModel != SubmarineModel || terrain == 18) && CanLeaveSurfaceVehicle(controlledModel, terrain))
        {
            movementEnabled = false;
            controlLock = 2;
            vehicle = vehicle with { DismountPhase = 1 };
            if (controlledModel == 3 || controlledModel == 5 && (transport.Flags & 128) != 0)
            {
                BeginHighwindFlight(-1);
            }
        }
        else if (vehicle.DismountPhase == 1 && vehicle.FlightState == 0)
        {
            vehicle = vehicle with { DismountPhase = 2 };
        }
        else if (vehicle.DismountPhase == 2)
        {
            CompleteSurfaceVehicleDismount(transport);
        }
        vehicle = vehicle with { CancelHeldTicks = cancel ? Math.Min(15, heldTicks + 1) : 0 };
    }

    private static bool CanLeaveSurfaceVehicle(int model, int terrain)
    {
        uint mask = model switch { 3 => 1u, 5 => 0x70u, 6 => 0x221B0F83u, SubmarineModel => uint.MaxValue, _ => 0 };
        return (mask & (1u << terrain)) != 0;
    }

    private Ff7WorldEntity OffsetDismountingVehicle(Ff7WorldEntity transport)
    {
        int distance = transport.Model is 5 or SubmarineModel ? (transport.Flags & 128) != 0 ? 100 : 800 : 300;
        int direction = transport.Direction + transport.TurnOffset + (transport.Model is 5 or SubmarineModel ? 0 : 1024);
        Vector3 step = inputMath.TransformRotation(new(0, 0, distance), 0, direction, 0);
        return OffsetWorldEntity(transport with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ }, (int)step.X, (int)step.Z);
    }

    private void CompleteSurfaceVehicleDismount(Ff7WorldEntity transport)
    {
        vehicle = vehicle! with { DismountPhase = 0 };
        movementEnabled = true;
        if (transport.X == input.PreviousX && transport.Z == input.PreviousZ)
        {
            if (transport.Model == 3 || transport.Model == 5 && (transport.Flags & 128) != 0)
            {
                BeginHighwindFlight(1);
            }
            return;
        }
        if (transport.Model == 5 && (transport.Flags & 128) != 0)
        {
            UpdateEntity(transport with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Flags = transport.Flags & ~(1 | 128) });
            return;
        }
        int riderModel = PopWorldRide();
        if (IsWorldChocobo(riderModel))
        {
            int attached = vehicle!.RideStack.LastOrDefault();
            Machine.EnsurePresentationModel(attached);
            Machine.DetachModel(attached);
            Machine.EnsurePresentationModel(riderModel);
            Ff7WorldEntity mounted = RequireEntity(riderModel);
            UpdateEntity(mounted with { AttachedModel = attached });
            chocobo = new(attached) { Model = riderModel };
        }
        else
        {
            Machine.EnsurePresentationModel(riderModel);
        }
        Ff7WorldEntity rider = RequireEntity(riderModel);
        int x = transport.X;
        int y = transport.Y;
        int z = transport.Z;
        UpdateEntity(transport with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, AttachedModel = null, Flags = transport.Flags & ~(1 | 128) });
        Machine.StartModel(transport.Model, 5);
        UpdateEntity(rider with { X = x, Y = y, Z = z, PreviousX = x, PreviousY = y, PreviousZ = z, Flags = rider.Flags | 2, Terrain = transport.Terrain, TerrainResolved = transport.TerrainResolved, GroundHeight = transport.GroundHeight, PresentationEpoch = rider.PresentationEpoch + 1 });
        SelectControlledWorldModel(riderModel);
        SynchronizeFreeDirection(RequireEntity(riderModel));
        camera = camera with { Roll = 0 };
        PresentationCameraEpoch++;
        PlayWorldSong(1);
        if (transport.Model is 5 or 6)
        {
            PlayPrioritizedWorldSound(transport.Model == 6 ? -0x1EC : -0x1ED);
        }
    }

    private void ValidateVehicleState(Ff7WorldProcess state)
    {
        if (state.Vehicle is { } saved && (saved.RideStack is null || saved.RideStack.Length > 8 || saved.RideStack.Any(model => model is < 0 or > 255) || saved.CancelHeldTicks is < 0 or > 15 || saved.DismountPhase is < 0 or > 2 || saved.BuggyAnimation is < 0 or > 4 || saved.FlightState is < -1 or > 1 || saved.FlightProgress is < 0 or > 256 || saved.FlightVelocity is < -64 or > 64 || saved.FlightSpeed is < 0 or > 720 || saved.FlightMaxHeight is not (0 or 2000 or 4000) || saved.FlightPreviousView is < 0 or > 3 || saved.FlightPitch is < -10 or > 10 || saved.FlightRoll is < -128 or > 128 || saved.HighwindPropellerFrame < 0 || saved.ChocoboFloatOffset is < -270 or > 0))
        {
            throw new InvalidDataException("FFVII world vehicle state is corrupt.");
        }
        foreach (Ff7WorldEntity entity in state.Entities.Where(entity => entity.AttachedModel is not null))
        {
            if (entity.AttachedModel == entity.Model || !state.Vm.Models.Any(model => model.Model == entity.AttachedModel && model.Detached))
            {
                throw new InvalidDataException("FFVII world attachment has no matching detached model.");
            }
        }
    }
}
