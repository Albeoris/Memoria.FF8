namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep RunInitializer(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, uint initializer, NativeBattleBlastingZone164Inputs inputs)
    {
        switch (initializer)
        {
            case 0x5F0B10:
                int first = AtRetained(0x5F0B70);
                Set(first, 24, Rand() % 4096);
                Set(first, 26, 30);
                Set(first, 30, 928);
                Set(first, 28, 928);
                break;
            case 0x5F0CB0:
                int second = AtRetained(0x5F0D10);
                Set(second, 24, Rand() % 4096);
                Set(second, 26, 20);
                Set(second, 28, 6144);
                break;
            case 0x5F1440:
                for (int index = 0; index < 4; index++)
                {
                    int ring = Append(0x5F1500);
                    Set(ring, 14, index + Rand() % 8);
                    Set(ring, 16, TipX());
                    Set(ring, 18, 0);
                    Set(ring, 20, TipZ());
                    Set(ring, 24, Rand() % 4096);
                    Set(ring, 28, 5632);
                    Set(ring, 26, -32 - Rand() % 72);
                    int speed = (Rand() % 6144 + 3584) / 16;
                    Set(ring, 34, speed);
                    Set(ring, 32, speed);
                }
                break;
            case 0x5F1E00:
                int glow = AtRetained(0x5F1E70);
                Set(glow, 22, (2000 * inputs.ReadScale38(frame.Parameters.Actor78)) >> 12);
                Set(glow, 30, 128);
                Set(glow, 28, 128);
                break;
            case 0x5F1F40:
                int marker = Append(0x5F1F80);
                Set(marker, 16, 0);
                Set(marker, 20, 0);
                Set(marker, 18, -900);
                Set(marker, 30, 768);
                Set(marker, 28, 768);
                break;
            case 0x5F20D0:
                int column = AtTip(0x5F2120, 300);
                Set(column, 28, 16384);
                Set(column, 30, 512);
                Set(column, 32, 8192);
                break;
            case 0x5F3AE0:
                int pillar = AtTip(0x5F3B30, 0);
                Set(pillar, 28, 3584);
                Set(pillar, 32, 10240);
                Set(pillar, 34, 426);
                break;
            case 0x5F3C70:
                int core = AtTip(0x5F3DF0, -9000);
                Set(core, 24, Rand() % 4096);
                Set(core, 26, Rand() % 4096);
                Set(core, 28, 12288);
                Set(core, 32, 12288);
                for (int index = 0; index < 8; index++)
                {
                    int shard = Append(0x5F3FA0);
                    Set(shard, 16, Rand() % 3400 + TipX() - 1700);
                    Set(shard, 18, Rand() % 700 - 700 * index - 11350);
                    Set(shard, 20, Rand() % 3400 + TipZ() - 1700);
                    Set(shard, 24, (pool.Slots[shard].ReadWord(16) & 1) << 11);
                    Set(shard, 28, Rand() % 1792 + 512);
                    int speed = Rand() % 25 + 5;
                    if (pool.Slots[shard].ReadWord(16) < unchecked((ushort)TipX()))
                    {
                        speed = -speed;
                    }
                    Set(shard, 30, speed);
                    short scale = unchecked((short)(Rand() % 1280 + 512));
                    Set(shard, 32, scale > pool.Slots[shard].ReadWord(28) ? 2 * scale / 3 : scale);
                }
                break;
            case 0x5F4090:
                for (int index = 0; index < 6; index++)
                {
                    int flame = Append(0x5F4180);
                    Set(flame, 14, 3 * index);
                    Set(flame, 16, TipX());
                    int height = Rand();
                    Set(flame, 20, TipZ());
                    Set(flame, 18, height % 900 - 900 * index - 11450);
                    Set(flame, 24, Rand() % 4096);
                    Set(flame, 26, Rand() % 110 + 10);
                    int width = (Rand() % 3072 + 1536) / 4;
                    Set(flame, 30, width);
                    Set(flame, 28, width);
                    int speed = (Rand() % 3584 + 1024) / 10;
                    Set(flame, 34, speed);
                    Set(flame, 32, speed);
                }
                break;
            case 0x5F44A0:
                int disk = AtTip(0x5F44F0, -50);
                Set(disk, 28, 3840);
                Set(disk, 34, 18);
                break;
            case 0x5F46A0:
                for (int index = 0; index < 4; index++)
                {
                    int morph = Append(0x5F4720);
                    Set(morph, 14, 2 * index);
                    Set(morph, 16, TipX());
                    Set(morph, 18, -12200);
                    Set(morph, 20, TipZ());
                    Set(morph, 28, 3072);
                    Set(morph, 34, 320);
                    Set(morph, 32, 320);
                    Set(morph, 24, index == 0 ? 0 : (index + 3) << 9);
                }
                break;
            case 0x5F4940:
                int pulse = AtTip(0x5F49B0, -12200);
                Set(pulse, 24, Rand() % 4096);
                Set(pulse, 26, -16);
                Set(pulse, 30, 96);
                Set(pulse, 28, 96);
                break;
            case 0x5F4B00:
                int flare = AtTip(0x5F4B70, -12200);
                Set(flare, 24, Rand() % 4096);
                Set(flare, 26, -48);
                Set(flare, 30, 266);
                Set(flare, 28, 266);
                break;
            case 0x5F4F70:
                int screen = Append(0x5F4FA0);
                Set(screen, 28, 16384);
                Set(screen, 32, 7680);
                break;
            case 0x5F53F0:
                int eruption = AtTip(0x5F5430, 0);
                Set(eruption, 28, 4096);
                Set(eruption, 32, 8192);
                break;
            case 0x5F5580:
                int ending = Append(0x5F55D0);
                Set(ending, 16, 0);
                Set(ending, 18, 0);
                Set(ending, 30, 32);
                Set(ending, 28, 32);
                Set(ending, 20, -1100);
                Set(ending, 34, 1024);
                Set(ending, 32, 1024);
                break;
            default:
                throw new NotSupportedException($"Native Blasting Zone initializer0x{initializer:X} is unsupported.");
        }
        return new(frame, pool, 0);

        int Append(uint callback)
        {
            (pool, int created) = pool.Append(callback);
            pool = pool.WriteSlot(created, pool.Slots[created].WriteWord(12, 0));
            return created;
        }
        int AtRetained(uint callback)
        {
            int created = Append(callback);
            NativeBattleModulePoolTask task = TaskDword(TaskDword(pool.Slots[created], 16, unchecked((uint)ReadDword(frame.Staging, 0x23C9D98))), 20, unchecked((uint)ReadDword(frame.Staging, 0x23C9D9C)));
            pool = pool.WriteSlot(created, task);
            return created;
        }
        int AtTip(uint callback, int y)
        {
            int created = Append(callback);
            Set(created, 16, TipX());
            Set(created, 18, y);
            Set(created, 20, TipZ());
            return created;
        }
        int TipX() => ReadWord(frame.Staging, 0x23C9D98);
        int TipZ() => ReadWord(frame.Staging, 0x23C9D9C);
        void Set(int targetSlot, int offset, int value)
        {
            pool = pool.WriteSlot(targetSlot, pool.Slots[targetSlot].WriteWord(offset, unchecked((ushort)value)));
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
