namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceMeshParticles(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x5EAAF0 or 0x5EAD50 or 0x5EB8D0 or 0x5ED8E0))
        {
            throw new NotSupportedException("Native Lion Heart mesh-particle callback is unsupported.");
        }
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        int marker = Word(task, 14);
        uint bank = callback == 0x5ED8E0 ? 0x23C4780u : 0x23C34C0u;
        int stride = callback == 0x5ED8E0 ? 32 : 24;
        int capacity = callback == 0x5ED8E0 ? 120 : 200;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 104);
        uint temporary = allocation.Address;
        frame = frame with { Staging = allocation.Staging };
        if (!paused && (callback is 0x5EAAF0 or 0x5EAD50) && counter <= 48)
        {
            frame = frame with { Staging = CopyTaskToStaging(frame.Staging, temporary, task, 16, 8) };
        }
        bool spawn = callback switch
        {
            0x5EAAF0 => counter <= 48 && counter % 8 == 1,
            0x5EAD50 => counter <= 48 && counter % 10 == 2,
            0x5EB8D0 => counter <= 34 && counter % 3 == 1,
            _ => counter <= 12 && counter % 4 == 0
        };
        if (!paused && spawn)
        {
            for (int index = 0; index < capacity; index++)
            {
                uint row = bank + (uint)(index * stride);
                if (ReadDword(frame.Staging, row) != 0)
                {
                    continue;
                }
                NativeBattleLionHeart165VisualIdentity.Allocated(task, row);
                NativeBattleFileStaging staging = WriteWord(WriteDword(frame.Staging, row, unchecked((uint)marker)), row + 4, 0);
                switch (callback)
                {
                    case 0x5EAAF0:
                        staging = CopyTaskToStaging(staging, row + 8, task, 16, 8);
                        staging = Set(Set(staging, row + 16, 1536), row + 18, 1536);
                        break;
                    case 0x5EAD50:
                        staging = CopyTaskToStaging(staging, row + 8, task, 16, 8);
                        staging = Set(staging, row + 10, ReadWord(staging, row + 10) + Rand() % 1200 - 600);
                        int speed = (Rand() % 1536 + 2048) / 8;
                        staging = Set(Set(staging, row + 20, speed), row + 16, speed);
                        staging = Set(staging, row + 18, Rand() % 1536 + 768);
                        staging = Set(staging, row + 14, Rand() % 2048);
                        staging = Set(staging, row + 22, Rand() % 60 + 20);
                        break;
                    case 0x5EB8D0:
                        staging = Set(staging, row + 6, Rand() % 12288 + 4096);
                        staging = Set(Set(staging, row + 12, 0), row + 14, 128);
                        staging = Set(staging, row + 20, Rand() % 4096);
                        int angular = Rand() % 40 + 10;
                        staging = Set(staging, row + 22, (angular & 1) != 0 ? -angular : angular);
                        break;
                    case 0x5ED8E0:
                        staging = CopyTaskToStaging(staging, row + 8, task, 16, 8);
                        int rise = (Rand() % 2048 + 4096) / 18;
                        staging = Set(Set(staging, row + 14, rise), row + 6, rise);
                        staging = Set(staging, row + 16, Rand() % 4096);
                        int pitch = Rand() % 32 + 16;
                        staging = Set(staging, row + 18, (pitch & 1) != 0 ? -pitch : pitch);
                        staging = Set(staging, row + 20, Rand() % 4096);
                        int roll = Rand() % 32 + 16;
                        staging = Set(staging, row + 22, (roll & 1) != 0 ? -roll : roll);
                        break;
                }
                frame = frame with { Staging = staging };
                break;
            }
        }
        NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        uint mesh = callback == 0x5EAAF0 ? 0xD83244u : callback == 0x5EAD50 ? 0xD85D74u : callback == 0x5EB8D0 ? 0xD818A0u : 0xD80230u;
        NativeBattleFileStaging scratch = WriteDword(WriteDword(context.Staging, context.Address, mesh), context.Address + 8, 0);
        NativeBattleActionMatrix parent = view;
        if (callback == 0x5EB8D0)
        {
            scratch = Point(scratch, temporary, new(0, 0, 0));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            parent = NativeBattlePoseMatrices.Compose56C2F0(view, local);
            scratch = StoreMatrix(scratch, temporary + 8, parent);
            scratch = WriteDword(WriteDword(scratch, temporary + 76, 12288), temporary + 80, 0);
        }
        else
        {
            scratch = StoreMatrix(scratch, temporary + 8, view);
        }
        scratch = Forget(scratch, temporary + 26, 2);
        scratch = WriteWord(scratch, temporary + 2, 0);
        if (callback != 0x5ED8E0)
        {
            scratch = WriteWord(WriteWord(scratch, temporary, 0), temporary + 4, 0);
        }
        frame = frame with { Staging = scratch };
        int alive = 0;
        for (int index = 0; index < capacity; index++)
        {
            uint row = bank + (uint)(index * stride);
            NativeBattleFileStaging staging = frame.Staging;
            if (ReadDword(staging, row) != marker)
            {
                continue;
            }
            short age = ReadWord(staging, row + 4);
            NativeBattleVector angles = callback switch
            {
                0x5EAD50 => new(0, ReadWord(staging, row + 14), 0),
                0x5EB8D0 => new(0, 0, ReadWord(staging, row + 20)),
                0x5ED8E0 => new(ReadWord(staging, row + 16), 0, ReadWord(staging, row + 20)),
                _ => new(0, 0, 0)
            };
            staging = Point(staging, temporary, angles);
            int sx = callback == 0x5EB8D0 ? unchecked(ReadWord(staging, row + 6) * NativeBattleCameraMath.Sine(ReadWord(staging, row + 12))) >> 12 : ReadWord(staging, row + (callback == 0x5ED8E0 ? 6u : 16u));
            int sy = callback == 0x5EB8D0 ? 12288 : callback == 0x5EAD50 ? ReadWord(staging, row + 18) : sx;
            int sz = callback == 0x5EB8D0 ? 0 : sx;
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles);
            if (callback != 0x5EB8D0)
            {
                local = local with { X = ReadWord(staging, row + 8), Y = ReadWord(staging, row + 10), Z = ReadWord(staging, row + 12) };
            }
            local = NativeBattlePoseMatrices.Scale56BEF0(local, sx, sy, sz);
            NativeBattleActionMatrix matrix = callback == 0x5EB8D0 ? local : NativeBattlePoseMatrices.Compose56C2F0(parent, local);
            if (callback == 0x5EB8D0)
            {
                for (int component = 0; component < 9; component++)
                {
                    staging = Set(staging, temporary + 40 + (uint)component * 2, matrix.Rotation[component]);
                }
            }
            else
            {
                staging = StoreMatrix(staging, temporary + 40, matrix);
            }
            staging = Forget(staging, temporary + 58, 2);
            staging = WriteDword(WriteDword(WriteDword(staging, temporary + 72, unchecked((uint)sx)), temporary + 76, unchecked((uint)sy)), temporary + 80, unchecked((uint)sz));
            int? fade = callback switch
            {
                0x5EAAF0 when age >= 4 => 409 * (age - 4),
                0x5EAD50 when age < 6 => 4096 - 682 * age,
                0x5EAD50 when age >= 8 => 409 * (age - 8),
                0x5EB8D0 when age < 4 => (4 - age) << 10,
                0x5EB8D0 when age >= 10 => 682 * (age - 10),
                0x5ED8E0 when age >= 16 => (age - 16) << 9,
                _ => null
            };
            uint flags = fade.HasValue ? 0xF3u : 0x33u;
            staging = WriteDword(staging, context.Address + 28, flags);
            if (fade.HasValue)
            {
                staging = WriteDword(staging, context.Address + 12, unchecked((uint)fade.Value));
            }
            frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleLionHeart165MeshDraw(mesh, callback == 0x5EB8D0 ? matrix with { X = parent.X, Y = parent.Y, Z = parent.Z } : matrix, new(flags, 0, fade, null, null, null, 17, 2), callback is 0x5EB8D0 or 0x5ED8E0) { PilotIdentity = NativeBattleLionHeart165VisualIdentity.Particle(task, row, mesh) });
            if (!paused)
            {
                staging = frame.Staging;
                int lifetime = callback == 0x5EAAF0 ? 14 : callback == 0x5EAD50 ? 18 : callback == 0x5EB8D0 ? 16 : 24;
                if (age < lifetime)
                {
                    staging = Set(staging, row + 4, age + 1);
                    if (callback == 0x5EAAF0)
                    {
                        int velocity = ReadWord(staging, row + 18);
                        staging = Set(Set(staging, row + 16, ReadWord(staging, row + 16) + velocity), row + 18, velocity - velocity / 5);
                    }
                    else if (callback == 0x5EAD50)
                    {
                        int velocity = ReadWord(staging, row + 20);
                        staging = Set(Set(Set(staging, row + 16, ReadWord(staging, row + 16) + velocity), row + 20, velocity - velocity / 24), row + 14, ReadWord(staging, row + 14) + ReadWord(staging, row + 22));
                    }
                    else if (callback == 0x5EB8D0)
                    {
                        staging = Set(Set(staging, row + 12, ReadWord(staging, row + 12) + ReadWord(staging, row + 14)), row + 20, ReadWord(staging, row + 20) + ReadWord(staging, row + 22));
                    }
                    else
                    {
                        int velocity = ReadWord(staging, row + 14);
                        staging = Set(Set(Set(Set(staging, row + 6, ReadWord(staging, row + 6) + velocity), row + 14, velocity - velocity / 18), row + 16, ReadWord(staging, row + 16) + ReadWord(staging, row + 18)), row + 20, ReadWord(staging, row + 20) + ReadWord(staging, row + 22));
                        alive++;
                    }
                }
                else
                {
                    staging = WriteDword(staging, row, 0);
                }
                frame = frame with { Staging = staging };
            }
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 88), 104) };
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = callback == 0x5ED8E0 ? alive == 0 : Word(task, 12) >= (callback == 0x5EB8D0 ? 44 : 58);
        if (released && callback != 0x5ED8E0)
        {
            NativeBattleFileStaging staging = frame.Staging;
            for (int index = 0; index < capacity; index++)
            {
                uint row = bank + (uint)(index * stride);
                if (ReadDword(staging, row) == marker)
                {
                    staging = WriteDword(staging, row, 0);
                }
            }
            frame = frame with { Staging = staging };
        }
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static NativeBattleFileStaging Set(NativeBattleFileStaging staging, uint address, int value) => WriteWord(staging, address, unchecked((ushort)value));
}
