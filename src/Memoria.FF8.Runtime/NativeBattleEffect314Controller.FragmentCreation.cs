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
        return CreateFragment6646A0(frame, inputs, main, face, vertices, rotationBank, velocityMask, texturePageAddition, clutAddition, (current, value) => inputs.Execute(current, new NativeBattleEffect314WriteArena(checked(faceAddress + 15), NativeBattleEffectArena.Clear().Write(0, [value]), 1)));
    }

    public static (NativeBattleEffect314Frame Frame, bool CapacityAvailable) CreateFragment6646A0(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint main, NativeBattleEffectArena face, ImmutableArray<NativeBattleEffectArena> vertices, uint rotationBank, short velocityMask, ushort texturePageAddition, ushort clutAddition, Func<NativeBattleEffect314Frame, byte, NativeBattleEffect314Frame> writeMarker)
    {
        return CreateFragmentCore(frame, inputs, main, new FragmentReadInputs(face, vertices), rotationBank, velocityMask, texturePageAddition, clutAddition, writeMarker);
    }

    internal static (NativeBattleEffect314Frame Frame, bool CapacityAvailable) CreateCapturedFragment6646A0(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint main, NativeBattleEffect314ReadOnlyCapture face, ReadOnlySpan<NativeBattleEffect314ReadOnlyCapture> vertices, uint rotationBank, short velocityMask, ushort texturePageAddition, ushort clutAddition, Func<NativeBattleEffect314Frame, byte, NativeBattleEffect314Frame> writeMarker)
    {
        return CreateFragmentCore(frame, inputs, main, new FragmentReadInputs(face, vertices), rotationBank, velocityMask, texturePageAddition, clutAddition, writeMarker);
    }

    private static (NativeBattleEffect314Frame Frame, bool CapacityAvailable) CreateFragmentCore(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint main, FragmentReadInputs fragment, uint rotationBank, short velocityMask, ushort texturePageAddition, ushort clutAddition, Func<NativeBattleEffect314Frame, byte, NativeBattleEffect314Frame> writeMarker)
    {
        int count = fragment.VertexCount;
        if (count is not (3 or 4))
        {
            throw new InvalidDataException("Effect314 fragment requires its actual triangle or quad corners.");
        }
        if ((fragment.FaceByte(15) & 0x80) != 0)
        {
            return (frame, true);
        }
        ushort flag = fragment.VertexUInt16(0, 6);
        int combined = 0xFF;
        for (int corner = 0; corner < count; corner++)
        {
            combined &= fragment.VertexByte(corner, 6);
            if (fragment.VertexUInt16(corner, 6) != flag)
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
            int sum = 0;
            for (int corner = 0; corner < count; corner++)
            {
                sum += fragment.VertexInt16(corner, axis * 2);
            }
            center[axis] = sum / count;
            row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)center[axis])));
        }
        row = row.Write(6, BitConverter.GetBytes((ushort)(count == 3 ? 1 : 2)));
        for (int corner = 0; corner < count; corner++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                row = row.Write(0x14 + corner * 6 + axis * 2, BitConverter.GetBytes(unchecked((short)(fragment.VertexInt16(corner, axis * 2) - center[axis]))));
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
        row = fragment.CopyFace(8, row, 0x2C, 8);
        row = fragment.CopyFace(count == 3 ? 6 : 16, row, 0x34, count == 3 ? 2 : 4);
        row = row.Write(0x32, BitConverter.GetBytes(unchecked((ushort)(U16(row, 0x32) + texturePageAddition))));
        row = row.Write(0x2E, BitConverter.GetBytes(unchecked((ushort)(U16(row, 0x2E) + clutAddition))));
        row = row.Write(0x10, BitConverter.GetBytes(checked(rotationBank + (uint)((Draw() & 63) * 20))));
        frame = WriteBytes(frame, address, row, FragmentStride);
        if (frame.CapturePresentation)
        {
            NativeBattleEffect32Presentation.Allocated(frame.Effect, main, main + FragmentOffset, checked((int)((address - main - FragmentOffset) / FragmentStride)));
        }
        frame = writeMarker(frame, fragment.MarkFace());
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
                if (frame.CapturePresentation)
                {
                    NativeBattleEffect32Presentation.Allocated(frame.Effect, main, main + SparkOffset, checked((int)((spark - main - SparkOffset) / SparkStride)));
                }
            }
        }
        return (frame, true);
    }

    private readonly ref struct FragmentReadInputs
    {
        private readonly bool captured;
        private readonly NativeBattleEffectArena face;
        private readonly ImmutableArray<NativeBattleEffectArena> vertices;
        private readonly NativeBattleEffect314ReadOnlyCapture capturedFace;
        private readonly ReadOnlySpan<NativeBattleEffect314ReadOnlyCapture> capturedVertices;

        internal FragmentReadInputs(NativeBattleEffectArena face, ImmutableArray<NativeBattleEffectArena> vertices)
        {
            captured = false;
            this.face = face;
            this.vertices = vertices;
            capturedFace = default;
            capturedVertices = default;
        }

        internal FragmentReadInputs(NativeBattleEffect314ReadOnlyCapture face, ReadOnlySpan<NativeBattleEffect314ReadOnlyCapture> vertices)
        {
            captured = true;
            this.face = null!;
            this.vertices = default;
            capturedFace = face;
            capturedVertices = vertices;
        }

        internal int VertexCount => captured ? capturedVertices.Length : vertices.Length;
        internal byte FaceByte(int offset) => captured ? capturedFace.ReadByte(offset) : face.Read(offset, 1)[0];
        internal byte VertexByte(int corner, int offset) => captured ? capturedVertices[corner].ReadByte(offset) : vertices[corner].Read(offset, 1)[0];
        internal ushort VertexUInt16(int corner, int offset) => captured ? capturedVertices[corner].ReadUInt16(offset) : U16(vertices[corner], offset);
        internal short VertexInt16(int corner, int offset) => captured ? capturedVertices[corner].ReadInt16(offset) : I16(vertices[corner], offset);

        internal NativeBattleEffectArena CopyFace(int offset, NativeBattleEffectArena destination, int destinationOffset, int count)
        {
            return captured ? capturedFace.CopyTo(offset, destination, destinationOffset, count) : NativeBattleEffectNodeContextMemory.Copy(face, offset, destination, destinationOffset, count);
        }

        internal byte MarkFace()
        {
            byte marked = (byte)(FaceByte(15) | 0x80);
            return captured ? marked : face.Write(15, [marked]).Read(15, 1)[0];
        }
    }
}
