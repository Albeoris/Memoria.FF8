using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect173Lower
{
    private static NativeBattleEffect173Step AdvanceOrbit(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        (frame, uint spriteAddress) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 64);
        NativeBattleEffectArena scratch = CopyBytes(frame.State.ReadView(0x2398AA0, 8), 0, Context(frame, scratchAddress, 64), 8, 8);
        NativeBattleEffectArena sprite = L173Word(L173Put(Context(frame, spriteAddress, 180), 0, 0xD5EAA4), 0x24, 0);
        for (int index = 0; index < 100; index++)
        {
            uint rowAddress = L173Records + (uint)(index * 24);
            NativeBattleEffectArena row = frame.State.ReadView(rowAddress, 24);
            if ((L173U(row, 0) & 1) == 0)
            {
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                scratch = L173Put(scratch, 0x30 + axis * 4, unchecked((uint)L173S(row, 8 + axis * 2)));
            }
            NativeBattleVector axisVector = L173Vector(row, 8);
            ImmutableArray<short> rotation = NativeBattleModuleAlignment.Rotation5714F0(L173S(row, 14), axisVector);
            scratch = L173Rotation(scratch, 16, rotation);
            NativeBattleActionMatrix matrix = new(rotation, 0, 0, 0, 4096);
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0, L173S(row, 16)));
            scratch = L173Store(scratch, 0, new(unchecked((short)(rotated.X + L173S(scratch, 8))), unchecked((short)(rotated.Y + L173S(scratch, 10))), unchecked((short)(rotated.Z + L173S(scratch, 12)))));
            frame = PublishContext(frame, scratchAddress, scratch, 64);
            int scale = L173S(row, 6);
            frame = L173Setup(frame, CopyBytes(scratch, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), unchecked((short)scale), unchecked((short)-(scale >> 4)), inputs);
            sprite = L173Word(Context(frame, spriteAddress, 180), 4, L173S(row, 4));
            sprite = L173Word(L173Put(sprite, 0, 0xD5EAA4), 0x24, 0);
            frame = L173Sprite(frame, spriteAddress, sprite, resources, inputs);
            if (!L173Paused(frame, inputs))
            {
                row = L173Word(row, 4, L173S(row, 4) + 1);
                if (L173S(Context(frame, spriteAddress, 180), 0x28) < 0)
                {
                    row = L173Put(row, 0, 0);
                }
                else
                {
                    row = L173Word(L173Word(row, 14, L173S(row, 14) + L173S(row, 22)), 16, L173S(row, 16) - L173S(row, 18));
                }
                frame = frame with { State = frame.State.WriteView(rowAddress, row, 24) };
            }
        }
        frame = Release(Release(frame, 64), 180);
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 12)
        {
            frame = BirthOrbit(frame);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next >= 36)
        {
            for (int index = 0; index < 100; index++)
            {
                uint row = L173Records + (uint)(index * 24);
                if ((frame.State.ReadDword(row) & 1) != 0)
                {
                    frame = frame with { State = frame.State.WriteDword(row, 0) };
                }
            }
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect173Frame BirthOrbit(NativeBattleEffect173Frame frame)
    {
        for (int birth = 0; birth < 3; birth++)
        {
            int free = L173Free(frame);
            if (free < 0)
            {
                break;
            }
            uint address = L173Records + (uint)(free * 24);
            NativeBattleEffectArena row = L173Word(L173Put(frame.State.ReadView(address, 24), 0, 1), 4, 0);
            row = L173Word(row, 6, L173Random(ref frame) % 1536 + 1536);
            short x = unchecked((short)(L173Random(ref frame) % 4096 - 2048));
            short y = unchecked((short)(L173Random(ref frame) % 4096 - 2048));
            short z = unchecked((short)(L173Random(ref frame) % 4096 - 2048));
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
            row = L173Store(row, 8, new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z)));
            row = L173Word(row, 14, L173Random(ref frame) % 4096);
            int radius = L173Random(ref frame) % 1200 + 400;
            row = L173Word(row, 16, radius);
            row = L173Word(row, 18, radius / (L173Random(ref frame) % 14 + 18));
            row = L173Word(row, 22, L173Random(ref frame) % 50 + 25);
            frame = frame with { State = frame.State.WriteView(address, row, 24) };
        }
        return frame;
    }

    private static NativeBattleEffect173Step AdvanceCloud(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena sprite = L173Word(L173Put(Context(frame, temporary, 180), 0, 0xD5E74C), 0x24, 4);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint rowAddress = L173Records + (uint)(index * 24);
            NativeBattleEffectArena row = frame.State.ReadView(rowAddress, 24);
            if ((L173U(row, 0) & 2) == 0)
            {
                continue;
            }
            int phase = L173S(row, 4);
            if ((uint)phase >= (uint)resources.Palette.Length)
            {
                throw new InvalidDataException("Cloud drawing leaves its actual frame-color directory.");
            }
            sprite = L173Put(L173Word(sprite, 4, phase), 0x1C, resources.Palette[phase]);
            int scale = L173S(row, 6);
            frame = L173Setup(frame, CopyBytes(row, 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), unchecked((short)scale), unchecked((short)-(scale >> 4)), inputs);
            frame = L173Sprite(frame, temporary, sprite, resources, inputs);
            sprite = Context(frame, temporary, 180);
            if (!L173Paused(frame, inputs))
            {
                row = L173Word(row, 4, phase + 1);
                if (L173S(sprite, 0x28) < 0)
                {
                    row = L173Put(row, 0, 0);
                }
                else
                {
                    row = L173Word(row, 6, scale + (scale >> 4));
                    foreach (int axis in new[] { 0, 2 })
                    {
                        int velocity = L173S(row, 16 + axis * 2);
                        row = L173Word(L173Word(row, 8 + axis * 2, L173S(row, 8 + axis * 2) + velocity), 16 + axis * 2, velocity - (velocity >> 5));
                    }
                    if (L173S(row, 10) < -300)
                    {
                        int velocity = L173S(row, 18);
                        row = L173Word(L173Word(row, 10, L173S(row, 10) + velocity), 18, velocity - (velocity >> 5));
                    }
                    survivors++;
                }
                frame = frame with { State = frame.State.WriteView(rowAddress, row, 24) };
            }
        }
        frame = Release(frame, 180);
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 30)
        {
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(0, Signed(task, 24), 0));
            NativeBattleEffectArena direction = frame.State.ReadView(0x2398A68, 12);
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(rotation, unchecked((int)L173U(direction, 0)), unchecked((int)L173U(direction, 4)), unchecked((int)L173U(direction, 8)));
            NativeBattleEffectArena rotated = L173Put(L173Put(L173Put(frame.State.ReadView(0x2398A80, 12), 0, unchecked((uint)x)), 4, unchecked((uint)y)), 8, unchecked((uint)z));
            frame = frame with { State = frame.State.WriteView(0x2398A80, rotated, 12) };
            frame = Write(frame, address, task.WriteWord(24, unchecked((ushort)(Signed(task, 24) + Signed(task, 26)))));
            frame = BirthCloud(frame, age);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next >= 4 && survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect173Frame BirthCloud(NativeBattleEffect173Frame frame, int age)
    {
        for (int birth = 0; birth < 2; birth++)
        {
            int free = L173Free(frame);
            if (free < 0)
            {
                break;
            }
            uint address = L173Records + (uint)(free * 24);
            NativeBattleEffectArena row = L173Word(L173Put(frame.State.ReadView(address, 24), 0, 2), 4, 0);
            row = L173Word(row, 6, L173Random(ref frame) % 1536 + 2560);
            row = CopyBytes(frame.State.ReadView(0x2398AA0, 8), 0, row, 8, 8);
            row = L173Word(row, 8, L173S(row, 8) + L173Random(ref frame) % 200 - 100);
            row = L173Word(row, 10, L173S(row, 10) + L173Random(ref frame) % 200 - age * 4);
            row = L173Word(row, 12, L173S(row, 12) + L173Random(ref frame) % 200 - 100);
            int speed = L173Random(ref frame) % 150 + 290;
            NativeBattleEffectArena direction = frame.State.ReadView(0x2398A80, 12);
            int x = unchecked((int)L173U(direction, 0) * speed) >> 12;
            int y = unchecked((int)L173U(direction, 4) * speed) >> 12;
            int z = unchecked((int)L173U(direction, 8) * speed) >> 12;
            row = L173Word(row, 16, x + L173Random(ref frame) % 20 - 10);
            int lift = L173Random(ref frame) % 120;
            row = L173Word(row, 18, y + lift + L173Random(ref frame) % 20 - 10);
            row = L173Word(row, 20, z + L173Random(ref frame) % 20 - 10);
            frame = frame with { State = frame.State.WriteView(address, row, 24) };
        }
        return frame;
    }

    private static int L173Free(NativeBattleEffect173Frame frame)
    {
        for (int index = 0; index < 100; index++)
        {
            if (frame.State.ReadDword(L173Records + (uint)(index * 24)) == 0)
            {
                return index;
            }
        }
        return -1;
    }
    private static NativeBattleEffectArena L173Rotation(NativeBattleEffectArena arena, int offset, ImmutableArray<short> rotation)
    {
        for (int index = 0; index < 9; index++)
        {
            arena = L173Word(arena, offset + index * 2, rotation[index]);
        }
        return arena;
    }
}
