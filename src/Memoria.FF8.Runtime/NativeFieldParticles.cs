using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeParticlePosition(short X, short Y, short Z);

public sealed record NativeFieldParticleSprite(int PoolIndex, byte TypeIndex, byte StageIndex, short X, short Y, short Z, short Angle, short Width, short Height, byte U, byte V, byte TextureWidth, byte TextureHeight, byte BlendMode, byte Red, byte Green, byte Blue, short DepthOffset, bool DoubleSize);

public sealed record NativeFieldParticlesFrame(NativeFieldParticlesState State, IReadOnlyList<NativeFieldParticleSprite> Sprites);

/// <summary>Physical particle definitions, emitter counters and the shared 128-slot particle pool.</summary>
public sealed record NativeFieldParticlesState([property: JsonRequired] byte[] Memory, [property: JsonRequired] byte[] Emitters, [property: JsonRequired] byte RandomPhase = 0)
{
    public const int DefinitionSize = 10016;
    public const int MemorySize = 14112;

    public static NativeFieldParticlesState Create(ReadOnlySpan<byte> definition)
    {
        if (definition.Length < DefinitionSize)
        {
            throw new InvalidDataException("A native particle definition must contain at least 10016 bytes.");
        }
        byte[] memory = new byte[MemorySize];
        definition[..DefinitionSize].CopyTo(memory);
        for (int index = 0; index < 16; index++)
        {
            int emitter = 5952 + index * 254;
            W16(memory, emitter + 240, 0);
            W16(memory, emitter + 242, 0);
            W16(memory, emitter + 244, 0);
            W16(memory, index * 372 + 348, 0);
            W16(memory, index * 372 + 350, 0);
        }
        return new NativeFieldParticlesState(memory, new byte[16]);
    }

    public NativeFieldParticlesState WithEmitter(int slot, byte value)
    {
        Validate();
        if ((uint)slot >= 16)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        byte[] emitters = (byte[])Emitters.Clone();
        emitters[slot] = value;
        return this with { Emitters = emitters };
    }

    public NativeFieldParticlesFrame Step(byte[] randomTable, Func<int, NativeParticlePosition>? actorPosition = null)
    {
        ValidateRandom(randomTable);
        byte phase = RandomPhase;
        byte NextRandom()
        {
            phase = unchecked((byte)(phase + 13));
            return randomTable[phase];
        }
        NativeFieldParticlesFrame frame = StepCore(NextRandom, actorPosition);
        return frame with { State = frame.State with { RandomPhase = phase } };
    }

    private NativeFieldParticlesFrame StepCore(Func<byte> nextRandom, Func<int, NativeParticlePosition>? actorPosition)
    {
        Validate();
        byte[] memory = (byte[])Memory.Clone();
        AdvanceEmitters(memory, Emitters, nextRandom, actorPosition);
        List<NativeFieldParticleSprite> sprites = [];
        AdvanceParticles(memory, sprites, false);
        return new NativeFieldParticlesFrame(this with { Memory = memory }, sprites);
    }

    public NativeFieldParticlesState WarmUp(byte[] randomTable, Func<int, NativeParticlePosition>? actorPosition = null)
    {
        ValidateRandom(randomTable);
        byte phase = RandomPhase;
        byte NextRandom()
        {
            phase = unchecked((byte)(phase + 13));
            return randomTable[phase];
        }
        return WarmUpCore(NextRandom, actorPosition) with { RandomPhase = phase };
    }

    private NativeFieldParticlesState WarmUpCore(Func<byte> nextRandom, Func<int, NativeParticlePosition>? actorPosition)
    {
        Validate();
        byte[] memory = (byte[])Memory.Clone();
        byte[] emitters = (byte[])Emitters.Clone();
        int count = 0;
        for (int index = 0; index < 16; index++)
        {
            count = Math.Max(count, R16(memory, 5952 + index * 254 + 248));
        }
        for (int frame = 0; frame < count; frame++)
        {
            for (int index = 0; index < 16; index++)
            {
                if (frame > R16(memory, 5952 + index * 254 + 248))
                {
                    emitters[index] = 0;
                }
            }
            AdvanceEmitters(memory, emitters, nextRandom, actorPosition, false);
            AdvanceParticles(memory, null, true);
        }
        return this with { Memory = memory };
    }

    private static void ValidateRandom(byte[] table)
    {
        if (table is null || table.Length != 256)
        {
            throw new InvalidDataException("Native particles require the 256-byte random table.");
        }
    }

    public void Validate()
    {
        if (Memory is null || Memory.Length != MemorySize || Emitters is null || Emitters.Length != 16)
        {
            throw new InvalidDataException("Invalid native field particle state.");
        }
        int[] counts = new int[16];
        for (int index = 0; index < 128; index++)
        {
            int particle = 10016 + index * 32;
            if (Memory[particle + 27] > 1)
            {
                throw new InvalidDataException("Invalid native particle pool status.");
            }
            if (Memory[particle + 27] == 1)
            {
                int type = Memory[particle + 24];
                int stage = Memory[particle + 25];
                if (type >= 16 || stage >= 17)
                {
                    throw new InvalidDataException("Invalid active native particle type or stage.");
                }
                counts[type]++;
            }
        }
        for (int type = 0; type < 16; type++)
        {
            if (R16(Memory, type * 372 + 348) != counts[type])
            {
                throw new InvalidDataException("Native particle occupancy does not match its type counter.");
            }
        }
    }

    public void ValidateDefinition(ReadOnlySpan<byte> definition)
    {
        Validate();
        if (definition.Length < DefinitionSize)
        {
            throw new InvalidDataException("Incomplete native particle definition.");
        }
        for (int offset = 0; offset < DefinitionSize; offset++)
        {
            bool mutable;
            if (offset < 5952)
            {
                int field = offset % 372;
                mutable = field == 334 || field is >= 348 and <= 351;
            }
            else
            {
                int field = (offset - 5952) % 254;
                mutable = field is >= 200 and <= 207 || field is >= 240 and <= 245;
            }
            if (!mutable && Memory[offset] != definition[offset])
            {
                throw new InvalidDataException("Saved native particle definitions differ from the field resource.");
            }
        }
    }

    public void ValidateDrawings(IReadOnlyList<NativeFieldParticleSprite> drawings)
    {
        Validate();
        if (drawings is null || drawings.Count > 128)
        {
            throw new InvalidDataException("Invalid native particle drawing count.");
        }
        HashSet<int> poolIndices = [];
        foreach (NativeFieldParticleSprite sprite in drawings)
        {
            if (sprite is null || (uint)sprite.PoolIndex >= 128 || !poolIndices.Add(sprite.PoolIndex) || sprite.TypeIndex >= 16 || sprite.StageIndex >= 17)
            {
                throw new InvalidDataException("Invalid native particle drawing slot or stage.");
            }
            int definition = sprite.TypeIndex * 372;
            int stage = definition + sprite.StageIndex * 20;
            if (sprite.U != Memory[stage + 10] || sprite.V != Memory[stage + 11] || sprite.TextureWidth != Memory[stage + 12] || sprite.TextureHeight != Memory[stage + 13] || sprite.BlendMode != Memory[stage + 15] || sprite.DepthOffset != R16(Memory, definition + 340) || sprite.DoubleSize != ((R16(Memory, definition + 362) & 0xFF80) != 0))
            {
                throw new InvalidDataException("Saved native particle drawing differs from its field definition.");
            }
        }
    }

    private static void AdvanceEmitters(byte[] memory, byte[] emitters, Func<byte> random, Func<int, NativeParticlePosition>? actorPosition, bool resetDisabled = true)
    {
        for (int slot = 0; slot < 16; slot++)
        {
            int emitter = 5952 + slot * 254;
            if (emitters[slot] == 0)
            {
                if (resetDisabled)
                {
                    W16(memory, emitter + 240, 0);
                    W16(memory, emitter + 242, 0);
                    W16(memory, emitter + 244, 0);
                }
                continue;
            }
            int phase = R16(memory, emitter + 244);
            CheckOffset(phase, 16, "emitter phase");
            if (R16(memory, emitter + 242) >= memory[emitter + 208 + phase])
            {
                W16(memory, emitter + 242, 0);
                phase++;
                CheckOffset(phase, 16, "emitter phase");
                if (memory[emitter + 208 + phase] == 0)
                {
                    phase = 0;
                    W16(memory, emitter + 240, 0);
                }
                W16(memory, emitter + 244, phase);
            }
            for (int lane = 0; lane < 4; lane++)
            {
                int type = R16(memory, emitter + 144 + lane * 18);
                if (type == -1)
                {
                    continue;
                }
                CheckOffset(type, 16, "particle type");
                int rate = memory[emitter + 128 + lane * 18 + phase];
                int count = 0;
                int counter = emitter + 200 + lane * 2;
                if (R16(memory, counter) >= (rate >> 3))
                {
                    W16(memory, counter, 0);
                    count = rate >= 8 ? 1 : rate;
                }
                W16(memory, counter, R16(memory, counter) + 1);
                NativeParticlePosition position;
                if (emitters[slot] == 1)
                {
                    int mode = R16(memory, emitter + 252);
                    position = EmitterPosition(memory, emitter, mode);
                }
                else
                {
                    position = actorPosition?.Invoke(emitters[slot] & 127) ?? throw new NotSupportedException("An actor-bound particle emitter requires an actor position provider.");
                }
                Spawn(memory, type, position, count, random);
            }
            W16(memory, emitter + 240, R16(memory, emitter + 240) + 1);
            W16(memory, emitter + 242, R16(memory, emitter + 242) + 1);
        }
    }

    private static NativeParticlePosition EmitterPosition(byte[] memory, int emitter, int mode)
    {
        if (mode is < 1 or > 3)
        {
            throw new NotSupportedException($"Native particle emitter position mode {mode} is unsupported.");
        }
        int frame = R16(memory, emitter + 240);
        int duration = memory[emitter + 224];
        if (mode != 1 && duration == 0)
        {
            throw new InvalidDataException("A moving particle emitter has a zero interpolation duration.");
        }
        short Coordinate(int axis)
        {
            int start = R16(memory, emitter + axis * 2);
            if (mode == 1)
            {
                return (short)start;
            }
            int middle = R16(memory, emitter + 8 + axis * 2);
            short first = unchecked((short)(start + frame * (middle - start) / duration));
            if (mode == 2)
            {
                return first;
            }
            int end = R16(memory, emitter + 16 + axis * 2);
            short second = unchecked((short)(middle + frame * (end - middle) / duration));
            return unchecked((short)(first + frame * (second - first) / duration));
        }
        return new NativeParticlePosition(Coordinate(0), Coordinate(1), Coordinate(2));
    }

    private static void Spawn(byte[] memory, int type, NativeParticlePosition origin, int count, Func<byte> random)
    {
        int definition = type * 372;
        memory[definition + 334] = 0;
        while (count-- > 0 && R16(memory, definition + 348) < R16(memory, definition + 346))
        {
            int particle = -1;
            for (int index = 0; index < 128; index++)
            {
                if (memory[10016 + index * 32 + 27] == 0)
                {
                    particle = 10016 + index * 32;
                    break;
                }
            }
            if (particle < 0)
            {
                break;
            }
            W16(memory, definition + 348, R16(memory, definition + 348) + 1);
            memory[particle + 24] = (byte)type;
            memory[particle + 27] = 1;
            int spread = R16(memory, definition + 354);
            W16(memory, particle + 18, 16 * (2 * R16(memory, definition + 352) - spread) + (unchecked((short)(32 * spread)) * random() >> 8));
            spread = R16(memory, definition + 356);
            W16(memory, particle + 22, (unchecked((short)(2 * spread)) * random() >> 8) - spread);
            int[] origins = [origin.X, origin.Y, origin.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                spread = axis == 2 ? memory[definition + 362] & 127 : R16(memory, definition + 358 + axis * 2);
                W32(memory, particle + axis * 4, 16 * (origins[axis] + 8 * (2 * unchecked((short)(spread * random() >> 8)) - spread)));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                spread = R16(memory, definition + 364 + axis * 2);
                W16(memory, particle + 12 + axis * 2, (unchecked((short)(32 * spread)) * random() >> 8) - 16 * spread);
            }
            memory[particle + 25] = 0;
            memory[particle + 26] = 0;
        }
    }

    private static void AdvanceParticles(byte[] memory, List<NativeFieldParticleSprite>? sprites, bool warmUp)
    {
        for (int index = 0; index < 128; index++)
        {
            int particle = 10016 + index * 32;
            if (memory[particle + 27] != 1)
            {
                continue;
            }
            int type = memory[particle + 24];
            int stageIndex = memory[particle + 25];
            CheckOffset(type, 16, "particle type");
            CheckOffset(stageIndex, 17, "particle stage");
            int definition = type * 372;
            int stage = definition + stageIndex * 20;
            int age = memory[particle + 26];
            int duration = memory[stage + 14];
            int InterpolateWord(int offset) => R16(memory, stage + offset) + age * (R16(memory, stage + 20 + offset) - R16(memory, stage + offset)) / duration;
            int InterpolateByte(int offset) => memory[stage + offset] + (duration == 0 ? 0 : age * (memory[stage + 20 + offset] - memory[stage + offset]) / duration);
            if (duration != 0)
            {
                W16(memory, particle + 18, R16(memory, particle + 18) + 16 * (InterpolateWord(6) + R16(memory, particle + 22)));
                for (int axis = 0; axis < 3; axis++)
                {
                    W32(memory, particle + axis * 4, R32(memory, particle + axis * 4) + InterpolateWord(axis * 2) + R16(memory, particle + 12 + axis * 2));
                }
            }
            sprites?.Add(new NativeFieldParticleSprite(index, (byte)type, (byte)stageIndex, unchecked((short)(R32(memory, particle) / 16)), unchecked((short)(R32(memory, particle + 4) / 16)), unchecked((short)(R32(memory, particle + 8) / 16)), R16(memory, particle + 18), unchecked((short)InterpolateByte(8)), unchecked((short)InterpolateByte(9)), memory[stage + 10], memory[stage + 11], memory[stage + 12], memory[stage + 13], memory[stage + 15], unchecked((byte)InterpolateByte(16)), unchecked((byte)InterpolateByte(17)), unchecked((byte)InterpolateByte(18)), R16(memory, definition + 340), (R16(memory, definition + 362) & 0xFF80) != 0));
            if (warmUp && duration == 0)
            {
                Retire(memory, particle, definition);
            }
            byte nextAge = unchecked((byte)(age + 1));
            memory[particle + 26] = nextAge;
            if (nextAge >= duration)
            {
                memory[particle + 26] = 0;
                memory[particle + 25]++;
            }
            if (!warmUp)
            {
                int nextStage = memory[particle + 25];
                CheckOffset(nextStage, 17, "particle stage");
                if (memory[definition + nextStage * 20 + 14] == 0)
                {
                    Retire(memory, particle, definition);
                }
            }
        }
    }

    private static void Retire(byte[] memory, int particle, int definition)
    {
        memory[particle + 27] = 0;
        W16(memory, definition + 348, R16(memory, definition + 348) - 1);
    }

    private static void CheckOffset(int value, int count, string name)
    {
        if ((uint)value >= count)
        {
            throw new NotSupportedException($"Native {name} {value} exceeds its record.");
        }
    }

    private static short R16(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));
    private static int R32(byte[] data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
    private static void W16(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset, 2), unchecked((short)value));
    private static void W32(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), value);
}

