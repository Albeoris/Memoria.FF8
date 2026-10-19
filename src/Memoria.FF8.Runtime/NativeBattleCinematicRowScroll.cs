using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Native row rotation into the current allocated texture buffer and shared rectangle queue.</summary>
public static class NativeBattleCinematicRowScroll
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, bool sineRows, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectTextureRectangle, uint, NativeBattleWaterExecutionFrame> enqueue)
    {
        uint payload = SlotDword(0xB8);
        NativeBattleCinematicTextureSelection source = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, profile, SignedWord(payload));
        frame = frame.WriteTemporaryDword(0xF8, (uint)(source.SourceMapByte & 0x80)).WriteTemporaryDword(0xFC, source.SourceAddress).WriteTemporaryDword(0xF0, source.RectangleAddress).WriteTemporaryDword(0xF4, source.TexturePage!.Value).WriteTemporaryDword(0x64, source.SourceAddress);
        uint output = SlotDword(0xBC);
        int width = source.Rectangle.Width;
        int height = source.Rectangle.Height;
        if (output == 0)
        {
            (frame, output) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)(width * height * 2)));
            frame = WriteSlotDword(frame, 0xBC, output);
        }
        frame = frame.WriteTemporaryDword(0x60, output).WriteTemporaryDword(0x80, output);
        uint runtime = CoreDword(frame, 0x279744C);
        byte counter = frame.Core.Read(unchecked(runtime + 0x43), 1)[0];
        uint rectangleAddress = 0x27974D4u + (uint)((counter & 15) * 8);
        frame = frame with { Core = frame.Core.Write(unchecked(runtime + 0x43), [unchecked((byte)(counter + 1))]) };
        frame = frame.WriteTemporaryDword(0x68, rectangleAddress);
        NativeBattleEffectTextureRectangle rectangle = new(SignedWord(payload + 2), SignedWord(payload + 4), source.Rectangle.Width, source.Rectangle.Height);
        frame = frame with { Core = frame.Core.Write(rectangleAddress, NativeBattleCinematicScroll.EncodeRectangle(rectangle)) };
        int mask = width - 1;
        int shift = unchecked((short)SlotWord(frame, 0x94)) & mask;
        frame = frame.WriteTemporaryDword(0x78, unchecked((uint)width)).WriteTemporaryDword(0x7C, unchecked((uint)height)).WriteTemporaryDword(0x6C, unchecked((uint)mask)).WriteTemporaryDword(0x70, unchecked((uint)shift)).WriteTemporaryDword(0x84, unchecked((uint)(width * 2)));
        if (sineRows)
        {
            frame = frame.WriteTemporaryDword(0x74, unchecked((uint)(int)(short)SlotWord(frame, 0x8C))).WriteTemporaryDword(0x88, unchecked((uint)(int)(short)SlotWord(frame, 0x8E))).WriteTemporaryDword(0x8C, unchecked((uint)(int)(short)SlotWord(frame, 0x90)));
        }
        for (int row = 0; row < height; row++)
        {
            int rowShift = shift;
            if (sineRows)
            {
                int angle = unchecked((int)frame.ReadTemporaryDword(0x88));
                int amplitude = unchecked((int)frame.ReadTemporaryDword(0x74));
                rowShift = unchecked(shift + (unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 20)) & mask;
            }
            uint destination = frame.ReadTemporaryDword(0x60);
            uint input = frame.ReadTemporaryDword(0x64);
            int remaining = width - rowShift;
            CopyWords(input, unchecked(destination + (uint)(rowShift * 2)), remaining);
            CopyWords(unchecked(input + (uint)(Math.Max(0, remaining) * 2)), destination, rowShift);
            frame = frame.WriteTemporaryDword(0x60, unchecked(destination + (uint)(width * 2))).WriteTemporaryDword(0x64, unchecked(input + (uint)(width * 2)));
            if (sineRows)
            {
                frame = frame.WriteTemporaryDword(0x88, unchecked(frame.ReadTemporaryDword(0x88) + frame.ReadTemporaryDword(0x8C)));
            }
            frame = frame.WriteTemporaryDword(0x7C, unchecked((uint)(height - row - 1)));
        }
        return enqueue(frame, rectangle, output);

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        void CopyWords(uint from, uint to, int count)
        {
            if (count <= 0)
            {
                return;
            }
            int length = checked(count * 2);
            if (count > 1 && to > from && (long)to < (long)from + length)
            {
                for (int index = 0; index < count; index++)
                {
                    CopyWords(unchecked(from + (uint)(index * 2)), unchecked(to + (uint)(index * 2)), 1);
                }
                return;
            }
            var snapshot = frame.Arena.ReadSnapshot(ArenaOffset(from, length), length);
            int offset = ArenaOffset(to, length);
            NativeBattleEffectArena arena = frame.Arena.Write(offset, snapshot.Data);
            for (int lane = 0; lane < length; lane++)
            {
                if (!snapshot.Known[lane])
                {
                    int start = lane;
                    while (lane + 1 < length && !snapshot.Known[lane + 1])
                    {
                        lane++;
                    }
                    arena = arena.MarkUnknown(offset + start, lane - start + 1);
                }
            }
            frame = frame with { Arena = arena };
        }
    }
}
