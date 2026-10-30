using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleWall189Presentation
{
    private static readonly ConditionalWeakTable<object, Dictionary<(long Task, uint Role, int Row), object>> births = new();

    internal static void Allocated(NativeBattleModulePoolTask task, uint role, int row)
    {
        var identity = NativeBattlePilotVisualLifetimes.Identity(189, task);
        births.GetValue(task.RequireOwner(), _ => [])[(identity.Generation, role, row)] = new object();
    }

    internal static NativeBattlePilotVisualIdentity Row(NativeBattleModulePoolTask task, uint role, int row, uint source)
    {
        var identity = NativeBattlePilotVisualLifetimes.Identity(189, task);
        Dictionary<(long Task, uint Role, int Row), object> rows = births.GetValue(task.RequireOwner(), _ => []);
        var key = (identity.Generation, role, row);
        if (!rows.TryGetValue(key, out object? birth))
        {
            birth = new object();
            rows.Add(key, birth);
        }
        return identity with { Primitive = unchecked((int)role * 397 + row), ResourceGeneration = unchecked(NativeBattlePilotVisualLifetimes.EpochFor(birth) * 397 ^ source) };
    }

    internal static NativeBattlePilotVisualGeometry CaptureClear(ImmutableArray<uint> words, NativeBattlePilotVisualIdentity identity)
    {
        System.Numerics.Vector3 Corner(int corner)
        {
            uint word = words[2 + corner];
            return new(unchecked((short)word), unchecked((short)(word >> 16)), 0);
        }
        return new(identity with { Primitive = unchecked(identity.Primitive * 397 + 4), ResourceGeneration = 0xD1B490 }, Corner(0), Corner(1), Corner(2), Corner(3), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true };
    }

    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { PilotGeometry: { Identity.Effect: 189 } geometry } primitive)
            {
                return attempt;
            }
            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Wall capture requires its actual empty or four-lane native depth payload.")
            };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
        }).ToImmutableArray() };
    }
}
