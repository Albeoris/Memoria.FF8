using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed record BattleSequenceSound(int AnimationIndex, int SoundId, bool ActorSound, int Attribute);
public sealed record BattleSequence(IReadOnlyList<int> Animations, IReadOnlyList<BattleSequenceSound> Sounds, bool HasUnresolvedBranches = false);

/// <summary>Static pose/sound projection of original model sequence bytecode, not an execution of its camera, effect or movement services.</summary>
public sealed partial class BattleSequenceSet
{
    private readonly byte[] bytes;
    private readonly Dictionary<int, BattleSequence> decoded = [];

    public Memoria.FF8.Runtime.NativeBattleGraphicsResource NativeResource => new(System.Collections.Immutable.ImmutableArray.CreateRange(bytes));

    public BattleSequenceSet(byte[] bytes)
    {
        if (bytes.Length < 2 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) * 2 + 2 > bytes.Length)
        {
            throw new InvalidDataException("Original battle sequence directory exceeds its section.");
        }
        this.bytes = bytes.ToArray();
    }

    public bool Contains(int sequence) => sequence > 0 && sequence <= BinaryPrimitives.ReadUInt16LittleEndian(bytes) && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(sequence * 2)) != 0;

    public BattleSequence Read(int sequence)
    {
        if (!Contains(sequence))
        {
            throw new NotSupportedException($"Original battle model has no sequence {sequence}.");
        }
        if (decoded.TryGetValue(sequence, out BattleSequence? cached))
        {
            return cached;
        }
        List<int> animations = [];
        List<BattleSequenceSound> sounds = [];
        bool conditional = false;
        Project(sequence, animations, sounds, [], ref conditional);
        // TODO(TD-009): A static pose palette does not resolve native branch predicates. Suppress uncertain audio rather than playing mutually exclusive branches.
        BattleSequence result = new(animations.ToArray(), conditional ? [] : sounds.ToArray(), conditional);
        decoded.Add(sequence, result);
        return result;
    }

    private void Project(int sequence, List<int> animations, List<BattleSequenceSound> sounds, HashSet<int> visited, ref bool conditional)
    {
        if (!visited.Add(sequence))
        {
            throw new NotSupportedException("Recursive original battle sequence projection is unsupported.");
        }
        int position = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(sequence * 2));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        int end = bytes.Length;
        for (int index = 1; index <= count; index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2));
            if (offset > position)
            {
                end = Math.Min(end, offset);
            }
        }
        while (position < end)
        {
            int opcode = bytes[position++];
            if (opcode < 0x80)
            {
                AddAnimation(opcode);
                continue;
            }
            if (opcode >= 0xC0)
            {
                if (opcode > 0xF3)
                {
                    throw new NotSupportedException($"Original battle sequence {sequence} expression opcode 0x{opcode:X2} is unsupported.");
                }
                conditional |= opcode is >= 0xE6 and <= 0xF3;
                // Native 50DB40 expression and relative-branch operands. They are data, never animation opcodes.
                Skip(opcode >= 0xE4 ? opcode == 0xE4 ? 0 : opcode >= 0xED && opcode <= 0xF3 ? 2 : 1 : (opcode & 3) == 0 ? 2 : 1);
                continue;
            }
            switch (opcode)
            {
                case 0xA0:
                    AddAnimation(ReadByte());
                    break;
                case 0xA2:
                case 0xAC:
                case 0xA9:
                    return;
                case 0xA3:
                    // Completion transitions back to the model's standing/status sequence in the native VM.
                    return;
                case 0xA7:
                    int target = ReadByte();
                    if (!Contains(target))
                    {
                        throw new NotSupportedException($"Original battle sequence {sequence} jumps to absent sequence {target}.");
                    }
                    Project(target, animations, sounds, visited, ref conditional);
                    return;
                case 0x97:
                case 0x98:
                case 0xB5:
                case 0xB6:
                case 0xB8:
                    int sound = ReadByte();
                    int flags = ReadByte();
                    int attribute = (flags & 2) != 0 ? ReadByte() << 8 : 0;
                    if (opcode is 0xB5 or 0xB6 or 0xB8)
                    {
                        sounds.Add(new BattleSequenceSound(animations.Count, sound, opcode != 0xB8, attribute));
                    }
                    break;
                case 0x9F:
                    SkipTerminated();
                    break;
                case 0x99:
                case 0xB1:
                    Skip(1);
                    SkipTerminated();
                    break;
                case 0xAB:
                    Skip(1);
                    while (ReadByte() != 0xA1)
                    {
                    }
                    break;
                case 0xB0:
                case 0xB4:
                    // Original 505900 effect packet has a flag-controlled operand layout.
                    Skip(1);
                    int packet = ReadByte();
                    Skip(((packet & 1) != 0 ? 1 : 0) + ((packet & 2) != 0 ? 1 : 0) + ((packet & 4) != 0 ? 1 : 0));
                    Skip((packet & 8) != 0 ? 1 : (packet & 0x10) != 0 ? 0 : (packet & 0x40) != 0 ? 6 : 0);
                    break;
                default:
                    Skip(opcode switch
                    {
                        0x80 or 0x81 or 0x83 or 0x84 or 0x90 or 0x92 or 0x9A or 0xA5 or 0xA8 or 0xAF or 0xB9 or 0xBC or 0xBE or 0xBF => 1,
                        0x91 or 0x93 or 0x9B or 0x9E or 0xBD => 2,
                        0x96 => 3,
                        0xAD => 4,
                        0xAE => 5,
                        0xA6 => 9,
                        0x82 or 0x85 or 0x86 or 0x94 or 0x95 or 0x9C or 0x9D or 0xA1 or 0xA4 or 0xAA or 0xB2 or 0xB7 or 0xBA or 0xBB => 0,
                        _ => throw new NotSupportedException($"Original battle sequence {sequence} opcode 0x{opcode:X2} has no projection contract.")
                    });
                    break;
            }
        }

        void AddAnimation(int animation)
        {
            if (animations.Count == 0 || animations[^1] != animation)
            {
                animations.Add(animation);
            }
        }

        int ReadByte()
        {
            if (position >= end)
            {
                throw new InvalidDataException($"Original battle sequence {sequence} has a truncated operand.");
            }
            return bytes[position++];
        }

        void Skip(int length)
        {
            for (int index = 0; index < length; index++)
            {
                ReadByte();
            }
        }

        void SkipTerminated()
        {
            while (ReadByte() != 0xFF)
            {
            }
        }
    }
}
