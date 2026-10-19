using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Additive BGR555 palette transformation and direct upload from the retained scripted process.</summary>
public static class NativeBattleCinematicPaletteCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, INativeBattleScriptedTextureProfile profile, NativeBattleWaterSlotBank bank, int currentIndex, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureRectangle, uint, NativeBattleWaterExecutionFrame> enqueue)
    {
        ushort raw = frame.ReadTemporaryWord(0x14A);
        if ((raw & 0x1FF) != 0x4B)
        {
            throw new NotSupportedException("Native cinematic palette transformation requires its selected instruction.");
        }
        short selection = Operand(2);
        NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, profile, selection & 0xFF);
        frame = frame.WriteTemporaryDword(0xF0, clut.RectangleAddress).WriteTemporaryDword(0xFC, clut.SourceAddress);
        frame = frame.WriteTemporaryDword(0xF0, frame.ReadTemporaryDword(0xFC));
        frame = frame.WriteTemporaryDword(0xF8, unchecked(0x279822Cu + (uint)((selection >> 8) << 9)));
        short packed = Operand(4);
        frame = frame.WriteTemporaryDword(0xFC, unchecked((uint)(int)packed));
        uint start = (uint)((packed & 0xFF) * 2);
        frame = frame.WriteTemporaryDword(0xF0, unchecked(frame.ReadTemporaryDword(0xF0) + start)).WriteTemporaryDword(0xF8, unchecked(frame.ReadTemporaryDword(0xF8) + start));
        int mode = raw >> 12;
        int count = mode is 1 or 2 ? Operand(6) : 256;
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)count));
        uint sourceBone = CoreDword(frame, 0x27973E8);
        if (mode == 1)
        {
            int selected = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, currentIndex, frame.Core.Read(0x2797454, 128).ToImmutableArray(), Operand(8));
            sourceBone = unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, selected));
        }
        int[] additions = new int[3];
        for (int lane = 0; lane < 3; lane++)
        {
            additions[lane] = Math.Min(31, (int)BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(sourceBone + 0x8Cu + (uint)(2 * lane)), 2), 2)));
        }
        frame = NativeBattleScriptedCommandReads.Advance(frame, mode == 1 ? 10u : mode == 2 ? 8u : 6u);
        uint source = frame.ReadTemporaryDword(0xF0);
        uint destination = frame.ReadTemporaryDword(0xF8);
        if (count > 0)
        {
            byte[] output = new byte[checked(count * 2)];
            for (int index = 0; index < count; index++)
            {
                ushort original = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(source + (uint)(2 * index)), 2), 2));
                int color = original & 0x8000;
                for (int lane = 0; lane < 3; lane++)
                {
                    color |= Math.Clamp(((original >> (5 * lane)) & 31) + additions[lane], 0, 31) << (5 * lane);
                }
                BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(index * 2), unchecked((ushort)color));
            }
            frame = frame with { Core = frame.Core.Write(destination, output) };
        }
        uint runtime = CoreDword(frame, 0x279744C);
        byte counter = frame.Core.Read(runtime + 0x43, 1)[0];
        int ring = counter & 15;
        frame = frame with { Core = frame.Core.Write(runtime + 0x43, [unchecked((byte)(counter + 1))]) };
        int origin = unchecked((short)CoreWord(frame, runtime + 0x1C));
        NativeBattleEffectTextureRectangle rectangle = new(unchecked((short)(16 * (origin & 0x3F) + (packed & 0xFF))), unchecked((short)(((origin >> 6) & 0x1FF) + ((packed >> 8) & 0x1F))), unchecked((short)count), 1);
        byte[] encoded = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(encoded, rectangle.X);
        BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(2), rectangle.Y);
        BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(4), rectangle.Width);
        BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(6), rectangle.Height);
        frame = frame with { Core = frame.Core.Write(0x27974D4u + (uint)(8 * ring), encoded) };
        return enqueue(frame, rectangle, destination);

        short Operand(uint delta) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, delta, 2));
    }
}
