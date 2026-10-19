namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F1AC0(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        if (!paused && counter <= 28)
        {
            int count = counter / 22 + 1;
            for (int index = 0; index < count; index++)
            {
                uint row = 0x23C72D8;
                while (row < 0x23C7C38 && ReadDword(frame.Staging, row) != 0)
                {
                    row += 24;
                }
                if (row == 0x23C7C38)
                {
                    break;
                }
                SetDword(row, 1);
                SetWord(row + 4, 0);
                SetWord(row + 6, Rand() % 1536 + 1024);
                int angle = Rand() % 4096;
                int distance = Rand() % 600 + 800;
                SetWord(row + 8, (distance * NativeBattleCameraMath.Sine(angle)) >> 12);
                SetWord(row + 10, 0);
                SetWord(row + 12, (distance * NativeBattleCameraMath.Cosine(angle)) >> 12);
                SetWord(row + 14, angle);
                int speed = (Rand() % 1024 + 96 * counter + 768) / 4;
                SetWord(row + 22, speed);
                SetWord(row + 18, speed);
            }
        }
        NativeBattleActionMatrix translated = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = ReadWord(frame.Staging, 0x23C9D98), Y = 0, Z = ReadWord(frame.Staging, 0x23C9D9C) };
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(view, translated);
        for (uint row = 0x23C72D8; row < 0x23C7C38; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 1) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, row + 4);
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, ReadWord(frame.Staging, row + 14), 0)), ReadWord(frame.Staging, row + 6), ReadWord(frame.Staging, row + 18), 0) with { X = ReadWord(frame.Staging, row + 8), Y = 0, Z = ReadWord(frame.Staging, row + 12) };
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(0xD95904, NativeBattlePoseMatrices.Compose56C2F0(basis, world), new(age >= 4 ? 0xF3u : 0x33u, 0, age >= 4 ? 682 * (age - 4) : null, null, null, null, 17, 2), true));
            if (!paused)
            {
                if (age < 10)
                {
                    SetWord(row + 4, age + 1);
                    short speed = ReadWord(frame.Staging, row + 22);
                    SetWord(row + 18, ReadWord(frame.Staging, row + 18) + speed);
                    SetWord(row + 22, speed - speed / 8);
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
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = Word(task, 12) >= 38;
        if (released)
        {
            for (uint row = 0x23C72D8; row < 0x23C7C38; row += 24)
            {
                if ((ReadDword(frame.Staging, row) & 1) != 0)
                {
                    SetDword(row, 0);
                }
            }
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, released ? 2 : 0);

        void SetWord(uint address, int value)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, address, unchecked((ushort)value)) };
        }
        void SetDword(uint address, uint value)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, address, value) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
