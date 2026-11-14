using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldChocoboPathControl(bool InitializationRequested = false, Ff7WorldChocoboRelease? Path = null);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldChocoboPathControl? chocoboPathControl;

    private static Ff7WorldChocoboPathControl? CopyChocoboPathControl(Ff7WorldChocoboPathControl? state)
    {
        return state?.Path is { } path ? state with { Path = path with { Path = path.Path.ToArray(), Visited = path.Visited.ToArray() } } : state;
    }

    private WorldHostReply? ExecuteChocoboPathControl(WorldHostRequest request)
    {
        Ff7WorldChocoboRelease? path = chocoboPathControl?.Path ?? chocobo?.Release;
        switch (request.Command.Operation)
        {
            case WorldOperation.BeginChocoboPath:
                chocoboPathControl = (chocoboPathControl ?? new()) with { InitializationRequested = true };
                return new(true);
            case WorldOperation.WaitChocoboPath:
                return new(path?.SearchComplete == true, path is { SearchComplete: true } ? path.SearchFailed ? -1 : 1 : 0);
            case WorldOperation.WaitChocoboRuns:
                return new(chocobo?.Release?.RunPhase is null or 0);
            case WorldOperation.RunChocoboPath:
                int action = request.Command.Arguments[0];
                if (action != 3)
                {
                    throw new NotSupportedException("FFVII Chocobo farm path actions require their separate native multi-runner contract.");
                }
                if (path is not { SearchComplete: true, SearchFailed: false })
                {
                    return new(true);
                }
                Ff7WorldEntity bird = RequireEntity(YellowChocoboModel);
                if (controlledModel == YellowChocoboModel || !Machine.IsModelAttached(YellowChocoboModel))
                {
                    throw new InvalidDataException("FFVII Chocobo release requires a resident dismounted bird.");
                }
                if (chocobo is { RiderModel: not null } || chocobo is { Model: not YellowChocoboModel })
                {
                    throw new NotSupportedException("FFVII Chocobo path release requires its caught-bird ownership contract.");
                }
                (int x, int z) = ChocoboFaceCenter(path.Path[0].Triangle);
                UpdateEntity(bird with { X = x, Y = (bird.Flags & 128) != 0 ? bird.Y : WorldGroundOffset(bird.Terrain & 31, bird.Model), Z = z, GroundHeight = 0, Flags = bird.Flags & ~1 | (x != bird.PreviousX || z != bird.PreviousZ ? 1 : 0), PresentationEpoch = bird.PresentationEpoch + 1 });
                chocobo = (chocobo ?? new() { Landing = new(bird.X, bird.Y, bird.Z, bird.Terrain, bird.GroundHeight) }) with { ReleaseRequested = true, Release = path with { RunPhase = 7, PathIndex = 0 } };
                return new(true, ActiveModel: YellowChocoboModel);
            default:
                return null;
        }
    }

    private void InitializeRequestedChocoboPath(Ff7WorldTerrainReference? triangle, Ff7WorldEntity player)
    {
        if (chocoboPathControl is not { InitializationRequested: true } || triangle is null)
        {
            return;
        }
        Ff7WorldChocoboRelease? current = chocoboPathControl.Path ?? chocobo?.Release;
        chocoboPathControl = chocoboPathControl with { InitializationRequested = false };
        if (current is { SearchComplete: false })
        {
            return;
        }
        bool towardFarm = player.X is >= 0x36000 and < 0x40000 && player.Z is >= 0x1C000 and < 0x26000;
        Ff7WorldChocoboRelease path = new(towardFarm, [new(triangle)], [triangle]);
        if (chocobo?.Release is not null)
        {
            chocobo = chocobo with { Release = path };
            chocoboPathControl = chocoboPathControl with { Path = null };
        }
        else
        {
            chocoboPathControl = chocoboPathControl with { Path = path };
        }
    }

    private void AdvanceChocoboPathControl()
    {
        if (chocoboPathControl?.Path is { SearchComplete: false } path)
        {
            chocoboPathControl = chocoboPathControl with { Path = SearchChocoboReleasePath(path) };
        }
    }
}
