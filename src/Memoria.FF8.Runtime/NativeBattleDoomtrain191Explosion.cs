using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191ExplosionServices(Func<uint> ReadFlags, Func<NativeBattleActionMatrix> ReadCurrentView, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191SteamDraw, NativeBattleDoomtrain191Frame> Draw);

public static class NativeBattleDoomtrain191Explosion
{
    public static NativeBattleDoomtrain191Frame Spawn646570(NativeBattleDoomtrain191Frame frame)
    {
        int count = Random() % 4 + 5;
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectTaskPool pool = Pool(frame);
            (pool, int slot) = pool.Append(0x646640);
            ushort mask = unchecked((ushort)(1u << (index + 1)));
            ushort x = unchecked((ushort)(Random() % 1000 + 1000));
            ushort y = unchecked((ushort)(Random() % 1200 - 800));
            ushort z = unchecked((ushort)(Random() % 9000 - 4300));
            ushort delay = unchecked((ushort)(Random() % 12 + 2 * index));
            ushort size = unchecked((ushort)(Random() % 4096 + 5632));
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0xE, mask).WriteWord(0x10, x).WriteWord(0x12, y).WriteWord(0x14, z).WriteWord(0x16, delay).WriteWord(0x1C, size);
            frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task) });
        }
        return frame;

        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleDoomtrain191LeafStep Advance646640(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191ParticleResources resources, NativeBattleDoomtrain191ExplosionServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x646640)
        {
            throw new InvalidDataException("Native Doomtrain explosion requires its actual task.");
        }
        bool paused = (services.ReadFlags() & 1) != 0;
        if (Word(0x16) > 0)
        {
            if (!paused)
            {
                frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(0x16, unchecked((ushort)(task.ReadWord(0x16) - 1)))) });
            }
            return new(frame, 0);
        }
        int age = Word(0xC);
        if (age < 5)
        {
            NativeBattleActionMatrix flash = Billboard(services.ReadCurrentView(), new(Word(0x10), Word(0x12), Word(0x14)), Word(0x1C), -(Word(0x1C) >> 3));
            frame = services.Draw(frame, new(flash, unchecked((ushort)age)) { ResourceAddress = 0xE3460C, Flags = 0x208 });
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain explosion requires its retained work pointer.");
        NativeBattleActionMatrix origin = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), origin);
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain explosion requires its current particle arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int first = checked((int)(state.WorkPointers[4] - state.ArenaAddress));
        uint mask = unchecked((uint)(int)Word(0xE));
        NativeBattleModuleResource sprite = resources.RequireSprite(0xE34548);
        int survivors = 0;
        for (int index = 0; index < 130; index++)
        {
            int record = first + index * 24;
            if (BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)) != mask)
            {
                continue;
            }
            ushort phase = unchecked((ushort)ParticleWord(record + 4));
            short size = ParticleWord(record + 6);
            NativeBattleActionMatrix alignment = new(NativeBattleModuleAlignment.DownToVelocity573C40(ParticleWord(record + 16), ParticleWord(record + 18), ParticleWord(record + 20)), 0, 0, 0, 4096);
            NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), 4096, size, 4096);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(alignment, scaled) with { X = ParticleWord(record + 8), Y = ParticleWord(record + 10), Z = ParticleWord(record + 12) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(camera, local);
            frame = services.Draw(frame, new(matrix, phase) { ResourceAddress = 0xE34548, Flags = 0x208 });
            if (!paused)
            {
                Store(record + 4, phase + 1);
                if (sprite.ProjectedFrame571C80(phase).NextOffset < 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
                else
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int at = record + 16 + 2 * axis;
                        short velocity = ParticleWord(at);
                        Store(record + 8 + 2 * axis, ParticleWord(record + 8 + 2 * axis) + velocity);
                        Store(at, velocity - (velocity >> 1));
                    }
                    survivors++;
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        if (age >= 0 && age <= 4)
        {
            for (int count = 0; count < 6; count++)
            {
                int free = 0;
                while (free < 130 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(first + free * 24, 4)) != 0)
                {
                    free++;
                }
                if (free == 130)
                {
                    break;
                }
                int record = first + free * 24;
                arena = arena.Write(record, BitConverter.GetBytes(mask));
                Store(record + 4, 0);
                Store(record + 6, Random() % 4096 + 5120);
                int x = Random() % 4096 - 2048;
                int y = Random() % 4096 - 2048;
                int z = Random() % 4096 - 2048;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int radius = Random() % 400 + 300;
                Store(record + 8, unchecked(direction.X * radius) >> 12);
                Store(record + 10, unchecked(direction.Y * radius) >> 12);
                Store(record + 12, unchecked(direction.Z * radius) >> 12);
                int speed = Random() % 260 + 220;
                Store(record + 16, unchecked(direction.X * speed) >> 12);
                Store(record + 18, unchecked(direction.Y * speed) >> 12);
                Store(record + 20, unchecked(direction.Z * speed) >> 12);
            }
        }
        ushort next = unchecked((ushort)(age + 1));
        pool = Pool(frame);
        frame = PutPool(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, next)) });
        return new(frame, unchecked((short)next) >= 4 && survivors == 0 ? 2 : 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        short ParticleWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    internal static NativeBattleActionMatrix Billboard(NativeBattleActionMatrix camera, NativeBattleVector point, short size, int offset)
    {
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, point);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac.X, mac.Y, mac.Z);
        return new([size, 0, 0, 0, size, 0, 0, 0, size], unchecked(mac.X + (unchecked(direction.X * offset) >> 12)), unchecked(mac.Y + (unchecked(direction.Y * offset) >> 12)), unchecked(mac.Z + (unchecked(direction.Z * offset) >> 12)), 4096);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
