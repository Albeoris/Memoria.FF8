using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterParticleServices(Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> ReadMetadata, Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveMatrixRow, Func<NativeBattleWaterExecutionFrame, int, NativeBattleWaterExecutionFrame> PrepareTexturePage, Func<NativeBattleWaterExecutionFrame, int, ushort> ReadClut, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, NativeBattleWaterExecutionFrame> DrawRectangles);

/// <summary>The finite particle instruction family and its selected type-six caller over current arena records.</summary>
public static class NativeBattleWaterParticles
{
    public static NativeBattleWaterExecutionFrame ExecuteOne(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, NativeBattleWaterParticleServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        uint pc = CoreDword(frame, 0x2797450);
        int next = 2;
        switch ((byte)instruction)
        {
            case 0:
                frame = ParticleWord(frame, 0, 0);
                frame = frame.WriteTemporaryWord(0x13E, unchecked((ushort)frame.ReadTemporaryDword(0x24)));
                frame = WriteCoreDword(frame, 0x2797450, 0);
                frame = ParticleDword(frame, 0x30, 0);
                return ParticleDword(frame, 8, 0);
            case 1:
                frame = frame.WriteTemporaryWord(0x13E, (ushort)((instruction >> 8) << 7));
                break;
            case 4:
            case 5:
                int group = instruction & 0x300;
                if (group == 0x300)
                {
                    throw new NotSupportedException("Native particle arithmetic requires its selected destination groups.");
                }
                int destination = group == 0 ? 0x10 : group == 0x100 ? 0x20 : 0x28;
                int stride = group == 0 ? 4 : 2;
                int shift = group == 0 ? 8 : 0;
                for (int lane = 0; lane < 4; lane++)
                {
                    if ((instruction & (0x8000 >> lane)) == 0)
                    {
                        continue;
                    }
                    short value = unchecked((short)ProgramWord(frame, program, unchecked(pc + (uint)next)));
                    next += 2;
                    int scalar;
                    if ((byte)instruction == 4)
                    {
                        if (value == 0x7654)
                        {
                            continue;
                        }
                        scalar = value;
                    }
                    else
                    {
                        if (value == 0)
                        {
                            continue;
                        }
                        NativeBattleWaterVectorRandom sampled = NativeBattleWaterVectorCommands.RandomB65740(value, frame.Random);
                        frame = frame with { Random = sampled.Random };
                        scalar = sampled.Value;
                    }
                    int offset = destination + stride * lane;
                    if (stride == 4)
                    {
                        uint output = unchecked((uint)(scalar << shift));
                        if ((byte)instruction == 5)
                        {
                            output = unchecked(ReadParticleDword(frame, offset) + output);
                        }
                        frame = ParticleDword(frame, offset, output);
                    }
                    else
                    {
                        ushort output = unchecked((ushort)(scalar << shift));
                        if ((byte)instruction == 5)
                        {
                            output = unchecked((ushort)(ReadParticleWord(frame, offset) + output));
                        }
                        frame = ParticleWord(frame, offset, output);
                    }
                }
                break;
            case 7:
                int sequenceIndex = instruction >> 8;
                if (sequenceIndex > 1)
                {
                    throw new NotSupportedException("Native particle sequence selection requires its two proved indices.");
                }
                frame = ParticleByte(frame, 0x4A, 0);
                uint manager = CoreDword(frame, 0x27973EC);
                uint map = CoreDword(frame, checked(manager + 0xAC));
                byte resourceIndex = ReadAddress(frame, unchecked(map + (uint)sequenceIndex), 1, services)[0];
                if (resourceIndex >= 100)
                {
                    throw new NotSupportedException("Native particle resource mapping leaves its physical pointer array.");
                }
                uint resource = CoreDword(frame, 0x2798A68 + (uint)(4 * resourceIndex));
                uint directory = unchecked(resource + ReadArenaDword(frame, unchecked(resource + 0xC)));
                frame = frame.WriteTemporaryDword(0xFC, directory);
                uint sequence = unchecked(directory + ReadArenaDword(frame, unchecked(directory + 4u + 4u * (uint)sequenceIndex)));
                frame = ParticleDword(frame, 8, sequence);
                frame = ParticleDword(frame, 0xC, sequence);
                frame = ParticleDword(frame, 0x4C, frame.ReadTemporaryDword(0xFC));
                frame = ParticleDword(frame, 0x30, unchecked(directory + ReadArenaDword(frame, sequence)));
                frame = ParticleWord(frame, 0x34, SlotWord(frame, 0xCA));
                break;
            case 0x0B:
            case 0x0C:
                int color = (byte)instruction == 0x0B ? 0x38 : 0x3C;
                frame = ParticleByte(frame, color, (byte)(instruction >> 8));
                ushort colors = ProgramWord(frame, program, unchecked(pc + 2));
                frame = ParticleByte(frame, color + 1, (byte)colors);
                frame = ParticleByte(frame, color + 2, (byte)(colors >> 8));
                next = 4;
                break;
            case 0x11:
                frame = ParticleWord(frame, 0x34, 0x7F80);
                break;
            case 0x12:
                short selector = unchecked((short)ProgramWord(frame, program, unchecked(pc + 2)));
                int current = CurrentIndex(frame, bank);
                int reference = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, current, ActiveOrder(frame), selector);
                int referenceOffset = bank.SlotOffset(frame.Arena, reference);
                int mask = unchecked((ushort)(instruction << 1));
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((mask & (0x8000 >> lane)) == 0)
                    {
                        continue;
                    }
                    short operand = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(referenceOffset + 0x94 + 2 * lane, 2));
                    if (operand != 0)
                    {
                        NativeBattleWaterVectorRandom sampled = NativeBattleWaterVectorCommands.RandomB65740(operand, frame.Random);
                        frame = frame with { Random = sampled.Random };
                        int offset = 0x14 + 4 * lane;
                        frame = ParticleDword(frame, offset, unchecked(ReadParticleDword(frame, offset) + (uint)(sampled.Value << 8)));
                    }
                }
                next = 4;
                break;
            case 0x13:
                int textureIndex = instruction >> 8;
                if (textureIndex != 1)
                {
                    throw new NotSupportedException("Native particle texture selection requires its proved index.");
                }
                frame = services.PrepareTexturePage(frame, textureIndex);
                frame = ParticleWord(frame, 0x40, unchecked((ushort)frame.ReadTemporaryDword(0xFC)));
                frame = ParticleWord(frame, 0x48, services.ReadClut(frame, textureIndex));
                break;
            default:
                throw new NotSupportedException("Native particle interpreter admits only its ten selected instruction kinds.");
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(pc + (uint)next));
    }

    public static NativeBattleWaterExecutionFrame AdvanceType6(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, NativeBattleWaterParticleServices services, int instructionBudget = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        frame = frame.WriteTemporaryWord(0x13E, 0).WriteTemporaryDword(0x38, 0).WriteTemporaryDword(0x3C, 0);
        frame = frame.WriteTemporaryDword(0x24, unchecked((uint)(short)SlotWord(frame, 0xC8))).WriteTemporaryDword(0x28, unchecked((uint)(short)SlotWord(frame, 0xCA)));
        frame = frame.WriteTemporaryDword(0x30, services.ResolveMatrixRow(frame, SlotWord(frame, 0x9C)));
        uint particles = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        frame = frame.WriteTemporaryDword(0x44, unchecked(particles + 0x10));
        frame = frame.WriteTemporaryDword(0x20, ReadArenaWord(frame, particles)).WriteTemporaryDword(0x34, ReadArenaWord(frame, unchecked(particles + 8)));
        int executed = 0;
        while (frame.ReadTemporaryDword(0x20) != 0)
        {
            frame = frame.WriteTemporaryDword(0x40, 0);
            uint pc = ReadParticleDword(frame, 4);
            if (pc != 0)
            {
                frame = WriteCoreDword(frame, 0x2797450, pc);
                if (frame.ReadTemporaryByte(0x145) == 0)
                {
                    ushort wait = ReadParticleWord(frame, 2);
                    if (wait > 0)
                    {
                        frame = ParticleWord(frame, 2, unchecked((ushort)(wait - frame.ReadTemporaryDword(0x24))));
                    }
                    else
                    {
                        do
                        {
                            if (executed >= instructionBudget)
                            {
                                throw new NotSupportedException("Native particle diagnostic instruction budget was exhausted without a native yield.");
                            }
                            ushort instruction = ProgramWord(frame, program, CoreDword(frame, 0x2797450));
                            frame = frame.WriteTemporaryWord(0x14A, instruction);
                            executed++;
                            frame = ExecuteOne(frame, program, bank, services);
                        }
                        while (frame.ReadTemporaryWord(0x13E) == 0);
                        ushort debt = ReadParticleWord(frame, 2);
                        frame = ParticleWord(frame, 2, unchecked((ushort)(debt + frame.ReadTemporaryWord(0x13E) - frame.ReadTemporaryDword(0x24))));
                        frame = frame.WriteTemporaryWord(0x13E, 0);
                        frame = ParticleDword(frame, 4, CoreDword(frame, 0x2797450));
                    }
                    for (int lane = 0; lane < 4; lane++)
                    {
                        int velocity = unchecked((short)ReadParticleWord(frame, 0x20 + 2 * lane)) + unchecked((short)ReadParticleWord(frame, 0x28 + 2 * lane));
                        frame = ParticleWord(frame, 0x20 + 2 * lane, unchecked((ushort)velocity));
                        frame = ParticleDword(frame, 0x10 + 4 * lane, unchecked(ReadParticleDword(frame, 0x10 + 4 * lane) + (uint)(velocity << 4)));
                    }
                    frame = PlaceMode1(frame);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        int color = ReadParticle(frame, 0x38 + lane, 1)[0] + unchecked((sbyte)ReadParticle(frame, 0x3C + lane, 1)[0]);
                        frame = ParticleByte(frame, 0x38 + lane, (byte)Math.Clamp(color, 0, 255));
                    }
                    if (ReadParticleDword(frame, 8) != 0)
                    {
                        int timer = unchecked((short)ReadParticleWord(frame, 0x34));
                        if (timer > 0)
                        {
                            timer = unchecked(timer - (int)frame.ReadTemporaryDword(0x28));
                            frame = ParticleWord(frame, 0x34, unchecked((ushort)timer));
                        }
                        if (timer <= 0)
                        {
                            uint sequence = ReadParticleDword(frame, 8);
                            frame = ParticleDword(frame, 0x30, unchecked(ReadParticleDword(frame, 0x4C) + ReadArenaDword(frame, sequence)));
                            frame = ParticleWord(frame, 0x34, unchecked((ushort)(ReadParticleWord(frame, 0x34) + ReadArenaWord(frame, unchecked(sequence + 4)))));
                            sbyte command = unchecked((sbyte)ReadArena(frame, unchecked(sequence + 6), 1)[0]);
                            if (command == 1)
                            {
                                frame = ParticleDword(frame, 4, 0);
                                frame = ParticleDword(frame, 0x30, 0);
                            }
                            else
                            {
                                sequence = command == 2 ? ReadParticleDword(frame, 0xC) : unchecked(sequence + 8);
                                frame = ParticleDword(frame, 8, sequence);
                            }
                        }
                    }
                }
                else if (ReadParticleDword(frame, 0x30) != 0)
                {
                    frame = PlaceMode1(frame);
                }
                if (ReadParticleDword(frame, 0x30) != 0)
                {
                    frame = Draw(frame, services);
                }
            }
            frame = frame.WriteTemporaryDword(0x44, unchecked(frame.ReadTemporaryDword(0x44) + 0x50));
            frame = frame.WriteTemporaryDword(0x20, unchecked(frame.ReadTemporaryDword(0x20) - 1));
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame PlaceMode1(NativeBattleWaterExecutionFrame frame)
    {
        if (ReadParticle(frame, 0x37, 1)[0] != 1)
        {
            throw new NotSupportedException("Native selected particle placement requires mode one.");
        }
        frame = frame.WriteTemporaryWord(0xA0, unchecked((ushort)(((int)ReadParticleDword(frame, 0x14) >> 8) - frame.ReadTemporaryWord(0x38))));
        frame = frame.WriteTemporaryWord(0xA2, unchecked((ushort)((int)ReadParticleDword(frame, 0x18) >> 8)));
        return frame.WriteTemporaryWord(0xA4, unchecked((ushort)(((int)ReadParticleDword(frame, 0x1C) >> 8) - frame.ReadTemporaryWord(0x3C))));
    }

    private static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleWaterParticleServices services)
    {
        uint address = frame.ReadTemporaryDword(0x30);
        byte[] raw = frame.Core.Read(address, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(i => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2 * i))).ToImmutableArray(), unchecked((int)CoreDword(frame, checked(address + 20))), unchecked((int)CoreDword(frame, checked(address + 24))), unchecked((int)CoreDword(frame, checked(address + 28))), 4096);
        NativeBattleVector point = new(unchecked((short)frame.ReadTemporaryWord(0xA0)), unchecked((short)frame.ReadTemporaryWord(0xA2)), unchecked((short)frame.ReadTemporaryWord(0xA4)));
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(basis, point);
        uint angle = unchecked((uint)((int)ReadParticleDword(frame, 0x10) >> 4));
        frame = frame.WriteTemporaryDword(0xA0, angle).WriteTemporaryDword(0xA8, angle).WriteTemporaryDword(0xA4, 0).WriteTemporaryDword(0xAC, 0).WriteTemporaryDword(0xB0, 0);
        byte[] rotation = frame.ReadTemporary(0xA0, 18);
        NativeBattleActionMatrix planar = new(Enumerable.Range(0, 9).Select(i => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(2 * i))).ToImmutableArray(), translation.X, translation.Y, translation.Z, 4096);
        if (frame.ReadTemporaryDword(0x40) != 0)
        {
            throw new NotSupportedException("Native selected particle draw has no proved optional postrotation producer.");
        }
        frame = frame.WriteTemporaryDword(0x90, ReadParticleDword(frame, 0x30)).WriteTemporaryDword(0x94, ReadParticleWord(frame, 0x40)).WriteTemporaryDword(0x98, ReadParticleDword(frame, 0x38)).WriteTemporaryDword(0x9C, unchecked((uint)(short)ReadParticleWord(frame, 0x48)));
        return services.DrawRectangles(frame, planar);
    }

    private static int ParticleOffset(NativeBattleWaterExecutionFrame frame, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 0x50 - length)
        {
            throw new NotSupportedException("Native particle access leaves its physical record.");
        }
        return checked(ArenaOffset(frame.ReadTemporaryDword(0x44), 0x50) + offset);
    }

    private static byte[] ReadParticle(NativeBattleWaterExecutionFrame frame, int offset, int length) => frame.Arena.Read(ParticleOffset(frame, offset, length), length);
    private static uint ReadParticleDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadParticle(frame, offset, 4));
    private static ushort ReadParticleWord(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadParticle(frame, offset, 2));
    private static byte[] ReadArena(NativeBattleWaterExecutionFrame frame, uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
    private static uint ReadArenaDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadArena(frame, address, 4));
    private static ushort ReadArenaWord(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadArena(frame, address, 2));

    private static byte[] ReadAddress(NativeBattleWaterExecutionFrame frame, uint address, int length, NativeBattleWaterParticleServices services)
    {
        if (address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress <= NativeBattleEffectArena.Length - length)
        {
            return ReadArena(frame, address, length);
        }
        byte[] bytes = services.ReadMetadata(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native particle metadata lost its exact addressed extent.");
    }

    private static ushort ProgramWord(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, uint pc)
    {
        uint relative = unchecked(pc - program.Address);
        if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
        {
            throw new InvalidDataException("Native particle instruction leaves its actual current program owner.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
    }

    private static int CurrentIndex(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank)
    {
        int relative = ArenaOffset(CoreDword(frame, 0x27973E8), 0x100) - bank.SlotBaseOffset;
        if (relative < 0 || relative % 0x100 != 0)
        {
            throw new InvalidDataException("Native particle lookup requires its actual current slot in the cached bank.");
        }
        int index = relative / 0x100;
        _ = bank.SlotOffset(frame.Arena, index);
        return index;
    }

    private static ImmutableArray<byte> ActiveOrder(NativeBattleWaterExecutionFrame frame)
    {
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = frame.Core.Read(0x2797454 + (uint)i, 1)[0];
            if (order[i] == 0xFF)
            {
                return order.ToImmutableArray();
            }
        }
        throw new NotSupportedException("Native particle lookup requires its terminated active-order prefix.");
    }

    private static NativeBattleWaterExecutionFrame ParticleByte(NativeBattleWaterExecutionFrame frame, int offset, byte value) => frame with { Arena = frame.Arena.Write(ParticleOffset(frame, offset, 1), new byte[] { value }) };
    private static NativeBattleWaterExecutionFrame ParticleWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ParticleOffset(frame, offset, 2), bytes) };
    }
    private static NativeBattleWaterExecutionFrame ParticleDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(ParticleOffset(frame, offset, 4), bytes) };
    }
}
