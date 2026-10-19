using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291ChildStep Step, NativeBattleGraphicsState Graphics) AdvanceNativePandemona291LayerParticle(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona particles require their original resources.");
        NativeBattlePandemona291SpriteResource resource = resources.LayerSprite ?? throw new NotSupportedException("Native Pandemona particles require their original sprite.");
        NativeBattleModuleDraw sprite = NativeBattlePandemona291LayerParticles.Draw6F0ED0(task, resource, resources.TrailTrigonometry);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Pandemona particles require their current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Pandemona particles require their current GTE projection.");
        NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection, camera.Roll, 17, 2);
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona particles require their current module state.");
        uint output = state.PacketCursor25562DC ?? throw new NotSupportedException("Native Pandemona particles require their current packet cursor.");
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Pandemona particles require their current ordering table.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        NativeBattleScriptedColorRegisters registers = written.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona particles require their retained GTE register owner.");
        registers = registers with { NormalInput = null, NormalInputKnown = null, Mac = [null, null, null], Ir = [null, null, null], Flags = null };
        NativeBattleModuleSpriteFrame selected = sprite.SpriteFrame!;
        if (!selected.Quads.IsEmpty)
        {
            ReadOnlySpan<byte> quad = selected.Quads.AsSpan().Slice(selected.Quads.Length - 20, 20);
            NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(sprite, projection.View ?? throw new NotSupportedException("Native Pandemona particles require their current view matrix."), camera.Roll);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(main, quad);
            short width = unchecked((short)(8 * quad[0]));
            short height = unchecked((short)(8 * quad[2]));
            byte[] input = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(input, width);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), height);
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, new(width, height, 0), projection);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = [true, true, true, true, true, true, false, false], Flags = projected.Flags };
        }
        frame = frame with { Memory = written.Memory with { Pandemona291 = state with { PacketCursor25562DC = unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor) }, ScriptedColorRegisters = registers } };
        return (NativeBattlePandemona291LayerParticles.Finish6F0ED0(frame, address, task), graphics);
    }
}
