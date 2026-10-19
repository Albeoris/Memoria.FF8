using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Texture metadata selection from the current authenticated initializer directory pointers.</summary>
public static class NativeBattleWaterTextureMetadata
{
    public static NativeBattleWaterExecutionFrame SelectTPageB665C0(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int index)
    {
        byte[] rectangle = ReadMetadata(frame, profile, 0x98, 0, unchecked(index * 8), 4);
        byte flags = ReadMetadata(frame, profile, 0xA4, 8, index, 1)[0];
        int x = BinaryPrimitives.ReadInt16LittleEndian(rectangle);
        int y = BinaryPrimitives.ReadUInt16LittleEndian(rectangle.AsSpan(2));
        uint page = unchecked((uint)((((x >> 2) & 0x3F0) | (y & 0x100)) >> 4));
        page |= (uint)(flags & 0x80) | (uint)(ReadSlot(frame, 0x92, 1)[0] & 0x60);
        return frame.WriteTemporaryDword(0xFC, page).WriteTemporaryDword(0xF8, 0);
    }

    public static ushort ReadClutB66640(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int index)
    {
        byte[] rectangle = ReadMetadata(frame, profile, 0x9C, 4, unchecked(index * 8), 4);
        int x = BinaryPrimitives.ReadInt16LittleEndian(rectangle);
        int y = BinaryPrimitives.ReadUInt16LittleEndian(rectangle.AsSpan(2));
        return unchecked((ushort)(((y & 0x1FF) << 6) | ((x >> 4) & 0x3F)));
    }

    public static NativeBattleWaterExecutionFrame Execute52AA3A60(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile)
    {
        uint pc = CoreDword(frame, 0x2797450);
        short selector = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(pc + 2), 2), 2));
        if (selector == -1)
        {
            frame = WriteSlotWord(frame, 0x92, 0);
            frame = WriteSlotWord(frame, 0x9A, 0);
            frame = WriteSlotWord(frame, 0x9E, 0);
        }
        else
        {
            frame = SelectTPageB665C0(frame, profile, selector);
            frame = WriteSlotWord(frame, 0x92, unchecked((ushort)frame.ReadTemporaryDword(0xFC)));
            frame = WriteSlotWord(frame, 0x9E, unchecked((ushort)frame.ReadTemporaryDword(0xF8)));
            frame = WriteSlotWord(frame, 0x9A, ReadClutB66640(frame, profile, selector));
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));
    }

    private static byte[] ReadMetadata(NativeBattleWaterExecutionFrame frame, INativeBattleScriptedTextureProfile profile, int managerOffset, int directoryOffset, int offset, int length)
    {
        profile.Validate();
        uint manager = CoreDword(frame, 0x27973EC);
        uint pointer = CoreDword(frame, checked(manager + (uint)managerOffset));
        uint authenticated = checked(profile.InitializationAddress + profile.InitializationOffset(directoryOffset));
        if (pointer != authenticated)
        {
            throw new NotSupportedException("Native scripted texture metadata requires its actual authenticated directory pointer.");
        }
        return profile.ReadStatic(unchecked(pointer + (uint)offset), length).ToArray();
    }

}
