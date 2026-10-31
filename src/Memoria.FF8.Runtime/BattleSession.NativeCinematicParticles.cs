using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicRectangles(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram? program, int secondFileLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics, byte kind)
    {
        NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic particles require their current ordering table.");
        using NativeBattleLogicalOrderingTransaction? ordering = primitiveFrame.Pending.Transaction is null ? new(primitiveFrame.Pending) : null;
        if (ordering is not null)
        {
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = ordering.View } };
        }
        using NativeBattleWaterFrameBatch batch = new();
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, secondFileLength);
        int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(graphics.OriginalEffect!.Binding.Effect);
        NativeBattleWaterParticleServices services = new((_, address, length) => resources.ReadStatic(address, length).ToArray(), (current, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, index, selector), (current, texture) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, resources, texture), (current, texture) => NativeBattleWaterTextureMetadata.ReadClutB66640(current, resources, texture), DrawRectangles)
        {
            ReadRegisters = ReadRegisters,
            PublishRegisters = PublishRegisters,
            ExecuteSelected = (current, selectedProgram) => profile.Effect == 262 && (byte)current.ReadTemporaryWord(0x14A) == 22 ? NativeBattleGuestParticle22.Execute(current, selectedProgram) : null,
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
        frame = batch.Begin(frame);
        frame = kind switch
        {
            5 => NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, services.ResolveMatrixRow, DrawRectangles, ReadRegisters, PublishRegisters),
            6 => NativeBattleWaterParticles.AdvanceType6(frame, program ?? throw new NotSupportedException("Native cinematic particles require their current programme."), bank, services),
            _ => throw new NotSupportedException("Native cinematic rectangles require their actual selected draw caller.")
        };
        frame = batch.Publish(frame);
        if (ordering is not null)
        {
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.Freeze() } };
        }
        return (frame, graphics);

        NativeBattleWaterExecutionFrame DrawRectangles(NativeBattleWaterExecutionFrame current, NativeBattleActionMatrix matrix)
        {
            current = batch.Publish(current);
            return batch.Begin(DrawCanonicalRectangles(current, matrix));
        }

        NativeBattleWaterExecutionFrame DrawCanonicalRectangles(NativeBattleWaterExecutionFrame current, NativeBattleActionMatrix matrix)
        {
            if (kind == 5)
            {
                byte mode = ReadSlot(current, 0x1E, 1)[0];
                uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.SpriteTable + (uint)mode * 4), 4));
                NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "spr_table", mode, selected);
            }
            NativeBattleScriptedGraphicsMemory registers = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native cinematic particles require their retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native cinematic particles require their current camera projection.");
            NativeBattlePilotVisualIdentity identity;
            if (kind == 6)
            {
                uint particleBank = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(current, 0xB8, 4));
                uint particle = current.ReadTemporaryDword(0x44);
                int particleIndex = checked((int)((particle - particleBank - 0x10) / 0x50));
                identity = NativeBattlePilotVisualLifetimes.Identity(checked((ushort)profile.Effect), current.Arena, particleBank, particleIndex);
            }
            else
            {
                identity = NativeBattleCinematicVisualIdentity.Identity(checked((ushort)profile.Effect), current, bank, index);
            }
            NativeBattleWaterParticleRectangleResult drawn = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, ReadRegisters(current), projection, registers.Zsf3 ?? throw new NotSupportedException("Native cinematic particles require their current ZSF3."), registers.Dqa, registers.Dqb, OtBucket, matrix, identity);
            drawn = drawn with { Packets = NativeBattleEffect249Presentation.FinalizeCapturedBatch(profile.Effect, kind, drawn.Packets) };
            current = PublishRegisters(drawn.Frame, drawn.Registers);
            if (graphics.OriginalEffect is { Binding.Effect: 331 })
            {
                drawn = drawn with { Packets = NativeBattleCinematic33Presentation.Depths(drawn.Packets) };
            }
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
                if (graphics.OriginalEffect is { } selected && (selected.Binding.Effect is 208 or 211 or 219 or 228 or 236 or 237 or 243 or 244 or 245 or 246 or 247 || (NativeBattleCinematic30Presentation.Owns(selected.Binding.Effect) || NativeBattleCinematic31Presentation.Owns(selected.Binding.Effect) || NativeBattleCinematic33Presentation.Owns(selected.Binding.Effect))))
                {
                    graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, "20DFAB8", 0, ArenaOffset(start, 0), drawn.Packets) } };
                }
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
                if (graphics.OriginalEffect is { } selected && (selected.Binding.Effect is 208 or 211 or 219 or 228 or 236 or 237 or 243 or 244 or 245 or 246 or 247 || (NativeBattleCinematic30Presentation.Owns(selected.Binding.Effect) || NativeBattleCinematic31Presentation.Owns(selected.Binding.Effect) || NativeBattleCinematic33Presentation.Owns(selected.Binding.Effect))))
                {
                    graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, common.PacketBank!, common.PacketBuffer, common.ByteCursor!.Value, drawn.Packets) } };
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, drawn.Packets);
                memory = memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            NativeEffectMemory = memory;
            return current with { Arena = memory.Arena!, Core = memory.ScriptedEffects! };
        }

        NativeBattleGeometryRegisterStep ReadRegisters(NativeBattleWaterExecutionFrame current)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            return new(memory.GeometryRegisters ?? throw new NotSupportedException("Native cinematic particles require current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic particles require current arithmetic registers."));
        }

        NativeBattleWaterExecutionFrame PublishRegisters(NativeBattleWaterExecutionFrame current, NativeBattleGeometryRegisterStep step)
        {
            NativeEffectMemory = RequireWaterMemory() with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
            return current;
        }
    }
}
