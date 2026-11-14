using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldChocoboState(int? RiderModel = null, int CancelHeldTicks = 0, int DismountPhase = 0, bool ReleaseRequested = false)
{
    public int Model { get; init; } = 4;
    public Ff7WorldChocoboLanding? Landing { get; init; }
    public Ff7WorldChocoboRelease? Release { get; init; }
}

public sealed record Ff7WorldChocoboLanding(int X, int Y, int Z, int Terrain, int GroundHeight);

public sealed partial class Ff7WorldRuntime
{
    private const int YellowChocoboModel = 4;
    private const uint YellowChocoboTerrainMask = 0x321B6F83;
    private Ff7WorldChocoboState? chocobo;
    private const int ChocoboCancelMask = 0x40;
    private int chocoboTint;

    private static bool IsWorldChocobo(int model) => model is YellowChocoboModel or 19 or 41 or 42;

    private static uint ChocoboTerrainMask(int tint) => tint switch { 0 => 0x321B6F83u, 1 => 0x321B6F87u, 2 => 0x321B6FF3u, 3 => 0x325B7FF7u, 4 => 0x375B7FFFu, 5 => 0, _ => throw new InvalidDataException("FFVII world Chocobo terrain selector is invalid.") };

    private IEnumerable<Ff7WorldEntity> ActiveWorldEntities()
    {
        HashSet<int> detached = Machine.Capture().Models.Where(model => model.Detached).Select(model => model.Model).ToHashSet();
        return entities.Values.Where(entity => !detached.Contains(entity.Model));
    }

    private void BoardWorldTransport(int model, int scriptWord)
    {
        if (model == controlledModel || controlledModel < 0 || !Machine.IsModelScriptEnabled(model))
        {
            return;
        }
        if (!IsWorldChocobo(model))
        {
            BoardSurfaceVehicle(model, scriptWord);
            return;
        }
        if (controlledModel is not (0 or 1 or 2) || viewMode == 3)
        {
            throw new NotSupportedException($"FFVII world model {model} has no supported transport contract at word {scriptWord:X4}.");
        }
        Ff7WorldEntity bird = RequireEntity(model);
        if (chocobo is { ReleaseRequested: true } or { DismountPhase: > 0 })
        {
            return;
        }
        Machine.DetachModel(controlledModel);
        PushWorldRide(controlledModel);
        chocobo = new(controlledModel) { Model = model };
        UpdateEntity(bird with { AttachedModel = controlledModel });
        SelectControlledWorldModel(model);
        input = input with { PreviousX = bird.X, PreviousY = bird.Y, PreviousZ = bird.Z };
        PresentationCameraEpoch++;
        PlayWorldSong(2);
    }

    private static Ff7WorldEntity OffsetWorldEntity(Ff7WorldEntity entity, int dx, int dz)
    {
        if (dx == 0 && dz == 0)
        {
            return entity;
        }
        return entity with { X = Wrap(entity.X + dx, 0x48000), Z = Wrap(entity.Z + dz, 0x38000), Flags = entity.Flags | 1 };
    }

    private void AdvanceChocoboTransport()
    {
        if (chocobo is { RiderModel: int riderModel } mounted)
        {
            Ff7WorldEntity bird = RequireEntity(mounted.Model);
            bool cancel = (input.Held & ChocoboCancelMask) != 0;
            int heldTicks = cancel ? Math.Min(15, mounted.CancelHeldTicks + 1) : 0;
            chocobo = mounted with { CancelHeldTicks = heldTicks };
            switch (mounted.DismountPhase)
            {
                case 0:
                    if (movementEnabled && !cancel && mounted.CancelHeldTicks is > 0 and < 15 && (input.Held & 0xF000) == 0 && input.Horizontal == 0 && input.Vertical == 0 && (0x221B0F03u & (1u << (bird.Terrain & 31))) != 0)
                    {
                        movementEnabled = false;
                        controlLock = 2;
                        if (mounted.Model == YellowChocoboModel)
                        {
                            Machine.SetModelScriptEnabled(mounted.Model, false);
                        }
                        chocobo = chocobo with { DismountPhase = 1 };
                    }
                    break;
                case 1:
                    chocobo = chocobo with { DismountPhase = 2 };
                    break;
                case 2:
                    CompleteChocoboDismount(bird, riderModel);
                    break;
            }
        }
        else if (chocobo is { Model: YellowChocoboModel, Landing: not null })
        {
            AdvanceReleasedChocobo();
        }
        else if (controlledModel is 0 or 1 or 2 && movementEnabled)
        {
            foreach (Ff7WorldEntity bird in ActiveWorldEntities().Where(entity => IsWorldChocobo(entity.Model) || entity.Model is 3 or 5 or 6 or SubmarineModel))
            {
                if ((bird.Flags & 8) == 0 && WorldModelsCollide(RequireEntity(controlledModel), bird))
                {
                    Machine.StartModel(bird.Model, (input.Held & 32) != 0 ? 4 : 3);
                }
            }
        }
    }

    private void CompleteChocoboDismount(Ff7WorldEntity bird, int riderModel)
    {
        int dx = WrappedDifference(bird.X - input.PreviousX, 0x48000);
        int dz = WrappedDifference(bird.Z - input.PreviousZ, 0x38000);
        Machine.SetModelScriptEnabled(bird.Model, true);
        if (dx == 0 && dz == 0)
        {
            movementEnabled = true;
            chocobo = chocobo! with { DismountPhase = 0 };
            return;
        }
        int direction = (inputMath.Direction(dx, dz) - 1024) & 4095;
        UpdateEntity(bird with { X = input.PreviousX, Y = input.PreviousY, Z = input.PreviousZ, Direction = direction, TargetDirection = direction, ScriptDirection = null, TurnOffset = 0, Flags = bird.Flags & ~1, AttachedModel = null });
        Ff7WorldEntity rider = RequireEntity(riderModel);
        UpdateEntity(rider with { X = bird.X, Y = bird.Y, Z = bird.Z, PreviousX = bird.X, PreviousY = bird.Y, PreviousZ = bird.Z, Direction = direction, TargetDirection = direction, Terrain = bird.Terrain, TerrainResolved = bird.TerrainResolved, GroundHeight = bird.GroundHeight, TurnOffset = 0, Animation = 2, Flags = rider.Flags | 2, PresentationEpoch = rider.PresentationEpoch + 1 });
        Machine.StartModel(bird.Model, 5);
        if (bird.Model == YellowChocoboModel)
        {
            Machine.SetModelScriptEnabled(bird.Model, false);
        }
        Machine.ReattachModel(riderModel);
        PopWorldRide();
        chocobo = new() { Model = bird.Model, Landing = new(bird.X, bird.Y, bird.Z, bird.Terrain, bird.GroundHeight) };
        SelectControlledWorldModel(riderModel);
        SynchronizeFreeDirection(RequireEntity(riderModel));
        movementEnabled = false;
        controlLock = 6;
        camera = camera with { Roll = 0 };
        PresentationCameraEpoch++;
        PlayWorldSong(1);
    }

    private void BeginChocoboRelease(int model, int scriptWord)
    {
        if (model != YellowChocoboModel || controlledModel == model || chocobo is not { Landing: not null })
        {
            throw new NotSupportedException($"FFVII world model {model} has no supported release contract at word {scriptWord:X4}.");
        }
        Machine.SetModelScriptEnabled(model, false);
        chocobo = chocobo with { ReleaseRequested = true };
    }

    private void PlaceDismountedRider(WorldHostRequest request)
    {
        if (chocobo?.Landing is not { } landing || request.ActiveModel != controlledModel || controlledModel is not (0 or 1 or 2))
        {
            throw new NotSupportedException($"FFVII world model {request.ActiveModel} has no supported release placement at word {request.ProgramCounter:X4}.");
        }
        Ff7WorldEntity rider = RequireEntity(controlledModel);
        UpdateEntity(rider with { X = landing.X, Y = landing.Y, Z = landing.Z, PreviousX = landing.X, PreviousY = landing.Y, PreviousZ = landing.Z, Terrain = landing.Terrain, GroundHeight = landing.GroundHeight, PresentationEpoch = rider.PresentationEpoch + 1 });
        input = input with { PreviousX = landing.X, PreviousY = landing.Y, PreviousZ = landing.Z };
    }

    private int WorldGroundOffset(int terrain, int model = 0)
    {
        if (model == 19)
        {
            vehicle ??= new([]);
            int offset = vehicle.ChocoboFloatOffset;
            if (terrain is >= 3 and <= 6 or 26)
            {
                if (entities.TryGetValue(controlledModel, out Ff7WorldEntity? player) && (player.Flags & 1) != 0)
                {
                    offset = Math.Min(0, offset + 20);
                }
                else
                {
                    int target = terrain is 4 or 6 ? -50 : -250;
                    if (offset > target)
                    {
                        offset -= 10;
                    }
                    else if (offset < target - 20)
                    {
                        offset = target;
                    }
                }
                vehicle = vehicle with { ChocoboFloatOffset = offset };
                return offset;
            }
            vehicle = vehicle with { ChocoboFloatOffset = 0 };
        }
        return model switch { 8 => -15, SubmarineModel => -240, 25 => 0, _ => terrain is 1 or 25 ? -192 : terrain is 4 or 7 ? -64 : 0 };
    }

    private void ValidateChocoboState(Ff7WorldProcess state)
    {
        ValidateChocoboRelease(state.Chocobo?.Release, state.WorldMapState);
        WorldEntitySnapshot[] detached = state.Vm.Models.Where(model => model.Detached).ToArray();
        if (state.Entities.Any(entity => entity.ScriptDirection is < 0 or > 4095) || state.Chocobo is { } bird && (!IsWorldChocobo(bird.Model) || bird.CancelHeldTicks is < 0 or > 15 || bird.DismountPhase is < 0 or > 2 || !state.Entities.Any(entity => entity.Model == bird.Model) || bird.RiderModel is not null && (bird.ReleaseRequested || bird.Landing is not null || bird.Release is not null) || bird.RiderModel is null && bird.Landing is null) ||
            state.Chocobo?.RiderModel is int rider && (state.ControlledModel != state.Chocobo.Model && !state.Entities.Any(entity => entity.Model == state.ControlledModel && entity.AttachedModel == state.Chocobo.Model) || rider is not (0 or 1 or 2) || !detached.Any(model => model.Model == rider) || detached.Any(model => model.Model != rider && !state.Entities.Any(entity => entity.AttachedModel == model.Model))) ||
            state.Chocobo?.RiderModel is null && (detached.Any(model => !state.Entities.Any(entity => entity.AttachedModel == model.Model)) || IsWorldChocobo(state.ControlledModel)))
        {
            throw new InvalidDataException("FFVII world Chocobo state has inconsistent model ownership.");
        }
    }
}
