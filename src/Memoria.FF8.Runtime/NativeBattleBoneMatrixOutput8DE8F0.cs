using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? BoneMatrixAnchor2795BC0 { get; init; }
}

public sealed record NativeBattleBoneMatrixOutputStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattlePackedMatrix32 Output, int Result);

public static class NativeBattleBoneMatrixOutput8DE8F0
{
    public static NativeBattleBoneMatrixOutputStep Query(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleGraphicsModel model, NativeBattlePoseSource source, int selector, int fraction, NativeBattlePackedMatrix32 output)
    {
        if ((model.Flags0 & 2) == 0)
        {
            return new(memory, staging, output, -1);
        }
        output.Validate();
        ImmutableArray<byte> scratch = memory.BoneMatrixAnchor2795BC0 ?? throw new NotSupportedException("Native8DE8F0 requires its retained shared anchor bytes.");
        if (scratch.IsDefault || scratch.Length != 8)
        {
            throw new InvalidDataException("Native8DE8F0 requires its exact eight-byte anchor owner.");
        }
        int bone = selector;
        if (selector >= 240)
        {
            int alias = selector - 236;
            if ((uint)alias >= 16)
            {
                throw new InvalidDataException("Native8DE8F0 selector leaves its current skeleton aliases.");
            }
            bone = source.Skeleton[alias];
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        if ((uint)bone >= (uint)bones.Length)
        {
            throw new InvalidDataException("Native8DE8F0 selector leaves its current bone bank.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + bone * 48, 2));
        byte[] anchor = scratch.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(4), parent < 0 ? (short)0 : unchecked((short)(unchecked(fraction * length) >> 12)));
        memory = memory with { BoneMatrixAnchor2795BC0 = anchor.ToImmutableArray() };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(model.GeometryWorldMatrix ?? throw new NotSupportedException("Native8DE8F0 requires its current world matrix."), bones[bone]);
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(staging, 52);
        staging = allocated.Staging;
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(composed, null);
        staging = staging.Write(allocated.Address, matrix.Bytes.AsSpan(0, 18)).Write(allocated.Address + 20, matrix.Bytes.AsSpan(20, 12));
        (byte[] padding, bool[] paddingKnown) = staging.ReadSnapshot(allocated.Address + 18, 2);
        short? pad = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Native8DE8F0 requires geometry registers."), composed, true, true, pad), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native8DE8F0 requires arithmetic registers."));
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(anchor), BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan(4))), BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan(6)));
        registers = NativeBattleGeometry.Transform(registers);
        byte[] bytes = output.Bytes.ToArray();
        bool[] known = output.Known.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            int mac = registers.Colors.Mac[axis] ?? throw new InvalidDataException("Native8DE8F0 did not publish full MAC output.");
            staging = staging.Write(allocated.Address + 32u + (uint)(axis * 4), BitConverter.GetBytes(mac));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + axis * 4), unchecked((short)mac));
            Array.Fill(known, true, 20 + axis * 4, 4);
        }
        staging = staging.Write(allocated.Address + 48, BitConverter.GetBytes(registers.Colors.Flags ?? throw new InvalidDataException("Native8DE8F0 did not publish transform flags.")));
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 52);
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, staging, new(bytes.ToImmutableArray(), known.ToImmutableArray()), 0);
    }
}
