using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenSpecialCameraWord(short Value) : NativeBattleRenzokuken160Operation;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance57B880(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        short delay = unchecked((short)task.ReadWord(14));
        if (delay > 0)
        {
            if (advance)
            {
                pool = pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(14, (ushort)(delay - 1))) };
            }
            return new(frame, pool, 0);
        }
        short counter = unchecked((short)task.ReadWord(12));
        int value = (200 * NativeBattleCameraMath.Cosine((counter << 10) / 10 + 1024)) >> 12;
        if ((counter & 1) == 0)
        {
            value = -value;
        }
        frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialCameraWord(unchecked((short)value)));
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, unchecked((short)task.ReadWord(12)) >= 10 ? 2 : 0);
    }

    public static NativeBattleRenzokuken160ChildStep Advance57B520(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleSearchSpriteResource sprite, Func<byte, NativeBattleModuleAnchor> readAnchor, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        short delay = unchecked((short)task.ReadWord(14));
        if (delay > 0)
        {
            if (advance)
            {
                pool = pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(14, (ushort)(delay - 1))) };
            }
            return new(frame, pool, 0);
        }
        short counter = unchecked((short)task.ReadWord(12));
        byte[] particles = ReadState(frame).ExtraStorage.ToArray();
        if (particles.Length != 3200)
        {
            throw new NotSupportedException("Native57B520 requires its100 physical32-byte particle rows.");
        }
        if (advance && counter <= 3)
        {
            if (counter == 0)
            {
                NativeBattleModuleAnchor anchor = readAnchor((byte)ReadState(frame).Core.FirstTarget);
                task = task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W));
            }
            for (int emitted = 0; emitted < 16; emitted++)
            {
                int row = 0;
                while (row < particles.Length && BinaryPrimitives.ReadInt32LittleEndian(particles.AsSpan(row)) != 0)
                {
                    row += 32;
                }
                if (row == particles.Length)
                {
                    break;
                }
                BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(row), 4);
                Put161(particles, row + 4, 0);
                Put161(particles, row + 6, Rand() % 3072 + 6144);
                int angle = Rand() % 4096;
                int cosine = NativeBattleCameraMath.Cosine(angle);
                int sine = NativeBattleCameraMath.Sine(angle);
                int radius = Rand() % 300 + 400;
                Put161(particles, row + 8, (radius * cosine) >> 12);
                Put161(particles, row + 10, -(Rand() % 50));
                Put161(particles, row + 12, (radius * sine) >> 12);
                int speed = Rand() % 160 + 250;
                Put161(particles, row + 16, (speed * cosine) >> 12);
                Put161(particles, row + 20, (speed * sine) >> 12);
            }
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = unchecked((short)task.ReadWord(16)), Y = 0, Z = unchecked((short)task.ReadWord(20)) };
        NativeBattleActionMatrix transform = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        int survivors = 0;
        for (int row = 0; row < particles.Length; row += 32)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(particles.AsSpan(row)) != 4)
            {
                continue;
            }
            short age = Get161(particles, row + 4);
            short scale = Get161(particles, row + 6);
            NativeBattleVector position = new(Get161(particles, row + 8), Get161(particles, row + 10), Get161(particles, row + 12));
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(new(sprite, unchecked((ushort)age), position, scale, null, 8), transform, projection, cameraRoll);
            frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialSpriteDraw(drawn.Packets));
            if (!advance)
            {
                continue;
            }
            if (drawn.AnimationEnded)
            {
                BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(row), 0);
                continue;
            }
            Put161(particles, row + 4, age + 1);
            Put161(particles, row + 8, position.X + Get161(particles, row + 16));
            Put161(particles, row + 12, position.Z + Get161(particles, row + 20));
            short x = Get161(particles, row + 16);
            short z = Get161(particles, row + 20);
            Put161(particles, row + 16, x - (x >> 3));
            Put161(particles, row + 20, z - (z >> 3));
            survivors++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = particles.ToImmutableArray() });
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, survivors == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
