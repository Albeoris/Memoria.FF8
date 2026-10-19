using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicGrid59(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleActionMatrix? projectionBasis = null;
        NativeBattleCinematicGrid59Services services = new(current =>
        {
            var prepared = PrepareNativeCinematicLatticeBasis(current, resourceLength, 4096);
            projectionBasis = prepared.Matrix;
            return prepared.Frame;
        }, (current, output) =>
        {
            return ProjectNativeCinematicLatticeVertex(current, projectionBasis ?? throw new InvalidOperationException("Native grid projection basis has not been selected."), current.TemporaryFrame.Address + 0xC0, output);
        }, TransformNativeCinematicCurve, (current, color, factor) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleCinematicColorScaleResult result = NativeBattleCinematicColorScale.ScaleB66A20(color, factor, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic grid requires its current color registers."));
            NativeEffectMemory = memory with { ScriptedColorRegisters = result.Registers };
            return new(current, result.Color);
        }, address => resources.ReadStatic(address, 1)[0], (current, output, ordering, batch) =>
        {
            var result = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = result.Graphics;
            return result.Frame;
        });
        return (NativeBattleCinematicGrid59.Draw(frame, services), graphics);
    }

    private (NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix) PrepareNativeCinematicLatticeBasis(NativeBattleWaterExecutionFrame current, int resourceLength, int scale)
    {
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
        uint selected = NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, SlotWord(current, 0x9C));
        byte[] rotation = current.Core.Read(selected, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(current, selected + 20)), unchecked((int)CoreDword(current, selected + 24)), unchecked((int)CoreDword(current, selected + 28)), 4096);
        NativeBattleVector global = new(unchecked((short)CoreWord(current, 0x27971E4)), unchecked((short)CoreWord(current, 0x27971E6)), unchecked((short)CoreWord(current, 0x27971E8)));
        NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(basis, global);
        current = current.WriteTemporaryDword(0xF4, unchecked((uint)transformed.X)).WriteTemporaryDword(0xF8, unchecked((uint)transformed.Y)).WriteTemporaryDword(0xFC, unchecked((uint)transformed.Z));
        (byte[] raw, bool[] known) = current.Core.ReadSnapshot(selected, 20);
        current = current.WriteTemporary(0xE0, raw);
        bool[] temporaryKnown = current.TemporaryFrame.Known.ToArray();
        known.CopyTo(temporaryKnown, 0xE0);
        current = current with { TemporaryFrame = current.TemporaryFrame with { Known = temporaryKnown.ToImmutableArray() } };
        current = current.WriteTemporaryDword(0xD4, unchecked((uint)scale)).WriteTemporaryDword(0xD8, unchecked((uint)scale)).WriteTemporaryDword(0xDC, unchecked((uint)scale));
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, scale, scale, scale);
        for (int index = 0; index < 9; index++)
        {
            current = current.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)scaled.Rotation[index]));
        }
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedColorRegisters registers = NativeBattleCinematicRibbonInput.Capture(current, 0x27971E4, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native grid basis requires its packed vector registers."));
        NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(registers, transformed) };
        return (current, scaled);
    }

    private NativeBattleWaterExecutionFrame TransformNativeCinematicCurve(NativeBattleWaterExecutionFrame current, NativeBattleActionMatrix matrix, uint input, uint output)
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedColorRegisters registers = NativeBattleCinematicRibbonInput.Capture(current, input, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native curve construction requires its packed vector registers."));
        NativeBattleVector vector = NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(checked((int)(input - current.TemporaryFrame.Address)), 6));
        NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(matrix, vector);
        byte[] result = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(result, (short)Math.Clamp(transformed.X, short.MinValue, short.MaxValue));
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(2), (short)Math.Clamp(transformed.Y, short.MinValue, short.MaxValue));
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(4), (short)Math.Clamp(transformed.Z, short.MinValue, short.MaxValue));
        if (output >= current.TemporaryFrame.Address && (long)output - current.TemporaryFrame.Address <= 0x180 - result.Length)
        {
            current = current.WriteTemporary(checked((int)(output - current.TemporaryFrame.Address)), result);
        }
        else
        {
            current = current with { Arena = current.Arena.Write(ArenaOffset(output, 6), result) };
        }
        current = current.WriteTemporaryDword(0xFC, transformed.Flags);
        NativeEffectMemory = memory with { ScriptedColorRegisters = TransformRegisters(registers, transformed) };
        return current;
    }

    private static NativeBattleScriptedColorRegisters TransformRegisters(NativeBattleScriptedColorRegisters registers, NativeBattleFullMacVertex transformed)
    {
        return registers with { Mac = [transformed.X, transformed.Y, transformed.Z], Ir = [Math.Clamp(transformed.X, short.MinValue, short.MaxValue), Math.Clamp(transformed.Y, short.MinValue, short.MaxValue), Math.Clamp(transformed.Z, short.MinValue, short.MaxValue)], Flags = transformed.Flags };
    }
}
