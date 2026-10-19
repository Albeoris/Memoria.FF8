using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) ExecuteNativeCinematicCapture96(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicCapture96Services services = new(_ => Process().Buffer80 ?? throw new NotSupportedException("Native cinematic capture requires its retained draw buffer."), (_, buffer) =>
        {
            NativeSoftwareDrawingEnvironmentBuffers environments = Process().Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native cinematic capture requires its actual current draw environment.");
            return buffer == 0 ? environments.Buffer0 : environments.Buffer1;
        }, Append);
        frame = NativeBattleCinematicCapture96.Execute(frame, program, services);
        return (frame, graphics);

        NativeBattlePrimitiveProcess Process() => RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic capture requires its retained primitive process.");

        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            int bucket = OtBucket(ordering);
            batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
            NativeBattleEffectMemory memory = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
            NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic capture requires its current ordering table.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic capture requires its COMMON packet owner.");
            if (process.ByteCursor != output)
            {
                throw new InvalidDataException("Native cinematic capture output disagrees with its current COMMON packet cursor.");
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitiveFrame.Pending, batch);
            memory = memory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
            NativeEffectMemory = memory;
            return current with { Arena = memory.Arena!, Core = memory.ScriptedEffects! };
        }
    }
}
