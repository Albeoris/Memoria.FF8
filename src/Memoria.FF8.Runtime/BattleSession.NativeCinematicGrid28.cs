using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGrid28(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics, uint? selectedHandler = null)
    {
        using NativeBattleEffectArenaTransaction? arenaTransaction = frame.Arena.Transaction is null ? new(frame.Arena) : null;
        frame = frame with { Arena = arenaTransaction?.View ?? frame.Arena };
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic grid requires its current ordering table.");
        using NativeBattleLogicalOrderingTransaction? orderingTransaction = primitiveFrame.Pending.Transaction is null ? new(primitiveFrame.Pending) : null;
        if (orderingTransaction is not null)
        {
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = orderingTransaction.View } };
        }
        NativeBattleActionMatrix? matrix = null;
        long referencedSlotBirth = 0;
        NativeBattleCommonPacketTransaction? commonTransaction = null;
        NativeBattleCinematicGrid28Services services = new((current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => new(current, NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture)), (current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int selected = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            if (graphics.OriginalEffect!.Binding.Effect == 219)
            {
                referencedSlotBirth = NativeBattleCinematicVisualIdentity.Identity(219, current, bank, selected).Generation;
            }
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, selected));
        }, current =>
        {
            NativeBattleCinematicScaledBasisResult prepared = NativeBattleCinematicScaledBasis.Prepare(current, resourceLength);
            current = prepared.Frame;
            matrix = prepared.Matrix;
            return current;
        }, (current, color) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic grid lighting requires its retained color registers.");
            NativeBattleScriptedMaterialStep prepared = NativeBattleScriptedMaterial.Prepare(current, registers, color);
            NativeEffectMemory = memory with { ScriptedLighting = prepared.Lighting, ScriptedColorRegisters = prepared.Registers };
            return prepared.Frame;
        }, (current, near, far, third) =>
        {
            int difference = far - near;
            if (difference == 0)
            {
                difference = 1;
            }
            int quotient = unchecked(far << 12) / difference;
            int coefficient = unchecked(-(unchecked(near * far) / difference) << 8);
            if (third != 0)
            {
                coefficient /= third;
            }
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic grid lighting requires its retained depth-cue controls.");
            NativeEffectMemory = memory with { ScriptedGraphics = registers with { Dqa = unchecked((short)coefficient), Dqb = unchecked(quotient << 12) } };
            return current;
        }, (current, color) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic grid lighting requires its canonical far-color owner.");
            NativeEffectMemory = memory with { PrimitiveProcess = process.PublishModelColor(color) };
            return current;
        }, (current, vertex, normal, material, screen, color, flags) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic grid projection requires its current depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic grid projection requires its current camera projection.");
            byte[] position = ReadNativeGridTemporary(current, vertex, 6);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix ?? throw new InvalidOperationException("Native cinematic grid basis has not been prepared."), NativeBattleScriptedMeshPreparation.Vector(position), projection, controls.Dqa, controls.Dqb);
            current = StoreNativeGridDword(current, screen, projected.Projected.Screen);
            current = StoreNativeGridDword(current, flags, projected.Projected.Flags);
            byte[] rawNormal = ReadNativeGridTemporary(current, normal, 6);
            int normalOffset = checked((int)(normal - current.TemporaryFrame.Address));
            byte[] normalBytes = new byte[8];
            bool[] normalFlags = new bool[8];
            current.TemporaryFrame.ReadWithKnownness(normalOffset, normalBytes, normalFlags);
            ImmutableArray<byte> packedNormal = normalBytes.ToImmutableArray();
            ImmutableArray<bool> normalKnown = normalFlags.ToImmutableArray();
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(ReadNativeGridTemporary(current, material, 4));
            NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Native cinematic grid projection requires its current lighting controls.");
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic grid projection requires its retained normal-color registers.");
            registers = registers with { NormalInput = packedNormal, NormalInputKnown = normalKnown, Material = command };
            NativeBattleModelRendererRegisters distant = memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Native cinematic grid projection requires its current far-color controls.");
            registers = NativeBattleScriptedNormalDepthColor.Transform45EE80(lighting, NativeBattleScriptedMeshPreparation.Vector(rawNormal), command, unchecked((short)projected.Cue), distant, registers);
            NativeEffectMemory = memory with { ScriptedColorRegisters = registers };
            current = StoreNativeGridDword(current, color, registers.Rgb[2] ?? throw new NotSupportedException("Native cinematic grid projection did not publish its normal color."));
            return new(current, projected.Projected.Depth >> 2) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix!, NativeBattleScriptedMeshPreparation.Vector(position)), projection) };
        }, (current, output, ordering, batch) =>
        {
            NativeBattleEffectMemory previousMemory = RequireWaterMemory();
            bool common = !(output >= NativeBattleEffectNodeMemory.ArenaAddress && output - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length);
            NativeBattleEffectMemory appendMemory = previousMemory;
            if (common && previousMemory.PrimitiveProcess is { CommonTransaction: null } process)
            {
                commonTransaction ??= new(process);
                appendMemory = previousMemory with { PrimitiveProcess = process with { CommonTransaction = commonTransaction } };
            }
            arenaTransaction?.BeginCheckpoint();
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
                ushort flags = selectedHandler is 0xAAB290 or 0xAAB6D0 ? (ushort)1 : (ushort)0;
                written = AppendNativeCinematicPacketsUnpublished(current, output, ordering, batch, graphics, appendMemory, flags);
            }
            catch
            {
                arenaTransaction?.RollbackCheckpoint();
                if (common)
                {
                    commonTransaction?.RollbackCheckpoint();
                }
                throw;
            }
            arenaTransaction?.CommitCheckpoint();
            if (common)
            {
                commonTransaction?.CommitCheckpoint();
            }
            NativeBattleEffectMemory acceptedMemory = written.Memory;
            if (arenaTransaction is not null)
            {
                acceptedMemory = acceptedMemory with { Arena = arenaTransaction.Freeze() };
            }
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
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        services = services with { PilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank)) };
        if (graphics.OriginalEffect!.Binding.Effect == 219)
        {
            services = services with { PilotResourceGeneration = () => referencedSlotBirth };
        }
        NativeBattleEffectArena? publishedArena = null;
        try
        {
            NativeBattleWaterExecutionFrame drawn = selectedHandler switch
            {
                null => NativeBattleCinematicGrid28.Draw(frame, services),
                0xAAB290 => NativeBattleApocalypse221Grid.Draw51(frame, services),
                0xAAB6D0 => NativeBattleApocalypse221Grid.Draw68(frame, services, resources.ReadStatic(0x186D4FC, 17).ToArray().ToImmutableArray()),
                0xAC2230 => NativeBattleCinematicGrid28.DrawEnemy67(frame, services),
                _ => throw new NotSupportedException("Native cinematic grid requires its authenticated selected spacing and UV directory.")
            };
            if (arenaTransaction is not null)
            {
                publishedArena = arenaTransaction.Freeze();
                drawn = drawn with { Arena = publishedArena };
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
                if (arenaTransaction is not null && NativeEffectMemory is { } memory && ReferenceEquals(memory.Arena?.Transaction, arenaTransaction))
                {
                    NativeEffectMemory = memory with { Arena = publishedArena ?? arenaTransaction.Freeze() };
                }
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

    private static byte[] ReadNativeGridTemporary(NativeBattleWaterExecutionFrame frame, uint address, int length)
        => frame.ReadTemporary(checked((int)(address - frame.TemporaryFrame.Address)), length);

    private static NativeBattleWaterExecutionFrame StoreNativeGridDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17C)
        {
            return frame.WriteTemporaryDword(checked((int)(address - frame.TemporaryFrame.Address)), value);
        }
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 4)
        {
            return frame with { Core = frame.Core.Write(address, bytes) };
        }
        return frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
    }
}
