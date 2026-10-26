using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame InitializeBoneParticles64B610(NativeBattleEffect187Frame frame)
    {
        (frame, uint address) = AppendRequired(frame, 0x64B640);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        return ClearRecordHeads(frame, 0x24FD510, 3);
    }

    public static NativeBattleEffect187Frame InitializePairedBoneParticles64BB20(NativeBattleEffect187Frame frame)
    {
        (frame, uint first) = AppendRequired(frame, 0x64BB90);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, 30).WriteWord(0x18, 1));
        (frame, uint second) = AppendRequired(frame, 0x64BB90);
        frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, 52).WriteWord(0x18, 2));
        frame = ClearRecordHeads(frame, 0x24FE7B8, 10);
        frame = ClearRecordHeads(frame, 0x24FD510, 3);
        return ClearRecordHeads(frame, 0x24FD5D8, 3);
    }

    public static NativeBattleEffect187Frame InitializeRibbons64C300(NativeBattleEffect187Frame frame)
    {
        (frame, uint address) = AppendRequired(frame, 0x64C330);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        return ClearRecordHeads(frame, 0x24FE7B8, 10);
    }

    public static NativeBattleEffect187Frame InitializeMeshTrails64C710(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources)
    {
        for (int row = 0; row < 3; row++)
        {
            frame = InitializeMeshTrailRow(frame, resources, row, 0x64C7F0, 0x64D6A0, 2, 30, 0x2DC0, 0xF40);
        }
        return frame;
    }

    public static NativeBattleEffect187Frame InitializeMeshTrails64D850(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources, int row)
    {
        if (row < 3 || row > 5)
        {
            throw new InvalidDataException("Effect187 model trail selected an unsupported source row.");
        }
        return InitializeMeshTrailRow(frame, resources, row, 0x64D930, 0x64DAD0, 4, 14, 0x2E80, 0x1740);
    }

    private static NativeBattleEffect187Frame InitializeMeshTrailRow(NativeBattleEffect187Frame frame, NativeBattleEffect187Resources resources, int row, uint leadingCallback, uint trailingCallback, int copies, int duration, ushort firstScale, ushort secondScale)
    {
        byte[] bytes = resources.ReadStatic(checked(0xE41EF8u + (uint)(row * 8)), 8);
        ushort x = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0));
        ushort y = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
        ushort z = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
        ushort selector = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6));
        (frame, uint leading) = AppendRequired(frame, leadingCallback);
        frame = Write(frame, leading, Read(frame, leading).WriteWord(12, 0).WriteWord(14, selector).WriteWord(0x10, x).WriteWord(0x12, y).WriteWord(0x14, z).WriteWord(0x16, 0).WriteWord(0x1C, firstScale).WriteWord(0x1E, secondScale).WriteWord(0x20, unchecked((ushort)(duration - z))).WriteWord(0x22, z));
        for (int index = 0; index < copies; index++)
        {
            (frame, uint trailing) = AppendRequired(frame, trailingCallback);
            NativeBattleModulePoolTask source = Read(frame, leading);
            NativeBattleModulePoolTask task = Read(frame, trailing).WriteWord(14, source.ReadWord(14)).WriteDword(0x10, source.ReadDword(0x10)).WriteDword(0x14, source.ReadDword(0x14)).WriteWord(0x1C, source.ReadWord(0x1C)).WriteWord(0x1E, source.ReadWord(0x1E));
            ushort depth = unchecked((ushort)(z + index));
            task = task.WriteWord(12, 0).WriteWord(0x14, depth).WriteWord(0x16, 0x800).WriteWord(0x20, unchecked((ushort)(duration - depth))).WriteWord(0x22, depth);
            frame = Write(frame, trailing, task);
        }
        return frame;
    }

    private static NativeBattleEffect187Frame ClearRecordHeads(NativeBattleEffect187Frame frame, uint address, int count)
    {
        for (int index = 0; index < count; index++)
        {
            frame = WriteBytes(frame, checked(address + (uint)(index * 20)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0u)), 4);
        }
        return frame;
    }
}
