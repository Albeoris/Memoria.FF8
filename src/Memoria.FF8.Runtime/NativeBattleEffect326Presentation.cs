using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect326Presentation
{
    private static readonly ConditionalWeakTable<object, Dictionary<uint, (long Task, int Age)>> history = new();

    internal static void RecordedHistory(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs, uint row, int age)
    {
        if (Task(inputs, row) is not { } task)
        {
            return;
        }
        var rows = history.GetValue(frame.State.Storage.PresentationStorageIdentity, _ => []);
        if (!rows.TryGetValue(row, out var old) || old != (task.Generation, age))
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, row, 0);
            rows[row] = (task.Generation, age);
        }
    }

    internal static NativeBattlePilotVisualIdentity? Ribbon(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs, int ring, int count)
    {
        if (Task(inputs, 0x24F2180) is not { } task)
        {
            return null;
        }
        long generation = task.Generation;
        for (int index = 0; index < count; index++)
        {
            uint row = checked(0x24F2180u + (uint)(ring * 20));
            generation = unchecked(generation * 397 ^ NativeBattlePilotVisualLifetimes.Identity(326, frame.State.Storage, row, 0).Generation);
            ring = ring == 0 ? 9 : ring - 1;
        }
        return task with { ResourceGeneration = generation };
    }

    internal static NativeBattlePilotVisualIdentity? Task(NativeBattleEffect326Inputs inputs, uint source, int role = 0)
    {
        if (!inputs.CapturePresentation)
        {
            return null;
        }
        var identity = inputs.PresentationTask ?? throw new NotSupportedException("Effect326 capture requires its actual scheduled task owner.");
        return identity with { Primitive = unchecked(identity.Primitive * 397 + role), ResourceGeneration = source };
    }

    internal static NativeBattlePilotVisualIdentity? Row(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs, uint row, uint source)
    {
        if (Task(inputs, source) is not { } parent)
        {
            return null;
        }
        return NativeBattlePilotVisualLifetimes.Identity(326, frame.Memory.Arena ?? throw new NotSupportedException("Effect326 capture requires its actual particle arena."), row, 0) with { ResourceGeneration = unchecked(parent.Generation * 397 ^ source) };
    }

    internal static void Allocated(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs, uint row)
    {
        if (inputs.CapturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena ?? throw new NotSupportedException("Effect326 capture requires its actual particle arena."), row, 0);
        }
    }

    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { PilotGeometry: { } geometry } primitive)
            {
                return attempt;
            }
            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Effect345 capture requires its actual empty or four-lane native depth payload.")
            };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
        }).ToImmutableArray() };
    }
}
