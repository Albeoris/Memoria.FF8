using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicEnvironment36(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicEnvironment36Services services = new(_ => Process().Buffer80 ?? throw new NotSupportedException("Native cinematic environment capture requires its retained draw buffer."), (_, buffer) =>
        {
            NativeSoftwareDrawingEnvironmentBuffers environments = Process().Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native cinematic environment capture requires both retained drawing environments.");
            return buffer == 0 ? environments.Buffer0 : environments.Buffer1;
        }, Append);
        return (NativeBattleCinematicEnvironment36.Draw(frame, services), graphics);

        NativeBattlePrimitiveProcess Process() => RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic environment capture requires its retained primitive process.");

        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = OtBucket(unchecked(ordering + (uint)(4 * primitive.Bucket))) } } : attempt).ToImmutableArray() };
            NativeBattleEffectMemory memory = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
            NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic environment capture requires its current ordering table.");
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(output, 128);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, offset, primitiveFrame.Pending, batch);
            NativeEffectMemory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
            return current with { Arena = written.Memory.Arena!, Core = written.Memory.ScriptedEffects! };
        }
    }
}
