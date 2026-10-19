using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicRectangles(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram? program, int secondFileLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics, byte kind)
    {
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, secondFileLength);
        int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(graphics.OriginalEffect!.Binding.Effect);
        NativeBattleWaterParticleServices services = new((_, address, length) => resources.ReadStatic(address, length).ToArray(), (current, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, index, selector), (current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture), DrawRectangles)
        {
            RequireHandler = (table, slot) =>
            {
                uint address = table switch
                {
                    "pop_table" when (uint)slot < 24 => profile.ParticleInstructionTable,
                    "ptype_table" when (uint)slot < 5 => profile.PlacementTable,
                    _ => throw new NotSupportedException("Native cinematic particle dispatch leaves its selected static table.")
                };
                NativeBattleCinematicDispatch.RequireSlot(profile.Effect, table, slot, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(address + 4u * (uint)slot), 4)));
            }
        };
        frame = kind switch
        {
            5 => NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, services.ResolveMatrixRow, DrawRectangles),
            6 => NativeBattleWaterParticles.AdvanceType6(frame, program ?? throw new NotSupportedException("Native cinematic particles require their current programme."), bank, services),
            _ => throw new NotSupportedException("Native cinematic rectangles require their actual selected draw caller.")
        };
        return (frame, graphics);

        NativeBattleWaterExecutionFrame DrawRectangles(NativeBattleWaterExecutionFrame current, NativeBattleActionMatrix matrix)
        {
            if (kind == 5)
            {
                uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.SpriteTable + 9u * 4), 4));
                NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "spr_table", 9, selected);
            }
            NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic particles require their retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic particles require their current camera projection.");
            NativeBattleWaterParticleRectangleResult drawn = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, matrix, projection, registers.Zsf3 ?? throw new NotSupportedException("Native cinematic particles require their current ZSF3."), registers.Dqa, registers.Dqb, OtBucket);
            current = drawn.Frame;
            if (drawn.Packets.Attempts.IsEmpty)
            {
                return current;
            }
            NativeBattleEffectMemory memory = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic particles require their current ordering table.");
            uint cursor = CoreDword(current, CoreDword(current, 0x27973EC) + 0x7C);
            uint start = unchecked(cursor - (uint)drawn.Packets.AllocationBytes);
            if ((ReadSlot(current, 0x4C, 1)[0] & 1) != 0)
            {
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, ArenaOffset(start, 0), primitive.Pending, drawn.Packets);
                memory = written.Memory;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                NativeBattlePrimitiveProcess common = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic particles require their current COMMON packet owner.");
                if (start != common.ByteCursor)
                {
                    throw new InvalidDataException("Native cinematic particle COMMON cursor disagrees with its physical packet owner.");
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, drawn.Packets);
                memory = memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            NativeEffectMemory = memory;
            return current with { Arena = memory.Arena!, Core = memory.ScriptedEffects! };
        }
    }
}
