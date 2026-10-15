using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldState InitializeNormalFootObjects(WorldState world, WorldEntryVariables inputs)
    {
        if (inputs.StoryWord is < 290 or >= 348)
        {
            return world;
        }
        RequireUnaliasedFootAuxiliary(session.State);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        if (saved.Slot4.ShapeId is not (0 or 255) || saved.Slot5.ShapeId != 0 || inputs.ReplacementFlags != 0)
        {
            throw new NotSupportedException("Native normal foot allocation requires its actual inactive saved-vehicle slots and disabled auxiliary object pairs.");
        }
        NativeWorldControlState controls = session.State.WorldControls ?? throw new NotSupportedException("Native normal rail allocation requires the executed event BYTE103.");
        if ((controls.EventFlags103 & 1) != 0)
        {
            throw new NotSupportedException("Native ordinary foot with both train routes disabled requires its actual absent-bank allocation contract.");
        }
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(inputs.StoryWord, controls.EventFlags103);
        NativeTrainRailBank primary = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1);
        NativeTrainRailBank? alternate = (controls.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1) : null;
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, primary, alternate, NativeWorldFootCameraState.Create(world), uint.MaxValue, alternate is null ? null : uint.MaxValue);
        process.Validate();
        return world with { NativeFootRuntime = world.NativeFootRuntime! with { NormalObjects = process } };
    }

    private void ValidateNormalFootObjects(WorldState world, bool validateProximity = false)
    {
        NativeFootWorldRuntimeState foot = world.NativeFootRuntime!;
        NativeNormalFootObjectProcess process = foot.NormalObjects ?? throw new InvalidDataException("Native normal foot world lost its executed dense object/rail allocation.");
        foot.RequireNativeGroundState();
        process.Validate();
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        if (saved.Slot4.ShapeId is not (0 or 255) || saved.Slot5.ShapeId != 0 || inputs.ReplacementFlags != 0)
        {
            throw new NotSupportedException("Native normal foot restoration requires the represented inactive saved vehicles and auxiliary object allocation.");
        }
        NativeTrainWorldAllocation actual = assets.ReadTrainWorldAllocation(inputs.StoryWord, process.Allocation.EventFlags103);
        bool invalidCallback = validateProximity ? process.Callback != world.Frame : process.Callback < world.Frame || process.Callback > world.Frame + 1;
        if (invalidCallback || actual.Story != process.Allocation.Story || actual.ProgramHash != process.Allocation.ProgramHash || actual.CoordinateHash != process.Allocation.CoordinateHash || !actual.ClauseWords.SequenceEqual(process.Allocation.ClauseWords) || actual.Objects.Length != process.Allocation.Objects.Length || actual.Objects.Where((item, index) => !item.CoordinateBytes.SequenceEqual(process.Allocation.Objects[index].CoordinateBytes)).Any())
        {
            throw new InvalidDataException("Native normal foot process differs from its actual original allocation, coordinates or callback clock.");
        }
        assets.ValidateNativeGroundCache(foot.GroundCache!, replacements);
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        foreach (NativeTrainRailBank bank in new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>())
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            bank.Validate(definition);
            if (process.Callback == 0)
            {
                NativeTrainRailBank initial = NativeTrainRailBank.Create(definition, 1);
                if (!bank.Nodes.SequenceEqual(initial.Nodes) || bank.Presentation.Any(pose => pose.Projection is not null || pose.Orientation is not null || pose.Angles is not null))
                {
                    throw new InvalidDataException("Native normal foot initial rail body acquired unexecuted interpolation or presentation words.");
                }
            }
            else
            {
                foreach (NativeWorldPathNodePresentation pose in bank.Presentation)
                {
                    NativeWorldPathNodeProjection projection = pose.Projection ?? throw new InvalidDataException("Native normal rail callback lost its executed5470E0 projection.");
                    context.ValidateProjection(projection);
                    if (pose.Orientation is not null && (pose.Orientation.Callback > process.Callback || pose.Angles != NativeWorldScriptedPathMath.LinkedOrientation(pose.Orientation.Middle.Words, pose.Orientation.Linked.Words)))
                    {
                        throw new InvalidDataException("Native normal train orientation differs from its actual linked projection words.");
                    }
                }
            }
        }
        if (validateProximity && process.Callback > 0)
        {
            NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(world.X, world.NativeHeight, -world.Z));
            if (process.PrimaryDistanceSquared != NormalTrainDistance(process.Primary, player, context) || process.Alternate is not null && process.AlternateDistanceSquared != NormalTrainDistance(process.Alternate, player, context))
            {
                throw new InvalidDataException("Native normal train speed lost the preceding54E650 distance-word producer.");
            }
        }
    }

    private NativeFootWorldRuntimeState StepNormalFootObjects(WorldState cameraState)
    {
        NativeFootWorldRuntimeState foot = cameraState.NativeFootRuntime!;
        NativeNormalFootObjectProcess process = foot.NormalObjects!;
        process.Validate();
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(cameraState);
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        long callback = checked(process.Callback + 1);
        NativeWorldControlState controls = session.State.WorldControls ?? throw new NotSupportedException("Native normal train endpoint needs the current BYTE103.");
        bool stopAtEndpoint = (controls.EventFlags103 & 64) != 0;
        NativeWorldPathWords player = context.RelativeWords(camera.Origin);
        NativeTrainRailBank Advance(NativeTrainRailBank bank, uint distance)
        {
            NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(bank.TrackIndex);
            return bank.Step(definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint, out _, next =>
            {
                if (next.Pause == 0 && WorldStationContactCertificate.EvaluateTrain(assets, next, definition, player, cameraState.NativeHeight, context.IsNear) >= 0)
                {
                    return next.ResolveContact(bank, definition, callback, context.Camera, camera.Origin, (node, _, _) => context.ProjectNode(node), stopAtEndpoint);
                }
                return next;
            }, NativeNormalFootObjectProcess.SelectSpeed(bank, distance, 48));
        }
        NativeTrainRailBank primary = Advance(process.Primary, process.PrimaryDistanceSquared);
        NativeTrainRailBank? alternate = process.Alternate is null ? null : Advance(process.Alternate, process.AlternateDistanceSquared!.Value);
        return foot with { NormalObjects = process with { Callback = callback, Primary = primary, Alternate = alternate, Camera = camera } };
    }

    private static uint NormalTrainDistance(NativeTrainRailBank bank, NativeWorldPathWords player, NativeWorldPathProjectionCertificate context)
    {
        uint minimum = uint.MaxValue;
        foreach (NativeWorldPathNodePresentation pose in bank.Presentation)
        {
            NativeWorldPathNodeProjection projection = pose.Projection ?? throw new InvalidDataException("Native54E650 requires its executed node WORD16/18/20 and proximity byte48.");
            if (!context.IsNear(projection.Source))
            {
                continue;
            }
            short dx = unchecked((short)(player.X - projection.Words.X));
            short dy = unchecked((short)(player.Y - projection.Words.Y));
            short dz = unchecked((short)(player.Z - projection.Words.Z));
            uint squared = unchecked((uint)(dx * dx + dy * dy + dz * dz));
            minimum = Math.Min(minimum, squared);
        }
        return minimum;
    }

    private static NativeFootWorldRuntimeState CompleteNormalFootObjectCallback(NativeFootWorldRuntimeState foot, int x, int rawZ, int height)
    {
        NativeNormalFootObjectProcess process = foot.NormalObjects!;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(x, height, -rawZ));
        return foot with { NormalObjects = process with { PrimaryDistanceSquared = NormalTrainDistance(process.Primary, player, context), AlternateDistanceSquared = process.Alternate is null ? null : NormalTrainDistance(process.Alternate, player, context) } };
    }

    private bool HasNormalFootObjectContact(NativeFootWorldRuntimeState foot, int x, int rawZ, int height)
    {
        NativeNormalFootObjectProcess process = foot.NormalObjects!;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(x, height, -rawZ));
        NativeTrainRailBank[] banks = new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>().ToArray();
        return WorldStationContactCertificate.Evaluate(assets, process.Allocation, banks, player, height, node => context.ProjectNode(node) ?? throw new NotSupportedException(context.UnsupportedReason), context.IsNear).Hit;
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadNormalFootObjectModels(WorldState world)
    {
        ValidateNormalFootObjects(world, true);
        NativeNormalFootObjectProcess process = world.NativeFootRuntime!.NormalObjects!;
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        List<NativeWorldStaticModelDraw> models = [];
        foreach (NativeTrainRailBank bank in new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>())
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
                        throw new NativeWorldPresentationDeferredException("Fresh ordinary train loader has not executed its first5499D0 presentation callback.");
                    }
                    continue;
                }
                if (!pose.Projection.Visible)
                {
                    continue;
                }
                NativeWorldPathAngles angles = pose.Angles ?? throw new NativeWorldPresentationDeferredException("Visible normal train node lacks its original linked orientation words.");
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(bank.Shape(definition, index)), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(pose.Projection), angles)));
            }
        }
        foreach (NativeTrainWorldObject item in process.Allocation.Objects)
        {
            if (item.EntityType is 0 or 1 || !context.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = context.ProjectNode(item.Position) ?? throw new NativeWorldPresentationDeferredException(context.UnsupportedReason ?? "Native normal static allocation projection is unsupported.");
            if (!projection.Visible)
            {
                continue;
            }
            NativeWorldPathAngles angles = WorldStaticModelTransform.AllocationAngles(item);
            models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(item.ShapeId), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
        }
        return models;
    }
}
