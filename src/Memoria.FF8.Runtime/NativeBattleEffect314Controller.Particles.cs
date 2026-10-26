using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314AddressStep SpawnParticle664CD0(NativeBattleEffect314Frame frame, NativeBattleEffect314Resources resources, uint model, uint bones, uint program)
    {
        if (!resources.Programs.TryGetValue(program, out ImmutableArray<byte> bytes))
        {
            throw new NotSupportedException("Effect314 particle creation requires its selected original program.");
        }
        short bone = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0, 2));
        (frame, uint address) = Append(frame, ParticleDescriptor, ParticleCallback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect314 original particle allocation exhausted its physical bank.");
        }
        frame = ClearDwords6DA380(frame, address + 0x14, 9);
        frame = WriteBytes(frame, address + 0xC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(model)).Write(4, BitConverter.GetBytes(bones)).Write(8, BitConverter.GetBytes(unchecked(bones + (uint)(bone << 4)))).Write(12, BitConverter.GetBytes(checked(program + 2))), 16);
        return new(frame, address);
    }

    public static (NativeBattleEffect314Frame Frame, uint Address) AllocateSpark6643D0(NativeBattleEffect314Frame frame, uint main)
    {
        uint first = checked(main + SparkOffset);
        uint selected = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, main + 0xF20, 4).Read(0, 4));
        uint limit = checked(first + SparkCapacity * SparkStride);
        if (selected < first || selected >= limit || (selected - first) % SparkStride != 0)
        {
            throw new InvalidDataException("Effect314 spark allocation hint leaves its physical ring.");
        }
        bool Available(uint candidate) => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, candidate + 6, 2).Read(0, 2)) == 0;
        if (!Available(selected))
        {
            selected = first;
            while (!Available(selected))
            {
                selected += SparkStride;
                if (selected == limit)
                {
                    return (frame, 0);
                }
            }
        }
        uint next = selected >= limit - SparkStride ? first : selected + SparkStride;
        frame = WriteBytes(frame, main + 0xF20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 4);
        return (frame, selected);
    }
}
