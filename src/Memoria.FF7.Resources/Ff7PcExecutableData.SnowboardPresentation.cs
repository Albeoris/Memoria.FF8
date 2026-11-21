using System.Buffers.Binary;
using System.Numerics;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7SnowboardJointBinding(Matrix4x4 EndpointBasis, Matrix4x4 RotationBasis, Matrix4x4 RotationSuffix, short PositionParent, short RotationParent, short Length, bool SkipEndpoint);
public sealed record Ff7SnowboardDrawBinding(short ModelIndex, short NativePrimitiveKind, short RotationJoint, short PositionJoint);
public sealed record Ff7SnowboardPoseFrame(IReadOnlyList<Vector3> Rotations, Vector3 RootOffset);
public sealed record Ff7SnowboardPresentationData(IReadOnlyList<Ff7SnowboardJointBinding> Joints, IReadOnlyList<Ff7SnowboardDrawBinding> DrawBindings, IReadOnlyList<Ff7SnowboardPoseFrame> Poses);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7SnowboardPresentationData LoadSnowboardPresentationData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] bindings = ReadVirtual(stream, pe, 0x957CE0, 19 * 20);
        Ff7SnowboardJointBinding[] joints = new Ff7SnowboardJointBinding[19];
        for (int index = 0; index < joints.Length; index++)
        {
            ReadOnlySpan<byte> record = bindings.AsSpan(index * 20, 20);
            joints[index] = new(ReadBasis(BinaryPrimitives.ReadUInt32LittleEndian(record)), ReadBasis(BinaryPrimitives.ReadUInt32LittleEndian(record[4..])), ReadBasis(BinaryPrimitives.ReadUInt32LittleEndian(record[8..])), BinaryPrimitives.ReadInt16LittleEndian(record[12..]), BinaryPrimitives.ReadInt16LittleEndian(record[14..]), BinaryPrimitives.ReadInt16LittleEndian(record[16..]), BinaryPrimitives.ReadInt16LittleEndian(record[18..]) != 0);
        }
        byte[] drawRecords = ReadVirtual(stream, pe, 0x9581A0, 16 * 8);
        Ff7SnowboardDrawBinding[] draws = new Ff7SnowboardDrawBinding[16];
        for (int index = 0; index < draws.Length; index++)
        {
            ReadOnlySpan<byte> record = drawRecords.AsSpan(index * 8, 8);
            draws[index] = new(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]));
        }
        byte[] angles = ReadVirtual(stream, pe, 0x958340, 209 * 29 * 4);
        byte[] offsets = ReadVirtual(stream, pe, 0x9615A8, 209 * 12);
        float angleScale = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(ReadVirtual(stream, pe, 0x7B7DC0, 4)));
        Ff7SnowboardPoseFrame[] poses = new Ff7SnowboardPoseFrame[209];
        for (int pose = 0; pose < poses.Length; pose++)
        {
            Vector3[] rotations = new Vector3[joints.Length];
            for (int joint = 0; joint < rotations.Length; joint++)
            {
                ReadOnlySpan<byte> record = angles.AsSpan(pose * 116 + joint * 6, 6);
                rotations[joint] = new(BinaryPrimitives.ReadInt16LittleEndian(record) * angleScale, BinaryPrimitives.ReadInt16LittleEndian(record[2..]) * angleScale, BinaryPrimitives.ReadInt16LittleEndian(record[4..]) * angleScale);
            }
            ReadOnlySpan<byte> root = offsets.AsSpan(pose * 12, 12);
            poses[pose] = new(Array.AsReadOnly(rotations), new(Float(root), Float(root[4..]), Float(root[8..])));
        }
        return new(Array.AsReadOnly(joints), Array.AsReadOnly(draws), Array.AsReadOnly(poses));

        Matrix4x4 ReadBasis(uint address)
        {
            byte[] raw = ReadVirtual(stream, pe, address, 18);
            float[] values = new float[9];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(index * 2)) / 4096f;
            }
            return new(values[0], values[3], values[6], 0, values[1], values[4], values[7], 0, values[2], values[5], values[8], 0, 0, 0, 0, 1);
        }

        static float Float(ReadOnlySpan<byte> bytes) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));
    }
}
