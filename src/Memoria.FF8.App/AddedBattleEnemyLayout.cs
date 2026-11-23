using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class AddedBattleEnemyLayout
{
    private const int LateralStep = 600;
    private const int RearStep = 750;
    private const int MinimumSearchMargin = 3600;
    private const int NormalGap = 300;
    private const int LargeModelSpan = 2400;

    internal readonly record struct Footprint(int HalfWidth, int HalfDepth, bool Large);
    private readonly record struct Occupant(NativeBattleVector Position, Footprint Size);

    internal static Dictionary<int, NativeBattleVector> Place(BattleDefinition definition, BattleEncounter encounter, ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources,
        Func<int, NativeBattleGraphicsResource> readEnemy, IReadOnlyDictionary<string, NativeBattleVector>? retainedPositions)
    {
        Dictionary<int, Footprint> sizes = [];
        var originals = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive && actor.SceneSlot is int slot && !sources.ContainsKey(slot))
            .Select(actor => new Occupant(BattleFormationPositions.AuthoredPosition(definition, encounter, actor.SceneSlot!.Value), Size(actor.ModelId))).ToArray();
        NativeBattleVector[] anchors = originals.Length > 0 ? originals.Select(enemy => enemy.Position).ToArray() : [new(0, 0, -1800)];
        int minX = anchors.Min(point => point.X);
        int maxX = anchors.Max(point => point.X);
        int centerX = (minX + maxX) / 2;
        int front = anchors.Max(point => point.Z);
        List<Occupant> occupied = [.. originals];
        Dictionary<int, NativeBattleVector> positions = [];
        var added = definition.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot is int slot && sources.ContainsKey(slot))
            .Select(actor => (Actor: actor, Slot: actor.SceneSlot!.Value, Size: Size(actor.ModelId)))
            .OrderByDescending(enemy => enemy.Actor.InitialActive).ThenByDescending(enemy => enemy.Size.Large)
            .ThenByDescending(enemy => (long)enemy.Size.HalfWidth * enemy.Size.HalfDepth).ThenBy(enemy => enemy.Slot).ToArray();
        // Leave room for the two widest or deepest bodies plus clearance and one grid step.
        Footprint[] footprints = [.. originals.Select(enemy => enemy.Size), .. added.Select(enemy => enemy.Size)];
        int lateralMargin = SearchMargin(footprints.Select(size => size.HalfWidth), LateralStep);
        int rearMargin = SearchMargin(footprints.Select(size => size.HalfDepth), RearStep);
        NativeBattleVector[] candidates = Candidates(centerX, front, minX - lateralMargin, maxX + lateralMargin, anchors.Min(point => point.Z) - rearMargin).ToArray();
        foreach (var enemy in added)
        {
            if (retainedPositions is not null && retainedPositions.TryGetValue(enemy.Actor.Id, out NativeBattleVector retained))
            {
                positions.Add(enemy.Slot, retained);
                occupied.Add(new(retained, enemy.Size));
            }
        }
        foreach (var enemy in added.Where(enemy => !positions.ContainsKey(enemy.Slot)))
        {
            NativeBattleVector position = candidates.MinBy(point => Score(point, enemy.Size, occupied, centerX, front));
            positions.Add(enemy.Slot, position);
            occupied.Add(new(position, enemy.Size));
        }
        return positions;

        Footprint Size(int model)
        {
            if (!sizes.TryGetValue(model, out Footprint size))
            {
                size = Measure(readEnemy(model));
                sizes.Add(model, size);
            }
            return size;
        }
    }

    private static int SearchMargin(IEnumerable<int> halfSpans, int step)
    {
        return Math.Max(MinimumSearchMargin, halfSpans.OrderDescending().Take(2).Sum() + 2 * NormalGap + step);
    }

    private static IEnumerable<NativeBattleVector> Candidates(int centerX, int front, int left, int right, int rear)
    {
        left = Math.Max(left, short.MinValue);
        right = Math.Min(right, short.MaxValue);
        rear = Math.Max(rear, short.MinValue);
        for (int z = front, row = 0; z >= rear; z -= RearStep, row++)
        {
            int x = centerX + row % 2 * (LateralStep / 2);
            x -= (x - left) / LateralStep * LateralStep;
            for (; x <= right; x += LateralStep)
            {
                yield return new((short)x, 0, (short)z);
            }
        }
    }

    private static (long Overlap, long GapOverlap, long Distance) Score(NativeBattleVector position, Footprint size, List<Occupant> occupied, int centerX, int front)
    {
        long overlap = 0;
        long gapOverlap = 0;
        foreach (Occupant other in occupied)
        {
            int x = size.HalfWidth + other.Size.HalfWidth - Math.Abs(position.X - other.Position.X);
            int z = size.HalfDepth + other.Size.HalfDepth - Math.Abs(position.Z - other.Position.Z);
            int gap = size.Large || other.Size.Large ? 2 * NormalGap : NormalGap;
            overlap += (long)Math.Max(0, x) * Math.Max(0, z);
            gapOverlap += (long)Math.Max(0, x + gap) * Math.Max(0, z + gap);
        }
        long lateral = position.X - centerX;
        long depth = front - position.Z;
        return (overlap, gapOverlap, lateral * lateral + 2 * depth * depth);
    }

    internal static Footprint Measure(NativeBattleGraphicsResource resource)
    {
        if (resource.Geometry is not { IsDefaultOrEmpty: false } meshes)
        {
            return new(0, 0, false);
        }
        NativeBattlePoseSource source = resource.PoseSource ?? throw new InvalidDataException("Enemy geometry requires its original skeleton and pose.");
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(source, source.Sample(0, 1, 0));
        int halfWidth = 0;
        int halfDepth = 0;
        int low = 0;
        int high = 0;
        foreach (NativeBattleRawVertex vertex in meshes.SelectMany(mesh => mesh.Vertices))
        {
            NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(bones[vertex.Bone], vertex.Position);
            halfWidth = Math.Max(halfWidth, Math.Abs(point.X));
            halfDepth = Math.Max(halfDepth, Math.Abs(point.Z));
            low = Math.Min(low, point.Y);
            high = Math.Max(high, point.Y);
        }
        // Symmetric bounds also cover entry poses facing the opposite direction.
        bool large = Math.Max(Math.Max(2 * halfWidth, 2 * halfDepth), high - low) >= LargeModelSpan;
        return new(halfWidth, halfDepth, large);
    }
}
