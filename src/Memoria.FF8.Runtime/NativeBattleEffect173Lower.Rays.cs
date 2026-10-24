using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect173Lower
{
    private static NativeBattleEffect173Step AdvanceRays(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        (frame, uint scratchAddress) = Allocate(frame, 136);
        NativeBattleEffectArena sprite = L173Word(L173Put(Context(frame, temporary, 180), 0, 0xD5E8F8), 0x24, 8);
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, 136);
        scratch = L173Put(L173Put(L173Put(scratch, 0x58, unchecked((uint)-4096)), 0x5C, 0), 0x60, 0);
        scratch = L173Store(scratch, 0, new(0, 0, 0));
        scratch = L173Rotation(scratch, 8, NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)).Rotation).MarkUnknown(26, 2);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint rowAddress = L173Records + (uint)(index * 24);
            NativeBattleEffectArena row = frame.State.ReadView(rowAddress, 24);
            if ((L173U(row, 0) & 4) == 0)
            {
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                scratch = L173Put(L173Put(scratch, 0x3C + axis * 4, unchecked((uint)L173S(row, 8 + axis * 2))), 0x68 + axis * 4, unchecked((uint)L173S(row, 16 + axis * 2)));
            }
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(-4096, 0, 0, L173S(row, 16), L173S(row, 18), L173S(row, 20));
            scratch = L173Put(L173Put(L173Put(scratch, 0x78, unchecked((uint)(int)alignment.Axis.X)), 0x7C, unchecked((uint)(int)alignment.Axis.Y)), 0x80, unchecked((uint)(int)alignment.Axis.Z));
            NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
            rotation = NativeBattleGfMatrices.Multiply56C090(rotation, NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0))) with { X = L173S(row, 8), Y = L173S(row, 10), Z = L173S(row, 12) };
            int scale = L173S(row, 6);
            scratch = L173Put(L173Put(L173Put(scratch, 0x48, unchecked((uint)scale)), 0x4C, unchecked((uint)(scale >> 1))), 0x50, unchecked((uint)(scale >> 1)));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale >> 1, scale >> 1);
            NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), rotation), null);
            scratch = CopyBytes(PackedArena(composed), 0, scratch, 0x28, 32);
            frame = PublishContext(frame, scratchAddress, scratch, 136);
            frame = LoadMatrix(frame, composed);
            sprite = L173Word(sprite, 4, L173S(row, 4));
            frame = L173Sprite(frame, temporary, sprite, resources, inputs);
            sprite = Context(frame, temporary, 180);
            if (!L173Paused(frame, inputs))
            {
                row = L173Word(row, 4, L173S(row, 4) + 1);
                if (L173S(sprite, 0x28) < 0)
                {
                    row = L173Put(row, 0, 0);
                }
                else
                {
                    int speed = L173S(row, 14);
                    row = L173Word(row, 8, L173S(row, 8) + ((speed * L173S(row, 16)) >> 12));
                    row = L173Word(row, 12, L173S(row, 12) + ((speed * L173S(row, 20)) >> 12));
                    survivors++;
                }
                frame = frame with { State = frame.State.WriteView(rowAddress, row, 24) };
            }
        }
        frame = Release(Release(frame, 136), 180);
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 2)
        {
            frame = BirthRays(frame, task);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next >= 4 && survivors == 0 ? 2 : 0);
    }

    public static NativeBattleEffect173Frame BirthRays(NativeBattleEffect173Frame frame, NativeBattleModulePoolTask task)
    {
        for (int birth = 0; birth < 7; birth++)
        {
            int free = L173Free(frame);
            if (free < 0)
            {
                break;
            }
            uint address = L173Records + (uint)(free * 24);
            NativeBattleEffectArena row = L173Word(L173Put(frame.State.ReadView(address, 24), 0, 4), 4, 0);
            row = L173Word(row, 6, L173Random(ref frame) % 4096 + 4096);
            row = CopyBytes(frame.State.ReadView(0x2398A90, 8), 0, row, 8, 8);
            row = L173Word(row, 8, L173S(row, 8) + L173Random(ref frame) % 5400 - 2700);
            row = L173Word(row, 12, L173S(row, 12) + L173Random(ref frame) % 2700 - 1350);
            row = L173Word(row, 14, L173Random(ref frame) % 70 + 30);
            short x = unchecked((short)(L173S(row, 8) - Signed(task, 16)));
            short z = unchecked((short)(L173S(row, 12) - Signed(task, 20)));
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, 0, z);
            row = L173Store(row, 16, new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z)));
            frame = frame with { State = frame.State.WriteView(address, row, 24) };
        }
        return frame;
    }

    private static NativeBattleEffect173Step AdvanceEmitter(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        if (L173Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age is >= 0 and <= 30)
        {
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(0, Signed(task, 24), 0));
            NativeBattleEffectArena direction = frame.State.ReadView(0x2398A80, 12);
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(rotation, unchecked((int)L173U(direction, 0)), unchecked((int)L173U(direction, 4)), unchecked((int)L173U(direction, 8)));
            task = task.WriteWord(24, unchecked((ushort)(Signed(task, 24) + Signed(task, 26))));
            frame = Write(frame, address, task);
            int factor = unchecked((L173Random(ref frame) % 4096 + 768) * (int)frame.State.ReadDword(0x2397278) * 2) >> 12;
            (frame, uint child) = Append(frame, 0x239A8D0, 0x5D90B0);
            if (child == 0)
            {
                throw new InvalidDataException("The selected emitter requires its admitted current task capacity.");
            }
            frame = NativeBattleEffect173Programme.Initialize8FB0(frame, child + 0x1C, 0xD61AD4, 64, resources);
            NativeBattleEffectArena anchor = frame.State.ReadView(0x2398AA0, 8);
            int[] components = [x, y, z];
            NativeBattleModulePoolTask spawned = Read(frame, child);
            for (int axis = 0; axis < 3; axis++)
            {
                int jitter = L173Random(ref frame) % 800 - 400;
                spawned = spawned.WriteWord(16 + axis * 2, unchecked((ushort)((unchecked(components[axis] * factor) >> 12) + L173S(anchor, axis * 2) + jitter)));
            }
            spawned = spawned.WriteWord(26, unchecked((ushort)(L173Random(ref frame) % 1024)));
            spawned = spawned.WriteWord(24, unchecked((ushort)(L173Random(ref frame) % 2560 + 1536)));
            frame = Write(frame, child, spawned);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next > 30 ? 2 : 0);
    }
}
