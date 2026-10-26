using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326PointStep ReadOwnedBone631050(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress, int bone, int fraction, NativeBattleEffectArena retained) => ReadOwnedBone(frame, resources, modelAddress, bone, fraction, retained, false);

    public static NativeBattleEffect326PointStep ReadOwnedBone502170(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress, int bone, int fraction, NativeBattleEffectArena retained) => ReadOwnedBone(frame, resources, modelAddress, bone, fraction, retained, true);

    private static NativeBattleEffect326PointStep ReadOwnedBone(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress, int bone, int fraction, NativeBattleEffectArena retained, bool composeWorld)
    {
        if ((uint)bone > byte.MaxValue || fraction < short.MinValue || fraction > short.MaxValue)
        {
            throw new InvalidDataException("Effect326 owned bone query leaves its actual skeleton.");
        }
        NativeBattleEffectOwnedModelResource resource = ModelResource(resources, modelAddress);
        NativeBattleCactuarRawModelState model = RequireModel(frame, modelAddress);
        if ((model.Model[0] & 2) == 0)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                retained = retained.Write(axis * 2, ReadBytes(frame, modelAddress + 0x1Cu + (uint)(axis * 2), 2).Read(0, 2));
            }
            return new(frame, retained);
        }
        uint skeleton = checked((model.WorkingBase ?? throw new NotSupportedException("Effect326 bone query requires its actual working model.")) + (uint)resource.Section(0));
        if (bone >= 240)
        {
            if (composeWorld && BinaryPrimitives.ReadUInt16LittleEndian(model.Model.AsSpan(4, 2)) == 0x8F)
            {
                if (resource.PoseSource.Model8FBoneAliases.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException("Effect326 model8F bone selectors require their actual source aliases.");
                }
                bone = resource.PoseSource.Model8FBoneAliases[bone - 240];
            }
            else
            {
                int offset = bone - 236;
                if (offset >= 16)
                {
                    throw new NotSupportedException("Effect326 bone selector exceeds its current skeleton header.");
                }
                bone = ReadBytes(frame, checked(skeleton + (uint)offset), 1).Read(0, 1)[0];
            }
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if ((uint)bone >= resource.PoseSource.BoneCount)
        {
            throw new InvalidDataException("Effect326 bone alias leaves its actual skeleton.");
        }
        uint record = checked(skeleton + 16u + (uint)(bone * 48));
        short parent = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, record, 2).Read(0, 2));
        short length = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, record + 2, 2).Read(0, 2));
        short distance = unchecked((short)(parent < 0 ? 0 : fraction * length >> 12));
        NativeBattlePackedMatrix32 matrix = ReadPacked(frame, record + 16);
        if (composeWorld)
        {
            matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, modelAddress + 0x40).ReadMatrix(), matrix.ReadMatrix()), null);
        }
        (frame, uint temporary) = Allocate(frame, 52);
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        if (composeWorld)
        {
            context = CopyBytes(PackedArena(matrix), 0, context, 0, 32).MarkUnknown(18, 2);
        }
        frame = LoadMatrix(frame, matrix);
        NativeBattleGeometryRegisterStep registers;
        if (composeWorld)
        {
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadAnchorScratchZ(Registers(frame).Geometry, distance);
            registers = NativeBattleGeometry.LoadAnchorVector(new(geometry, Registers(frame).Colors));
        }
        else
        {
            registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, new(0, 0, distance), null);
        }
        registers = NativeBattleGeometry.Transform(registers);
        frame = Publish(frame, registers);
        for (int axis = 0; axis < 3; axis++)
        {
            int value = registers.Colors.Mac[axis] ?? throw new InvalidDataException("Effect326 bone query did not publish its actual MAC output.");
            context = context.Write(32 + axis * 4, BitConverter.GetBytes(value));
            retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        context = context.Write(48, BitConverter.GetBytes(registers.Colors.Flags ?? throw new InvalidDataException("Effect326 bone query did not publish its actual flags.")));
        frame = Release(PublishContext(frame, temporary, context, 52), 52);
        return new(frame, retained);
    }

}
