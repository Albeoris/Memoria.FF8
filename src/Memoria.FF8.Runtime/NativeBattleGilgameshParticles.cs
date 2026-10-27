using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshParticleStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleEffectArena Arena, NativeCrtRandomState Random, ImmutableArray<NativeBattleModulePacketBatch> Packets, int Survivors, int Result);

/// <summary>5915C0/591850/591B80 scan the original physical particle banks and reuse571C80.</summary>
public static partial class NativeBattleGilgameshParticles
{
    public static NativeBattleGilgameshParticleStep Step(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshParticleResources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, uint graphicsFlags)
    {
        if (task.PayloadLength != 24 || task.Callback is not (0x5915C0 or 0x591850 or 0x591B80))
        {
            throw new InvalidDataException("Native Gilgamesh particles require their original raw0x24-byte callback task.");
        }
        arena.Validate();
        resources.Validate();
        bool paused = (graphicsFlags & 0x201) != 0;
        bool sparks = task.Callback == 0x591850;
        bool streaks = task.Callback == 0x591B80;
        int bank = streaks ? 0x2F800 : 0x2E000;
        int stride = streaks ? 0x20 : 0x18;
        int mask = unchecked((short)task.ReadWord(0x16));
        int counter = unchecked((short)task.ReadWord(0x0C));
        if (!paused && counter <= (sparks || streaks ? 2 : 1))
        {
            int spawnCount = streaks ? 4 : sparks ? 6 : 2;
            for (int spawn = 0; spawn < spawnCount; spawn++)
            {
                int record = -1;
                for (int index = 0; index < 200; index++)
                {
                    int current = bank + index * stride;
                    if (Dword(arena, current) == 0)
                    {
                        record = current;
                        break;
                    }
                }
                if (record < 0)
                {
                    break;
                }
                (arena, random) = Spawn(task.Callback, arena, record, mask, random);
            }
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleActionMatrix baseView = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleModuleResource resource = streaks ? resources.Streak : sparks ? resources.Sparks : task.ReadWord(0x0E) == 0 ? resources.SourceA : resources.SourceB;
        ImmutableArray<NativeBattleModulePacketBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleModulePacketBatch>();
        int survivors = 0;
        for (int index = 0; index < 200; index++)
        {
            int record = bank + index * stride;
            if ((unchecked((int)Dword(arena, record)) & mask) == 0)
            {
                continue;
            }
            short phase = Word(arena, record + 4);
            short size = Word(arena, record + 6);
            NativeBattleModuleSpriteFrame frame = resource.ProjectedFrame571C80(streaks ? 0 : phase);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resource,
                SpriteFrame = frame,
                Phase = streaks ? (ushort)0 : unchecked((ushort)phase),
                Anchor = new(Word(arena, record + 8), Word(arena, record + 10), Word(arena, record + 12), 0),
                Scale = size,
                DepthOffset = sparks || streaks ? 0 : -(size >> 3),
                SpriteFlags = sparks ? (ushort)12 : (ushort)8,
                SpriteColor = task.ReadWord(0x0E) == 0 ? 0x808080u : 0x404080u
            };
            NativeBattleActionMatrix spriteView = baseView;
            if (streaks)
            {
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, Word(arena, record + 0x18), Word(arena, record + 0x1A), Word(arena, record + 0x1C));
                NativeBattleActionMatrix aligned = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), Word(arena, record + 8), Word(arena, record + 10), Word(arena, record + 12), 4096);
                aligned = NativeBattlePoseMatrices.Scale56BEF0(aligned, 3072, size, 0);
                NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(baseView, aligned);
                spriteView = camera with { Rotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096] };
                draw = draw with { Anchor = new(0, 0, 0, 0), SpriteWorldRotation = camera.Rotation };
                if (phase >= 6)
                {
                    int nextSize = unchecked((8188 - 682 * phase) * Word(arena, record + 0x0E)) >> 12;
                    arena = WriteWord(arena, record + 6, nextSize);
                }
            }
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = spriteView }, 0, 17, 2);
            packets.Add(batch);
            if (paused)
            {
                continue;
            }
            bool dead = streaks ? phase >= 12 : frame.NextOffset < 0;
            if (dead)
            {
                arena = WriteDword(arena, record, 0);
                continue;
            }
            arena = WriteWord(arena, record + 4, phase + 1);
            if (sparks || streaks)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int point = record + 8 + axis * 2;
                    int velocity = record + 0x10 + axis * 2;
                    int value = Word(arena, velocity);
                    arena = WriteWord(arena, point, Word(arena, point) + value);
                    arena = WriteWord(arena, velocity, value - (value >> 3));
                }
            }
            survivors++;
        }
        if (paused)
        {
            return new(task, arena, random, packets.ToImmutable(), survivors, 0);
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, arena, random, packets.ToImmutable(), survivors, survivors == 0 ? 2 : 0);
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena WriteDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
