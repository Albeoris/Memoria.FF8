using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicTextureCommandStep(NativeBattleWaterExecutionFrame Frame, NativeBattleEffectTextureState Textures);

public static class NativeBattleCinematicTextureCommands
{
    public static NativeBattleCinematicTextureCommandStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, INativeBattleScriptedTextureProfile profile, NativeBattleEffectTextureState textures)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        if (opcode is not (0x27 or 0x28 or 0x29))
        {
            throw new NotSupportedException("Native cinematic texture commands require their selected pair, CLUT or pixel instruction.");
        }
        if (frame.Core.Read(0x2798219, 1)[0] != 0)
        {
            return Wait();
        }
        uint pc = CoreDword(frame, 0x2797450);
        uint manager = CoreDword(frame, 0x27973EC);
        if (opcode == 0x29 && (instruction & 0xE000) != 0)
        {
            NativeBattleEffectTextureRectangle rectangle;
            uint source;
            uint advance;
            if ((instruction & 0x8000) != 0)
            {
                rectangle = PixelRectangle();
                source = CoreDword(frame, manager + 0xB4);
                advance = 4;
            }
            else if ((instruction & 0x4000) != 0)
            {
                rectangle = PixelRectangle();
                int file = NativeBattleScriptedCommandReads.Program(frame, program, 4, 1)[0] & 0x7F;
                short offset = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 6, 2));
                source = unchecked(CoreDword(frame, 0x2798A68u + (uint)(file * 4)) + (uint)(offset << 12));
                advance = 8;
            }
            else
            {
                byte[] bytes = NativeBattleScriptedCommandReads.Program(frame, program, 2, 8);
                rectangle = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
                source = CoreDword(frame, manager + 0xB4);
                advance = 10;
            }
            textures = textures.EnqueueArenaRectangle505DF0(rectangle, frame.Arena, ArenaOffset(source, 0));
            return new(WriteCoreDword(frame, 0x2797450, unchecked(pc + advance)), textures);
        }

        int index = unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2)));
        if (opcode != 0x28)
        {
            NativeBattleCinematicTextureSelection pixels = NativeBattleCinematicTextureMetadata.ResolvePixelB664A0(frame, profile, index);
            frame = frame.WriteTemporaryDword(0xF0, pixels.RectangleAddress).WriteTemporaryDword(0xF4, pixels.TexturePage!.Value).WriteTemporaryDword(0xF8, (uint)(pixels.SourceMapByte & 0x80)).WriteTemporaryDword(0xFC, pixels.SourceAddress);
            if (!Submit(pixels))
            {
                return Wait();
            }
            frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));
            if (opcode == 0x29)
            {
                return new(frame, textures);
            }
            frame = WriteCoreDword(frame, 0x2797450, pc);
        }
        NativeBattleCinematicTextureSelection clut = NativeBattleCinematicTextureMetadata.ResolveClutB66560(frame, profile, index);
        frame = frame.WriteTemporaryDword(0xF0, clut.RectangleAddress).WriteTemporaryDword(0xFC, clut.SourceAddress);
        if (!Submit(clut))
        {
            return Wait();
        }
        return new(WriteCoreDword(frame, 0x2797450, unchecked(pc + 4)), textures);

        bool Submit(NativeBattleCinematicTextureSelection selection)
        {
            byte count = frame.Core.Read(manager + 0xD2, 1)[0];
            if (count >= 12)
            {
                return false;
            }
            frame = frame with { Core = frame.Core.Write(manager + 0xD2, [unchecked((byte)(count + 1))]) };
            textures = NativeBattleCinematicTextureMetadata.Enqueue505DF0(textures, frame, selection);
            return true;
        }

        NativeBattleEffectTextureRectangle PixelRectangle()
        {
            int index = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
            uint pointer = CoreDword(frame, manager + 0x98);
            uint expected = checked(profile.InitializationAddress + profile.InitializationOffset(0));
            if (pointer != expected)
            {
                throw new NotSupportedException("Native cinematic direct texture transfer requires its current pixel rectangle directory.");
            }
            byte[] bytes = profile.ReadStatic(unchecked(pointer + (uint)(index * 8)), 8).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        }

        NativeBattleCinematicTextureCommandStep Wait() => new(frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8)), textures);
    }
}
