using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState CreateTrainDismount(WorldState before, int stationIndex)
    {
        NativeTrainWorldRuntimeState train = before.NativeTrainRuntime ?? throw new InvalidOperationException("Native station dismount requires the current riding-train runtime.");
        ValidateTrainRide(train);
        NativeWorldTrainStation station = assets.ReadTrainStation(stationIndex);
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateTrainDismount(train, station, train.CameraGeometry.Pitch);
        //548AF0 writes the station pose, clears203FE50 and LOWORD203FE54,
        //selects the existing controlled foot object, and releases203FD5C.
        //The camera target/velocity and ground are retained from the same callback.
        return before with
        {
            X = station.X,
            Z = station.RawZ,
            NativeHeight = station.NativeHeight,
            BodyYaw = 0,
            NativeFootRuntime = foot,
            NativeTrainRuntime = null
        };
    }

    private void ValidateStationTrainResources(NativeFootWorldRuntimeState foot)
    {
        foot.RequireNativeGroundState();
        NativeTrainWorldRuntimeState trains = foot.StationTrains ?? throw new InvalidDataException("Native station foot state lost its existing train process.");
        NativeWorldTrainDismountReceipt receipt = foot.TrainDismount ?? throw new InvalidDataException("Native station foot state lost its executed dismount receipt.");
        ValidateTrainRide(trains);
        NativeWorldTrainStation actual = assets.ReadTrainStation(receipt.Station.Index);
        if (actual.SectionHash != receipt.Station.SectionHash || !actual.Bytes.AsSpan().SequenceEqual(receipt.Station.Bytes.AsSpan()))
        {
            throw new InvalidDataException("Native station foot receipt differs from its original station resource.");
        }
        assets.ValidateNativeGroundCache(foot.GroundCache!, replacements);
        ValidateStationTrainProcess(foot);
    }

    private void ValidateStationTrainProcess(NativeFootWorldRuntimeState foot)
    {
        foot.RequireNativeGroundState();
        NativeStationTrainProcess process = foot.StationTrainProcess ?? throw new InvalidDataException("Native station foot state lost its continuing train banks.");
        NativeTrainWorldRuntimeState source = foot.StationTrains!;
        process.Validate(source);
        foreach (NativeTrainRailBank bank in new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>())
        {
            bank.Validate(assets.ReadScriptedPath(bank.TrackIndex));
            bank.ValidateProjectionPhase(process.Callback, (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0);
        }
    }

    private NativeFootWorldRuntimeState StepStationTrains(WorldState cameraState)
    {
        NativeFootWorldRuntimeState foot = cameraState.NativeFootRuntime ?? throw new InvalidDataException("Native background trains require current foot authority.");
        ValidateStationTrainProcess(foot);
        NativeStationTrainProcess process = foot.StationTrainProcess!;
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(cameraState);
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        long callback = checked(process.Callback + 1);
        bool stopAtEndpoint = (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0;
        int graphicsZ = -cameraState.Z & 2047;
        NativeWorldPathWords player = new(unchecked((short)(cameraState.X & 2047)), unchecked((short)cameraState.NativeHeight), unchecked((short)(graphicsZ == 0 ? 0 : graphicsZ - 2048)));

        NativeTrainRailBank Advance(NativeTrainRailBank bank)
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            return bank.Step(definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint, out _, next =>
            {
                if (next.Pause == 0 && WorldStationContactCertificate.EvaluateTrain(assets, next, definition, player, cameraState.NativeHeight, context.IsNear) >= 0)
                {
                    return next.ResolveContact(bank, definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint);
                }
                return next;
            });
        }

        NativeTrainRailBank primary = Advance(process.Primary);
        NativeTrainRailBank? alternate = process.Alternate is null ? null : Advance(process.Alternate);
        return foot with { StationTrainProcess = new NativeStationTrainProcess(1, callback, primary, alternate, camera) };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadStationTrainModels(NativeFootWorldRuntimeState foot)
    {
        ValidateStationTrainProcess(foot);
        NativeStationTrainProcess process = foot.StationTrainProcess!;
        NativeTrainWorldRuntimeState source = foot.StationTrains!;
        NativeWorldPathProjectionCertificate context = process.ProjectionCamera is null ? NativeWorldPathProjectionCertificate.Create(source.Camera, source.BodyPosition, source.EntityType) : NativeWorldPathProjectionCertificate.CreateFoot(process.ProjectionCamera);
        List<NativeWorldStaticModelDraw> models = [];
        foreach (NativeTrainRailBank bank in new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>())
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            for (int index = 0; index < bank.Presentation.Length; index++)
            {
                NativeWorldPathNodePresentation pose = bank.Presentation[index];
                if (NativeTrainRailBank.Link(index) == 0 || pose.Projection is not { Visible: true } projection)
                {
                    continue;
                }
                NativeWorldPathAngles angles = pose.Angles ?? throw new NativeWorldPresentationDeferredException("Visible station train node has unknown physical orientation words.");
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(bank.Shape(definition, index)), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
            }
        }
        foreach (NativeTrainWorldObject item in source.Load.Allocation.Objects)
        {
            if (item.EntityType is 0 or 1)
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = context.ProjectNode(item.Position) ?? throw new NativeWorldPresentationDeferredException(context.UnsupportedReason ?? "Native station static allocation has unproved projection words.");
            if (!projection.Visible)
            {
                continue;
            }
            NativeWorldPathAngles angles = WorldStaticModelTransform.AllocationAngles(item);
            models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
        }
        return models;
    }

    private bool HasStationContact(NativeFootWorldRuntimeState foot, int x, int rawZ, int nativeHeight)
    {
        NativeStationTrainProcess process = foot.StationTrainProcess ?? throw new InvalidDataException("Native station contact lost its active train banks.");
        NativeWorldFootCameraState camera = process.ProjectionCamera ?? throw new InvalidDataException("Native station contact requires the executed current foot camera.");
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(x, nativeHeight, -rawZ));
        NativeTrainRailBank[] banks = new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>().ToArray();
        return WorldStationContactCertificate.Evaluate(assets, foot.StationTrains!.Load.Allocation, banks, player, nativeHeight, position => context.ProjectNode(position) ?? throw new NotSupportedException(context.UnsupportedReason), context.IsNear).Hit;
    }
}
