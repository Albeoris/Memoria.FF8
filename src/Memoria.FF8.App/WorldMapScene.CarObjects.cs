using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeNormalFootObjectProcess CreateCarObjectProcess(NativeTrainWorldAllocation allocation, NativeWorldFootCameraState camera)
    {
        NativeTrainRailBank? primary = (allocation.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) : null;
        NativeTrainRailBank? alternate = (allocation.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1) : null;
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, primary, alternate, camera, primary is null ? null : uint.MaxValue, alternate is null ? null : uint.MaxValue);
        process.ValidateCar();
        return process;
    }

    private void ValidateCarObjects(WorldState world, NativeCarWorldRuntimeState car, bool validateProximity = false)
    {
        NativeNormalFootObjectProcess process = car.ObjectProcess ?? throw new NotSupportedException("Native car rail allocation requires its executed ordinary object process; migrate the historical snapshot explicitly.");
        process.ValidateCar();
        bool invalidCallback = validateProximity ? process.Callback != world.Frame : process.Callback < world.Frame || process.Callback > world.Frame + 1;
        if (invalidCallback)
        {
            throw new InvalidDataException("Native car object process differs from its actual callback clock.");
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        bool stopAtEndpoint = (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0;
        foreach (NativeTrainRailBank bank in CarRailBanks(car))
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            bank.Validate(definition);
            bank.ValidateProjectionPhase(process.Callback, stopAtEndpoint);
            if (process.Callback == 0)
            {
                NativeTrainRailBank initial = NativeTrainRailBank.Create(definition, 1);
                if (!bank.Nodes.SequenceEqual(initial.Nodes) || bank.Presentation.Any(pose => pose.Projection is not null || pose.Orientation is not null || pose.Angles is not null))
                {
                    throw new InvalidDataException("Native car initial rail bank acquired unexecuted presentation words.");
                }
                continue;
            }
            foreach (NativeWorldPathNodePresentation pose in bank.Presentation)
            {
                NativeWorldPathNodeProjection projection = pose.Projection ?? throw new InvalidDataException("Native car rail callback lost its executed projection.");
                context.ValidateProjection(projection);
                if (pose.Orientation is not null && (pose.Orientation.Callback > process.Callback || pose.Angles != NativeWorldScriptedPathMath.LinkedOrientation(pose.Orientation.Middle.Words, pose.Orientation.Linked.Words)))
                {
                    throw new InvalidDataException("Native car rail orientation differs from its retained linked projection words.");
                }
            }
        }
        if (validateProximity && process.Callback > 0)
        {
            NativeWorldPathPosition distancePosition = process.CarDistancePosition ?? throw new InvalidDataException("Native car rail process lost its preceding selected-body distance origin.");
            NativeWorldPathWords player = context.RelativeWords(distancePosition);
            if (process.Primary is not null && process.PrimaryDistanceSquared != NormalTrainDistance(process.Primary, player, context) || process.Alternate is not null && process.AlternateDistanceSquared != NormalTrainDistance(process.Alternate, player, context))
            {
                throw new InvalidDataException("Native car train speed lost its preceding54E650 distance-word producer.");
            }
        }
    }

    private static NativeTrainRailBank[] CarRailBanks(NativeCarWorldRuntimeState car)
    {
        return car.ObjectProcess is { } process ? new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>().ToArray() : [];
    }

    private NativeCarWorldRuntimeState StepCarObjects(WorldState cameraState, NativeCarWorldRuntimeState car, int selectedEntity)
    {
        if (car.ObjectProcess is not { } process)
        {
            return car;
        }
        process.ValidateCar();
        NativeWorldFootCameraState camera = cameraState.NativeFootRuntime is not null ? NativeWorldFootCameraState.Create(cameraState) : car.ProjectionCamera ?? throw new InvalidDataException("Native car rails require the current pre-terrain camera.");
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeWorldPathWords player = context.RelativeWords(camera.Origin);
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(selectedEntity));
        long callback = checked(process.Callback + 1);
        bool stopAtEndpoint = (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0;
        NativeTrainRailBank Advance(NativeTrainRailBank bank, uint distance)
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            return bank.Step(definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint, out _, next =>
            {
                if (next.Pause == 0 && WorldStationContactCertificate.EvaluateTrain(assets, next, definition, player, camera.NativeHeight, context.IsNear, probeRadius: NativeWorldTerrain.ProbeRadius(selectedEntity)) >= 0)
                {
                    return next.ResolveContact(bank, definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint);
                }
                return next;
            }, NativeNormalFootObjectProcess.SelectSpeed(bank, distance, WorldStationContactCertificate.ContactRadius(selected)));
        }
        NativeTrainRailBank? primary = process.Primary is null ? null : Advance(process.Primary, process.PrimaryDistanceSquared!.Value);
        NativeTrainRailBank? alternate = process.Alternate is null ? null : Advance(process.Alternate, process.AlternateDistanceSquared!.Value);
        return car with { ObjectProcess = process with { Callback = callback, Primary = primary, Alternate = alternate, Camera = camera } };
    }

    private static NativeCarWorldRuntimeState CompleteCarObjectCallback(NativeCarWorldRuntimeState car, int x, int rawZ, int height)
    {
        if (car.ObjectProcess is not { } process)
        {
            return car;
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        NativeWorldPathWords player = context.RelativeWords(new(x, height, -rawZ));
        return car with { ObjectProcess = process with { CarDistancePosition = new(x, height, -rawZ), PrimaryDistanceSquared = process.Primary is null ? null : NormalTrainDistance(process.Primary, player, context), AlternateDistanceSquared = process.Alternate is null ? null : NormalTrainDistance(process.Alternate, player, context) } };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadCarRailModels(NativeCarWorldRuntimeState car)
    {
        if (car.ObjectProcess is not { } process)
        {
            return [];
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        List<NativeWorldStaticModelDraw> models = [];
        foreach (NativeTrainRailBank bank in CarRailBanks(car))
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            for (int index = 0; index < bank.Presentation.Length; index++)
            {
                if (NativeTrainRailBank.Link(index) == 0)
                {
                    continue;
                }
                NativeWorldPathNodePresentation pose = bank.Presentation[index];
                if (pose.Projection is null)
                {
                    if (context.IsNear(bank.Nodes[index].Position))
                    {
                        throw new NativeWorldPresentationDeferredException("Fresh car train loader has not executed its first presentation callback.");
                    }
                    continue;
                }
                if (!pose.Projection.Visible)
                {
                    continue;
                }
                NativeWorldPathAngles angles = pose.Angles ?? throw new NativeWorldPresentationDeferredException("Visible car train node lacks its original linked orientation words.");
                models.Add(new(assets.ReadScriptedStaticModel(bank.Shape(definition, index)), new(context.CanonicalModelAnchor(pose.Projection), angles)) { ObjectId = new(WorldRenderObjectKind.Rail, index, ReferenceEquals(bank, process.Alternate) ? 1 : 0) });
            }
        }
        return models;
    }
}
