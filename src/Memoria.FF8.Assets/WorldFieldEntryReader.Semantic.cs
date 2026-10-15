using System.Security.Cryptography;

namespace Memoria.FF8.Assets;

public readonly record struct WorldFieldSelectionRegion(int MinimumX, int MaximumXExclusive, int MinimumRawZ, int MaximumRawZExclusive)
{
    public bool Contains(int x, int rawZ)
    {
        return x >= MinimumX && x < MaximumXExclusive && rawZ >= MinimumRawZ && rawZ < MaximumRawZExclusive;
    }
}

/// <summary>Current resource eligibility regions; these do not certify movement or physical entrance contact.</summary>
public sealed record SemanticWorldFieldResolution(WorldFieldEntry Entry, IReadOnlyList<WorldFieldSelectionRegion> Regions, string WmsetSection7Sha256, string Wm2FieldSha256);

public sealed partial class WorldFieldEntryReader
{
    public SemanticWorldFieldResolution ResolveSemanticEntry(ushort targetFieldId, ushort storyWord, byte entityType)
    {
        if (!entries.Any(entry => entry.FieldId == targetFieldId))
        {
            throw new NotSupportedException($"No current wm2field packet targets field{targetFieldId}.");
        }
        int[] xBoundaries = SemanticAxisBoundaries(true);
        int[] zBoundaries = SemanticAxisBoundaries(false);
        Dictionary<int, List<WorldFieldSelectionRegion>> matches = [];
        for (int xIndex = 0; xIndex < xBoundaries.Length - 1; xIndex++)
        {
            for (int zIndex = 0; zIndex < zBoundaries.Length - 1; zIndex++)
            {
                int x = xBoundaries[xIndex];
                int rawZ = zBoundaries[zIndex];
                // Every spatial predicate is constant over this integer region. Bit8 here enables
                // resource selection only; the caller must independently find an actual entrance polygon.
                WorldFieldSelection? selection = Select(new WorldFieldSelectionInput(x, rawZ, storyWord, entityType), 8);
                if (selection?.Entry.FieldId != targetFieldId)
                {
                    continue;
                }
                if (!matches.TryGetValue(selection.Entry.Index, out List<WorldFieldSelectionRegion>? regions))
                {
                    regions = [];
                    matches.Add(selection.Entry.Index, regions);
                }
                regions.Add(new WorldFieldSelectionRegion(x, xBoundaries[xIndex + 1], rawZ, zBoundaries[zIndex + 1]));
            }
        }
        if (matches.Count != 1)
        {
            throw new NotSupportedException($"Semantic world entry to field{targetFieldId} requires one current eligible wm2field row; story{storyWord}/entity{entityType} has {matches.Count}.");
        }
        KeyValuePair<int, List<WorldFieldSelectionRegion>> match = matches.Single();
        return new SemanticWorldFieldResolution(entries[match.Key], match.Value.AsReadOnly(), Convert.ToHexString(SHA256.HashData(program)), Convert.ToHexString(SHA256.HashData(table)));
    }

    private int[] SemanticAxisBoundaries(bool xAxis)
    {
        int extent = xAxis ? 131072 : 98304;
        SortedSet<int> boundaries = [-extent, extent];
        int cellSize = 8192;
        List<int> localBoundaries = [];
        for (int position = firstInstruction; position < program.Length; position += 4)
        {
            ushort opcode = Word(position);
            ushort operand = Word(position + 2);
            if (opcode == 0xFF07)
            {
                cellSize = 2048;
            }
            if (opcode == (xAxis ? 0xFF0F : 0xFF10) && operand is > 0 and < 8192)
            {
                localBoundaries.Add(operand);
            }
            if (opcode == (xAxis ? 0xFF11 : 0xFF12) && operand < 8191)
            {
                localBoundaries.Add(operand + 1);
            }
        }
        for (int coordinate = -extent; coordinate < extent; coordinate += cellSize)
        {
            boundaries.Add(coordinate);
        }
        for (int segment = -extent; segment < extent; segment += 8192)
        {
            foreach (int local in localBoundaries)
            {
                boundaries.Add(segment + local);
            }
        }
        return boundaries.ToArray();
    }
}
