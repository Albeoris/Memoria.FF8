using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8AppearanceModel(uint Address, NativeBattleEffectArena Bytes, ImmutableArray<NativeBattleRawMesh> Meshes, ImmutableArray<NativeBattlePackedMatrix32> Bones);
public sealed record NativeBattleDoom8AppearanceStep(NativeBattleDoom8Frame Frame, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch, uint TemporaryAddress);

public static partial class NativeBattleDoom8ReaperAppearance
{
    public const uint ScratchAddress = 0x2758510;
    public const int ScratchLength = 632;
    public const uint DescriptorAddress = 0x275F9E0;
    public const int DescriptorLength = 56;
    public const uint VertexAddress = 0x275ABF0;
    public const int VertexLength = 302 * 8;
    public const uint CursorAddress = 0x2758504;
    public const uint TrianglePalette = 0x275F984;
    public const uint QuadPalette = 0x275F930;

    public static NativeBattleDoom8Frame Initialize8CD020(NativeBattleDoom8Frame frame, uint modelAddress)
    {
        if (modelAddress == 0)
        {
            throw new InvalidDataException("Doom appearance requires its actual model address.");
        }
        NativeBattleDoom8State state = frame.State.Write(ScratchAddress, new byte[ScratchLength]);
        state = state.WriteDword(ScratchAddress + 0x40, VertexAddress).WriteDword(ScratchAddress + 0x44, DescriptorAddress).WriteDword(ScratchAddress + 0x48, modelAddress).WriteDword(ScratchAddress + 0x4C, CursorAddress).WriteDword(ScratchAddress + 0x50, CursorAddress);
        state = state.WriteDword(DescriptorAddress + 4, VertexAddress).WriteWord(DescriptorAddress + 0x14, 0).WriteWord(DescriptorAddress + 0x16, 0).WriteWord(DescriptorAddress + 0x18, 320).WriteWord(DescriptorAddress + 0x1A, 0).Write(DescriptorAddress + 0x1C, [128, 128, 128]).WriteDword(DescriptorAddress + 0x20, uint.MaxValue).WriteWord(DescriptorAddress + 0x24, 0);
        return frame with { State = state };
    }

    public static NativeBattleDoom8Frame PrepareCamera8CC6B0(NativeBattleDoom8Frame frame, NativeBattlePackedMatrix32 world, NativeBattlePackedMatrix32 camera)
    {
        world.Validate();
        camera.Validate();
        NativeBattleActionMatrix worldMatrix = world.ReadMatrix();
        NativeBattleActionMatrix view = camera.ReadMatrix();
        NativeBattleDoom8State state = CopyPacked(frame.State, ScratchAddress, world);
        NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, Padding(camera)) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)worldMatrix.Rotation[column]), unchecked((ushort)worldMatrix.Rotation[column + 3]), unchecked((ushort)worldMatrix.Rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, translation: false, irInput: true);
            for (int row = 0; row < 3; row++)
            {
                state = state.WriteWord(ScratchAddress + 0x20u + (uint)(row * 6 + column * 2), unchecked((ushort)Require(registers.Colors.Ir[row])));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, false, true, Padding(camera)) };
        short z = unchecked((short)worldMatrix.Z);
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)worldMatrix.X), unchecked((short)worldMatrix.Y), z), z < 0 ? (short)-1 : (short)0);
        registers = NativeBattleGeometry.Transform(registers);
        for (int row = 0; row < 3; row++)
        {
            state = state.WriteDword(ScratchAddress + 0x34u + (uint)(row * 4), unchecked((uint)Require(registers.Colors.Mac[row])));
        }
        return frame with { State = state, Memory = Publish(frame.Memory, registers) };
    }

    public static NativeBattleEffectMemory LoadBoneLight(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 bone)
    {
        bone.Validate();
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedBackColor back = lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown();
        for (int row = 0; row < 3; row++)
        {
            int offset = 20 + row * 4;
            if (!bone.Known.AsSpan().Slice(offset, 4).ToArray().All(value => value))
            {
                back = back with { Known = (back.Known ?? [true, true, true]).SetItem(row, false) };
                continue;
            }
            back = back.WriteLane(row, BinaryPrimitives.ReadInt32LittleEndian(bone.Bytes.AsSpan().Slice(offset, 4)));
        }
        return memory with { ScriptedLighting = lighting with { LightMatrix = new(bone.Bytes.Take(20).ToImmutableArray(), bone.Known.Take(20).ToImmutableArray()), BackColor = back } };
    }

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory)
    {
        return new(memory.GeometryRegisters ?? throw new NotSupportedException("Doom appearance requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Doom appearance requires its current arithmetic registers."));
    }

    private static NativeBattleEffectMemory Publish(NativeBattleEffectMemory memory, NativeBattleGeometryRegisterStep registers)
    {
        return memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }

    private static short? Padding(NativeBattlePackedMatrix32 matrix)
    {
        return matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
    }

    private static NativeBattleDoom8State CopyPacked(NativeBattleDoom8State state, uint address, NativeBattlePackedMatrix32 matrix)
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        for (int index = 0; index < 32; index++)
        {
            if (matrix.Known[index])
            {
                source = source.Write(index, [matrix.Bytes[index]]);
            }
        }
        return CopyToState(state, address, source, 0, 32);
    }

    private static NativeBattleDoom8State CopyToState(NativeBattleDoom8State state, uint address, NativeBattleEffectArena source, int offset, int length)
    {
        int destination = checked((int)(address - NativeBattleDoom8State.BaseAddress));
        return state with { Globals = Copy(source, offset, state.Globals, destination, length) };
    }

    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(sourceOffset, length);
        destination = destination.MarkUnknown(destinationOffset, length);
        int index = 0;
        while (index < length)
        {
            if (!known[index])
            {
                index++;
                continue;
            }
            int first = index;
            while (index < length && known[index])
            {
                index++;
            }
            destination = destination.Write(destinationOffset + first, bytes.AsSpan(first, index - first));
        }
        return destination;
    }

    private static NativeBattlePackedMatrix32 Matrix(NativeBattleDoom8State state, uint address)
    {
        (byte[] bytes, bool[] known) = state.Globals.ReadSnapshot(checked((int)(address - NativeBattleDoom8State.BaseAddress)), 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short I16(NativeBattleEffectArena arena, int offset) => unchecked((short)U16(arena, offset));
    private static int Require(int? value) => value ?? throw new NotSupportedException("Doom appearance requires its reached arithmetic output.");
}
