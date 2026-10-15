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
}
