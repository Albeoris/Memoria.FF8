using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAnimatedAnchorChannels(byte StartActor, byte StartAnchor, NativeBattleVector StartOffset, byte EndActor, byte EndAnchor, NativeBattleVector EndOffset, short Ratio);
public sealed record NativeBattleAnimatedAnchorPosition(int X, int Y, int Z);

/// <summary>Normalized8822A0/8921B0/887760's two channel-selected anchors, rotated S16 offsets and unchecked Q12-to-Q16 interpolation.</summary>
public static class NativeBattleEffectAnimatedAnchor
{
    public static NativeBattleAnimatedAnchorPosition Evaluate(NativeBattleAnimatedAnchorChannels channels, int targetIndex, NativeBattleActionMatrix matrix, Func<int, uint> readContext)
    {
        NativeBattleVector first = Anchor(channels.StartActor, channels.StartAnchor, channels.StartOffset);
        NativeBattleVector second = Anchor(channels.EndActor, channels.EndAnchor, channels.EndOffset);
        return new(Interpolate(first.X, second.X), Interpolate(first.Y, second.Y), Interpolate(first.Z, second.Z));

        int Interpolate(short start, short end) => unchecked(((end - start) * channels.Ratio + (start << 12)) << 4);
        NativeBattleVector Anchor(byte actor, byte selector, NativeBattleVector offset)
        {
            if (actor > 1 || selector > 3)
            {
                throw new NotSupportedException("Native animated anchor requires its observed prior stack VECTOR for an out-of-range selector.");
            }
            int context = actor == 1 ? 0x1D4 + selector * 16 : 0x94 + targetIndex * 16 + selector * 0x40;
            NativeBattleVector rotated = NativeBattleCuragaMatrices.RotateVector45DF80(matrix, offset);
            short x = unchecked((short)((int)readContext(context) / 65536));
            short y = unchecked((short)((int)readContext(context + 4) / 65536));
            short z = unchecked((short)((int)readContext(context + 8) / 65536));
            return new(unchecked((short)(x + rotated.X)), unchecked((short)(y + rotated.Y)), unchecked((short)(z + rotated.Z)));
        }
    }

    public static NativeBattleAnimatedAnchorChannels ReadChannels(byte[] source, short frame, Func<uint, int, byte[]> readBank)
    {
        if (source.Length < 0x12C)
        {
            throw new InvalidDataException("Native animated anchor requires its original source channel pointers.");
        }
        byte firstAnchor = Byte(0x108);
        byte firstActor = Byte(0x100);
        NativeBattleVector firstOffset = new(Word(0x110), Word(0x114), Word(0x118));
        byte secondActor = Byte(0x104);
        byte secondAnchor = Byte(0x10C);
        NativeBattleVector secondOffset = new(Word(0x11C), Word(0x120), Word(0x124));
        short ratio = Word(0x128);
        return new(firstActor, firstAnchor, firstOffset, secondActor, secondAnchor, secondOffset, ratio);

        uint Pointer(int field) => BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(field));
        byte Byte(int field) => readBank(unchecked(Pointer(field) + (uint)(int)frame), 1)[0];
        short Word(int field) => BinaryPrimitives.ReadInt16LittleEndian(readBank(unchecked(Pointer(field) + (uint)(frame * 2)), 2));
    }

    internal static void Advance(NativeBattleEffectNodeMemory memory, int node, byte[] source, Func<uint, int, byte[]> readBank)
    {
        short frame = unchecked((short)memory.Word(node + 0x60));
        NativeBattleAnimatedAnchorChannels channels = ReadChannels(source, frame, readBank);
        System.Collections.Immutable.ImmutableArray<short>.Builder rotation = System.Collections.Immutable.ImmutableArray.CreateBuilder<short>(9);
        for (int index = 0; index < 9; index++)
        {
            rotation.Add(unchecked((short)memory.Word(node + 0x2C + index * 2)));
        }
        NativeBattleActionMatrix matrix = new(rotation.ToImmutable(), 0, 0, 0, 4096);
        NativeBattleAnimatedAnchorPosition position = Evaluate(channels, unchecked((sbyte)memory.Byte(node + 0x6B)), matrix, memory.ContextDword);
        memory.Dword(node + 0x4C, unchecked((uint)position.X));
        memory.Dword(node + 0x50, unchecked((uint)position.Y));
        memory.Dword(node + 0x54, unchecked((uint)position.Z));
    }
}
