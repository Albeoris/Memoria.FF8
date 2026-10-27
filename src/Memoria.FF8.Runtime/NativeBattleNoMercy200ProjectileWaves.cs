namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Advance63D800(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short tick = Word(task, 12);
        uint path = ProjectileRow(task);
        int marker = Word(task, 14) + 32;
        if (!paused && tick <= 8 && (tick & 1) == 0)
        {
            for (int index = 0; index < 4; index++)
            {
                uint row = FreeParticle(frame.Staging);
                if (row == 0)
                {
                    break;
                }
                SetDword(row, checked((uint)marker));
                SetWord(row + 4, 0);
                int scale = (Rand() % 768 + (tick << 8) + 1536) / 3;
                SetWord(row + 16, scale);
                SetWord(row + 8, scale);
                SetWord(row + 10, 1536);
                SetWord(row + 14, Word(task, 32));
            }
        }
        NativeBattleActionMatrix orientation = ReadMatrix(frame.Staging, path + 560) with { X = 0, Y = 0, Z = 0 };
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(inputs.View, orientation);
        int alive = 0;
        for (uint row = 0x24F8DB0; row < 0x24F9710; row += 24)
        {
            if (ReadDword(frame.Staging, row) != marker)
            {
                continue;
            }
            int sample = ReadWord(frame.Staging, row + 14);
            if (sample is < 0 or >= 64)
            {
                throw new NotSupportedException("Native No Mercy wave sample leaves its original64-point target path.");
            }
            uint point = checked(path + 48 + (uint)sample * 8);
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), ReadWord(frame.Staging, row + 10), ReadWord(frame.Staging, row + 8), ReadWord(frame.Staging, row + 8)) with { X = ReadWord(frame.Staging, point), Y = 0, Z = ReadWord(frame.Staging, point + 4) };
            short age = ReadWord(frame.Staging, row + 4);
            frame = inputs.Execute(frame, new NativeBattleNoMercy200MeshDraw(0xE3260C, NativeBattlePoseMatrices.Compose56C2F0(basis, local), new(age >= 0 ? 0xF3u : 0x33u, 0, age >= 0 ? 682 * age : null, null, null, null, 17, 2)) { Common = true });
            if (!paused)
            {
                if (age < 6)
                {
                    short speed = ReadWord(frame.Staging, row + 16);
                    SetWord(row + 4, age + 1);
                    SetWord(row + 8, ReadWord(frame.Staging, row + 8) + speed);
                    SetWord(row + 16, speed - speed / 4);
                    alive++;
                }
                else
                {
                    SetDword(row, 0);
                }
            }
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        short increment = Word(task, 34);
        task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + increment)));
        if (tick > 4 && increment < 6)
        {
            task = task.WriteWord(34, unchecked((ushort)(increment + 1)));
        }
        task = task.WriteWord(12, unchecked((ushort)(tick + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        void SetWord(uint address, int value)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, address, value) };
        }
        void SetDword(uint address, uint value)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, address, value) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }

    private static uint FreeParticle(NativeBattleFileStaging staging)
    {
        for (uint row = 0x24F8DB0; row < 0x24F9710; row += 24)
        {
            if (ReadDword(staging, row) == 0)
            {
                return row;
            }
        }
        return 0;
    }
}
