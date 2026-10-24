using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static (NativeBattleRenzokuken160Frame Frame, NativeBattleEffectTaskPool Pool, int Slot) AllocateStoredTask(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, uint callback)
    {
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] storage = state.ExtraStorage.ToArray();
        if (storage.Length != 3200)
        {
            throw new InvalidDataException("Native special Renzokuken stored task requires its hundred physical rows.");
        }
        int row = 0;
        while (row < 100 && BinaryPrimitives.ReadInt32LittleEndian(storage.AsSpan(row * 32)) != 0)
        {
            row++;
        }
        if (row == 100)
        {
            throw new NotSupportedException("Native special Renzokuken stored task has exhausted its original local rows.");
        }
        BinaryPrimitives.WriteInt32LittleEndian(storage.AsSpan(row * 32), -1);
        frame = Store(frame, state with { ExtraStorage = storage.ToImmutableArray() });
        (pool, int slot) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 0).WriteWord(14, checked((ushort)row))) };
        return (frame, pool, slot);
    }

    public static NativeBattleRenzokuken160ChildStep AdvanceStoredBurst(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll, int emissionLastFrame)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] bank = state.ExtraStorage.ToArray();
        int source = StoredOffset(task);
        short counter = unchecked((short)task.ReadWord(12));
        int tag = unchecked((short)task.ReadWord(26));
        if ((inputs.GraphicsFlags & 0x201) == 0 && counter <= emissionLastFrame)
        {
            int dx = StoredRand(1536) + StoredWord(bank, source + 24) - 768;
            int dy = StoredWord(bank, source + 26) - StoredRand(768) - 256;
            int dz = StoredRand(256) + StoredWord(bank, source + 28) + 128;
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
            NativeBattleActionMatrix actor = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken burst requires its current actor matrix64.");
            var rotated = NativeBattleGfMatrices.RotateLong56C600(actor, direction.X, direction.Y, direction.Z);
            for (int emission = 0; emission < 3; emission++)
            {
                int row = 0;
                while (row < 100 && BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(row * 32)) != 0)
                {
                    row++;
                }
                if (row == 100)
                {
                    break;
                }
                int offset = row * 32;
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), tag);
                StoredWrite(bank, offset + 4, 0);
                StoredWrite(bank, offset + 6, StoredRand(2176) + 1792);
                bank.AsSpan(source + 8, 8).CopyTo(bank.AsSpan(offset + 8, 8));
                int speed = StoredRand(150) + 35;
                StoredWrite(bank, offset + 16, StoredRand(40) + (unchecked(speed * rotated.X) >> 12) - 20);
                StoredWrite(bank, offset + 18, unchecked(speed * rotated.Y) >> 12);
                StoredWrite(bank, offset + 20, unchecked(speed * rotated.Z) >> 12);
            }
        }
        int alive = 0;
        for (int row = 0; row < 100; row++)
        {
            int offset = row * 32;
            if (BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(offset)) != tag)
            {
                continue;
            }
            NativeBattleRenzokuken160Sprite current = new(sprite, unchecked((ushort)StoredWord(bank, offset + 4)), new(StoredWord(bank, offset + 8), StoredWord(bank, offset + 10), StoredWord(bank, offset + 12)), StoredWord(bank, offset + 6), null, 0);
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(current, view, projection, cameraRoll);
            frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialSpriteDraw(drawn.Packets));
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            if (drawn.AnimationEnded)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), 0);
                continue;
            }
            StoredWrite(bank, offset + 4, StoredWord(bank, offset + 4) + 1);
            short scale = StoredWord(bank, offset + 6);
            StoredWrite(bank, offset + 6, scale + (scale >> 4));
            for (int axis = 0; axis < 3; axis++)
            {
                short velocity = StoredWord(bank, offset + 16 + 2 * axis);
                StoredWrite(bank, offset + 8 + 2 * axis, StoredWord(bank, offset + 8 + 2 * axis) + velocity);
                StoredWrite(bank, offset + 16 + 2 * axis, velocity - (velocity >> 4));
            }
            alive++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        if (alive == 0)
        {
            bank = ReadState(frame).ExtraStorage.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(source), 0);
            frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, alive == 0 ? 2 : 0);

        int StoredRand(int maximum)
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value % maximum;
        }
    }

    private static int StoredOffset(NativeBattleModulePoolTask task)
    {
        int index = unchecked((short)task.ReadWord(14));
        if (index is < 0 or >= 100)
        {
            throw new InvalidDataException("Native special Renzokuken task index exceeds its retained rows.");
        }
        return index * 32;
    }

    private static short StoredWord(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));

    private static void StoredWrite(byte[] bytes, int offset, int value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
    }
}
