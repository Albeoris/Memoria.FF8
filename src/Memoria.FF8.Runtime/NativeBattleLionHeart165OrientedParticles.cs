using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceOrientedParticles(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x5ECE60 or 0x5EE4C0))
        {
            throw new NotSupportedException("Native Lion Heart oriented particle callback is unsupported.");
        }
        bool fan = callback == 0x5ECE60;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        int marker = Word(task, 14);
        int localLength = fan ? 104 : 120;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, localLength);
        uint temporary = allocation.Address;
        frame = frame with { Staging = allocation.Staging };
        if (!paused && counter <= (fan ? 0 : 4))
        {
            for (int index = 0; index < (fan ? 12 : 8); index++)
            {
                uint row = 0;
                for (uint candidate = 0x23C4780; candidate < 0x23C5680; candidate += 32)
                {
                    if (ReadMemoryDword(frame.Staging, candidate) == 0)
                    {
                        row = candidate;
                        break;
                    }
                }
                if (row == 0)
                {
                    break;
                }
                NativeBattleFileStaging staging = SetMemory(WriteMemoryDword(frame.Staging, row, unchecked((uint)marker)), row + 4, 0);
                staging = SetMemory(staging, row + 6, Rand() % 6144 + (fan ? 3072 : 7168));
                int dx;
                int dy;
                int dz;
                int angle = 0;
                if (fan)
                {
                    angle = Rand() % 4096;
                    dx = -NativeBattleCameraMath.Sine(angle);
                    dy = NativeBattleCameraMath.Cosine(angle);
                    dz = 0;
                    staging = WriteMemoryDword(WriteMemoryDword(staging, temporary + 72, unchecked((uint)dx)), temporary + 76, unchecked((uint)dy));
                }
                else
                {
                    int x = Rand() % 8192 - 4096;
                    int y = Rand() % 8192 - 4096;
                    int z = Rand() % 8192 - 4096;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    dx = direction.X;
                    dy = direction.Y;
                    dz = direction.Z;
                    staging = LongVector(staging, temporary + 72, dx, dy, dz);
                }
                int radius = Rand() % (fan ? 300 : 800) + (fan ? 500 : 900);
                staging = SetMemory(SetMemory(SetMemory(staging, row + 8, (radius * dx) >> 12), row + 10, (radius * dy) >> 12), row + 12, (radius * dz) >> 12);
                int speed = Rand() % (fan ? 300 : 450) + (fan ? 140 : 250);
                staging = SetMemory(SetMemory(staging, row + 16, (speed * dx) >> 12), row + 18, (speed * dy) >> 12);
                staging = fan ? SetMemory(staging, row + 20, angle) : SetMemory(SetMemory(SetMemory(SetMemory(staging, row + 20, (speed * dz) >> 12), row + 24, dx), row + 26, dy), row + 28, dz);
                frame = frame with { Staging = staging };
            }
        }
        NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        NativeBattleFileStaging current = WriteMemoryDword(WriteMemoryDword(context.Staging, context.Address, 0xD81620), context.Address + 8, 0);
        NativeBattleActionMatrix parent;
        if (fan)
        {
            uint row = unchecked(0x23C32F8u + (uint)(Word(task, 26) * 28));
            NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(0, 0, -4096, ReadMemoryDword(current, row + 8), ReadMemoryDword(current, row + 12), ReadMemoryDword(current, row + 16));
            NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), 0, 0, 0, 4096);
            current = LongVector(current, temporary + 72, aligned.Axis.X, aligned.Axis.Y, aligned.Axis.Z);
            for (int component = 0; component < 9; component++)
            {
                current = SetMemory(current, temporary + 40 + (uint)component * 2, basis.Rotation[component]);
            }
            current = Forget(current, temporary + 58, 2);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 24))));
            local = local with { X = ReadMemoryWord(current, row), Y = ReadMemoryWord(current, row + 2), Z = ReadMemoryWord(current, row + 4) };
            parent = NativeBattlePoseMatrices.Compose56C2F0(view, local);
            current = Point(current, temporary, new(0, 0, Word(task, 24)));
            current = WriteMemoryDword(WriteMemoryDword(current, temporary + 72, 3072), temporary + 80, 0);
        }
        else
        {
            parent = NativeBattlePoseMatrices.Compose56C2F0(view, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) });
            current = Point(current, temporary, new(0, 0, 0));
            current = WriteMemoryDword(WriteMemoryDword(current, temporary + 104, 8192), temporary + 112, 0);
        }
        current = Forget(StoreMatrix(current, temporary + 8, parent), temporary + 26, 2);
        if (fan)
        {
            current = WriteMemoryWord(WriteMemoryWord(current, temporary, 0), temporary + 2, 0);
        }
        frame = frame with { Staging = current };
        int alive = 0;
        int count = fan ? 120 : 200;
        for (int index = 0; index < count; index++)
        {
            uint row = 0x23C4780u + (uint)index * 32;
            if (NativeBattleEffectPoolBytes.Contains(pool, row) ? !NativeBattleEffectPoolBytes.EqualDword(pool, row, unchecked((uint)marker)) : ReadMemoryDword(frame.Staging, row) != marker)
            {
                continue;
            }
            current = frame.Staging;
            short age = ReadMemoryWord(current, row + 4);
            int scale = ReadMemoryWord(current, row + 6);
            NativeBattleActionMatrix local;
            if (fan)
            {
                short angle = ReadMemoryWord(current, row + 20);
                current = Point(current, temporary, new(0, 0, angle));
                local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, angle));
            }
            else
            {
                int x = ReadMemoryWord(current, row + 24);
                int y = ReadMemoryWord(current, row + 26);
                int z = ReadMemoryWord(current, row + 28);
                current = LongVector(current, temporary + 72, x, y, z);
                NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(0, -4096, 0, x, y, z);
                current = LongVector(current, temporary + 88, aligned.Axis.X, aligned.Axis.Y, aligned.Axis.Z);
                local = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), 0, 0, 0, 4096);
            }
            local = local with { X = ReadMemoryWord(current, row + 8), Y = ReadMemoryWord(current, row + 10), Z = ReadMemoryWord(current, row + 12) };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, fan ? 3072 : 8192, scale, 0);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(parent, local);
            current = Forget(StoreMatrix(current, temporary + 40, matrix), temporary + 58, 2);
            current = WriteMemoryDword(current, temporary + (fan ? 76u : 108u), unchecked((uint)scale));
            int? fade = age >= (fan ? 4 : 6) ? fan ? (age - 4) << 9 : 682 * (age - 6) : null;
            uint flags = fade.HasValue ? 0xF3u : 0x33u;
            current = WriteMemoryDword(current, context.Address + 28, flags);
            if (fade.HasValue)
            {
                current = WriteMemoryDword(current, context.Address + 12, unchecked((uint)fade.Value));
            }
            frame = inputs.Execute(frame with { Staging = current }, new NativeBattleLionHeart165MeshDraw(0xD81620, matrix, new(flags, 0, fade, null, null, null, 17, 2), true));
            if (paused)
            {
                continue;
            }
            current = frame.Staging;
            if (age < 12)
            {
                current = SetMemory(current, row + 4, age + 1);
                for (uint axis = 0; axis < (fan ? 4u : 6u); axis += 2)
                {
                    int speed = ReadMemoryWord(current, row + 16 + axis);
                    current = SetMemory(SetMemory(current, row + 8 + axis, ReadMemoryWord(current, row + 8 + axis) + speed), row + 16 + axis, speed - (speed >> 3));
                }
                alive++;
            }
            else
            {
                current = WriteMemoryDword(current, row, 0);
            }
            frame = frame with { Staging = current };
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 88), localLength) };
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = pool.Slots[slot];
        task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, alive == 0 ? 2 : 0);

        short ReadMemoryWord(NativeBattleFileStaging staging, uint address) => NativeBattleEffectPoolBytes.Contains(pool, address) ? BinaryPrimitives.ReadInt16LittleEndian(NativeBattleEffectPoolBytes.Read(pool, address, 2)) : ReadWord(staging, address);
        int ReadMemoryDword(NativeBattleFileStaging staging, uint address) => NativeBattleEffectPoolBytes.Contains(pool, address) ? BinaryPrimitives.ReadInt32LittleEndian(NativeBattleEffectPoolBytes.Read(pool, address, 4)) : ReadDword(staging, address);
        NativeBattleFileStaging WriteMemoryWord(NativeBattleFileStaging staging, uint address, ushort value) => SetMemory(staging, address, value);
        NativeBattleFileStaging SetMemory(NativeBattleFileStaging staging, uint address, int value)
        {
            if (!NativeBattleEffectPoolBytes.Contains(pool, address))
            {
                return Set(staging, address, value);
            }
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            pool = NativeBattleEffectPoolBytes.Write(pool, address, bytes);
            return staging;
        }
        NativeBattleFileStaging WriteMemoryDword(NativeBattleFileStaging staging, uint address, uint value)
        {
            if (!NativeBattleEffectPoolBytes.Contains(pool, address))
            {
                return WriteDword(staging, address, value);
            }
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            pool = NativeBattleEffectPoolBytes.Write(pool, address, bytes);
            return staging;
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
