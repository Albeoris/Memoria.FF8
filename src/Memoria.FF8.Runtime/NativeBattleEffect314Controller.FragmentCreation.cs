using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static (NativeBattleEffect314Frame Frame, uint Address) AllocateFragment664B90(NativeBattleEffect314Frame frame, uint main)
    {
        uint first = checked(main + FragmentOffset);
        uint limit = checked(first + FragmentCapacity * FragmentStride);
        uint selected = unchecked((uint)I32(ReadBytes(frame, main + 0x4724, 4), 0));
        if (selected < first || selected >= limit || (selected - first) % FragmentStride != 0)
        {
            throw new InvalidDataException("Effect314 fragment hint leaves its physical bank.");
        }
        bool Available(uint candidate) => U16(ReadBytes(frame, candidate + 6, 2), 0) == 0;
        if (!Available(selected))
        {
            selected = first;
            while (!Available(selected))
            {
                selected += FragmentStride;
                if (selected == limit)
                {
                    return (frame, 0);
                }
            }
        }
        uint next = selected >= limit - FragmentStride ? first : selected + FragmentStride;
        frame = WriteBytes(frame, main + 0x4724, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 4);
        return (frame, selected);
    }

    public static (NativeBattleEffect314Frame Frame, bool CapacityAvailable) CreateFragment6646A0(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint main, uint faceAddress, NativeBattleEffectArena face, ImmutableArray<NativeBattleEffectArena> vertices, uint rotationBank, short velocityMask, ushort texturePageAddition, ushort clutAddition)
    {
        int count = vertices.Length;
        if (count is not (3 or 4))
        {
            throw new InvalidDataException("Effect314 fragment requires its actual triangle or quad corners.");
        }
        if ((face.Read(15, 1)[0] & 0x80) != 0)
        {
            return (frame, true);
        }
        ushort flag = U16(vertices[0], 6);
        int combined = 0xFF;
        foreach (NativeBattleEffectArena vertex in vertices)
        {
            combined &= vertex.Read(6, 1)[0];
            if (U16(vertex, 6) != flag)
            {
                return (frame, true);
            }
        }
        if (combined == 0)
        {
            return (frame, true);
        }
        (frame, uint address) = AllocateFragment664B90(frame, main);
        if (address == 0)
        {
            return (frame, false);
        }
        NativeBattleEffectArena row = ReadBytes(frame, address, FragmentStride);
        int[] center = new int[3];
        for (int axis = 0; axis < 3; axis++)
        {
            center[axis] = vertices.Sum(vertex => (int)I16(vertex, axis * 2)) / count;
            row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)center[axis])));
        }
        row = row.Write(6, BitConverter.GetBytes((ushort)(count == 3 ? 1 : 2)));
        for (int corner = 0; corner < count; corner++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                row = row.Write(0x14 + corner * 6 + axis * 2, BitConverter.GetBytes(unchecked((short)(I16(vertices[corner], axis * 2) - center[axis]))));
            }
        }
        int Draw()
        {
            uint next = unchecked(frame.State.ReadDword(RandomGlobal) * 125 + 14) & 0x7FFF;
            frame = frame with { State = frame.State.WriteDword(RandomGlobal, next) };
            return (int)next;
        }
        int velocityX = (Draw() & velocityMask) - (velocityMask >> 1);
        row = row.Write(8, BitConverter.GetBytes(unchecked((short)(count == 3 ? velocityX >> 2 : velocityX))));
        row = row.Write(12, BitConverter.GetBytes(unchecked((short)-((Draw() & velocityMask) + (velocityMask >> 1)))));
        int velocityY = -(Draw() & velocityMask);
        row = row.Write(10, BitConverter.GetBytes(unchecked((short)(count == 3 ? velocityY >> 1 : velocityY))));
        row = row.Write(14, BitConverter.GetBytes((ushort)((Draw() & 7) + 4)));
        row = NativeBattleEffectNodeContextMemory.Copy(face, 8, row, 0x2C, 8);
        row = NativeBattleEffectNodeContextMemory.Copy(face, count == 3 ? 6 : 16, row, 0x34, count == 3 ? 2 : 4);
        row = row.Write(0x32, BitConverter.GetBytes(unchecked((ushort)(U16(row, 0x32) + texturePageAddition))));
        row = row.Write(0x2E, BitConverter.GetBytes(unchecked((ushort)(U16(row, 0x2E) + clutAddition))));
        row = row.Write(0x10, BitConverter.GetBytes(checked(rotationBank + (uint)((Draw() & 63) * 20))));
        frame = WriteBytes(frame, address, row, FragmentStride);
        face = face.Write(15, [(byte)(face.Read(15, 1)[0] | 0x80)]);
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(faceAddress + 15, NativeBattleEffectArena.Clear().Write(0, face.Read(15, 1)), 1));
        if (Draw() < 0x2000)
        {
            (frame, uint spark) = AllocateSpark6643D0(frame, main);
            if (spark != 0)
            {
                NativeBattleEffectArena particle = NativeBattleEffectNodeContextMemory.Copy(row, 0, ReadBytes(frame, spark, SparkStride), 0, 8);
                particle = particle.Write(12, new byte[4]).Write(6, BitConverter.GetBytes((short)11)).Write(8, BitConverter.GetBytes((short)4096));
                particle = particle.Write(13, [unchecked((byte)-(Draw() & 7))]);
                particle = particle.Write(14, [unchecked((byte)(-64 - (Draw() & 31)))]).Write(15, [1]);
                frame = WriteBytes(frame, spark, particle, SparkStride);
            }
        }
        return (frame, true);
    }
}
