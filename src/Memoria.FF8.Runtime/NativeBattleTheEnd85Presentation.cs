using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleTheEnd85Presentation
{
    internal static NativeBattlePilotVisualIdentity NodeIdentity(NativeBattleTheEnd85Frame frame, NativeBattleModulePoolTask owner, uint node, uint model, int instance)
    {
        NativeBattlePilotVisualIdentity task = NativeBattlePilotVisualLifetimes.Identity(85, owner);
        NativeBattlePilotVisualIdentity row = NativeBattlePilotVisualLifetimes.Identity(85, frame.Memory.Arena!, node, 0);
        return task with { Primitive = unchecked((int)node * 65537 + instance), ResourceGeneration = unchecked(row.Generation * 397 ^ model) };
    }

    internal static NativeBattleGfGeometryBatch Screen(NativeBattleGfGeometryBatch batch, NativeBattlePilotVisualIdentity identity)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { } primitive)
            {
                return attempt;
            }
            int stride = (primitive.Words[1] >> 24 & 0x10) != 0 ? 3 : 1;
            System.Numerics.Vector3 Corner(int corner)
            {
                uint word = primitive.Words[2 + corner * stride];
                return new(unchecked((short)word), unchecked((short)(word >> 16)), 0);
            }
            var geometry = new NativeBattlePilotVisualGeometry(identity with { Primitive = unchecked(identity.Primitive * 397 + attempt.ByteOffsetRelative) }, Corner(0), Corner(1), Corner(2), Corner(3), 4, false) { ScreenSpace = true, ScreenScale = 8 };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry } };
        }).ToImmutableArray() };
    }

    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { PilotGeometry: { Identity.Effect: 85 } geometry } primitive)
            {
                return attempt;
            }
            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("The End capture requires its actual empty or four-lane native depth payload.")
            };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
        }).ToImmutableArray() };
    }
}
