using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Chimney
{
    public static NativeBattleDoomtrain191LeafStep Advance643D10(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleModuleResource resource, NativeBattleDoomtrain191SteamServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x643D10)
        {
            throw new InvalidDataException("Native Doomtrain chimney requires its actual task.");
        }
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain chimney requires its retained module state.");
        NativeBattleActionMatrix anchor = (state.Model ?? throw new NotSupportedException("Native Doomtrain chimney requires its current creature anchor.")).Anchor.ReadMatrix();
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], -180, -1110, -9300, 4096);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), NativeBattleGfMatrices.Compose56C2F0(anchor, local));
        NativeBattleScriptedLightingMemory lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain chimney requires its current particle bank.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        int start = checked((int)(state.WorkPointers[0] - state.ArenaAddress));
        bool paused = (services.ReadFlags() & 1) != 0;
        int survivors = 0;
        for (int index = 0; index < 170; index++)
        {
            int record = checked(start + index * 24);
            if ((arena.Read(record, 1)[0] & 4) == 0)
            {
                continue;
            }
            ushort phase = unchecked((ushort)Word(record + 4));
            NativeBattleModuleSpriteFrame sprite = resource.ProjectedFrame571C80(phase);
            NativeBattleVector position = new(Word(record + 8), Word(record + 10), Word(record + 12));
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, position);
            short size = Word(record + 6);
            NativeBattleActionMatrix billboard = new([size, 0, 0, 0, size, 0, 0, 0, size], mac.X, mac.Y, mac.Z, 4096);
            frame = services.Draw(frame, new(billboard, phase) { ResourceAddress = 0xE342A4 });
            if (!paused)
            {
                Store(record + 4, phase + 1);
                if (sprite.NextOffset < 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
                else
                {
                    Store(record + 6, size + (size >> 3));
                    short velocity = Word(record + 18);
                    Store(record + 10, Word(record + 10) + velocity);
                    Store(record + 18, velocity - velocity / 11);
                    survivors++;
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        if (unchecked((short)task.ReadWord(0xC)) < 12)
        {
            for (int count = 0; count < 7; count++)
            {
                int index = 0;
                while (index < 170 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + index * 24, 4)) != 0)
                {
                    index++;
                }
                if (index == 170)
                {
                    break;
                }
                int record = checked(start + index * 24);
                arena = arena.Write(record, BitConverter.GetBytes(4u));
                Store(record + 4, 0);
                Store(record + 6, Random() % 144 + 144);
                Store(record + 8, Random() % 30 - 15);
                Store(record + 10, -(Random() % 40));
                Store(record + 12, Random() % 30 - 15);
                Store(record + 18, -115 - Random() % 240);
            }
        }
        ushort age = unchecked((ushort)(task.ReadWord(0xC) + 1));
        int result = 0;
        if (unchecked((short)age) >= 12 && survivors == 0)
        {
            for (int index = 0; index < 170; index++)
            {
                int record = start + index * 24;
                if ((arena.Read(record, 1)[0] & 4) != 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
            }
            result = 2;
        }
        pool = Pool(frame);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool.WriteSlot(slot, task.WriteWord(0xC, age))) }, Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } };
        return new(frame, result);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value)
        {
            arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        }
        int Random()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
}
