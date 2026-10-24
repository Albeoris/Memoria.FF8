using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    private static NativeBattleRenzokuken160Frame EmitTarget161(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160Inputs inputs)
    {
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        Func<byte, int, short, NativeBattleVector> read = inputs.ReadBone ?? throw new NotSupportedException("Native5FD230 requires actual target bone240 endpoints.");
        NativeBattleVector start = read((byte)state.Core.FirstTarget, 240, 0);
        NativeBattleVector end = read((byte)state.Core.FirstTarget, 240, 4096);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)end.X) - unchecked((short)start.X), unchecked((short)end.Y) - unchecked((short)start.Y), unchecked((short)end.Z) - unchecked((short)start.Z));
        byte[] particles = state.TargetParticles.ToArray();
        for (int emitted = 0; emitted < 3; emitted++)
        {
            int offset = Free161(particles, 20);
            if (offset == particles.Length)
            {
                break;
            }
            Put161(particles, offset, 0);
            Put161(particles, offset + 2, Rand() % 1792 + 1536);
            Put161(particles, offset + 4, end.X);
            Put161(particles, offset + 6, end.Y);
            Put161(particles, offset + 8, end.Z);
            int speed = Rand() % 120 + 100;
            Put161(particles, offset + 12, Rand() % 20 + ((direction.X * speed) >> 12) - 10);
            Put161(particles, offset + 14, Rand() % 20 + ((direction.Y * speed) >> 12) - 10);
            Put161(particles, offset + 16, Rand() % 20 + ((direction.Z * speed) >> 12) - 10);
            Put161(particles, offset + 18, Rand() % 18 + 10);
        }
        return Store(frame, ReadState(frame) with { TargetParticles = particles.ToImmutableArray() });

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleRenzokuken160Frame EmitBurst161(NativeBattleRenzokuken160Frame frame, NativeBattleVector anchor)
    {
        byte[] particles = ReadState(frame).BurstParticles.ToArray();
        for (int offset = 0; offset < particles.Length - 40; offset += 20)
        {
            Put161(particles, offset, 0);
            Put161(particles, offset + 2, Rand() % 1536 + 5120);
            Put161(particles, offset + 4, anchor.X);
            Put161(particles, offset + 6, anchor.Y);
            Put161(particles, offset + 8, anchor.Z);
            int angle = Rand() % 4096;
            int speed = Rand() % 200 + 220;
            Put161(particles, offset + 12, (speed * NativeBattleCameraMath.Sine(angle)) >> 12);
            Put161(particles, offset + 16, (speed * NativeBattleCameraMath.Cosine(angle)) >> 12);
        }
        return Store(frame, ReadState(frame) with { BurstParticles = particles.ToImmutableArray() });

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static short Get161(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static void Put161(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
    private static int Free161(byte[] bytes, int stride)
    {
        for (int offset = 0; offset < bytes.Length; offset += stride)
        {
            if (Get161(bytes, offset) < 0)
            {
                return offset;
            }
        }
        return bytes.Length;
    }
}
