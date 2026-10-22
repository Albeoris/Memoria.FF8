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
        NativeBattleEffectOwnedModelCameraStep prepared = NativeBattleEffectOwnedModelRendering.PrepareCamera84(frame.Memory, frame.State.ReadView(ScratchAddress, 84), world, camera);
        return frame with { Memory = prepared.Memory, State = frame.State.WriteView(ScratchAddress, prepared.Scratch, 84) };
    }

    public static NativeBattleEffectMemory LoadBoneLight(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 bone)
    {
        return NativeBattleLightBackTransform.LoadBone(memory, bone);
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
