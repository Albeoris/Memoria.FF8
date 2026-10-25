using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicProfileHeight70Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, uint, short> ReadProfileWord, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> UpdateSelected);

/// <summary>Current-slot height sampling from a referenced owner's retained radial profile.</summary>
public static class NativeBattleCinematicProfileHeight70
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicProfileHeight70Services services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.ResolveSlot);
        ArgumentNullException.ThrowIfNull(services.ReadProfileWord);
        ArgumentNullException.ThrowIfNull(services.UpdateSelected);
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x70)
        {
            throw new NotSupportedException("Native profile height requires its selected Main70 instruction.");
        }
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint current = CoreDword(frame, 0x27973E8);
        uint first = services.ResolveSlot(frame, selector);
        frame = WriteCoreDword(frame, 0x27973E8, first);
        ushort secondSelector = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(first + 0x9C), 2), 2));
        uint second = services.ResolveSlot(frame, unchecked((short)secondSelector));
        frame = WriteCoreDword(frame, 0x27973E8, current);
        int x = unchecked(Word(current, 0x94) - Word(second, 0x94) - Word(first, 0x94));
        int z = unchecked(Word(current, 0x98) - Word(second, 0x98) - Word(first, 0x98));
        int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + z * z));
        uint profile = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(first + 0xBC), 4), 4));
        if (profile == 0)
        {
            throw new NotSupportedException("Native profile height reached an unproduced retained CPU-local value through a null profile.");
        }
        int height;
        if (distance > services.ReadProfileWord(frame, unchecked(profile + 0x9E)))
        {
            height = 0;
        }
        else
        {
            int? selected = null;
            for (int offset = 2; offset < 0x40; offset += 2)
            {
                if (distance < services.ReadProfileWord(frame, unchecked(profile + 0x60u + (uint)offset)))
                {
                    selected = services.ReadProfileWord(frame, unchecked(profile + 0x1Eu + (uint)offset));
                    break;
                }
            }
            height = selected ?? throw new NotSupportedException("Native profile height reached an unproduced retained CPU-local value without a matching threshold.");
        }
        int y = unchecked(height + Word(second, 0x96) + Word(first, 0x96));
        frame = WriteSlotDword(frame, 0x60, unchecked((uint)(y << 16)));
        frame = services.UpdateSelected(frame, ReadSlot(frame, 0x18, 1)[0]);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);

        short Word(uint address, uint offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(address + offset), 2), 2));
    }
}
