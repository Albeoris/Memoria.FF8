using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private static bool IsSupportedBoatPacket(WorldTransition packet) => unchecked((byte)packet.Mode) == 66 && unchecked((byte)packet.EntryIndex) == 11 && unchecked((byte)packet.Extra) == 61;

    private WorldState CreateBoatState(WorldTransition arrival, WorldEntryVariables inputs)
    {
        NativeSavedWorldState saved = session.State.SavedWorld ?? throw new NotSupportedException("Boat entry has no authentic saved-world state.");
        WorldState previous = session.State.DepartingWorldState ?? throw new NotSupportedException("Boat entry has no preceding native world globals.");
        RequireUnaliasedFootAuxiliary(session.State);
        NativeBoatWorldFootRecord foot = assets.ReadScriptedBoatFootRecord(inputs.StoryWord);
        NativeBoatWorldLoadReceipt load = NativeBoatWorldLoadReceipt.Create(saved, previous, foot);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(unchecked((byte)arrival.EntryIndex));
        NativeWorldScriptedPathState path = NativeWorldScriptedPathState.Create(definition, arrival, null);
        _ = assets.ReadScriptedStaticModel(path.ShapeId);
        var boat = new NativeBoatWorldRuntimeState(1, NativeBoatWorldRuntimeState.RuntimeRule, path, load, null);
        return boat.ToWorldState() with { ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte) };
    }

    private void ValidateBoatState(WorldState state)
    {
        NativeBoatWorldRuntimeState boat = state.NativeBoatRuntime ?? throw new NotSupportedException("Boat state has no executed native runtime authority.");
        boat.Validate();
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(unchecked((byte)boat.Path.Packet.EntryIndex));
        boat.Path.Validate(definition);
        NativeBoatWorldFootRecord actualFoot = assets.ReadScriptedBoatFootRecord(WorldEntryVariables.Read(session.State).StoryWord);
        if (boat.Load.FootRecord0 != actualFoot)
        {
            throw new InvalidDataException("Restored boat static foot allocation differs from the installed original resources.");
        }
        if (boat.Path.ModelProjection is NativeWorldPathNodeProjection projection)
        {
            NativeWorldPathProjectionCertificate.Create(projection.Camera, projection.Origin, boat.Path.EntityType).ValidateProjection(projection);
        }
        if (boat.Path.OrientationReceipt is NativeWorldPathOrientationReceipt orientation)
        {
            NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(orientation.Middle.Camera, orientation.Middle.Origin, boat.Path.EntityType);
            context.ValidateProjection(orientation.Middle);
            context.ValidateProjection(orientation.Linked);
        }
        _ = assets.ReadScriptedStaticModel(boat.Path.ShapeId);
    }

    private byte[] RenderBoat(WorldState state)
    {
        ValidateBoatState(state);
        NativeWorldScriptedPathState path = state.NativeBoatRuntime!.Path;
        if (path.Callback == 0)
        {
            throw new NativeWorldPresentationDeferredException("Original boat allocation has no model draw until its first actual world callback.");
        }
        NativeWorldPathNodeProjection projection = path.ModelProjection ?? throw new NativeWorldPresentationDeferredException("Native boat pixels await unique actual node projection authority.");
        NativeWorldStaticModelPose? pose = null;
        if (projection.Visible)
        {
            NativeWorldPathAngles angles = path.ModelAngles ?? throw new NativeWorldPresentationDeferredException("Visible native boat model has unproved linked-node angle words.");
            NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.Create(path.Camera, projection.Origin, path.EntityType);
            pose = new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles);
        }
        WorldCamera camera = WorldCamera.ScriptedBoat(path.Camera, 2048);
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        return renderer!.Render(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, replacements, sky, projection.Visible, assets.ReadScriptedStaticModel(path.ShapeId), staticModelPose: pose);
    }

    private WorldState StepBoat(WorldState before, out WorldFieldSelection? fieldEntry, out ReplayWorldNavigationStimulus inputProposal, ReplayWorldNavigationStimulus? proposal, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> projectNode)
    {
        ValidateBoatState(before);
        NativeBoatWorldRuntimeState boat = before.NativeBoatRuntime!;
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(unchecked((byte)boat.Path.Packet.EntryIndex));
        NativeWorldScriptedPathState path = boat.Path.Step(definition, projectNode);
        NativeBoatWorldRuntimeState next = boat with { Path = path };
        if (path.EndpointRequested && boat.Return is null)
        {
            WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
            short location = assets.GetNativeMapLocationId(path.EntityPosition.X, unchecked(-path.EntityPosition.Z));
            byte endpointRow = unchecked((byte)path.Packet.Extra);
            if (endpointRow >= assets.FieldEntries.Entries.Count)
            {
                throw new InvalidDataException("Original boat endpoint references an absent wm2field row.");
            }
            WorldFieldEntry entry = assets.FieldEntries.Entries[endpointRow];
            FieldTransition transition = new(entry.FieldId, entry.X, entry.Y, entry.Triangle, entry.Direction);
            next = next with { Return = NativeSavedWorldBoatReturn.Create(next, checked((ushort)session.State.FieldId), inputs.StoryWord, location, transition) };
        }
        WorldState candidate = next.ToWorldState(before);
        inputProposal = new ReplayWorldNavigationStimulus(candidate.X, candidate.Z);
        if (proposal is not null && proposal != inputProposal)
        {
            throw new InvalidDataException("Recorded boat callback proposal differs from the executed original rail; player movement cannot override scripted coordinates.");
        }
        fieldEntry = null;
        if (path.CompletedFieldRow is byte row)
        {
            if (row >= assets.FieldEntries.Entries.Count)
            {
                throw new InvalidDataException("Completed original boat packet references an absent wm2field row.");
            }
            fieldEntry = new WorldFieldSelection(-1, -1, -1, assets.FieldEntries.Entries[row], []);
        }
        return candidate;
    }
}
