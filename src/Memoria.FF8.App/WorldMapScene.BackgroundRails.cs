using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static NativeWorldPathProjectionCertificate RailProjection(NativeWorldFootCameraState camera, int projectionCurveThreshold)
    {
        return projectionCurveThreshold == 5216 ? NativeWorldPathProjectionCertificate.CreateFoot(camera) : NativeWorldPathProjectionCertificate.CreateRagnarok(camera, projectionCurveThreshold);
    }

    private NativeWorldBackgroundRailState CreateBackgroundRails(byte flags265, NativeWorldFootCameraState camera)
    {
        NativeTrainRailBank? primary = (flags265 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) : null;
        NativeTrainRailBank? alternate = (flags265 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1) : null;
        var state = new NativeWorldBackgroundRailState(1, flags265, 0, primary, alternate, camera, primary is null ? null : uint.MaxValue, alternate is null ? null : uint.MaxValue);
        state.Validate();
        return state;
    }

    private void ValidateBackgroundRails(WorldState world, NativeWorldBackgroundRailState rails, bool validateProximity = false)
    {
        rails.Validate();
        if (((rails.LoadFlags265 ^ WorldEntryVariables.Read(session.State).EventFlags265) & 1) != 0)
        {
            throw new InvalidDataException("Native background rails differ from their original allocation flag.");
        }
        ValidateOrdinaryRailPresentation(world, rails.Callback, rails.Primary, rails.Alternate, rails.Camera, rails.PrimaryDistanceSquared, rails.AlternateDistanceSquared, rails.DistancePosition, validateProximity, rails.ProjectionCurveThreshold);
    }

    private NativeWorldBackgroundRailState StepBackgroundRails(NativeWorldBackgroundRailState rails, NativeWorldFootCameraState camera, int selectedEntity, byte scriptMode = 0, int projectionCurveThreshold = 5216)
    {
        rails.Validate();
        long callback = checked(rails.Callback + 1);
        NativeTrainRailBank? primary = rails.Primary is null ? null : AdvanceOrdinaryRailBank(rails.Primary, rails.PrimaryDistanceSquared!.Value, callback, camera, selectedEntity, scriptMode, projectionCurveThreshold);
        NativeTrainRailBank? alternate = rails.Alternate is null ? null : AdvanceOrdinaryRailBank(rails.Alternate, rails.AlternateDistanceSquared!.Value, callback, camera, selectedEntity, scriptMode, projectionCurveThreshold);
        return rails with { Callback = callback, Primary = primary, Alternate = alternate, Camera = camera, ProjectionCurveThreshold = projectionCurveThreshold };
    }

    private NativeTrainRailBank AdvanceOrdinaryRailBank(NativeTrainRailBank bank, uint distance, long callback, NativeWorldFootCameraState camera, int selectedEntity, byte scriptMode, int projectionCurveThreshold = 5216)
    {
        NativeWorldPathProjectionCertificate context = RailProjection(camera, projectionCurveThreshold);
        NativeWorldPathWords player = context.RelativeWords(camera.Origin);
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(selectedEntity));
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
        bool stopAtEndpoint = (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0;
        return bank.Step(definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint, out _, next =>
        {
            //549B40 suppresses riding modes1/2 and UI13, independently of the selected body.
            if (scriptMode is not (1 or 2 or 13) && next.Pause == 0 && WorldStationContactCertificate.EvaluateTrain(assets, next, definition, player, camera.NativeHeight, context.IsNear, probeRadius: NativeWorldTerrain.ProbeRadius(selectedEntity)) >= 0)
            {
                return next.ResolveContact(bank, definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint);
            }
            return next;
        }, NativeNormalFootObjectProcess.SelectSpeed(bank, distance, WorldStationContactCertificate.ContactRadius(selected)));
    }

    private static NativeWorldBackgroundRailState CompleteBackgroundRails(NativeWorldBackgroundRailState rails, int x, int rawZ, int height)
    {
        NativeWorldPathProjectionCertificate context = RailProjection(rails.Camera, rails.ProjectionCurveThreshold);
        NativeWorldPathPosition producer = new(x, height, -rawZ);
        NativeWorldPathWords player = context.RelativeWords(producer);
        return rails with { DistancePosition = producer, PrimaryDistanceSquared = rails.Primary is null ? null : NormalTrainDistance(rails.Primary, player, context), AlternateDistanceSquared = rails.Alternate is null ? null : NormalTrainDistance(rails.Alternate, player, context) };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadBackgroundRailModels(NativeWorldBackgroundRailState rails)
    {
        return ReadOrdinaryRailModels(rails.Primary, rails.Alternate, rails.Camera, rails.ProjectionCurveThreshold);
    }

    private void ValidateOrdinaryRailPresentation(WorldState world, long callback, NativeTrainRailBank? primary, NativeTrainRailBank? alternate, NativeWorldFootCameraState camera, uint? primaryDistance, uint? alternateDistance, NativeWorldPathPosition? distanceProducer, bool validateProximity, int projectionCurveThreshold = 5216)
    {
        bool invalidCallback = validateProximity ? callback != world.Frame : callback < world.Frame || callback > world.Frame + 1;
        if (invalidCallback)
        {
            throw new InvalidDataException("Native car object process differs from its actual callback clock.");
        }
        NativeWorldPathProjectionCertificate context = RailProjection(camera, projectionCurveThreshold);
        bool stopAtEndpoint = (WorldEntryVariables.Read(session.State).EventFlags265 & 64) != 0;
        foreach (NativeTrainRailBank bank in new[] { primary, alternate }.OfType<NativeTrainRailBank>())
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            bank.Validate(definition);
            bank.ValidateProjectionPhase(callback, stopAtEndpoint);
            if (callback == 0)
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
                if (pose.Orientation is not null && (pose.Orientation.Callback > callback || pose.Angles != NativeWorldScriptedPathMath.LinkedOrientation(pose.Orientation.Middle.Words, pose.Orientation.Linked.Words)))
                {
                    throw new InvalidDataException("Native car rail orientation differs from its retained linked projection words.");
                }
            }
        }
        if (validateProximity && callback > 0)
        {
            NativeWorldPathPosition distancePosition = distanceProducer ?? throw new InvalidDataException("Native car rail process lost its preceding selected-body distance origin.");
            NativeWorldPathWords player = context.RelativeWords(distancePosition);
            if (primary is not null && primaryDistance != NormalTrainDistance(primary, player, context) || alternate is not null && alternateDistance != NormalTrainDistance(alternate, player, context))
            {
                throw new InvalidDataException("Native car train speed lost its preceding54E650 distance-word producer.");
            }
        }
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadOrdinaryRailModels(NativeTrainRailBank? primary, NativeTrainRailBank? alternate, NativeWorldFootCameraState camera, int projectionCurveThreshold = 5216)
    {
        NativeWorldPathProjectionCertificate context = RailProjection(camera, projectionCurveThreshold);
        List<NativeWorldStaticModelDraw> models = [];
        foreach (NativeTrainRailBank bank in new[] { primary, alternate }.OfType<NativeTrainRailBank>())
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
                models.Add(new(assets.ReadScriptedStaticModel(bank.Shape(definition, index)), new(context.CanonicalModelAnchor(pose.Projection), angles)) { ObjectId = new(WorldRenderObjectKind.Rail, index, ReferenceEquals(bank, alternate) ? 1 : 0) });
            }
        }
        return models;
    }
}
