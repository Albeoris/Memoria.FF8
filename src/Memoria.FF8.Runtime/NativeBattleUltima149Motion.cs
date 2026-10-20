using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltima149Particle(ImmutableArray<byte> Bytes)
{
    public uint Type => BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan());
    public short Frame => Signed(4);
    public short Scale => Signed(6);
    public NativeBattleVector Position => Vector(8);
    public NativeBattleVector Velocity => Vector(16);
    public short Signed(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(offset));
    public NativeBattleVector Vector(int offset) => new(Signed(offset), Signed(offset + 2), Signed(offset + 4));
    public NativeBattleUltima149Particle Word(int offset, int value)
    {
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
        return this with { Bytes = bytes.ToImmutableArray() };
    }
    public NativeBattleUltima149Particle SetType(uint type)
    {
        byte[] bytes = Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, type);
        return this with { Bytes = bytes.ToImmutableArray() };
    }
    public NativeBattleUltima149Particle SetVector(int offset, NativeBattleVector value) => Word(offset, value.X).Word(offset + 2, value.Y).Word(offset + 4, value.Z);
}

public static class NativeBattleUltima149Motion
{
    public static NativeBattleUltima149Particle Move(NativeBattleUltima149Particle particle, uint type)
    {
        NativeBattleVector point = particle.Position;
        NativeBattleVector velocity = particle.Velocity;
        if (type == 1)
        {
            return particle.Word(16, unchecked((short)(velocity.X + velocity.Z)));
        }
        if (type == 4)
        {
            return particle.Word(6, unchecked((short)(particle.Scale + particle.Scale / 6))).Word(14, unchecked((short)(particle.Signed(14) + particle.Signed(22))));
        }
        particle = particle.SetVector(8, new(unchecked((short)(point.X + velocity.X)), unchecked((short)(point.Y + velocity.Y)), unchecked((short)(point.Z + velocity.Z))));
        int shiftX = type == 2 ? 3 : type == 8 ? 4 : type == 16 ? 2 : throw new NotSupportedException("Native Ultima particle motion requires its actual array/type owner.");
        particle = particle.SetVector(16, new(unchecked((short)(velocity.X - (velocity.X >> shiftX))), unchecked((short)(velocity.Y - (velocity.Y >> 3))), unchecked((short)(velocity.Z - (velocity.Z >> shiftX)))));
        return type == 2 ? particle.Word(6, unchecked((short)(particle.Scale - (particle.Scale >> 3)))) : particle;
    }

    public static NativeBattleModulePoolTask Energy(NativeBattleModulePoolTask task)
    {
        short step = unchecked((short)task.ReadWord(30));
        int scale = unchecked((short)(unchecked((short)task.ReadWord(28)) + step));
        task = task.WriteWord(24, unchecked((ushort)(unchecked((short)task.ReadWord(24)) + unchecked((short)task.ReadWord(26))))).WriteWord(28, (ushort)(scale < 0 ? 32767 : scale));
        return task.WriteWord(30, unchecked((ushort)(step + (unchecked((short)task.ReadWord(12)) < 34 ? step >> 5 : step / 12))));
    }

    public static ImmutableArray<byte> MorphCore(NativeBattleStaticMeshResource core, ImmutableArray<byte> shape1, ImmutableArray<byte> retained, int frame)
    {
        if (core.Bytes.IsDefault || core.Bytes.Length < 968 || shape1.IsDefault || shape1.Length != 960 || retained.IsDefault || retained.Length != 960)
        {
            throw new InvalidDataException("Native Ultima core requires both complete120-vertex shapes and its retained vertex padding.");
        }
        byte[] bytes = retained.ToArray();
        int weight = NativeBattleCameraMath.Sine(unchecked(frame << 10) / 40);
        for (int vertex = 0; vertex < 120; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = vertex * 8 + axis * 2;
                int first = BinaryPrimitives.ReadInt16LittleEndian(core.Bytes.AsSpan().Slice(8 + offset));
                int second = BinaryPrimitives.ReadInt16LittleEndian(shape1.AsSpan().Slice(offset));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)((unchecked((second - first) * weight) >> 12) + first)));
            }
        }
        return bytes.ToImmutableArray();
    }
}
