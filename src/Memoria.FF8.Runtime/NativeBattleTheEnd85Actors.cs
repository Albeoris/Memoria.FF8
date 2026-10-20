namespace Memoria.FF8.Runtime;

public static class NativeBattleTheEnd85Actors
{
    public static NativeBattleTheEnd85TaskStep Advance(NativeBattleTheEnd85TaskInput input, NativeBattleTheEnd85TaskServices services)
    {
        NativeBattleTheEnd85TaskCursor c = new(input, services);
        switch (input.Task.Callback)
        {
            case 0x785560:
                switch (c.Phase)
                {
                    case 0:
                        c.Input = services.InitializeModel(c.Input);
                        c.W(28, 0);
                        c.W(30, 0);
                        c.W(32, 0);
                        Scale();
                        c.W(136, 1024);
                        c.Draw(0x785660);
                        c.Input = services.AppendActors(c.Input);
                        c.Next();
                        break;
                    case 1:
                        if (c.Stage(6))
                        {
                            c.End(1);
                        }
                        else
                        {
                            c.Input = services.AppendActors(c.Input);
                            if (c.Draw(0x785660) == 0)
                            {
                                c.End(1);
                            }
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Native The End model phase is outside its callback table.");
                }
                break;
            case 0x787B50:
                switch (c.Phase)
                {
                    case 0:
                        if (c.S(36) >= (c.U(138) == 0 ? 8 : 14))
                        {
                            c.Next();
                        }
                        break;
                    case 1:
                        c.Input = services.InitializeModel(c.Input);
                        Scale();
                        c.W(716, -2816);
                        c.W(718, -3072);
                        c.W(720, 0);
                        c.W(726, c.U(138) == 0 ? 0 : 2048);
                        c.W(728, c.U(138) == 0 ? 224 : 288);
                        c.W(730, c.U(138) == 0 ? -1280 : -1472);
                        c.W(724, 0);
                        c.W(710, 3072);
                        c.W(708, 640);
                        c.Next();
                        break;
                    case 2:
                        c.W(724, c.U(724) + 1);
                        if (c.S(724) >= 26)
                        {
                            c.W(708, c.S(708) - 64);
                            if (c.S(708) <= 0)
                            {
                                c.W(708, 0);
                            }
                        }
                        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), c.S(710));
                        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, c.S(708));
                        NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, 0, c.S(730)));
                        c.Vector(104, velocity);
                        c.W(716, c.S(716) + c.S(104) / 16);
                        c.W(718, c.S(718) + c.S(106) / 16);
                        c.W(720, c.S(720) + c.S(108) / 16);
                        c.W(726, (c.U(726) + c.U(728)) & 4095);
                        matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, c.S(726));
                        NativeBattleVector orbit = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, -192, 0));
                        c.W(28, orbit.X + c.S(716));
                        c.W(30, orbit.Y + c.S(718));
                        c.W(32, orbit.Z + c.S(720));
                        c.W(98, c.S(710));
                        if (c.Stage(6) || c.Draw(0x785660) == 0)
                        {
                            c.End(1);
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Native The End orbit model phase is outside its callback table.");
                }
                break;
            case 0x788A90:
                switch (c.Phase)
                {
                    case 0:
                        if (c.S(36) >= 5)
                        {
                            c.Input = services.InitializeModel(c.Input);
                            Scale();
                            c.Draw(0x785660);
                            c.Next();
                        }
                        break;
                    case 1:
                        if (c.Stage(3) || c.Draw(0x785660) == 0)
                        {
                            c.End(1);
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Native The End grounded model phase is outside its callback table.");
                }
                break;
            case 0x788B90:
                switch (c.Phase)
                {
                    case 0:
                        if (c.S(36) >= c.S(664))
                        {
                            c.Draw(0x788C20);
                            c.Draw(0x789700);
                            c.Next();
                        }
                        break;
                    case 1:
                        if (c.Draw(0x789700) != 0)
                        {
                            c.End(1);
                        }
                        break;
                    case 2:
                        break;
                    default:
                        throw new InvalidDataException("Native The End grounded object phase is outside its callback table.");
                }
                break;
            default:
                throw new InvalidDataException("Native The End actor callback is not owned by this helper.");
        }
        return c.Finish();

        void Scale()
        {
            c.DW(80, 4096);
            c.DW(84, 4096);
            c.DW(88, 4096);
        }
    }
}
