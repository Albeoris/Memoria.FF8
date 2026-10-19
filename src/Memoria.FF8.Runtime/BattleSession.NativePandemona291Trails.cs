using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Trails(NativeBattlePandemona291Frame frame, uint taskAddress, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona requires its current original trail resources.");
        if (resources.TrailModels.IsDefault || resources.TrailModels.Length != 4)
        {
            throw new NotSupportedException("Native Pandemona requires all four original trail meshes.");
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona requires its current world/view matrix.");
        short zsf3 = frame.Memory.ScriptedGraphics?.Zsf3 ?? throw new NotSupportedException("Native Pandemona requires its current ZSF3.");
        int start = checked((int)(taskAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona trails require their current arena.");
            int offset = checked(start + 0x42C + index * 24);
            if (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset + 6, 2)) == 0)
            {
                continue;
            }
            byte[] record = new byte[24];
            arena.Read(offset, 6).CopyTo(record, 0);
            arena.Read(offset + 0xE, 6).CopyTo(record, 0xE);
            byte selector = arena.Read(offset + 0x16, 1)[0];
            NativeBattleStaticMeshResource mesh = resources.TrailModels[selector];
            NativeBattleActionMatrix matrix = NativeBattlePandemona291TrailDrawing.Prepare6F1650(record, resources.TrailTrigonometry, view);
            NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona trails require their retained GTE input registers.");
            NativeBattleGfGeometryBatch batch = NativeBattlePandemona291TrailDrawing.Draw6F17C0(mesh, matrix, projection, zsf3, input => registers = registers with { NormalInput = input, NormalInputKnown = null }, flags => registers = registers with { Flags = flags });
            registers = registers with { Mac = [null, null, null], Ir = [null, null, null] };
            frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers } };
            NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona requires its current module state.");
            uint output = state.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona requires its current packet cursor.");
            NativeBattlePrimitiveFrame primitiveFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona requires its current ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), primitiveFrame.Pending, batch);
            frame = frame with { Memory = written.Memory with { Pandemona291 = state with { PacketCursor25562DC = unchecked(output + (uint)batch.AllocationBytes) } } };
            graphics = graphics with { PrimitiveFrame = primitiveFrame with { Pending = written.Ordering } };
        }
        return (frame, graphics);
    }
}
