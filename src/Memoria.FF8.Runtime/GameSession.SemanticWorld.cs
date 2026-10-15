namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void CommitSemanticWorldFieldEntry(GameState expectedDeparture, WorldState resourceEndpoint, FieldTransition transition, NativeSemanticWorldTravel proof, short mapLocationId, Action<GameState> validatePrepared)
    {
        ArgumentNullException.ThrowIfNull(expectedDeparture);
        ArgumentNullException.ThrowIfNull(resourceEndpoint);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(validatePrepared);
        proof.Validate();
        GameState departure = State;
        if (!ReferenceEquals(departure, expectedDeparture) || departure.Scene != SceneKind.WorldMap || departure.World is not WorldState live || departure.SavedWorld is null || departure.WorldControls is null || departure.WorldArrival is null)
        {
            throw new NotSupportedException("Semantic world entry requires its unchanged current world allocation and saved/control owners.");
        }
        byte entity = live.NativeFootRuntime?.EntityType ?? live.NativeCarRuntime?.EntityType ?? throw new NotSupportedException("Semantic world entry requires its current supported foot or car entity.");
        proof.ValidateReturn(checked((ushort)departure.Variables.GetValueOrDefault(256)), entity, new NativeSavedWorldPose(resourceEndpoint.X, resourceEndpoint.Z, checked((short)resourceEndpoint.NativeHeight), checked((ushort)resourceEndpoint.BodyYaw)));
        if (transition.FieldId != proof.TargetFieldId || resourceEndpoint.Frame != live.Frame || resourceEndpoint.BodyYaw != live.BodyYaw || !resourceEndpoint.Actions.SequenceEqual(live.Actions))
        {
            throw new InvalidDataException("Semantic travel must retain current clocks/actions/facing and use its current-resource destination.");
        }
        WorldState originalPose = resourceEndpoint with { X = live.X, Z = live.Z, NativeHeight = live.NativeHeight, GroundType = live.GroundType, NativeFootRuntime = live.NativeFootRuntime, NativeCarRuntime = live.NativeCarRuntime };
        if (originalPose != live || resourceEndpoint.NativeFootRuntime is { } foot && foot with { GroundCache = live.NativeFootRuntime?.GroundCache } != live.NativeFootRuntime || resourceEndpoint.NativeCarRuntime is { } car && car with { GroundCache = live.NativeCarRuntime?.GroundCache } != live.NativeCarRuntime)
        {
            throw new InvalidDataException("Semantic resource anchoring cannot synthesize motor, camera, animation or callback effects.");
        }
        Validate(departure with { World = resourceEndpoint });
        NativeSavedWorldState saved;
        if (entity is 0 or 1)
        {
            NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(resourceEndpoint, checked((ushort)departure.FieldId), proof.StoryWord, mapLocationId) with { Rule = NativeSemanticWorldTravel.ReturnRule, SemanticTravel = proof };
            saved = departure.SavedWorld.WithFootReturn(receipt);
        }
        else
        {
            NativeSavedWorldCarReturn receipt = NativeSavedWorldCarReturn.Create(resourceEndpoint, checked((ushort)departure.FieldId), proof.StoryWord, mapLocationId) with { Rule = NativeSemanticWorldTravel.ReturnRule, SemanticTravel = proof };
            saved = departure.SavedWorld.WithCarReturn(receipt);
        }
        NativeWorldConfiguration configuration = new(1, NativeSemanticWorldTravel.ReturnRule, entity, checked((ushort)departure.FieldId), resourceEndpoint.Frame, SemanticTravel: proof);
        GameState candidate = departure with { Scene = SceneKind.Field, FieldId = transition.FieldId, PreviousFieldId = departure.FieldId, FieldName = $"field_{transition.FieldId}", EntryPoint = 0, X = transition.X, Y = transition.Y, EventState = null, DepartingFieldState = departure.DepartingFieldState ?? departure.EventState, DialogueDraft = null, FieldArrival = transition, WorldArrival = null, World = null, DepartingWorldState = resourceEndpoint, DebugView = null, SavedWorld = saved, WorldControls = departure.WorldControls.WithNormalFieldLoad(), WorldConfiguration = configuration };
        Validate(candidate);
        validatePrepared(candidate);
        if (!ReferenceEquals(State, departure))
        {
            throw new InvalidOperationException("The world session changed during semantic destination preparation.");
        }
        ReplaySemanticTrigger trigger = new(1, ReplaySemanticTriggerKind.WorldFieldEntry, departure.FieldId, proof.WmsetHash, -1, -1, -1, -1, -1, proof.WmsetRow, ExecutableHash: proof.ExecutableHash);
        ReplayJournalEntry intent = new(ReplayEntryKind.SemanticTrigger, SceneKind.WorldMap, departure.FieldId, resourceEndpoint.Frame) { Value = transition.FieldId, SemanticTrigger = trigger, NativeSourceHash = proof.WmsetHash, ResultScene = SceneKind.Field, ResultFieldId = transition.FieldId };
        candidate = candidate with { ReplayJournal = candidate.ReplayJournal?.Append(intent) };
        Dialog = null;
        Change(candidate);
        if (candidate.ReplayJournal is not null)
        {
            ReplayJournalChanged?.Invoke(candidate.ReplayJournal);
        }
    }
}
