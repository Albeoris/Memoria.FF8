using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291ChildStep Step, NativeBattleGraphicsState Graphics) AdvanceNativePandemona291RisingChild(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona ribbons require their original trigonometry.");
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona ribbons require their current module state.");
        bool paused = (state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona ribbons require their current pause DWORD.")) != 0;
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona ribbons require their actual nodes.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x45C);
        int age = unchecked((short)task.ReadWord(0xC));
        int count = checked((int)task.ReadDword(0x20));
        var snapshot = arena.ReadSnapshot(start + 0x24, 30 * 36);
        byte[] nodes = snapshot.Data;
        bool[] known = snapshot.Known;
        if (age <= 20 && !paused)
        {
            int progress = unchecked(age << 12) / 20;
            int radius = 4096 + (NativeBattleCameraMath.Cosine(unchecked(progress * 2)) >> 3);
            int angle = unchecked((short)task.ReadWord(0x12));
            short x = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 14));
            short z = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 14));
            short upper = unchecked((short)(task.ReadWord(0x10) + progress));
            int length = age < 8 ? unchecked(age * 64 + 32) : age > 12 ? unchecked((20 - age) * 64 + 32) : 512;
            byte[] lowerPoint = new byte[8];
            byte[] upperPoint = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(lowerPoint, x);
            BinaryPrimitives.WriteInt16LittleEndian(lowerPoint.AsSpan(2), unchecked((short)(upper - length)));
            BinaryPrimitives.WriteInt16LittleEndian(lowerPoint.AsSpan(4), z);
            BinaryPrimitives.WriteInt16LittleEndian(upperPoint, x);
            BinaryPrimitives.WriteInt16LittleEndian(upperPoint.AsSpan(2), upper);
            BinaryPrimitives.WriteInt16LittleEndian(upperPoint.AsSpan(4), z);
            ushort nextAngle = unchecked((ushort)(task.ReadWord(0x12) + task.ReadWord(0x14)));
            task = task.WriteWord(0x12, nextAngle);
            arena = arena.Write(start + 0x12, BitConverter.GetBytes(nextAngle));
            NativeBattleBlizzagaRibbon.RecordInsertion(task, 0x24, count);
            NativeBattleBlizzagaRibbonNodes inserted = NativeBattleBlizzagaRibbon.Insert6D99D0(count, nodes.ToImmutableArray(), lowerPoint.ToImmutableArray(), upperPoint.ToImmutableArray(), 16, 88, unchecked((byte)((3 - (age & 3)) * 32)));
            if (count < 30)
            {
                known.AsSpan(0, count * 36).CopyTo(known.AsSpan(36));
            }
            count = inserted.Count;
            nodes = inserted.Nodes.ToArray();
            Array.Fill(known, true, 0, 16);
            known[6] = false;
            known[7] = false;
            known[14] = false;
            known[15] = false;
            Array.Fill(known, true, 0x20, 3);
        }
        short scale = unchecked((short)task.ReadWord(0x1A));
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona ribbons require their current center.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona ribbons require their current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona ribbons require their current view.");
        NativeBattleActionMatrix local = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, task.ReadWord(0x16), scale, unchecked((scale >> 2) + 3072)) with { Z = center };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Pandemona ribbons require their current depth controls.");
        NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona ribbons require their actual GTE input owner.");
        for (int index = 0; index < count; index++)
        {
            if (!paused)
            {
                RequireKnown(index * 36 + 0x20, 2);
            }
            else if (count >= 2)
            {
                RequireKnown(index * 36 + 0x20, 1);
            }
            if (count >= 2)
            {
                RequireKnown(index * 36 + 0x22, 1);
            }
        }
        NativeBattleBlizzagaRibbonResult drawn = NativeBattleBlizzagaRibbon.Emit6F1290(count, nodes.ToImmutableArray(), -4, matrix, paused, 17, 2, point =>
        {
            NativeBattleModuleDepthCueVertex vertex = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            registers = registers with { Flags = vertex.Projected.Flags, Mac = [null, null, null], Ir = [null, null, null] };
            return vertex.Projected;
        }, offset =>
        {
            RequireKnown(offset, 6);
            registers = registers with { NormalInput = nodes.AsSpan(offset, 8).ToArray().ToImmutableArray(), NormalInputKnown = known.AsSpan(offset, 8).ToArray().ToImmutableArray() };
        }, projection, NativeBattlePilotVisualLifetimes.Identity(291, task), NativeBattleBlizzagaRibbon.NodeBirths(task, 0x24, count));
        if (!paused)
        {
            for (int index = 0; index < count; index++)
            {
                Array.Fill(known, true, index * 36 + 0x20, 2);
            }
        }
        if (drawn.Count >= 2)
        {
            for (int index = 0; index < drawn.Count - 1; index++)
            {
                RequireKnown(index * 36 + 0x22, 1);
            }
            for (int index = 0; index < drawn.Count; index++)
            {
                Array.Fill(known, true, index * 36 + 0x10, 16);
            }
        }
        arena = arena.Write(start + 0x24, drawn.Nodes.AsSpan());
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                arena = arena.MarkUnknown(start + 0x24 + index, 1);
            }
        }
        task = task.WriteDword(0x20, unchecked((uint)drawn.Count));
        arena = arena.Write(start + 0x20, BitConverter.GetBytes(drawn.Count));
        frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers } };
        uint output = state.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona ribbons require their actual packet cursor.");
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona ribbons require their actual ordering table.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, NativeBattleWaterExecutionFields.ArenaOffset(output, 0), primitive.Pending, drawn.Packets);
        frame = frame with { Memory = written.Memory with { Pandemona291 = state with { PacketCursor25562DC = unchecked(output + (uint)drawn.Packets.AllocationBytes) } } };
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return (NativeBattlePandemona291RisingChildren.Finish6F1060(frame, address, task), graphics);

        void RequireKnown(int offset, int length)
        {
            for (int index = offset; index < offset + length; index++)
            {
                if (!known[index])
                {
                    throw new NotSupportedException("Native Pandemona ribbon consumed an unresolved node field.");
                }
            }
        }
    }
}
