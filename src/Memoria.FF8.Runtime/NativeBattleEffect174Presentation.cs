namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect174Presentation
{
    internal static NativeBattleGeometryPrimitive PreserveNativeDepths(NativeBattleGeometryPrimitive primitive)
    {
        if (primitive.PilotGeometry is not { } geometry)
        {
            return primitive;
        }
        System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
        {
            0 => System.Numerics.Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new NotSupportedException("Effect174 capture requires its actual empty or four-lane native depth payload.")
        };
        return primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } };
    }

    internal static NativeBattlePilotVisualIdentity Task(NativeBattleEffect174Frame frame, uint address, uint source = 0, int role = 0)
    {
        return NativeBattlePilotVisualLifetimes.Identity(174, NativeBattleEffect174Controller.Read(frame, address), role) with { ResourceGeneration = source };
    }

    internal static NativeBattlePilotVisualIdentity Row(NativeBattleEffect174Frame frame, uint bank, int slot, uint task, uint source, int role = 0)
    {
        return NativeBattlePilotVisualLifetimes.Identity(174, frame.State.Storage, bank, slot, role) with { ResourceGeneration = unchecked(Task(frame, task).Generation * 397 ^ source) };
    }
}
