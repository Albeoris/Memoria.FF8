using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>502170's reached ordinary bone selectors over the current raw model pose and matrix.</summary>
public static class NativeBattleShivaModelBones
{
    public static NativeBattleVector Read502170(NativeBattleCactuarRawModelState model, NativeBattleEffectOwnedModelResource resource, int selector, short fraction)
    {
        NativeBattleCactuarRawModel.Validate(model, resource, NativeBattleShivaModelTask.Binding);
        if ((model.Model[0] & 2) == 0 || selector >= 240 || (uint)selector >= resource.PoseSource.BoneCount)
        {
            throw new NotSupportedException("Native Shiva bone sampling requires its initialized raw model and a reached ordinary skeleton selector.");
        }
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Native Shiva bone sampling requires its current selected and advanced pose.");
        NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(model.Model.AsSpan().Slice(0x40 + index * 2))).ToImmutableArray(), Dword(0x54), Dword(0x58), Dword(0x5C), 4096);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, matrix);
        int record = 16 + selector * 48;
        short parent = BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(record));
        short length = BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(record + 2));
        short distance = parent < 0 ? (short)0 : unchecked((short)(length * fraction >> 12));
        NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(bones[selector], new(0, 0, distance));
        return new(unchecked((short)point.X), unchecked((short)point.Y), unchecked((short)point.Z));

        int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(model.Model.AsSpan().Slice(offset));
    }
}
