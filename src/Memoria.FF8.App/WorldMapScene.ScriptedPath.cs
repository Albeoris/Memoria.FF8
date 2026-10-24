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
        ushort mask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte);
        IReadOnlyDictionary<int, int> selectedReplacements = WorldTerrainSelection.Replacements(mask);
        WorldGroundSelection ground = assets.LookupNativeGround(path.EntityPosition.X, -path.EntityPosition.Z, selectedReplacements, []) ?? throw new NotSupportedException("Scripted loader has no original initial ground polygon.");
        NativeWorldProcess process = session.State.WorldProcess ?? throw new NotSupportedException("Scripted particle allocation requires its executed native process counter.");
        var scripted = new NativeScriptedWorldRuntimeState(1, NativeScriptedWorldRuntimeState.RuntimeRule, path, load, null) { GroundCache = CreateFreshGroundCache(ground.Identity), Forest = NativeWorldForestState.Create(process.MainLoopCounter, process.CameraCooldown) };
        return scripted.ToWorldState() with { ReplacementMask = WorldTerrainSelection.ReplacementMask(inputs.ReplacementFlags, storyByte), EventUi = NativeWorldEventUi.Create().WithScriptMode(path.Mode) };
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

    private WorldRenderFrame PrepareScriptedFrame(WorldState state)
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
                models.Add(new NativeWorldStaticModelDraw(assets.ReadScriptedStaticModel(path.NodeShape(definition, index)), new NativeWorldStaticModelPose(context.CanonicalModelAnchor(projection), angles)) { ObjectId = new(WorldRenderObjectKind.Rail, index) });
            }
        }
        WorldCamera camera = WorldCamera.ScriptedPath(path.Camera, 2048);
        WorldSky sky = WorldTerrainSelection.SelectSky(assets.Skies, state.X, state.Z, storyByte);
        return new WorldRenderFrame(new Vector3(state.X, -state.NativeHeight, state.Z), state.BodyYaw * MathF.Tau / 4096, state.AnimationId, state.AnimationCounter >> 4, camera, sky, AvatarVisible: false, StaticModels: models, Forest: state.NativeScriptedRuntime.Forest);
    }

    private WorldState StepScripted(WorldState before, uint buttons, out WorldFieldSelection? fieldEntry, Func<NativeWorldPathPosition, NativeWorldPathCamera, NativeWorldPathPosition, NativeWorldPathNodeProjection?> projectNode)
    {
        ValidateScriptedState(before);
        NativeScriptedWorldRuntimeState scripted = before.NativeScriptedRuntime!;
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(unchecked((byte)scripted.Path.Packet.EntryIndex));
        NativeWorldScriptedPathState path = scripted.Path.Step(definition, projectNode);
        NativeWorldForestState? forest = scripted.Forest;
        if (path.EntityType is 65 or 66 && !scripted.Path.EndpointRequested)
        {
            if (forest is null)
            {
                throw new NotSupportedException("Scripted steam requires its executed loader particle pool; migrate this save explicitly.");
            }
            NativeWorldPathNodePresentation carriage = path.Presentation[1];
            NativeWorldPathNodeProjection projection = carriage.Projection ?? throw new NotSupportedException("Scripted steam requires its actual carriage position.");
            NativeWorldPathAngles angles = carriage.Angles ?? throw new NotSupportedException("Scripted steam requires its retained native carriage angles.");
            forest = forest.AdvanceScriptedSteam(path.EntityType, projection.Source, angles, assets.ForestTables.Random, assets.ForestTables.AllEffects);
        }
        NativeWorldGroundCacheState cache = scripted.GroundCache ?? throw new NotSupportedException("Scripted ground callback requires its executed loader cache; migrate this save explicitly.");
        if (!scripted.Path.EndpointRequested)
        {
            //557A90 leaves zero velocity for the scripted entities; modes1/2
            //bypass blocking contacts and XYZ publication, but still execute53BB90.
            int? group = (buttons & 0x8000) != 0 ? 0 : (buttons & 0x2000) != 0 ? 1 : null;
            WorldNativeGroundResult ground = assets.ResolveNativeGround(path.EntityPosition.X, -path.EntityPosition.Z, 0, 0, -unchecked((short)path.EntityPosition.Y), group, cache, replacements, path.EntityType);
            cache = ground.Cache;
        }
        NativeScriptedWorldRuntimeState next = scripted with { Path = path, GroundCache = cache, Forest = forest };
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
