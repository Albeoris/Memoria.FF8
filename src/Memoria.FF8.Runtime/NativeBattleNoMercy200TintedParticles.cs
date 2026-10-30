namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Advance63B080(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short tick = Word(task, 12);
        if (!paused && tick <= 16)
        {
            for (uint row = 0x24F8DB0; row < 0x24F9710; row += 24)
            {
                if (ReadDword(frame.Staging, row) != 0)
                {
                    continue;
                }
                SetDword(row, 1);
                if (inputs.CapturePresentation)
                {
                    NativeBattleShotVisualLifetimes.Allocated(200, task, unchecked((int)row));
                }
                SetWord(row + 4, 0);
                SetWord(row + 6, Rand() % 6144 + 1024);
                SetWord(row + 8, Rand() % 8);
                SetWord(row + 10, 0);
                int angle = Rand() % 4096;
                SetWord(row + 12, angle);
                SetWord(row + 18, -8 - Rand() % 24);
                int speed = Rand() % 40 + 10;
                SetWord(row + 20, (angle & 1) != 0 ? -speed : speed);
                break;
            }
        }
        NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(ReadWord(frame.Staging, 0x24FBCD8), ReadWord(frame.Staging, 0x24FBCDA), ReadWord(frame.Staging, 0x24FBCDC), 0), Scale = 4096, SpriteFlags = 0 };
        NativeBattleActionMatrix translation = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, inputs.View, 0);
        int alive = 0;
        for (uint row = 0x24F8DB0; row < 0x24F9710; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 1) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, row + 4);
            short angle = ReadWord(frame.Staging, row + 12);
            int tint = ReadWord(frame.Staging, row + 8);
            if (tint is < 0 or >= 8)
            {
                throw new NotSupportedException("Native No Mercy tint index leaves its original eight-color table.");
            }
            NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, angle)), ReadWord(frame.Staging, row + 6), 4096, 4096);
            NativeBattleActionMatrix matrix = rotation with { X = translation.X, Y = translation.Y, Z = translation.Z };
            bool fading = age < 6 || age >= 10;
            uint flags = fading ? 0xF3u : 0x33u;
            int? fade = fading ? age < 6 ? 4096 - 682 * age : 682 * (age - 10) : null;
            uint color = frame.Memory.NoMercy200?.Resources?.Dword(checked(0xE33D50u + (uint)tint * 4)) ?? throw new NotSupportedException("Native No Mercy requires its original tint table.");
            frame = inputs.Execute(frame, new NativeBattleNoMercy200MeshDraw(0xE2F8DC, matrix, new(flags, 0, fade, null, null, null, 17, 2)) { Tint = color, PilotIdentity = Visual200(task, inputs, 0xE2F8DC, row) });
            if (!paused)
            {
                if (age < 16)
                {
                    SetWord(row + 4, age + 1);
                    SetWord(row + 8, tint + 1 >= 8 ? 0 : tint + 1);
                    SetWord(row + 10, ReadWord(frame.Staging, row + 10) + ReadWord(frame.Staging, row + 18));
                    SetWord(row + 12, angle + ReadWord(frame.Staging, row + 20));
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
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
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
}
