using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeteor223TextureServices(uint MagicBuffer, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureRectangle, uint, bool, NativeBattleWaterExecutionFrame> Enqueue);

/// <summary>The Meteor texture save, restore and alternative-texture copy commands.</summary>
public static class NativeBattleMeteor223Commands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, INativeBattleScriptedTextureProfile profile, NativeBattleMeteor223TextureServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        uint pc = CoreDword(frame, 0x2797450);
        if (opcode == 0xBB)
        {
            NativeBattleCinematicTextureSelection source = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, profile, Operand(2));
            frame = frame.WriteTemporaryDword(0xF0, source.RectangleAddress).WriteTemporaryDword(0xF4, source.TexturePage!.Value).WriteTemporaryDword(0xF8, (uint)(source.SourceMapByte & 0x80)).WriteTemporaryDword(0xFC, source.SourceAddress);
            uint destination = unchecked(services.MagicBuffer + (uint)(Operand(4) << 4));
            int length = unchecked(2 * source.Rectangle.Width * source.Rectangle.Height);
            if (unchecked((int)source.SourceAddress) < unchecked((int)destination))
            {
                int remaining = unchecked(length - 4);
                if (remaining > 0)
                {
                    uint count = unchecked((uint)(remaining + 3)) >> 2;
                    uint offset = unchecked((uint)remaining);
                    while (count-- != 0)
                    {
                        Copy(unchecked(source.SourceAddress + offset), unchecked(destination + offset));
                        offset = unchecked(offset - 4);
                    }
                }
            }
            else if (length > 0)
            {
                uint count = unchecked((uint)(length + 3)) >> 2;
                for (uint index = 0; index < count; index++)
                {
                    Copy(unchecked(source.SourceAddress + 4 * index), unchecked(destination + 4 * index));
                }
            }
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        if (opcode is not (0x35 or 0xBD))
        {
            throw new NotSupportedException("Native Meteor texture service requires command35,BB orBD.");
        }
        bool indirect = (instruction & 0x8000) != 0;
        uint rectangleAddress;
        uint address;
        if (indirect)
        {
            rectangleAddress = unchecked(pc + (uint)Operand(2));
            address = unchecked(services.MagicBuffer + (uint)(Operand(4) << 4));
            frame = NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        else if (opcode == 0x35)
        {
            rectangleAddress = unchecked(pc + 2);
            int length = unchecked(2 * Operand(8) * Operand(6));
            (frame, address) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)length));
            frame = WriteSlotDword(frame, 0xC4, address);
            frame = NativeBattleScriptedCommandReads.Advance(frame, 10);
        }
        else
        {
            throw new NotSupportedException("Native Meteor BD without bit8000 requires its actual entry ECX stack owner.");
        }
        NativeBattleEffectTextureRectangle rectangle = Rectangle(frame.Arena.Read(ArenaOffset(rectangleAddress, 8), 8));
        byte indexByte = frame.Core.Read(0x2798BF4, 1)[0];
        uint ring = 0x27978E8u + (uint)(8 * (indexByte & 15));
        frame = frame with { Core = frame.Core.Write(ring, NativeBattleCinematicScroll.EncodeRectangle(rectangle)).Write(0x2798BF4, new byte[] { unchecked((byte)(indexByte + 1)) }) };
        return services.Enqueue(frame, rectangle, address, opcode == 0x35);

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, unchecked(pc + offset - CoreDword(frame, 0x2797450)), 2));

        void Copy(uint sourceAddress, uint destinationAddress)
        {
            byte[] bytes = frame.Arena.Read(ArenaOffset(sourceAddress, 4), 4);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(destinationAddress, 4), bytes) };
        }
    }

    public static NativeBattleWaterExecutionFrame SpreadResults(NativeBattleWaterExecutionFrame frame, int targetCount, Func<NativeBattleWaterExecutionFrame, int, NativeBattleWaterExecutionFrame> apply)
    {
        if (targetCount is < 0 or > 255)
        {
            throw new InvalidDataException("Native Meteor result pacing requires its actual BYTE target count.");
        }
        uint age = Dword(0xBC);
        if (age == 0)
        {
            uint parameter = Dword(0xB8);
            short duration = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(parameter, 2), 2));
            frame = WriteSlotDword(frame, 0xB8, unchecked((uint)(int)duration));
            frame = WriteSlotDword(frame, 0xC0, 0);
            frame = WriteSlotDword(frame, 0xC4, checked((uint)targetCount));
        }
        int count = unchecked((int)Dword(0xC4));
        int index = unchecked((int)Dword(0xC0));
        if (index >= count)
        {
            return frame;
        }
        int threshold = unchecked((index + 1) * (int)Dword(0xB8)) / unchecked(count + 1);
        if (threshold <= unchecked((int)age))
        {
            frame = apply(frame, index);
            frame = WriteSlotDword(frame, 0xC0, unchecked(Dword(0xC0) + 1));
        }
        return WriteSlotDword(frame, 0xBC, unchecked(Dword(0xBC) + 1));

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
    }

    private static NativeBattleEffectTextureRectangle Rectangle(ReadOnlySpan<byte> bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
}
