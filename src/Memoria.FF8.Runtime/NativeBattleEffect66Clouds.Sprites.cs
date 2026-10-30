using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    private static NativeBattleEffect66Step AdvanceSpritePool(NativeBattleEffect66Frame frame, uint callback, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        (frame, uint outerAddress) = Allocate(frame, 72);
        NativeBattleEffectArena outer = Context(frame, outerAddress, 72);
        bool broad = callback == 0x626F80;
        bool first = callback == 0x624DF0;
        uint pool = first ? FirstPointAddress : SecondPointAddress;
        int slots = first ? 100 : 200;
        int age = unchecked((short)Read(frame, task).ReadWord(12));
        int kind = broad ? 1 : unchecked((short)Read(frame, task).ReadWord(14));
        int limit = callback switch { 0x624DF0 => 4, 0x625110 => 1, 0x625470 => 8, _ => 110 };
        int births = callback switch { 0x625110 => 5, 0x626F80 => 2, _ => 1 };
        if (!Paused(frame, inputs) && age <= limit)
        {
            outer = NativeBattleEffectNodeContextMemory.Copy(broad ? frame.State.ReadView(CenterAddress, 8) : ReadBytes(frame, task + 0x10, 8), 0, outer, 0, 8);
            for (int birth = 0; birth < births; birth++)
            {
                uint address = Free(frame, pool, slots);
                if (address == 0)
                {
                    break;
                }
                NativeBattleEffectArena row = Word(Put(ReadBytes(frame, address, 24), 0, kind), 4, 0);
                int scale;
                if (first && age == 0)
                {
                    scale = 0x1200;
                }
                else
                {
                    frame = Random(frame, out int random);
                    scale = callback switch { 0x624DF0 => random % 0xC00 + 0x600, 0x625110 => (random & 1023) + 768, 0x625470 => random % 0x880 + 0x600, _ => (random & 8191) + 0x1600 };
                }
                row = Word(row, 6, scale);
                if (callback == 0x625470 || broad)
                {
                    row = NativeBattleEffectNodeContextMemory.Copy(outer, 0, row, 8, 8);
                }
                if (callback != 0x625470)
                {
                    int range = broad ? 7600 : first ? 800 : 1000;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = Random(frame, out int random);
                        row = Word(row, 8 + axis * 2, S16(outer, axis * 2) + random % range - range / 2);
                    }
                }
                if (callback is 0x625110 or 0x625470)
                {
                    int x = S16(row, 8) - unchecked((short)frame.State.ReadWord(CenterAddress));
                    int y = S16(row, 10) - unchecked((short)frame.State.ReadWord(CenterAddress + 2));
                    int z = S16(row, 12) - unchecked((short)frame.State.ReadWord(CenterAddress + 4));
                    var unit = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    outer = Put(Put(Put(outer, 0x28, unit.X), 0x2C, unit.Y), 0x30, unit.Z);
                    frame = Random(frame, out int random);
                    int speed = callback == 0x625110 ? random % 300 + 150 : random % 250 + 130;
                    row = Word(Word(Word(row, 16, unchecked(unit.X * speed) >> 12), 18, unchecked(unit.Y * speed) >> 12), 20, unchecked(unit.Z * speed) >> 12);
                }
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(view), 0, outer, 8, 32);
        frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory, view) };
        uint source = callback switch { 0x624DF0 => 0xDE43E8, 0x625110 => 0xDE4910, 0x625470 => 0xDE423C, _ => 0xDE45D0 };
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, BitConverter.GetBytes((short)8));
        int remaining = 0;
        for (int index = 0; index < slots; index++)
        {
            uint address = checked(pool + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, address, 24);
            if (broad ? (U32(row, 0) & 1) == 0 : unchecked((int)U32(row, 0)) != kind)
            {
                continue;
            }
            int radius = broad ? -(S16(row, 6) >> 3) : -unchecked((short)frame.State.ReadWord(CenterAddress + 6));
            (frame, outer) = SpritePoint(frame, outer, row, 8, S16(row, 6), radius);
            sprite = Word(sprite, 4, S16(row, 4));
            frame = PublishContext(PublishContext(frame, outerAddress, outer, 72), spriteAddress, sprite, 180);
            frame = NativeBattleEffect66Drawing.DrawSprite(frame, spriteAddress, 2, 0x1D8E054, resources, inputs, NativeBattleEffect66VisualIdentity.Point(frame, address, U32(sprite, 0)));
            sprite = Context(frame, spriteAddress, 180);
            if (!Paused(frame, inputs))
            {
                row = ReadBytes(frame, address, 24);
                if (S16(sprite, 0x28) < 0)
                {
                    row = Put(row, 0, 0);
                }
                else
                {
                    row = Word(row, 4, S16(row, 4) + 1);
                    if (callback is 0x625110 or 0x625470)
                    {
                        if (callback == 0x625470)
                        {
                            int scale = S16(row, 6);
                            row = Word(row, 6, scale + (scale >> 5));
                        }
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int velocity = S16(row, 16 + axis * 2);
                            row = Word(row, 8 + axis * 2, S16(row, 8 + axis * 2) + velocity);
                            row = Word(row, 16 + axis * 2, velocity - (velocity >> 3));
                        }
                    }
                    remaining++;
                }
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        frame = PublishContext(frame, outerAddress, outer, 72);
        frame = Release(Release(frame, 180), 72);
        if (!broad)
        {
            return Finish(frame, task, remaining, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int next = unchecked((short)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, unchecked((ushort)next)));
        if (next < 120)
        {
            return new(frame, 0);
        }
        for (int index = 0; index < slots; index++)
        {
            uint address = checked(pool + (uint)(index * 24));
            if ((U32(ReadBytes(frame, address, 4), 0) & 1) != 0)
            {
                frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear(), 4);
            }
        }
        return new(frame, 2);
    }
}
