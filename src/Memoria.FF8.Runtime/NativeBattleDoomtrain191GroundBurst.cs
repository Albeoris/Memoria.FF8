using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191GroundBurst
{
    public static NativeBattleDoomtrain191LeafStep Advance646F70(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleModuleResource sprite, NativeBattleDoomtrain191ExplosionServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x646F70)
        {
            throw new InvalidDataException("Native Doomtrain ground burst requires its actual task.");
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain ground burst requires its retained particle pointer.");
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain ground burst requires its actual particle bank.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int first = checked((int)(state.WorkPointers[0] - state.ArenaAddress));
        bool paused = (services.ReadFlags() & 1) != 0;
        uint mask = unchecked((uint)(int)Word(0xE));
        int age = Word(0xC);
        if (!paused && age <= 1)
        {
            for (int count = 0; count < 8; count++)
            {
                int free = 0;
                while (free < 170 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(first + free * 24, 4)) != 0)
                {
                    free++;
                }
                if (free == 170)
                {
                    break;
                }
                int record = first + free * 24;
                arena = arena.Write(record, BitConverter.GetBytes(mask));
                Store(record + 4, 0);
                Store(record + 6, Random() % 3072 + 1536);
                int angle = Random() % 4096;
                int size = Word(0x1C);
                int divisor = (size >> 1) & ~1;
                int radius = Random() % divisor - (size >> 2) + size;
                arena = arena.Write(record + 8, BitConverter.GetBytes(task.ReadDword(0x10))).Write(record + 12, BitConverter.GetBytes(task.ReadDword(0x14)));
                Store(record + 8, ParticleWord(record + 8) + (unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12));
                Store(record + 10, -(Random() % 200));
                Store(record + 12, ParticleWord(record + 12) + (unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12));
                int speed = Random() % 190 + 120;
                Store(record + 16, unchecked(NativeBattleCameraMath.Sine(angle) * speed) >> 12);
                Store(record + 20, unchecked(NativeBattleCameraMath.Cosine(angle) * speed) >> 12);
            }
        }
        NativeBattleActionMatrix camera = services.ReadCurrentView();
        var lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        frame = frame with { Memory = frame.Memory with { ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        int survivors = 0;
        for (int index = 0; index < 170; index++)
        {
            int record = first + index * 24;
            if ((BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)) & mask) == 0)
            {
                continue;
            }
            ushort phase = unchecked((ushort)ParticleWord(record + 4));
            short size = ParticleWord(record + 6);
            NativeBattleActionMatrix matrix = NativeBattleDoomtrain191Explosion.Billboard(camera, new(ParticleWord(record + 8), ParticleWord(record + 10), ParticleWord(record + 12)), size, -(size >> 3));
            frame = services.Draw(frame, new(matrix, phase) { ResourceAddress = 0xE34404, Flags = 12, Color = 0x586880 });
            if (!paused)
            {
                Store(record + 4, phase + 1);
                if (sprite.ProjectedFrame571C80(phase).NextOffset < 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
                else
                {
                    Store(record + 8, ParticleWord(record + 8) + ParticleWord(record + 16));
                    Store(record + 12, ParticleWord(record + 12) + ParticleWord(record + 20));
                    Store(record + 16, ParticleWord(record + 16) - (ParticleWord(record + 16) >> 2));
                    Store(record + 20, ParticleWord(record + 20) - (ParticleWord(record + 20) >> 2));
                    survivors++;
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        pool = Pool(frame);
        frame = PutPool(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)))) });
        return new(frame, survivors == 0 ? 2 : 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        short ParticleWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
