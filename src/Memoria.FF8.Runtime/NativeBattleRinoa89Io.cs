using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89IoEvents(short Age) : NativeBattleRinoa89Operation;

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame QueueBodyIo6FA780(NativeBattleRinoa89Frame frame, short age)
    {
        NativeBattleEffectMemory memory = frame.Memory;
        switch (unchecked((ushort)age))
        {
            case 0:
                Queue(758, 1);
                Queue(759, 1);
                Queue(760, 5);
                break;
            case 76:
                Queue(761, 1);
                Queue(762, 1);
                Queue(763, 1);
                break;
            case 101:
                Queue(764, 5);
                break;
            case 227:
                Queue(360, 1);
                Queue(361, 1);
                Queue(362, 1);
                break;
        }
        frame = frame with { Memory = memory };
        if (age == 74)
        {
            Enqueue(48132);
            Enqueue(56868);
            Enqueue(65604);
            Enqueue(70244);
        }
        else if (age == 173)
        {
            Enqueue(48132);
            Enqueue(65572);
        }
        return frame;

        void Queue(short file, byte kind)
        {
            memory = NativeBattleEffectIo.Queue5341D0(memory, file, ArenaAddress + 48132, kind);
        }
        void Enqueue(int offset)
        {
            NativeBattleEffectArena arena = RequireArena(frame);
            uint tag = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
            uint flags = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 4, 4));
            if (tag != 16 || (flags & 8) == 0)
            {
                throw new InvalidDataException("Native6FA780 requires its actual paletted scene TIM at the selected arena offset.");
            }
            int first = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 8, 4));
            int second = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked(offset + 8 + first), 4));
            NativeBattleEffectHeapAllocation source = new("native-limit89-scene-texture", arena.Read(offset, checked(8 + first + second)).ToImmutableArray());
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native6FA780 requires its retained texture queue.");
            (textures, _) = textures.EnqueueTim505E30(source);
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
        }
    }
}
