using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Controller
{
    public static NativeBattleEffect54Frame Birth811C80(NativeBattleEffect54Frame frame, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        uint context = frame.State.ReadDword(CurrentContextGlobal);
        uint table = ReadU54(frame, context + 0x224);
        for (int index = 0; index < 16; index++)
        {
            uint source = Dword54(Source(frame, resources, table + (uint)(index * 4), 4), 0);
            if (source == 0 || (ReadW54(frame, context + 0x22) & (1 << index)) != 0)
            {
                continue;
            }
            byte[] descriptor = resources.ReadResource(RequireStaging(frame), source, 0x158);
            if (descriptor[0x11] is not (0 or 1 or 4) || unchecked((sbyte)descriptor[0x13]) != ReadS54(frame, context + 0x18))
            {
                continue;
            }
            if (descriptor[0x37] == 1)
            {
                for (int target = 0; target < ReadS54(frame, context + 0x1C); target++)
                {
                    (frame, _) = NativeBattleEffect54Lower.AllocateEmitter810560(frame, 0, index, target, resources, inputs);
                }
            }
            else
            {
                (frame, _) = NativeBattleEffect54Lower.AllocateEmitter810560(frame, 0, index, 0, resources, inputs);
            }
        }
        return frame;
    }

    public static NativeBattleEffect54Frame Emit810600(NativeBattleEffect54Frame frame, uint node, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        byte[] source = Descriptor54(frame, node, resources, 0x6A);
        if (ReadW54(frame, node + 0x62) == 0)
        {
            frame = InitializeEmitter54(frame, node, source);
            frame = PutW54(frame, node + 0x62, unchecked((ushort)(ReadW54(frame, node + 0x62) + 1)));
        }
        int kind = unchecked((sbyte)ReadB54(frame, node + 0x68));
        if (kind == 3)
        {
            frame = FollowParent54(frame, node, source);
        }
        else if (kind != 0)
        {
            throw new NotSupportedException("Effect54 emitter type is outside its selected source.");
        }
        short age = ReadS54(frame, node + 0x60);
        if (age <= 39)
        {
            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0xC8));
            int count = resources.ReadStatic(unchecked(channel + (uint)age), 1)[0];
            if (count != 0)
            {
                int divisor = unchecked((sbyte)source[0x36]);
                int quotient = count / divisor;
                int remainder = count % divisor;
                for (int index = 0; index < quotient; index++)
                {
                    frame = Spawn810700(frame, node, divisor, source, resources, inputs);
                }
                if (remainder > 0)
                {
                    frame = Spawn810700(frame, node, remainder, source, resources, inputs);
                }
            }
        }
        frame = PutW54(frame, node + 0x60, unchecked((ushort)(age + 1)));
        short remaining = unchecked((short)(ReadS54(frame, node + 0x64) - 1));
        frame = PutW54(frame, node + 0x64, unchecked((ushort)remaining));
        if (remaining <= 0)
        {
            frame = Unlink8117C0(frame, node);
            frame = PutB54(frame, node + 0x69, 0);
            uint context = frame.State.ReadDword(CurrentContextGlobal);
            frame = PutW54(frame, context + 0x14, unchecked((ushort)(ReadW54(frame, context + 0x14) - 1)));
        }
        return frame;
    }

    private static NativeBattleEffect54Frame InitializeEmitter54(NativeBattleEffect54Frame frame, uint node, byte[] source)
    {
        frame = PutB54(frame, node + 0x68, source[0x11]);
        if (source[0x1C] != 0)
        {
            throw new NotSupportedException("Effect54 selected emitter requires its original identity orientation.");
        }
        frame = Identity8DD770(frame, node + 0x2C);
        switch (unchecked((sbyte)source[0x11]))
        {
            case 0:
                int selector = unchecked((sbyte)source[0x25]);
                if (selector is >= 0 and <= 3)
                {
                    int target = unchecked((sbyte)ReadB54(frame, node + 0x6B));
                    uint context = frame.State.ReadDword(CurrentContextGlobal);
                    uint origin = checked(context + 0x94 + (uint)(selector * 64 + target * 16));
                    frame = WriteBytes(frame, node + 0x4C, ReadBytes(frame, origin, 16), 16);
                }
                NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
                for (int lane = 0; lane < 3; lane++)
                {
                    short value = unchecked((short)(BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(lane * 4)) / 65536));
                    point = point.Write(lane * 2, BitConverter.GetBytes(value));
                }
                (frame, NativeBattleVector delta) = TransformVector(frame, Packed(ReadBytes(frame, node + 0x2C, 32)), point);
                frame = AddQ16(frame, node + 0x4C, delta);
                break;
            case 3:
                frame = FollowParent54(frame, node, source);
                break;
            default:
                throw new NotSupportedException("Effect54 emitter initialization leaves its selected source types.");
        }
        return PutW54(frame, node + 0x64, unchecked((ushort)(short)(sbyte)source[0x12]));
    }

    private static NativeBattleEffect54Frame FollowParent54(NativeBattleEffect54Frame frame, uint node, byte[] source)
    {
        uint parent = ReadU54(frame, node + 0x5C);
        if (parent == 0)
        {
            return frame;
        }
        if (source[0x35] == 1)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                int sum = unchecked((int)(ReadU54(frame, parent + 0x11C + (uint)(lane * 4)) + ReadU54(frame, parent + 0xDC + (uint)(lane * 4))));
                frame = PutU54(frame, node + 0x4C + (uint)(lane * 4), unchecked((uint)(sum / 2)));
            }
        }
        else
        {
            frame = WriteBytes(frame, node + 0x4C, ReadBytes(frame, parent + 0xDC, 16), 16);
        }
        if (source[0x1C] != 0)
        {
            throw new NotSupportedException("Effect54 parent orientation is outside its selected descriptor.");
        }
        return frame;
    }

    public static NativeBattleEffect54Frame Link811790(NativeBattleEffect54Frame frame, uint node, short type)
    {
        frame = PutW54(frame, node + 8, unchecked((ushort)type));
        uint context = frame.State.ReadDword(CurrentContextGlobal);
        if (ReadU54(frame, context + 0x2C) == 0)
        {
            frame = PutU54(PutU54(frame, context + 0x2C, node), context + 0x30, node);
        }
        else
        {
            uint tail = ReadU54(frame, context + 0x30);
            frame = PutU54(PutU54(PutU54(frame, context + 0x30, node), node, tail), tail + 4, node);
        }
        return frame;
    }

    public static NativeBattleEffect54Frame Unlink8117C0(NativeBattleEffect54Frame frame, uint node)
    {
        uint context = frame.State.ReadDword(CurrentContextGlobal);
        uint previous = ReadU54(frame, node);
        uint next = ReadU54(frame, node + 4);
        frame = PutU54(frame, previous == 0 ? context + 0x2C : previous + 4, next);
        return PutU54(frame, next == 0 ? context + 0x30 : next, previous);
    }

    private static NativeBattleEffect54Frame AddQ16(NativeBattleEffect54Frame frame, uint address, NativeBattleVector delta)
    {
        short[] values = [delta.X, delta.Y, delta.Z];
        for (int lane = 0; lane < 3; lane++)
        {
            uint at = address + (uint)(lane * 4);
            frame = PutU54(frame, at, unchecked(ReadU54(frame, at) + (uint)(values[lane] << 16)));
        }
        return frame;
    }

    private static byte[] Descriptor54(NativeBattleEffect54Frame frame, uint node, NativeBattleEffect54Resources resources, int indexOffset)
    {
        int index = unchecked((sbyte)ReadB54(frame, node + (uint)indexOffset));
        if (index is < 0 or > 1)
        {
            throw new NotSupportedException("Effect54 node descriptor index is outside its two selected entries.");
        }
        uint context = frame.State.ReadDword(CurrentContextGlobal);
        uint table = ReadU54(frame, context + 0x224);
        uint address = Dword54(Source(frame, resources, table + (uint)(index * 4), 4), 0);
        return resources.ReadResource(RequireStaging(frame), address, 0x158);
    }

    private static uint ReadU54(NativeBattleEffect54Frame frame, uint address) => Dword54(ReadBytes(frame, address, 4), 0);
    private static ushort ReadW54(NativeBattleEffect54Frame frame, uint address) => unchecked((ushort)Word54(ReadBytes(frame, address, 2), 0));
    private static short ReadS54(NativeBattleEffect54Frame frame, uint address) => Word54(ReadBytes(frame, address, 2), 0);
    private static byte ReadB54(NativeBattleEffect54Frame frame, uint address) => ReadBytes(frame, address, 1).Read(0, 1)[0];
    private static NativeBattleEffect54Frame PutU54(NativeBattleEffect54Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    private static NativeBattleEffect54Frame PutW54(NativeBattleEffect54Frame frame, uint address, ushort value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    private static NativeBattleEffect54Frame PutB54(NativeBattleEffect54Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
}
