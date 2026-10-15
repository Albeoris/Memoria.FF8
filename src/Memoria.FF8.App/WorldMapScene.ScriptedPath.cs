using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static bool IsSupportedScriptedPacket(WorldTransition packet) => unchecked((byte)packet.Mode) is >= 16 and <= 22 or 65 or 66 && unchecked((byte)packet.EntryIndex) < 14 && unchecked((sbyte)packet.Extra) >= 0;

    private WorldState CreateScriptedState(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld ?? throw new NotSupportedException("Scripted entry has no authentic saved-world state.");
        WorldState previous = session.State.DepartingWorldState ?? throw new NotSupportedException("Scripted entry has no preceding native world globals.");
        RequireUnaliasedFootAuxiliary(session.State);
        NativeScriptedWorldFootRecord foot = assets.ReadScriptedFootRecord(inputs.StoryWord, inputs.EventFlags265);
        NativeScriptedWorldLoadReceipt load = NativeScriptedWorldLoadReceipt.Create(saved, previous, foot);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(unchecked((byte)arrival.EntryIndex));
        if (unchecked((byte)arrival.Extra) >= assets.FieldEntries.Entries.Count)
        {
            throw new InvalidDataException("Original scripted entry references an absent wm2field endpoint row.");
        }
        NativeWorldScriptedPathState path = NativeWorldScriptedPathState.Create(definition, arrival, (inputs.EventFlags265 & 64) != 0);
        ValidateScriptedModels(path, definition);
        var scripted = new NativeScriptedWorldRuntimeState(1, NativeScriptedWorldRuntimeState.RuntimeRule, path, load, null);
        return scripted.ToWorldState() with { ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte) };
    }

    private void ValidateScriptedState(WorldState state)
    {
        NativeScriptedWorldRuntimeState scripted = state.NativeScriptedRuntime ?? throw new NotSupportedException("Scripted state has no executed native runtime authority.");
        scripted.Validate();
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(unchecked((byte)scripted.Path.Packet.EntryIndex));
        scripted.Path.Validate(definition);
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        NativeScriptedWorldFootRecord actualFoot = assets.ReadScriptedFootRecord(inputs.StoryWord, inputs.EventFlags265);
        if (scripted.Load.FootRecord0 != actualFoot || scripted.Path.StopAtEndpoint != ((inputs.EventFlags265 & 64) != 0))
        {
            throw new InvalidDataException("Restored scripted static foot allocation differs from the installed original resources.");
        }
        foreach (NativeWorldPathNodePresentation pose in scripted.Path.Presentation)
        {
            if (pose.Projection is NativeWorldPathNodeProjection projection)
            {
                NativeWorldPathProjectionCertificate.Create(projection.Camera, projection.Origin, scripted.Path.EntityType).ValidateProjection(projection);
            }
            if (pose.Orientation is NativeWorldPathOrientationReceipt orientation)
            {
                NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(orientation.Middle.Camera, orientation.Middle.Origin, scripted.Path.EntityType);
                context.ValidateProjection(orientation.Middle);
                context.ValidateProjection(orientation.Linked);
            }
        }
        ValidateScriptedModels(scripted.Path, definition);
    }

    private void ValidateScriptedModels(NativeWorldScriptedPathState path, NativeWorldScriptedPathDefinition definition)
    {
        for (int index = 0; index < path.NodeCount; index++)
        {
            if (path.NodeLink(index) != 0)
            {
                _ = assets.ReadScriptedStaticModel(path.NodeShape(definition, index));
            }
        }
    }

    private byte[] RenderScripted(WorldState state)
    {
        ValidateScriptedState(state);
        NativeWorldScriptedPathState path = state.NativeScriptedRuntime!.Path;
        if (path.Callback == 0)
        {
            throw new NativeWorldPresentationDeferredException("Original scripted allocation has no model draw until its first actual world callback.");
        }
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(unchecked((byte)path.Packet.EntryIndex));
        List<NativeWorldStaticModelDraw> models = [];
        for (int index = 0; index < path.NodeCount; index++)
        {
            if (path.NodeLink(index) == 0)
            {
                continue;
            }
            NativeWorldPathNodePresentation presentation = path.Presentation[index];
            NativeWorldPathNodeProjection projection = presentation.Projection ?? throw new NativeWorldPresentationDeferredException("Native scripted pixels await actual node projection authority.");
            if (projection.Visible)
            {
                NativeWorldPathAngles angles = presentation.Angles ?? throw new NativeWorldPresentationDeferredException("Visible native scripted model has unproved linked-node angle words.");
                NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(path.Camera, projection.Origin, path.EntityType);
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(path.NodeShape(definition, index)), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)));
            }
        }
        WorldCamera camera = WorldCamera.ScriptedPath(path.Camera, 2048);
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        return renderer!.Render(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, replacements, sky, avatarVisible: false, staticModels: models);
    }

    private WorldState StepScripted(WorldState before, out WorldFieldSelection? fieldEntry, out ReplayWorldNavigationStimulus inputProposal, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> projectNode)
    {
        ValidateScriptedState(before);
        NativeScriptedWorldRuntimeState scripted = before.NativeScriptedRuntime!;
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(unchecked((byte)scripted.Path.Packet.EntryIndex));
        NativeWorldScriptedPathState path = scripted.Path.Step(definition, projectNode);
        NativeScriptedWorldRuntimeState next = scripted with { Path = path };
        if (path.EndpointRequested && scripted.Return is null)
        {
            WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
            short location = assets.GetNativeMapLocationId(path.EntityPosition.X, unchecked(-path.EntityPosition.Z));
            byte endpointRow = unchecked((byte)path.Packet.Extra);
            if (endpointRow >= assets.FieldEntries.Entries.Count)
            {
                throw new InvalidDataException("Original scripted endpoint references an absent wm2field row.");
            }
            WorldFieldEntry entry = assets.FieldEntries.Entries[endpointRow];
            FieldTransition transition = new(entry.FieldId, entry.X, entry.Y, entry.Triangle, entry.Direction);
            next = next with { Return = NativeSavedWorldScriptedReturn.Create(next, checked((ushort)session.State.FieldId), inputs.StoryWord, location, transition) };
        }
        WorldState candidate = next.ToWorldState(before);
        inputProposal = new ReplayWorldNavigationStimulus(candidate.X, candidate.Z);
        fieldEntry = null;
        if (path.CompletedFieldRow is byte row)
        {
            if (row >= assets.FieldEntries.Entries.Count)
            {
                throw new InvalidDataException("Completed original scripted packet references an absent wm2field row.");
            }
            fieldEntry = new WorldFieldSelection(-1, -1, -1, assets.FieldEntries.Entries[row], []);
        }
        return candidate;
    }
}
