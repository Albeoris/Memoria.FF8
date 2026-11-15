using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The original eight-byte parameter window captured by its publishing writer.</summary>
internal sealed record NativeBattleEffectParameterWindow(ImmutableArray<byte> Bytes, byte KnownMask)
{
    internal const uint Address = 0x1D99A78;
    internal const int Length = 8;
    internal const uint HeaderBase = 0x1D280C4;
    internal const int HeaderSize = 20;

    internal static NativeBattleEffectParameterWindow Capture(NativeBattleEffectParameters parameters, NativeBattleModelAliases aliases)
    {
        byte[] bytes = new byte[Length];
        byte known = 0x02;
        bytes[1] = parameters.Flags79;
        if (aliases.TryEncodeExact(parameters.Source, out byte source))
        {
            bytes[0] = source;
            known |= 0x01;
        }
        ushort mask = 0;
        bool targetsEncodable = true;
        foreach (BattleModelOwner target in parameters.TargetActors.Enumerate())
        {
            if (!aliases.TryEncodeExact(target, out byte operand))
            {
                targetsEncodable = false;
                break;
            }
            mask |= (ushort)(1 << operand);
        }
        if (targetsEncodable)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), mask);
            known |= 0x0C;
        }
        if (parameters.Header.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity)
        {
            uint pointer = checked(HeaderBase + (uint)parameters.Header.Ordinal * HeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), pointer);
            known |= 0xF0;
        }
        return new([.. bytes], known);
    }

    internal byte ReadByte(int offset)
    {
        if ((uint)offset >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if ((KnownMask & (1 << offset)) == 0)
        {
            throw new NotSupportedException("The original effect parameter byte has no exact writer projection.");
        }
        return Bytes[offset];
    }
}
