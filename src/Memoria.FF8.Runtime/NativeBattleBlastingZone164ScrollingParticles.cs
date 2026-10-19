namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F1190(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        if (!paused && counter <= 48 && (counter & 1) != 0)
        {
            for (uint row = 0x23C6978; row < 0x23C72D8; row += 24)
            {
                if (ReadDword(frame.Staging, row) != 0)
                {
                    continue;
                }
                SetDword(row, 2);
                SetWord(row + 4, 0);
                SetWord(row + 6, Rand() % 6144 + 1024);
                SetWord(row + 10, 0);
                int angle = Rand() % 4096;
                SetWord(row + 12, angle);
                SetWord(row + 18, -8 - Rand() % 24);
                int speed = Rand() % 40 + 10;
                SetWord(row + 20, (angle & 1) != 0 ? -speed : speed);
                break;
            }
        }
        NativeBattleModuleDraw anchor = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(ReadWord(frame.Staging, 0x23C9D98), ReadWord(frame.Staging, 0x23C9D9A), ReadWord(frame.Staging, 0x23C9D9C), 0), Scale = 4096, SpriteFlags = 0 };
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.ModuleSpriteCamera(anchor, view, 0);
        for (uint row = 0x23C6978; row < 0x23C72D8; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 2) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, row + 4);
            short angle = ReadWord(frame.Staging, row + 12);
            NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, angle)), ReadWord(frame.Staging, row + 6), 4096, 4096);
            NativeBattleActionMatrix matrix = AtCamera(rotation, camera);
            bool fading = age < 6 || age >= 10;
            int? fade = fading ? age < 6 ? 4096 - 682 * age : 682 * (age - 10) : null;
            uint flags = fading ? 0xF3u : 0x33u;
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(0xD91AD0, matrix, new(flags, 0, fade, null, null, null, 17, 2), false) { Scrolling = new(flags, fade, 0, ReadWord(frame.Staging, row + 10), 17, 2, new(0, 0, 64, 128)) });
            if (!paused)
            {
                if (age < 16)
                {
                    SetWord(row + 4, age + 1);
                    SetWord(row + 10, ReadWord(frame.Staging, row + 10) + ReadWord(frame.Staging, row + 18));
                    SetWord(row + 12, angle + ReadWord(frame.Staging, row + 20));
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
        bool released = Word(task, 12) >= 54;
        if (released)
        {
            for (uint row = 0x23C6978; row < 0x23C72D8; row += 24)
            {
                if ((ReadDword(frame.Staging, row) & 2) != 0)
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
