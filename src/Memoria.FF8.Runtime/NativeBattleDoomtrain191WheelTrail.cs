using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191WheelTrail
{
    public static NativeBattleDoomtrain191LeafStep Advance642EF0(NativeBattleDoomtrain191Frame frame, int slot, int sourceLength, NativeBattleModuleResource sprite, NativeBattleDoomtrain191SteamServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x642EF0)
        {
            throw new InvalidDataException("Native Doomtrain wheel trail requires its actual task.");
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain wheel trail requires its retained work pointers.");
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain wheel trail requires its actual particle banks.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int first = checked((int)(state.WorkPointers[0] - state.ArenaAddress));
        int second = checked((int)(state.WorkPointers[1] - state.ArenaAddress));
        bool paused = (services.ReadFlags() & 1) != 0;
        NativeBattleActionMatrix camera = state.BaseView.ReadMatrix();
        NativeBattleScriptedLightingMemory lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        frame = frame with { Memory = frame.Memory with { ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        for (int index = 0; index < 170; index++)
        {
            if ((arena.Read(first + index * 24, 1)[0] & 1) == 0)
            {
                continue;
            }
            Draw(first + index * 24);
            NativeBattleModuleSpriteFrame selected = Draw(second + index * 24);
            if (!paused)
            {
                Store(first + index * 24 + 4, Word(first + index * 24 + 4) + 1);
                Store(second + index * 24 + 4, Word(second + index * 24 + 4) + 1);
                if (selected.NextOffset < 0)
                {
                    Store(first + index * 24 + 4, 0);
                    Store(second + index * 24 + 4, 0);
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        if (task.ReadWord(0xC) == 0)
        {
            frame = NativeBattleDoomtrain191Model.Select642470(frame, 1, sourceLength);
            for (int index = 0; index < 119; index++)
            {
                int free = 0;
                while (free < 170 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(first + 24 * free, 4)) != 0)
                {
                    free++;
                }
                if (free == 170)
                {
                    break;
                }
                var memory = NativeBattleDoomtrain191Model.Require(frame);
                int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(memory.Descriptor.Read(12, 4)) - state.ArenaAddress));
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(NativeBattleArenaPose.ReadMatrix(memory.Model, 0x40), NativeBattleArenaPose.ReadMatrix(arena, skeleton + 0x50));
                Seed(first + free * 24, matrix, -1200);
                Seed(second + free * 24, matrix, 1200);
                frame = NativeBattleDoomtrain191Model.Advance642CA0(frame, sourceLength);
            }
            frame = NativeBattleDoomtrain191Model.Select642470(frame, 1, sourceLength);
        }
        ushort next = unchecked((ushort)(task.ReadWord(0xC) + 1));
        int result = unchecked((short)next) >= 100 ? 2 : 0;
        if (result != 0)
        {
            for (int index = 0; index < 170; index++)
            {
                foreach (int start in new[] { first, second })
                {
                    int record = start + index * 24;
                    if ((arena.Read(record, 1)[0] & 1) != 0)
                    {
                        arena = arena.Write(record, new byte[4]);
                    }
                }
            }
        }
        pool = Pool(frame);
        frame = PutPool(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, next)) });
        return new(frame, result);

        NativeBattleModuleSpriteFrame Draw(int record)
        {
            ushort phase = unchecked((ushort)Word(record + 4));
            short size = Word(record + 6);
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, new(Word(record + 8), Word(record + 10), Word(record + 12)));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac.X, mac.Y, mac.Z);
            int offset = -(size >> 4);
            NativeBattleActionMatrix matrix = new([size, 0, 0, 0, size, 0, 0, 0, size], unchecked(mac.X + (unchecked(offset * direction.X) >> 12)), unchecked(mac.Y + (unchecked(offset * direction.Y) >> 12)), unchecked(mac.Z + (unchecked(offset * direction.Z) >> 12)), 4096);
            frame = services.Draw(frame, new(matrix, phase));
            return sprite.ProjectedFrame571C80(phase);
        }
        void Seed(int record, NativeBattleActionMatrix matrix, short x)
        {
            NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(x, 700, 0));
            byte[] data = new byte[14];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(6), 384);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), unchecked((short)(point.X + matrix.X)));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), unchecked((short)(point.Y + matrix.Y)));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), unchecked((short)(point.Z + matrix.Z)));
            arena = arena.Write(record, data).MarkUnknown(record + 14, 2);
        }
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
