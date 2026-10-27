using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicScanlines37(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicScanlines37Services services = new((current, command, factor) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic scanlines require their retained color registers.");
            NativeBattleCinematicColorScaleResult scaled = NativeBattleCinematicColorScale.ScaleB66A20(command, factor, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = scaled.Registers };
            return new(current, scaled.Color);
        }, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (NativeBattleCinematicScanlines37.Draw(frame, services), graphics);
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicPointCloud27(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics)
    {
        using NativeBattleEffectArenaTransaction? arenaTransaction = frame.Arena.Transaction is null ? new(frame.Arena) : null;
        frame = frame with { Arena = arenaTransaction?.View ?? frame.Arena };
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic points require their current ordering table.");
        using NativeBattleLogicalOrderingTransaction? orderingTransaction = primitiveFrame.Pending.Transaction is null ? new(primitiveFrame.Pending) : null;
        if (orderingTransaction is not null)
        {
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = orderingTransaction.View } };
        }
        NativeBattleCommonPacketTransaction? commonTransaction = null;
        NativeBattleActionMatrix? matrix = null;
        NativeBattleCinematicPointCloud27Services services = new(current =>
        {
            matrix = NativeBattleCinematicAnchor.Prepare(current, NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength));
            return current;
        }, (current, address) =>
        {
            NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic points require their retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic points require their actual projection controls.");
            byte[] point = current.Arena.Read(ArenaOffset(address, 6), 6);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native cinematic point basis has not been prepared."), NativeBattleScriptedMeshPreparation.Vector(point), projection, registers.Dqa, registers.Dqb);
            return new(current, projected.Projected.Screen, projected.Projected.Depth >> 2);
        }, (current, output, ordering, batch) =>
        {
            if (arenaTransaction is not null && ReferenceEquals(current.Arena.Transaction, arenaTransaction))
            {
                current = current with { Arena = arenaTransaction.Freeze() };
            }
            NativeBattleEffectMemory previousMemory = RequireWaterMemory();
            bool common = !(output >= NativeBattleEffectNodeMemory.ArenaAddress && output - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length);
            NativeBattleEffectMemory appendMemory = previousMemory;
            if (common && previousMemory.PrimitiveProcess is { CommonTransaction: null } process)
            {
                commonTransaction ??= new(process);
                appendMemory = previousMemory with { PrimitiveProcess = process with { CommonTransaction = commonTransaction } };
            }
            if (common)
            {
                commonTransaction?.BeginCheckpoint();
            }
            if (orderingTransaction is not null)
            {
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = orderingTransaction.View } };
            }
            (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory) written;
            try
            {
                written = AppendNativeCinematicPacketsUnpublished(current, output, ordering, batch, graphics, appendMemory);
            }
            catch
            {
                if (common)
                {
                    commonTransaction?.RollbackCheckpoint();
                }
                throw;
            }
            if (common)
            {
                commonTransaction?.CommitCheckpoint();
            }
            NativeBattleEffectMemory acceptedMemory = written.Memory;
            if (commonTransaction is not null && common)
            {
                acceptedMemory = acceptedMemory with { PrimitiveProcess = commonTransaction.Freeze(acceptedMemory.PrimitiveProcess!) };
            }
            graphics = written.Graphics;
            if (orderingTransaction is not null)
            {
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = orderingTransaction.Freeze() } };
            }
            NativeEffectMemory = acceptedMemory;
            return written.Frame;
        });
        try
        {
            NativeBattleWaterExecutionFrame drawn = NativeBattleCinematicPointCloud27.Draw(frame, services);
            if (arenaTransaction is not null && ReferenceEquals(drawn.Arena.Transaction, arenaTransaction))
            {
                drawn = drawn with { Arena = arenaTransaction.Freeze() };
            }
            if (orderingTransaction is not null)
            {
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = orderingTransaction.Freeze() } };
            }
            return (drawn, graphics);
        }
        finally
        {
            try
            {
                if (commonTransaction is not null && NativeEffectMemory?.PrimitiveProcess is { } process && ReferenceEquals(process.CommonTransaction, commonTransaction))
                {
                    NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = commonTransaction.Freeze(process) };
                }
            }
            finally
            {
                commonTransaction?.Dispose();
            }
        }
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicColumns2D(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsState graphics)
    {
        frame = NativeBattleCinematicColumns2D.Draw(frame, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        return (frame, graphics);
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) AppendNativeCinematicPackets(NativeBattleWaterExecutionFrame current, uint output, uint? ordering, NativeBattleGfGeometryBatch batch, NativeBattleGraphicsState graphics, ushort flags20 = 0)
    {
        var written = AppendNativeCinematicPacketsUnpublished(current, output, ordering, batch, graphics, RequireWaterMemory(), flags20);
        NativeEffectMemory = written.Memory;
        return (written.Frame, written.Graphics);
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory) AppendNativeCinematicPacketsUnpublished(NativeBattleWaterExecutionFrame current, uint output, uint? ordering, NativeBattleGfGeometryBatch batch, NativeBattleGraphicsState graphics, NativeBattleEffectMemory inputMemory, ushort flags20 = 0)
    {
        if (ordering is uint tableAddress)
        {
            int bucket = OtBucket(tableAddress);
            batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
        }
        NativeBattleEffectMemory memory = inputMemory with { Arena = current.Arena, ScriptedEffects = current.Core };
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic packets require their current ordering table.");
        if (output >= NativeBattleEffectNodeMemory.ArenaAddress && output - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), primitiveFrame.Pending, batch, flags20);
            memory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        }
        else
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic packets require their current COMMON packet owner.");
            if (process.ByteCursor != output)
            {
                throw new InvalidDataException("Native cinematic output disagrees with its COMMON packet cursor.");
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitiveFrame.Pending, batch, flags20);
            memory = memory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        }
        return (current with { Arena = memory.Arena!, Core = memory.ScriptedEffects! }, graphics, memory);
    }
}
