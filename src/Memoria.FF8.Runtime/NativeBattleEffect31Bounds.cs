using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect31BoundsStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleModulePoolTask Task, uint TemporaryAddress);

public static class NativeBattleEffect31Bounds
{
    public static NativeBattleEffect31BoundsStep Advance(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleModulePoolTask task, NativeBattlePoseSource source, ImmutableArray<NativeBattlePackedMatrix32> currentBones)
    {
        source.Validate();
        if (currentBones.IsDefault || currentBones.Length != source.BoneCount)
        {
            throw new InvalidDataException("Effect31 bounds require every current physical local bone matrix.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x2C);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 0x2C);
        NativeBattlePackedMatrix32 first = currentBones[0];
        first.Validate();
        for (int axis = 0; axis < 3; axis++)
        {
            int at = 20 + axis * 4;
            if (!first.Known[at] || !first.Known[at + 1])
            {
                throw new NotSupportedException("Effect31 bounds require the actual first bone translation WORDs.");
            }
            short value = BinaryPrimitives.ReadInt16LittleEndian(first.Bytes.AsSpan().Slice(at));
            temporary = Word(Word(temporary, 0x18 + axis * 2, value), 0x20 + axis * 2, value);
        }
        temporary = Word(Word(temporary, 0, 0), 2, 0);
        for (int bone = 1; bone < currentBones.Length; bone++)
        {
            NativeBattlePackedMatrix32 packed = currentBones[bone];
            packed.Validate();
            short? padding = packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan().Slice(18)) : null;
            NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect31 bounds require current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect31 bounds require current arithmetic registers."));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, packed.ReadMatrix(), true, true, padding) };
            short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + bone * 48));
            temporary = Word(temporary, 4, unchecked((short)-length));
            (byte[] w, bool[] known) = temporary.ReadSnapshot(6, 2);
            short? sourceW = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null;
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(0, 0, unchecked((short)-length)), sourceW);
            registers = NativeBattleGeometry.Transform(registers);
            for (int axis = 0; axis < 3; axis++)
            {
                int mac = registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect31 bounds require the actual full signed MAC results.");
                temporary = Dword(temporary, 8 + axis * 4, unchecked((uint)mac));
            }
            temporary = Dword(temporary, 0x28, registers.Colors.Flags ?? throw new NotSupportedException("Effect31 bounds require their current transform flags."));
            for (int axis = 0; axis < 3; axis++)
            {
                int mac = BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(8 + axis * 4, 4));
                short minimum = BinaryPrimitives.ReadInt16LittleEndian(temporary.Read(0x18 + axis * 2, 2));
                if (mac < minimum)
                {
                    temporary = Word(temporary, 0x18 + axis * 2, unchecked((short)mac));
                }
                else
                {
                    short maximum = BinaryPrimitives.ReadInt16LittleEndian(temporary.Read(0x20 + axis * 2, 2));
                    if (mac > maximum)
                    {
                        temporary = Word(temporary, 0x20 + axis * 2, unchecked((short)mac));
                    }
                }
            }
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        }
        task = CopyDword(task, 0x48, temporary, 0x18);
        task = CopyDword(task, 0x50, temporary, 0x20);
        task = CopyDword(task, 0x4C, temporary, 0x1C);
        task = CopyDword(task, 0x54, temporary, 0x24);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 0x2C);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x2C);
        return new(memory, staging, task, allocation.Address);
    }

    private static NativeBattleModulePoolTask CopyDword(NativeBattleModulePoolTask task, int destination, NativeBattleEffectArena temporary, int source)
    {
        (byte[] bytes, bool[] known) = temporary.ReadSnapshot(source, 4);
        task = task.WriteBytes(destination, bytes);
        for (int index = 0; index < 4; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(destination + index, 1);
            }
        }
        return task;
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, short value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
