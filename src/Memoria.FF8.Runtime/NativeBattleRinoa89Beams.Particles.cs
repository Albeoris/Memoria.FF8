using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    private static NativeBattleRinoa89Frame BeamSpawnEmitters(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix basis, int count, byte palette, bool tilted, bool distributed)
    {
        for (int index = 0; index < count; index++)
        {
            NativeBattleRinoa89EmitterAllocation allocation = AllocateEmitter6FDB20(frame);
            frame = allocation.Frame;
            if (allocation.Offset is not int offset)
            {
                break;
            }
            short angle = unchecked((short)BeamRandom(ref frame));
            NativeBattleActionMatrix rotation = BeamRotationZ7012C0(angle);
            if (tilted)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(rotation, BeamRotationX701220(-512));
            }
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
            int x = basis.X;
            int y = basis.Y;
            int z = basis.Z;
            if (distributed)
            {
                int distance = (BeamRandom(ref frame) & 0x7FF) - 1024;
                x = unchecked(x + distance * rotation.Rotation[1] / 4096);
                y = unchecked(y + distance * rotation.Rotation[4] / 4096);
                z = unchecked(z + distance * rotation.Rotation[7] / 4096);
            }
            byte[] fields = new byte[34];
            fields[0] = 13;
            fields[1] = palette;
            BinaryPrimitives.WriteUInt16LittleEndian(fields.AsSpan(4), 2048);
            BinaryPrimitives.WriteUInt16LittleEndian(fields.AsSpan(6), 8192);
            BinaryPrimitives.WriteUInt16LittleEndian(fields.AsSpan(8), 2048);
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(fields.AsSpan(10 + axis * 2), unchecked((short)(rotation.Rotation[1 + axis * 3] / 6)));
            }
            for (int lane = 0; lane < 9; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(fields.AsSpan(16 + lane * 2), rotation.Rotation[lane]);
            }
            NativeBattleEffectArena arena = RequireArena(frame).Write(offset, fields).MarkUnknown(offset + 34, 2).Write(offset + 36, BitConverter.GetBytes(x)).Write(offset + 40, BitConverter.GetBytes(y)).Write(offset + 44, BitConverter.GetBytes(z));
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        return frame;
    }

    private static NativeBattleRinoa89Frame BeamSpawnSparks(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix basis, int count, bool lateral)
    {
        for (int index = 0; index < count; index++)
        {
            NativeBattleRinoa89SparkAllocation allocation = AllocateSpark6FCC90(frame);
            frame = allocation.Frame;
            if (allocation.Offset is not int offset)
            {
                break;
            }
            ushort angle = BeamRandom(ref frame);
            NativeBattleVector input = new(unchecked((short)NativeBattleCameraMath.Cosine(angle)), unchecked((short)NativeBattleCameraMath.Sine(angle)), lateral ? (short)0 : (short)8192);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, input);
            short x = unchecked((short)basis.X);
            short y = unchecked((short)basis.Y);
            short z = unchecked((short)basis.Z);
            if (lateral)
            {
                int radius = (BeamRandom(ref frame) & 0x200) + 512;
                int along = BeamRandom(ref frame) & 0xFFF;
                x = unchecked((short)(x + (along * basis.Rotation[2] >> 12) + (radius * velocity.X >> 12)));
                y = unchecked((short)(y + (radius * velocity.Y >> 12) + (along * basis.Rotation[5] >> 12)));
                z = unchecked((short)(z + (along * basis.Rotation[8] >> 12) + (radius * velocity.Z >> 12)));
                velocity = new(unchecked((short)(velocity.X >> 6)), unchecked((short)(velocity.Y >> 6)), unchecked((short)(velocity.Z >> 6)));
            }
            else
            {
                int speed = (BeamRandom(ref frame) & 0x7F) + 128;
                velocity = new(unchecked((short)(speed * velocity.X >> 12)), unchecked((short)(speed * velocity.Y >> 12)), unchecked((short)(speed * velocity.Z >> 12)));
            }
            byte[] spark = new byte[24];
            BinaryPrimitives.WriteUInt32LittleEndian(spark, 0x14A07F4);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(4), x);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(6), y);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(8), z);
            BinaryPrimitives.WriteUInt16LittleEndian(spark.AsSpan(10), 16);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(12), velocity.X);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(14), velocity.Y);
            BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(16), velocity.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(spark.AsSpan(18), 2048);
            frame = frame with { Memory = frame.Memory with { Arena = RequireArena(frame).Write(offset, spark) } };
        }
        return frame;
    }

    private static NativeBattleActionMatrix BeamRotationZ7012C0(short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
    }

    private static NativeBattleActionMatrix BeamRotationX701220(short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return new([4096, 0, 0, 0, cosine, sine, 0, unchecked((short)-sine), cosine], 0, 0, 0, 4096);
    }

    private static ushort BeamRandom(ref NativeBattleRinoa89Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
