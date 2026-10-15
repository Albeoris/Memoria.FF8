using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>A canonical current-resource entrance anchor, not an executed movement or collision endpoint.</summary>
public sealed record SemanticWorldFieldAnchor(SemanticWorldFieldResolution Resolution, WorldFieldSelection Selection, int X, int RawZ, short NativeHeight, WorldGroundSelection Ground);

public sealed partial class WorldAssets
{
    public SemanticWorldFieldAnchor ResolveSemanticFieldAnchor(ushort targetFieldId, ushort storyWord, byte entityType, ushort replacementMask, int currentHeight)
    {
        SemanticWorldFieldResolution resolution = FieldEntries.ResolveSemanticEntry(targetFieldId, storyWord, entityType);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(replacementMask);
        SortedSet<int> segments = [];
        foreach (WorldFieldSelectionRegion region in resolution.Regions)
        {
            for (int x = region.MinimumX; x < region.MaximumXExclusive; x += SegmentUnits)
            {
                for (int rawZ = region.MinimumRawZ; rawZ < region.MaximumRawZExclusive; rawZ += SegmentUnits)
                {
                    segments.Add(SegmentAt(x, rawZ));
                    segments.Add(SegmentAt(region.MaximumXExclusive - 1, rawZ));
                    segments.Add(SegmentAt(x, region.MaximumRawZExclusive - 1));
                    segments.Add(SegmentAt(region.MaximumXExclusive - 1, region.MaximumRawZExclusive - 1));
                }
            }
        }
        foreach (int segment in segments)
        {
            foreach (WorldTriangle triangle in GetSegment(segment, replacements).Triangles.OrderBy(triangle => triangle.NativeBlockIndex).ThenBy(triangle => triangle.NativePolygonIndex))
            {
                uint flags = triangle.GroundType | (uint)triangle.TextureFlags << 8 | (uint)triangle.VertexFlags << 16;
                if (!triangle.FieldEntrance || !NativeWorldTerrain.CanTraverse(flags, entityType))
                {
                    continue;
                }
                int x = (int)Wrap((triangle.A.X + triangle.B.X + triangle.C.X) / 3, 262144);
                int rawZ = (int)Wrap((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3, 196608);
                if (!resolution.Regions.Any(region => region.Contains(x, rawZ)))
                {
                    continue;
                }
                WorldGroundSelection? ground = LookupNativeGround(x, rawZ, replacements, []);
                if (ground is null || !ground.Ground.Triangle.FieldEntrance)
                {
                    continue;
                }
                WorldTriangle selected = ground.Ground.Triangle;
                uint selectedFlags = selected.GroundType | (uint)selected.TextureFlags << 8 | (uint)selected.VertexFlags << 16;
                if (!NativeWorldTerrain.CanTraverse(selectedFlags, entityType))
                {
                    continue;
                }
                WorldFieldSelection? selection = FieldEntries.Select(new WorldFieldSelectionInput(x, rawZ, storyWord, entityType), selected.TextureFlags);
                if (selection?.Entry != resolution.Entry)
                {
                    continue;
                }
                short height = checked((short)NativeWorldTerrain.ResolveHeight(entityType, -(int)ground.Ground.Height, selected.GroundType, currentHeight).Height);
                return new SemanticWorldFieldAnchor(resolution, selection, x, rawZ, height, ground);
            }
        }
        throw new NotSupportedException($"The unique current packet to field{targetFieldId} has no supported canonical entrance polygon for story{storyWord}/entity{entityType}.");
    }
}
