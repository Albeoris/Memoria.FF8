using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    public static (NativeBattleElectrocute275Frame Frame, uint Address) CreateBoneTrail(NativeBattleElectrocute275Frame frame, uint bindingAddress, uint pathDirectoryAddress, int pathIndex, short steps, short scale, NativeBattleElectrocute275Resources resources)
    {
        short offset = BonePathWord(resources, unchecked(pathDirectoryAddress + (uint)(pathIndex * 2 + 2)));
        uint path = unchecked(pathDirectoryAddress + (uint)(offset * 2));
        frame = DrawRandom(frame, out ushort selector);
        int width = (selector & 3) switch { 0 => 150, 1 => 180, 2 => 220, _ => 260 };
        if (scale != 4096)
        {
            width = unchecked(width * scale) >> 12;
        }
        return StartBoneTrail(frame, bindingAddress, path, steps, unchecked((short)width), resources);
    }

    private static (NativeBattleElectrocute275Frame Frame, uint Address) StartBoneTrail(NativeBattleElectrocute275Frame frame, uint bindingAddress, uint path, short steps, short width, NativeBattleElectrocute275Resources resources)
    {
        frame = DrawRandom(frame, out ushort first);
        frame = DrawRandom(frame, out ushort second);
        short intensity = (short)((first & 1023) + (second & 511) + 256);
        short fade = unchecked((short)-(intensity >> 3));
        short bone = BonePathWord(resources, path);
        short vertex = BonePathWord(resources, unchecked(path + 2));
        (frame, uint point) = AllocateBonePoint(frame, bone, vertex, width, false, intensity, fade);
        if (point == 0)
        {
            return (frame, 0);
        }
        (frame, uint address) = Append(frame, 3, 0x689280);
        if (address == 0)
        {
            frame = BoneWord(frame, point, -1);
            frame = WriteArena(frame, Working(frame) + BoneFreePointerOffset, BitConverter.GetBytes(point));
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[28]);
        task = task.WriteWord(12, unchecked((ushort)steps)).WriteWord(14, 1).WriteWord(16, unchecked((ushort)width)).WriteWord(18, unchecked((ushort)intensity)).WriteWord(20, unchecked((ushort)fade));
        task = task.WriteDword(24, unchecked(path + 4)).WriteDword(28, point).WriteDword(32, point).WriteDword(36, bindingAddress);
        return (Write(frame, address, task), address);
    }

    public static (NativeBattleElectrocute275Frame Frame, uint Address) AllocateBonePoint(NativeBattleElectrocute275Frame frame, short bone, short vertex, short width, bool jitter, short intensity, short fade)
    {
        uint working = Working(frame);
        uint cached = BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(working + BoneFreePointerOffset, 4), 4));
        uint point = cached;
        if (point != 0)
        {
            _ = BonePointOffset(frame, point);
            frame = WriteArena(frame, working + BoneFreePointerOffset, BitConverter.GetBytes(0u));
        }
        else
        {
            for (int index = 0; index < PointCount; index++)
            {
                uint candidate = checked(working + BonePointsOffset + (uint)(index * PointStride));
                if (BoneReadWord(frame, candidate) == -1)
                {
                    point = candidate;
                    break;
                }
            }
            if (point == 0)
            {
                return (frame, 0);
            }
        }
        frame = BoneWord(BoneWord(BoneWord(frame, point + 8, 0), point + 6, 0), point + 4, 0);
        for (int axis = 0; axis < 3; axis++)
        {
            byte value = 0;
            if (jitter)
            {
                frame = DrawRandom(frame, out ushort first);
                frame = DrawRandom(frame, out ushort second);
                value = unchecked((byte)(((first + second) >> 10) - 32));
            }
            frame = WriteArena(frame, point + 12 + (uint)axis, [value]);
        }
        frame = WriteArena(frame, point + 15, [unchecked((byte)bone)]);
        frame = BoneWord(frame, point + 2, width);
        frame = BoneWord(frame, point, vertex);
        frame = BoneWord(frame, point + 0x22, fade);
        frame = BoneWord(frame, point + 0x20, intensity);
        frame = WriteArena(frame, point + 0x24, BitConverter.GetBytes(0u));
        return (frame, point);
    }

    public static (NativeBattleElectrocute275Frame Frame, int Result) AdvanceBoneTrail(NativeBattleElectrocute275Frame frame, uint address, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x689280)
        {
            throw new InvalidDataException("Electrocute requires its selected bone trail callback.");
        }
        int steps = unchecked((short)task.ReadWord(12));
        if (steps <= 0)
        {
            throw new NotSupportedException("Electrocute bone trails require their positive native path step count.");
        }
        for (int step = 0; step < steps; step++)
        {
            task = Read(frame, address);
            uint path = task.ReadDword(24);
            if (path == 0)
            {
                continue;
            }
            short bone = BonePathWord(resources, path);
            path = unchecked(path + 2);
            frame = Write(frame, address, task.WriteDword(24, path));
            while (bone < 0)
            {
                task = Read(frame, address);
                (frame, _) = StartBoneTrail(frame, task.ReadDword(36), path, unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(16)), resources);
                task = Read(frame, address);
                path = unchecked(task.ReadDword(24) + (uint)((-1 - bone) * 2));
                bone = BonePathWord(resources, path);
                path = unchecked(path + 2);
                frame = Write(frame, address, task.WriteDword(24, path));
            }
            short vertex = BonePathWord(resources, path);
            path = unchecked(path + 2);
            task = Read(frame, address).WriteDword(24, path);
            frame = Write(frame, address, task);
            short lookahead = BonePathWord(resources, path);
            if (lookahead < 0)
            {
                lookahead = BonePathWord(resources, unchecked(path - (uint)(lookahead * 2)));
            }
            bool jitter = true;
            if (lookahead == short.MaxValue)
            {
                if (unchecked((short)task.ReadWord(14)) <= 3)
                {
                    return (ReleaseBoneChain(frame, task.ReadDword(28)), 2);
                }
                frame = Write(frame, address, task.WriteDword(24, 0));
                jitter = false;
            }
            task = Read(frame, address);
            short width = unchecked((short)task.ReadWord(16));
            frame = DrawRandom(frame, out ushort random);
            short pointWidth = unchecked((short)((unchecked(random * width) >> 15) + width));
            (frame, uint point) = AllocateBonePoint(frame, bone, vertex, pointWidth, jitter, unchecked((short)task.ReadWord(18)), unchecked((short)task.ReadWord(20)));
            if (point == 0)
            {
                return (ReleaseBoneChain(frame, Read(frame, address).ReadDword(28)), 2);
            }
            task = Read(frame, address);
            uint tail = task.ReadDword(32);
            _ = BonePointOffset(frame, tail);
            frame = WriteArena(frame, tail + 0x24, BitConverter.GetBytes(point));
            task = task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))).WriteDword(32, point);
            frame = Write(frame, address, task);
        }
        task = Read(frame, address);
        uint current = task.ReadDword(28);
        HashSet<uint> visited = [];
        while (current != 0)
        {
            int at = BonePointOffset(frame, current);
            if (!visited.Add(current))
            {
                throw new InvalidDataException("Electrocute bone trail contains a cyclic physical chain.");
            }
            for (int axis = 0; axis < 3; axis++)
            {
                sbyte velocity = unchecked((sbyte)Arena(frame).Read(at + 12 + axis, 1)[0]);
                frame = BoneWord(frame, current + 4 + (uint)axis * 2, unchecked((short)(BoneReadWord(frame, current + 4 + (uint)axis * 2) + velocity)));
            }
            short intensity = BoneReadWord(frame, current + 0x20);
            if (intensity > 0)
            {
                intensity = unchecked((short)(intensity + BoneReadWord(frame, current + 0x22)));
                frame = BoneWord(frame, current + 0x20, Math.Max(intensity, (short)0));
            }
            current = BoneReadDword(frame, current + 0x24);
        }
        task = Read(frame, address);
        short count = unchecked((short)task.ReadWord(14));
        if (count >= 2)
        {
            frame = inputs.Execute(frame, new NativeBattleElectrocute275DrawBoneTrail(task.ReadDword(36), task.ReadDword(28), count));
        }
        task = Read(frame, address);
        uint head = task.ReadDword(28);
        while (BoneReadWord(frame, head + 0x20) <= 16)
        {
            uint next = BoneReadDword(frame, head + 0x24);
            if (BoneReadWord(frame, next + 0x20) > 16)
            {
                break;
            }
            frame = WriteArena(frame, Working(frame) + BoneFreePointerOffset, BitConverter.GetBytes(head));
            frame = BoneWord(frame, head, -1);
            task = Read(frame, address).WriteWord(14, unchecked((ushort)(Read(frame, address).ReadWord(14) - 1))).WriteDword(28, next);
            frame = Write(frame, address, task);
            if (unchecked((short)task.ReadWord(14)) < 2)
            {
                return (frame, 2);
            }
            head = next;
        }
        return (frame, 0);
    }

    private static NativeBattleElectrocute275Frame ReleaseBoneChain(NativeBattleElectrocute275Frame frame, uint head)
    {
        frame = WriteArena(frame, Working(frame) + BoneFreePointerOffset, BitConverter.GetBytes(head));
        HashSet<uint> visited = [];
        for (uint point = head; point != 0; point = BoneReadDword(frame, point + 0x24))
        {
            _ = BonePointOffset(frame, point);
            if (!visited.Add(point))
            {
                throw new InvalidDataException("Electrocute bone trail contains a cyclic physical chain.");
            }
            frame = BoneWord(frame, point, -1);
        }
        return frame;
    }

    private static int BonePointOffset(NativeBattleElectrocute275Frame frame, uint address)
    {
        uint bank = checked(Working(frame) + BonePointsOffset);
        uint relative = unchecked(address - bank);
        if (relative >= PointCount * PointStride || relative % PointStride != 0)
        {
            throw new InvalidDataException("Electrocute bone point leaves its retained record bank.");
        }
        return Offset(address, PointStride);
    }

    private static short BonePathWord(NativeBattleElectrocute275Resources resources, uint address)
    {
        uint offset = unchecked(address - BonePathsAddress);
        if (resources.BonePaths.IsDefault || offset > resources.BonePaths.Length - 2)
        {
            throw new InvalidDataException("Electrocute bone path leaves its original addressed source.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(resources.BonePaths.AsSpan().Slice(checked((int)offset), 2));
    }

    private static short BoneReadWord(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint BoneReadDword(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleElectrocute275Frame BoneWord(NativeBattleElectrocute275Frame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
}
