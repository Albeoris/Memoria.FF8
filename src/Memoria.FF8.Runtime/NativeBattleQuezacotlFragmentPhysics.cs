using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlFragmentMotion(ImmutableArray<byte> Task44, int Returned);

/// <summary>6C6707's post-draw WORD motion, signed-BYTE angular velocity and signed age comparison.</summary>
public static class NativeBattleQuezacotlFragmentPhysics
{
    public static NativeBattleQuezacotlFragmentMotion Advance6C6707(ImmutableArray<byte> task, bool paused)
    {
        if (task.IsDefault || task.Length != NativeBattleQuezacotlFragmentPool.Stride)
        {
            throw new InvalidDataException("Native6C6707 requires its complete current2C-byte task.");
        }
        if (paused)
        {
            return new(task, 0);
        }
        byte[] bytes = task.ToArray();
        Word(0x20, unchecked((short)(Read(0x20) + (sbyte)bytes[0x28])));
        Word(0x24, unchecked((short)(Read(0x24) + (sbyte)bytes[0x2A])));
        Word(0x1A, unchecked((short)(Read(0x1A) + 10)));
        Word(0x22, unchecked((short)(Read(0x22) + (sbyte)bytes[0x29])));
        Word(0x10, unchecked((short)(Read(0x10) + Read(0x18))));
        Word(0x12, unchecked((short)(Read(0x12) + Read(0x1A))));
        Word(0x14, unchecked((short)(Read(0x14) + Read(0x1C))));
        if (Read(0x12) > 0)
        {
            Word(0x12, 0);
            Word(0x1A, unchecked((short)-Read(0x1A)));
        }
        short age = unchecked((short)(Read(0x0C) + 1));
        Word(0x0C, age);
        return new(bytes.ToImmutableArray(), age < 30 ? 0 : 2);

        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));

        void Word(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
