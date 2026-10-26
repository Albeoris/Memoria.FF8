namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect187ModelService
{
    Initialize,
    Scale,
    Select,
    Advance,
    Draw,
    InitializeChild,
    Append,
    CaptureBone,
    Translate,
    AddZ,
    Flash,
    RestoreActorFlags,
    Complete
}

public sealed record NativeBattleEffect187ModelOperation(NativeBattleEffect187ModelService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0);
public sealed record NativeBattleEffect187ModelInputs(uint GraphicsFlags, Func<int> ReadCameraSign, Action<NativeBattleEffect187ModelOperation> Execute);
public sealed record NativeBattleEffect187ModelDirectorStep(ushort Counter, int Result);

public static class NativeBattleEffect187ModelDirector
{
    public static NativeBattleEffect187ModelDirectorStep Advance649D00(NativeBattleModulePoolTask task, NativeBattleEffect187ModelInputs inputs)
    {
        if (task.Callback != 0x649D00 || task.Payload.IsDefault || task.Payload.Length != 24)
        {
            throw new InvalidDataException("Effect187 model director requires its actual 36-byte task.");
        }
        int age = unchecked((short)task.ReadWord(12));
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (age == 0)
        {
            Emit(NativeBattleEffect187ModelService.Initialize);
        }
        if (age < 12)
        {
            if (!paused)
            {
                if (age == 0)
                {
                    Select(0);
                }
                else if (age == 3)
                {
                    Append(0x64A370);
                    Initialize(0x64A690);
                    Append(0x64A9D0);
                }
            }
            DrawAdvance();
        }
        else if (age < 40)
        {
            Emit(NativeBattleEffect187ModelService.Scale, 4096);
            if (!paused)
            {
                if (age == 12)
                {
                    Select(1);
                }
                else if (age == 21)
                {
                    Append(0x64ACB0);
                }
                Advance();
            }
            Draw();
        }
        else if (age < 53)
        {
            if (!paused)
            {
                if (age == 40)
                {
                    Select(2);
                    Advance();
                }
                else if (age == 52)
                {
                    Initialize(0x64B040);
                }
                Advance();
            }
            Draw();
        }
        else if (age < 65)
        {
            Draw();
        }
        else if (age < 108)
        {
            if (!paused)
            {
                if (age == 65)
                {
                    Select(3);
                }
                else if (age == 82)
                {
                    Initialize(0x64B610);
                }
                else if (age == 106)
                {
                    Initialize(0x64BB20);
                }
            }
            DrawAdvance();
        }
        else if (age < 120)
        {
            if (!paused && age == 108)
            {
                Select(4);
            }
            Draw();
            if (!paused)
            {
                if (age < 112)
                {
                    Emit(NativeBattleEffect187ModelService.AddZ, 90);
                }
                Advance();
                Emit(NativeBattleEffect187ModelService.CaptureBone, 0, 0);
            }
        }
        else if (age < 131)
        {
            if (!paused)
            {
                if (age == 120)
                {
                    Emit(NativeBattleEffect187ModelService.Translate, 0, 0, unchecked((short)task.ReadWord(0x14)) - 100);
                    Select(5);
                }
                EmitAttackEvents(120, 3);
            }
            DrawAdvance();
        }
        else if (age == 131 || age == 143)
        {
            if (!paused)
            {
                Emit(NativeBattleEffect187ModelService.RestoreActorFlags, 1);
            }
            Draw();
        }
        else if (age < 143)
        {
            if (!paused)
            {
                if (age == 132)
                {
                    Select(5);
                }
                EmitAttackEvents(132, 4);
            }
            DrawAdvance();
        }
        else if (age < 155)
        {
            if (!paused)
            {
                if (age == 144)
                {
                    Select(5);
                }
                EmitAttackEvents(144, 5);
            }
            DrawAdvance();
        }
        else if (age < 157)
        {
            if (!paused && age == 155)
            {
                Emit(NativeBattleEffect187ModelService.RestoreActorFlags, 2);
                Initialize(0x64C710);
            }
            Draw();
        }
        else if (age < 168)
        {
            if (!paused && age == 157)
            {
                Emit(NativeBattleEffect187ModelService.Translate, unchecked(-1900 * inputs.ReadCameraSign()), 700, -8900);
                Select(6);
            }
            Draw();
        }
        else if (age < 198)
        {
            DrawAdvance();
        }
        if (paused)
        {
            return new(task.ReadWord(12), 0);
        }
        ushort counter = unchecked((ushort)(task.ReadWord(12) + 1));
        if (unchecked((short)counter) >= 198)
        {
            Emit(NativeBattleEffect187ModelService.Complete);
            return new(counter, 2);
        }
        return new(counter, 0);

        void Emit(NativeBattleEffect187ModelService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));

        void Select(int animation) => Emit(NativeBattleEffect187ModelService.Select, animation);

        void Advance() => Emit(NativeBattleEffect187ModelService.Advance);

        void Draw() => Emit(NativeBattleEffect187ModelService.Draw);

        void Initialize(int address, int argument = 0) => Emit(NativeBattleEffect187ModelService.InitializeChild, address, argument);

        void Append(int address) => Emit(NativeBattleEffect187ModelService.Append, address);

        void DrawAdvance()
        {
            Draw();
            if (!paused)
            {
                Advance();
            }
        }

        void EmitAttackEvents(int start, int row)
        {
            if (age == start + 1)
            {
                Initialize(0x64D850, row);
            }
            else if (age == start + 3)
            {
                Initialize(0x64C300);
                Emit(NativeBattleEffect187ModelService.Flash, 0, 1, 2, 255);
            }
        }
    }
}
