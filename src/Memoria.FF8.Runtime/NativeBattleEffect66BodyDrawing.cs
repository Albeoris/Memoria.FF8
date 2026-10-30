using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66BodyStep(NativeBattleEffect66Frame Frame, NativeBattleEffectArena Temporary176, NativeBattleEffectArena Scratch);

public static partial class NativeBattleEffect66BodyDrawing
{
    public static NativeBattleEffect66BodyStep Draw(NativeBattleEffect66Frame frame, uint geometryAddress, Func<uint, int, NativeBattleEffectArena> readGeometry, ImmutableArray<NativeBattlePackedMatrix32> currentBones, NativeBattlePackedMatrix32 view, NativeBattleEffectArena temporary176, NativeBattleEffectArena scratch, int scratchLength, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (scratchLength < 0 || (scratchLength & 7) != 0 || currentBones.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Effect66 requires its actual current bone and renderer scratch owners.");
        }
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        int objects = Integer(geometryAddress);
        if (objects < 0)
        {
            throw new InvalidDataException("Effect66 body requires its current raw object directory.");
        }
        uint mask = Dword(temporary176, 0x20);
        for (int item = 0; item < objects; item++)
        {
            uint cursor = checked(geometryAddress + (uint)Integer(geometryAddress + 4u + (uint)(item * 4)));
            if ((mask & (1u << (item & 31))) == 0)
            {
                continue;
            }
            int destination = 0;
            int groups = Short(cursor);
            cursor += 2;
            if (groups < 0)
            {
                throw new InvalidDataException("Effect66 body requires nonnegative bone group counts.");
            }
            for (int group = 0; group < groups; group++)
            {
                int bone = Short(cursor);
                int count = Short(cursor + 2);
                cursor += 4;
                if ((uint)bone >= (uint)currentBones.Length || count < 0 || destination > scratchLength - checked(count * 8))
                {
                    throw new InvalidDataException("Effect66 body leaves its current bones or physical scratch capacity.");
                }
                registers = Matrix(registers, currentBones[bone]);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    NativeBattleVector xyz = Vector(readGeometry(cursor, 6), 0);
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, xyz, 0));
                    for (int axis = 0; axis < 3; axis++)
                    {
                        scratch = Word(scratch, destination + axis * 2, registers.Colors.Ir[axis]!.Value);
                    }
                    scratch = Word(scratch, destination + 6, 0);
                    destination += 8;
                    cursor += 6;
                }
            }
            cursor = (cursor + 3) & ~3u;
            for (int group = 0; group < 4; group++)
            {
                temporary176 = Word(temporary176, 8 + group * 2, Short(cursor + (uint)(group * 2)));
            }
            cursor += 12;
            temporary176 = Put(temporary176, 0, cursor);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect66 body requires its current primitive state.");
            NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect66 body requires its current far-color owner.");
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far.PublishFarColor45DDA0(255, 255, 255) } } };
            registers = Matrix(registers, view);
            frame = Publish(frame, registers);
            if (Dword(temporary176, 0x44) == 0)
            {
                (frame, temporary176) = InitializeThresholds(frame, temporary176, scratch, scratchLength, readGeometry);
            }
            else
            {
                NativeBattleEffect66BodyStep result = DrawFaces(frame, temporary176, scratch, scratchLength, readGeometry, inputs, pilotIdentity, item);
                frame = result.Frame;
                temporary176 = result.Temporary176;
                scratch = result.Scratch;
            }
            registers = Registers(frame);
        }
        return new(Publish(frame, registers), temporary176, scratch);

        int Integer(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static (NativeBattleEffect66Frame Frame, NativeBattleEffectArena Temporary) InitializeThresholds(NativeBattleEffect66Frame frame, NativeBattleEffectArena temporary, NativeBattleEffectArena scratch, int scratchLength, Func<uint, int, NativeBattleEffectArena> readGeometry)
    {
        uint source = Dword(temporary, 0);
        uint flags = Dword(temporary, 0x2C);
        for (int group = 0; group < 2; group++)
        {
            int count = Uword(temporary, 8 + group * 2);
            int stride = group == 0 ? 16 : 20;
            for (int face = 0; face < count; face++)
            {
                int vertex = BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(source + 2, 2).Read(0, 2)) & 0xFFF;
                int offset = checked(vertex * 8);
                if (offset > scratchLength - 8)
                {
                    throw new InvalidDataException("Effect66 face threshold leaves current scratch rows.");
                }
                temporary = NativeBattleEffectNodeContextMemory.Copy(scratch, offset, temporary, 0x60, 8);
                int threshold = unchecked(S16(temporary, 0x62) * 320) / 6000 + 320;
                NativeBattleEffectArena flag = ReadBytes(frame, flags, 2);
                frame = WriteBytes(frame, flags, Word(flag, 0, Uword(flag, 0) | threshold), 2);
                source += (uint)stride;
                flags += 2;
            }
        }
        return (frame, Put(temporary, 0x2C, flags));
    }

    private static NativeBattleGeometryRegisterStep Matrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 packed)
    {
        short? padding = packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan().Slice(18, 2)) : null;
        return registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, packed.ReadMatrix(), true, true, padding) };
    }
    private static uint Dword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static ushort Uword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Read(offset, 2));
    private static short S16(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static NativeBattleVector Vector(NativeBattleEffectArena source, int offset) => new(S16(source, offset), S16(source, offset + 2), S16(source, offset + 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena source, int offset, uint value) => source.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena source, int offset, int value) => source.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
}
