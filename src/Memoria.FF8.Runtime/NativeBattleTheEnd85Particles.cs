namespace Memoria.FF8.Runtime;

public static class NativeBattleTheEnd85Particles
{
    public static NativeBattleTheEnd85TaskInput Spawn78CED0(NativeBattleTheEnd85TaskInput input, NativeBattleTheEnd85TaskServices services)
    {
        NativeBattleTheEnd85TaskCursor c = new(input, services);
        ushort[] values = new ushort[20];
        for (int i = 0; i < 4; i++)
        {
            values[i * 5] = unchecked((ushort)((c.Random() & 32767) - 16384));
            values[i * 5 + 1] = unchecked((ushort)Math.Min(0, 1024 - (c.Random() & 8191)));
            values[i * 5 + 2] = unchecked((ushort)((c.Random() & 32767) - 8192));
            values[i * 5 + 3] = unchecked((ushort)((c.Random() & 4095) + 1024));
            values[i * 5 + 4] = unchecked((ushort)(c.Random() & 4095));
        }
        c.Spawn(0x25BC898, 0x78CFA0, 228, child =>
        {
            for (int i = 0; i < 4; i++)
            {
                child = child.WriteWord(84 + i * 8, values[i * 5]);
                child = child.WriteWord(86 + i * 8, values[i * 5 + 1]);
                child = child.WriteWord(88 + i * 8, values[i * 5 + 2]);
                child = child.WriteWord(212 + i * 2, values[i * 5 + 3]);
                child = child.WriteWord(184 + i * 8, values[i * 5 + 4]);
            }
            return child;
        });
        c.W(222, c.U(222) + 1);
        if (c.Stage(6))
        {
            c.End(1);
        }
        return c.Input;
    }

    public static NativeBattleTheEnd85TaskStep Advance(NativeBattleTheEnd85TaskInput input, NativeBattleTheEnd85TaskServices services)
    {
        NativeBattleTheEnd85TaskCursor c = new(input, services);
        switch (input.Task.Callback)
        {
            case 0x788420:
                switch (c.Phase)
                {
                    case 0:
                        if (c.S(36) >= 16)
                        {
                            c.Next();
                        }
                        break;
                    case 1:
                        int selector = c.S(220);
                        if (selector is not (0 or 8 or 14) && c.Stage(3))
                        {
                            c.W(220, c.U(220) + 1);
                            c.End(1);
                        }
                        else
                        {
                            ushort kind = (ushort)(selector switch { 0 => 0, 8 => 1, 14 => 2, _ => 127 });
                            c.Spawn(0x25BC898, 0x788580, 228, child => child.WriteWord(222, kind));
                            c.W(220, c.U(220) + 1);
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Native The End initial emitter phase is outside its callback table.");
                }
                break;
            case 0x788580:
                if (c.Phase == 0)
                {
                    InitializePrimary(c);
                }
                else if (c.Phase == 1)
                {
                    GroundPrimary(c);
                }
                else if (c.Phase != 2)
                {
                    throw new InvalidDataException("Native The End primary particle phase is outside its callback table.");
                }
                c.W(180, (c.U(180) + 32) & 4095);
                c.W(148, 768 * NativeBattleCameraMath.Sine(c.S(180)) / 4096);
                NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), c.S(150));
                matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, c.S(148));
                NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, c.S(212), 0));
                c.Vector(116, velocity);
                c.W(84, c.S(84) + c.S(116) / 16);
                c.W(86, c.S(86) + c.S(118) / 16);
                c.W(88, c.S(88) + c.S(120) / 16);
                c.DW(28, c.D(84));
                c.DW(32, c.D(88));
                int depth = c.S(32);
                Scale(c, depth < 0 ? Math.Max(depth + 4096, 2048) : Math.Min(2 * depth + 4096, 16384));
                c.Draw(0x788700);
                break;
            case 0x78CFA0:
                SecondaryPhase(c);
                for (int i = 0; i < 4; i++)
                {
                    int offset = i * 8;
                    c.W(184 + offset, (c.U(184 + offset) + 64) & 4095);
                    c.W(152 + offset, (NativeBattleCameraMath.Sine(c.S(184 + offset)) << 8) / 4096 - 192);
                    NativeBattleActionMatrix spin = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.Identity8DD770(), c.S(152 + offset));
                    NativeBattleVector vector = NativeBattleGfMatrices.RotateShort56C4F0(spin, new(c.S(212 + i * 2), 0, 0));
                    c.Vector(116 + offset, vector);
                    c.W(84 + offset, c.S(84 + offset) + c.S(116 + offset) / 16);
                    c.W(86 + offset, c.S(86 + offset) + c.S(118 + offset) / 16);
                    if (c.S(86 + offset) >= 0)
                    {
                        c.W(86 + offset, 0);
                    }
                    c.W(88 + offset, c.S(88 + offset) + c.S(120 + offset) / 16);
                }
                for (int i = 0; i < 4; i++)
                {
                    c.DW(28, c.D(84 + i * 8));
                    c.DW(32, c.D(88 + i * 8));
                    Scale(c, Math.Clamp(c.S(32) + 4096, 1024, 8192));
                    c.Draw(0x788700);
                }
                break;
            default:
                throw new InvalidDataException("Native The End particle callback is not owned by this helper.");
        }
        return c.Finish();
    }

    private static void Scale(NativeBattleTheEnd85TaskCursor c, int scale)
    {
        c.DW(56, unchecked((uint)scale));
        c.DW(52, unchecked((uint)scale));
        c.DW(48, unchecked((uint)scale));
    }

    private static void InitializePrimary(NativeBattleTheEnd85TaskCursor c)
    {
        c.DW(76, 0x156C7FC);
        Scale(c, 4096);
        c.W(80, 4096);
        c.W(212, 1024);
        switch (c.S(222))
        {
            case 0:
                c.W(180, 0);
                c.W(150, 1024);
                c.W(84, -1792);
                c.W(86, -1536);
                c.W(88, 512);
                break;
            case 1:
                c.W(180, 0);
                c.W(150, 3072);
                c.W(84, 1280);
                c.W(86, -1536);
                c.W(88, 0);
                break;
            case 2:
                c.W(84, -896);
                c.W(180, 1024);
                c.W(150, 1024);
                c.W(86, -1536);
                c.W(88, 0);
                break;
            default:
                c.W(180, c.Random() & 4095);
                c.W(150, c.Random() & 4095);
                c.W(84, (c.Random() & 8191) - 4096);
                c.W(86, -1024 - (c.Random() & 4095));
                c.W(88, (c.Random() & 8191) - 5120);
                break;
        }
        c.Next();
    }

    private static void GroundPrimary(NativeBattleTheEnd85TaskCursor c)
    {
        if (c.Stage(3))
        {
            c.End(1);
            return;
        }
        if (c.S(30) >= 0)
        {
            c.W(30, 0);
            if (c.U(222) <= 2)
            {
                c.Spawn(0x25C0F40, 0x788A90, 732, child => child.WriteWord(128, 0).WriteDword(116, 0x1715D28).WriteDword(120, 560).WriteWord(130, 0));
                uint xy = c.D(28);
                uint zw = c.D(32);
                c.Spawn(0x25C1748, 0x788B90, 676, child => child.WriteDword(48, 0x17151EC).WriteWord(664, 0).WriteWord(668, 3).WriteWord(670, 15).WriteDword(648, xy).WriteDword(652, zw));
            }
            c.End(5);
        }
        else if (c.S(36) > 32)
        {
            c.W(80, c.S(80) + 512);
            if (c.S(80) >= 4096)
            {
                c.W(80, 4096);
                c.End(5);
            }
        }
        else
        {
            c.W(80, c.S(80) - 512);
            if (c.S(80) < 0)
            {
                c.W(80, 0);
            }
        }
    }

    private static void SecondaryPhase(NativeBattleTheEnd85TaskCursor c)
    {
        switch (c.Phase)
        {
            case 0:
                Scale(c, 4096);
                c.W(80, 4096);
                c.DW(76, 0x156C85C);
                c.Next();
                break;
            case 1:
                if (c.Stage(6))
                {
                    c.Flags(5);
                }
                else
                {
                    c.W(80, c.S(80) - 512);
                    if (c.S(80) <= 0)
                    {
                        c.W(80, 0);
                        c.Next();
                    }
                }
                break;
            case 2:
                if (c.Stage(6))
                {
                    c.Flags(5);
                }
                else if (c.S(36) >= 24)
                {
                    c.Next();
                }
                break;
            case 3:
                if (c.Stage(6))
                {
                    c.Flags(5);
                }
                else
                {
                    c.W(80, c.S(80) + 512);
                    if (c.S(80) >= 4096)
                    {
                        c.W(80, 4096);
                        c.End(5);
                    }
                }
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native The End secondary particle phase is outside its callback table.");
        }
    }
}
