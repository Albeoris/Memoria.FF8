using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicClampedMesh22(NativeBattleWaterExecutionFrame frame, int secondFileLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(graphics.OriginalEffect!.Binding.Effect);
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, secondFileLength);
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native clamped mesh requires its retained graphics controls.");
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Native clamped mesh requires its retained lighting controls.");
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native clamped mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native clamped mesh requires its current arithmetic registers."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native clamped mesh requires its current camera projection.");
        NativeBattleScriptedMeshProjected projected = NativeBattleCinematicClampedMesh22.Project(frame, (current, selector) =>
        {
            int found = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, found));
        }, (current, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, selector).Selected, projection, controls, lighting, step);
        frame = projected.Frame;
        uint shape = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD8, 4));
        int extent = NativeBattleCinematicClampedMesh22.MeasureExtent(frame, shape, ReadStride);
        uint start = frame.ReadTemporaryDword(0x60);
        NativeBattleScriptedMeshSubmission submitted = NativeBattleCinematicClampedMesh22.Submit(projected, extent, ReadHandler, ReadStride, OtBucket);
        frame = submitted.Frame;
        memory = RequireWaterMemory() with
        {
            Arena = frame.Arena,
            ScriptedEffects = frame.Core,
            ScriptedLighting = submitted.Lighting,
            ScriptedColorRegisters = submitted.Registers,
            GeometryRegisters = submitted.Geometry ?? throw new NotSupportedException("Native clamped mesh did not publish its current geometry registers.")
        };
        if (!submitted.Packets.Attempts.IsEmpty)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native clamped mesh requires its current ordering table.");
            if ((ReadSlot(frame, 0x4C, 1)[0] & 1) != 0)
            {
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, ArenaOffset(start, 0), primitive.Pending, submitted.Packets);
                memory = written.Memory;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                NativeBattlePrimitiveProcess common = memory.PrimitiveProcess ?? throw new NotSupportedException("Native clamped mesh requires its current COMMON packet owner.");
                if (start != common.ByteCursor)
                {
                    throw new InvalidDataException("Native clamped mesh COMMON cursor disagrees with its current physical packet owner.");
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, submitted.Packets);
                memory = memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
        }
        NativeEffectMemory = memory;
        return (frame with { Arena = memory.Arena!, Core = memory.ScriptedEffects! }, graphics);

        byte ReadStride(int type)
        {
            if ((uint)type >= 20)
            {
                throw new NotSupportedException("Native clamped mesh primitive size leaves its selected static directory.");
            }
            return resources.ReadStatic(checked(profile.PrimitiveSizes + (uint)type), 1)[0];
        }

        uint ReadHandler(int type)
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.PrimitiveTable + 4u * (uint)type), 4));
            return NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "prim_table", type, selected);
        }
    }
}
