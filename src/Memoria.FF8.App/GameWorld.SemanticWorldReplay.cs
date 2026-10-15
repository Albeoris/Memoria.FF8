using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private void CompleteSemanticWorldVisit(ReplayCompletedVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        GameState departure = session.State;
        if (visit.Scene != SceneKind.WorldMap || visit.Exit.Scene != SceneKind.Field || departure.Scene != SceneKind.WorldMap || departure.FieldId != visit.FieldId || departure.World is not WorldState current || worldScene is null)
        {
            throw new InvalidDataException("Semantic world travel must leave its actual current completed world visit.");
        }
        DrainSemanticAutonomousWorldProgress(visit.FieldId);
        departure = session.State;
        if (departure.Scene != SceneKind.WorldMap)
        {
            if (departure.Scene != visit.Exit.Scene || departure.FieldId != visit.Exit.FieldId)
            {
                throw new InvalidDataException("The current automatic world transport departed to a different semantic destination.");
            }
            return;
        }
        current = departure.World ?? throw new InvalidDataException("Automatic world progress lost its current world owner.");
        if (worldScene.CurrentDialogue is not null)
        {
            throw new NotSupportedException("Automatic world transport requires its retained current external station decision.");
        }
        WorldEntryVariables inputs = WorldEntryVariables.Read(departure);
        byte entityType = current.NativeFootRuntime?.EntityType ?? current.NativeCarRuntime?.EntityType ?? throw new NotSupportedException("Semantic travel requires its current supported foot or car allocation.");
        if (entityType is not (0 or 1 or 32 or 33))
        {
            throw new NotSupportedException("Semantic world travel has no current-resource return contract for this entity.");
        }
        using WorldAssets worldAssets = new(assets.GameDirectory);
        SemanticWorldFieldAnchor anchor = worldAssets.ResolveSemanticFieldAnchor(checked((ushort)visit.Exit.FieldId), inputs.StoryWord, entityType, current.ReplacementMask, current.NativeHeight);
        WorldFieldEntry entry = anchor.Selection.Entry;
        FieldAssets destination = assets.LoadField(entry.FieldId);
        if (destination.Information is null || entry.Triangle >= destination.Walkmesh.Triangles.Count || entry.X == short.MaxValue || FieldPartyTraceGeometry.ResolveTriangleHeight(destination.Walkmesh, entry.Triangle, entry.X, entry.Y) is null)
        {
            throw new NotSupportedException("Semantic world entry requires the current explicit destination triangle packet and field INF.");
        }
        FieldTransition transition = new(entry.FieldId, entry.X, entry.Y, entry.Triangle, entry.Direction);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(anchor.Ground.Identity, null);
        WorldState endpoint = current with
        {
            X = anchor.X,
            Z = anchor.RawZ,
            NativeHeight = anchor.NativeHeight,
            GroundType = anchor.Ground.Ground.Triangle.GroundType,
            NativeFootRuntime = current.NativeFootRuntime is { } foot ? foot with { GroundCache = cache } : null,
            NativeCarRuntime = current.NativeCarRuntime is { } car ? car with { GroundCache = cache } : null
        };
        NativeSemanticWorldTravel proof = new(1, entry.FieldId, inputs.StoryWord, entityType, anchor.X, anchor.RawZ, anchor.NativeHeight, checked((ushort)current.BodyYaw), anchor.Selection.ClauseIndex, entry.Index, anchor.Resolution.WmsetSection7Sha256, anchor.Resolution.Wm2FieldSha256, assets.NativeExecutableHash ?? throw new NotSupportedException("Semantic world travel requires its authenticated current native executable."));
        session.CommitSemanticWorldFieldEntry(departure, endpoint, transition, proof, worldAssets.GetNativeMapLocationId(anchor.X, anchor.RawZ), candidate =>
        {
            if (candidate.FieldArrival != transition || candidate.PreviousFieldId != departure.FieldId || candidate.FieldId != destination.Id)
            {
                throw new InvalidDataException("The prepared semantic world entry changed its authenticated destination packet.");
            }
        });
    }

    private bool TryApplySemanticAutonomousWorldEffect(ReplayJournalEntry input)
    {
        if (input.Kind != ReplayEntryKind.PersistentEffect || input.Scene != SceneKind.WorldMap || input.Code != (int)ReplayPersistentEffectKind.WorldCallbackEffects || input.EffectArguments is null)
        {
            return false;
        }
        input.Validate();
        ReplayWorldEffectCause cause = ReplayWorldEffectCause.FromArguments(input.EffectArguments);
        if (cause.SampleSalaryAndDrawPoints || cause.FuelGroundType is not null || cause.EntityType is not (3 or >= 16 and <= 22 or 65 or 66))
        {
            return false;
        }
        if (cause.MovementUnits != 0)
        {
            throw new InvalidDataException("Automatic world effect causes cannot claim ordinary locomotion salary units.");
        }
        GameState state = session.State;
        if (state.Scene != SceneKind.WorldMap)
        {
            WorldState? previous = state.DepartingWorldState;
            int? departedType = (int?)previous?.NativeScriptedRuntime?.Path.EntityType ?? (previous?.NativeTrainRuntime is not null ? 3 : null);
            if (state.Scene != SceneKind.Field || state.PreviousFieldId != input.FieldId || departedType != cause.EntityType)
            {
                throw new InvalidDataException("Obsolete automatic world effects do not belong to the actual current transport departure.");
            }
            return true;
        }
        if (state.FieldId != input.FieldId || unchecked((byte)(state.WorldArrival?.Mode ?? -1)) != cause.EntityType)
        {
            throw new InvalidDataException("Automatic world effect causes identify a different current world allocation.");
        }
        // These receipts describe automatic progress, not a request to repeat an
        // old number of UI callbacks before running the transport a second time.
        DrainSemanticAutonomousWorldProgress(input.FieldId);
        return true;
    }

    private void DrainSemanticAutonomousWorldProgress(int fieldId)
    {
        const int maximumCallbacks = 30000;
        for (int callback = 0; callback < maximumCallbacks; callback++)
        {
            GameState before = session.State;
            if (before.Scene != SceneKind.WorldMap)
            {
                return;
            }
            if (before.FieldId != fieldId || before.World is not WorldState world || worldScene is null)
            {
                throw new InvalidDataException("Automatic world progress crossed an unexpected allocation.");
            }
            if (world.NativeScriptedRuntime is null && world.NativeTrainRuntime is null || worldScene.CurrentDialogue is not null)
            {
                return;
            }
            PlayerActionResult result = Apply(new PlayerAction(PlayerActionKind.Wait));
            if (!result.Accepted || result.FramesAdvanced != 1 || session.State.Scene == SceneKind.WorldMap && (session.State.World is not WorldState after || after.Frame <= world.Frame))
            {
                throw new NotSupportedException("The current automatic world transport did not advance through its ordinary callback.");
            }
        }
        throw new NotSupportedException("Automatic world transport exceeded the bounded current callback budget before its next decision or departure.");
    }
}
