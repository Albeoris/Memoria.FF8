using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMasamuneParticlePreparation(NativeBattleModulePoolTask Task, NativeBattleEffectArena Arena, NativeCrtRandomState Random, bool Delayed);

public static class NativeBattleMasamuneParticles
{
    public const int Capacity = 200;
    public static int Bank(bool ring) => ring ? 0x2E000 : 0x2F800;
    public static int Stride(bool ring) => ring ? 24 : 32;

    public static NativeBattleMasamuneParticlePreparation Prepare(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, bool ring, bool paused, Action<NativeBattleVector>? observeDirection = null)
    {
        return PrepareWithPresentation(task, arena, random, ring, paused, observeDirection, false);
    }

    public static NativeBattleMasamuneParticlePreparation PrepareWithPresentation(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, bool ring, bool paused, Action<NativeBattleVector>? observeDirection = null, bool capturePresentation = false)
    {
        if (task.Callback != (ring ? 0x590F10u : 0x590B60u) || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Masamune particles require their actual raw callback task.");
        }
        if (ring && Signed(task, 14) > 0)
        {
            return new(paused ? task : task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))), arena, random, true);
        }
        if (!paused && Signed(task, 12) <= 2)
        {
            for (int birth = 0; birth < 8; birth++)
            {
                int record = -1;
                for (int index = 0; index < Capacity; index++)
                {
                    int current = checked(Bank(ring) + index * Stride(ring));
                    if (NativeBattleStaticMeshContext88.Dword(arena, current) == 0)
                    {
                        record = current;
                        break;
                    }
                }
                if (record < 0)
                {
                    break;
                }
                arena = NativeBattleStaticMeshContext88.Put(arena, record, unchecked((uint)(int)Signed(task, 0x16)));
                arena = PutWord(arena, record + 4, 0);
                int size = ring ? Draw() % 4096 + 1280 : Draw() % 2048 + 1536;
                arena = PutWord(arena, record + 6, size);
                int[] direction;
                if (ring)
                {
                    int angle = Draw() % 4096;
                    direction = [NativeBattleCameraMath.Sine(angle), 0, NativeBattleCameraMath.Cosine(angle)];
                }
                else
                {
                    int x = Draw() % 8192 - 4096;
                    int y = Draw() % 8192 - 4096;
                    int z = Draw() % 8192 - 4096;
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    direction = [normalized.X, normalized.Y, normalized.Z];
                }
                observeDirection?.Invoke(new(unchecked((short)direction[0]), unchecked((short)direction[1]), unchecked((short)direction[2])));
                int radius = ring ? Draw() % 300 + 400 : Draw() % 400 + 300;
                for (int axis = 0; axis < 3; axis++)
                {
                    arena = PutWord(arena, record + 8 + axis * 2, unchecked(direction[axis] * radius) >> 12);
                }
                if (ring)
                {
                    arena = PutWord(arena, record + 10, -(Draw() % 50));
                }
                int speed = ring ? Draw() % 200 + 90 : Draw() % 350 + 120;
                for (int axis = 0; axis < 3; axis++)
                {
                    if (ring && axis == 1)
                    {
                        continue;
                    }
                    arena = PutWord(arena, record + 0x10 + axis * 2, unchecked(direction[axis] * speed) >> 12);
                }
                if (!ring)
                {
                    arena = PutWord(arena, record + 0x1C, Draw() % 2048);
                    int angular = Draw() % 90 + 30;
                    arena = PutWord(arena, record + 0x1E, (angular & 1) != 0 ? -angular : angular);
                }
                if (capturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)Bank(ring)), (record - Bank(ring)) / Stride(ring));
                }
            }
        }
        return new(task, arena, random, false);

        int Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleEffectArena AdvanceRecord(NativeBattleEffectArena arena, int record, bool ring, bool dead)
    {
        if (dead)
        {
            return NativeBattleStaticMeshContext88.Put(arena, record, 0);
        }
        arena = PutWord(arena, record + 4, Word(arena, record + 4) + 1);
        for (int axis = 0; axis < 3; axis++)
        {
            if (ring && axis == 1)
            {
                continue;
            }
            int velocity = Word(arena, record + 0x10 + axis * 2);
            arena = PutWord(arena, record + 8 + axis * 2, Word(arena, record + 8 + axis * 2) + velocity);
            arena = PutWord(arena, record + 0x10 + axis * 2, velocity - (velocity >> 3));
        }
        if (!ring)
        {
            arena = PutWord(arena, record + 0x1C, Word(arena, record + 0x1C) + Word(arena, record + 0x1E));
        }
        return arena;
    }

    internal static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    internal static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }
}
