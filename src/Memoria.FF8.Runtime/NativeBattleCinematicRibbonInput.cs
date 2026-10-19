using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicRibbonInputResult(NativeBattleActionMatrix Matrix, NativeBattleScriptedColorRegisters Registers);

public static class NativeBattleCinematicRibbonInput
{
    public static NativeBattleScriptedColorRegisters Capture(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleScriptedColorRegisters registers)
    {
        registers.Validate();
        byte[] raw;
        bool[] known;
        if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x180 - 8)
        {
            int offset = checked((int)(address - frame.TemporaryFrame.Address));
            raw = frame.TemporaryFrame.Bytes.AsSpan().Slice(offset, 8).ToArray();
            known = frame.TemporaryFrame.Known.AsSpan().Slice(offset, 8).ToArray();
        }
        else if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 8)
        {
            (raw, known) = frame.Core.ReadSnapshot(address, 8);
        }
        else
        {
            int offset = checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
            raw = new byte[8];
            known = new bool[8];
            for (int index = 0; index < 8; index++)
            {
                try
                {
                    raw[index] = frame.Arena.Read(offset + index, 1)[0];
                    known[index] = true;
                }
                catch (NotSupportedException)
                {
                    known[index] = false;
                }
            }
        }
        return registers with { NormalInput = raw.ToImmutableArray(), NormalInputKnown = known.All(value => value) ? null : known.ToImmutableArray() };
    }

    public static NativeBattleCinematicRibbonInputResult PrepareParent(NativeBattleActionMatrix basis, NativeBattleScriptedColorRegisters registers)
    {
        registers.Validate();
        byte[] input = registers.NormalInput?.ToArray() ?? throw new NotSupportedException("Native cinematic ribbon parent transform requires its retained packed V0 input.");
        bool[] known = registers.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(true, 8).ToArray();
        if (known.Take(4).Any(value => !value))
        {
            throw new NotSupportedException("Native cinematic ribbon parent transform requires its actual retained V0 X/Y WORDs.");
        }
        Array.Clear(input, 4, 4);
        Array.Fill(known, true, 4, 4);
        registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = null };
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(input), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan(2)), 0);
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, point);
        return new(basis with { X = translated.X, Y = translated.Y, Z = translated.Z }, registers);
    }
}
