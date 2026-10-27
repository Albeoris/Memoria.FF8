using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance5790D0(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int contact = unchecked((short)task.ReadWord(14));
        if (contact is < 1 or > 6)
        {
            throw new InvalidDataException("Native5790D0 requires the reached contact selector1..6.");
        }
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] particles = state.ExtraStorage.ToArray();
        if (particles.Length != 3200)
        {
            throw new InvalidDataException("Native5790D0 requires its hundred physical32-byte particle rows.");
        }
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        short counter = unchecked((short)task.ReadWord(12));
        if (advance && counter <= 14)
        {
            int source = contact * 156;
            ImmutableArray<byte> impacts = state.Core.ImpactStates;
            int dx = BinaryPrimitives.ReadInt32LittleEndian(impacts.AsSpan().Slice(source + 8));
            int dy = BinaryPrimitives.ReadInt32LittleEndian(impacts.AsSpan().Slice(source + 12));
            int dz = BinaryPrimitives.ReadInt32LittleEndian(impacts.AsSpan().Slice(source + 16));
            for (int emitted = 0; emitted < 2; emitted++)
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
                BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(row), contact);
                Put161(particles, row + 4, 0);
                Put161(particles, row + 6, Rand() % 2432 + 1792);
                impacts.AsSpan().Slice(source, 8).CopyTo(particles.AsSpan(row + 8));
                for (int axis = 0; axis < 3; axis++)
                {
                    Put161(particles, row + 8 + 2 * axis, Get161(particles, row + 8 + 2 * axis) + Rand() % 140 - 70);
                }
                int speed = Rand() % 170 + 25;
                Put161(particles, row + 16, Rand() % 80 + ((speed * dx) >> 12) - 40);
                Put161(particles, row + 18, Rand() % 60 + ((speed * dy) >> 12) - 30);
                Put161(particles, row + 20, Rand() % 80 + ((speed * dz) >> 12) - 40);
            }
        }
        int alive = 0;
        for (int row = 0; row < particles.Length; row += 32)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(particles.AsSpan(row)) != contact)
            {
                continue;
            }
            short age = Get161(particles, row + 4);
            short scale = Get161(particles, row + 6);
            NativeBattleVector position = new(Get161(particles, row + 8), Get161(particles, row + 10), Get161(particles, row + 12));
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(new(sprite, unchecked((ushort)age), position, scale, null, 0), view, projection, cameraRoll);
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
            Put161(particles, row + 6, scale + (scale >> 4));
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = Get161(particles, row + 16 + axis * 2);
                Put161(particles, row + 8 + axis * 2, Get161(particles, row + 8 + axis * 2) + velocity);
                Put161(particles, row + 16 + axis * 2, velocity - (velocity >> 4));
            }
            Put161(particles, row + 18, Get161(particles, row + 18) + 5);
            alive++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = particles.ToImmutableArray() });
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
