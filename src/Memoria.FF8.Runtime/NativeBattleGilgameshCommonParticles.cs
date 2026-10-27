using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>593770/5939C0 operate directly on the shared physical particle banks.</summary>
public static partial class NativeBattleGilgameshCommonParticles
{
    public static NativeBattleGilgameshParticleStep Step(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshCommonParticleResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags)
    {
        ValidateTask(task);
        if (task.Callback is not (0x593770 or 0x5939C0))
        {
            throw new NotSupportedException("Native Gilgamesh common particles require their selected finite callback.");
        }
        arena.Validate();
        resources.Validate();
        bool moving = task.Callback == 0x5939C0;
        bool paused = (graphicsFlags & 0x201) != 0;
        int bank = moving ? 0x2F800 : 0x2E000;
        int stride = moving ? 0x20 : 0x18;
        uint mask = moving ? 0x10u : 1u;
        int counter = Signed(task, 12);
        if (!paused && counter <= (moving ? 8 : 62))
        {
            for (int spawn = 0; spawn < (moving ? 12 : 8); spawn++)
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
                (arena, random) = Spawn(arena, record, counter, moving, random);
            }
        }
        ImmutableArray<NativeBattleModulePacketBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleModulePacketBatch>();
        int survivors = 0;
        for (int index = 0; index < 200; index++)
        {
            int record = bank + index * stride;
            if ((Dword(arena, record) & mask) == 0)
            {
                continue;
            }
            short phase = Word(arena, record + 4);
            NativeBattleModuleSpriteFrame frame = resources.Sprite.ProjectedFrame571C80(phase);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resources.Sprite,
                SpriteFrame = frame,
                Phase = unchecked((ushort)phase),
                Anchor = new(Word(arena, record + 8), Word(arena, record + 10), Word(arena, record + 12), 0),
                Scale = Word(arena, record + 6),
                DepthOffset = 0,
                SpriteFlags = 0
            };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = worldView }, 0, 17, 2);
            packets.Add(batch);
            if (paused)
            {
                continue;
            }
            if (frame.NextOffset < 0)
            {
                arena = WriteDword(arena, record, 0);
                continue;
            }
            arena = WriteWord(arena, record + 4, phase + 1);
            if (moving)
            {
                foreach (int axis in new[] { 0, 4 })
                {
                    int velocity = Word(arena, record + 0x10 + axis);
                    arena = WriteWord(arena, record + 8 + axis, Word(arena, record + 8 + axis) + velocity);
                    arena = WriteWord(arena, record + 0x10 + axis, velocity - (velocity >> 3));
                }
            }
            survivors++;
        }
        if (paused)
        {
            return new(task, arena, random, packets.ToImmutable(), survivors, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        int result = moving ? survivors == 0 ? 2 : 0 : Signed(task, 12) >= 68 ? 2 : 0;
        if (!moving && result == 2)
        {
            for (int index = 0; index < 200; index++)
            {
                int record = bank + index * stride;
                if ((Dword(arena, record) & 1) != 0)
                {
                    arena = WriteDword(arena, record, 0);
                }
            }
        }
        return new(task, arena, random, packets.ToImmutable(), survivors, result);
    }

    private static (NativeBattleEffectArena Arena, NativeCrtRandomState Random) Spawn(NativeBattleEffectArena arena, int record, int counter, bool moving, NativeCrtRandomState random)
    {
        int radius = moving ? 145 * counter + 200 : 27 * (100 - counter);
        arena = WriteDword(arena, record, moving ? 0x10u : 1u);
        arena = WriteWord(arena, record + 4, 0);
        arena = WriteWord(arena, record + 6, Draw() % 2048 + 1536);
        int angle = Draw() % 4096;
        int sine = NativeBattleCameraMath.Sine(angle);
        int cosine = NativeBattleCameraMath.Cosine(angle);
        arena = WriteWord(arena, record + 8, unchecked(sine * radius) >> 12);
        arena = WriteWord(arena, record + 10, -(Draw() % 50));
        arena = WriteWord(arena, record + 12, unchecked(cosine * radius) >> 12);
        int spread = moving ? 60 : 40;
        arena = WriteWord(arena, record + 8, Word(arena, record + 8) + Draw() % spread - spread / 2);
        arena = WriteWord(arena, record + 12, Word(arena, record + 12) + Draw() % spread - spread / 2);
        if (moving)
        {
            int speed = Draw() % 100 + 150;
            arena = WriteWord(arena, record + 0x10, sine * speed >> 12);
            arena = WriteWord(arena, record + 0x14, cosine * speed >> 12);
        }
        return (arena, random);

        int Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task is null || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native Gilgamesh common callbacks require their raw0x24-byte task.");
        }
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

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
