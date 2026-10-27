namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect326ModelService
{
    Initialize,
    Scale,
    Select,
    Advance,
    Draw,
    InitializeChild,
    Append,
    CaptureBone,
    TranslateXZ,
    AddZ,
    SetMask,
    Flash,
    SplitHeld,
    SplitAnimated,
    SetDistance,
    AddDistance,
    TargetPairFlags,
    Complete
}

public sealed record NativeBattleEffect326ModelOperation(NativeBattleEffect326ModelService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0);
public sealed record NativeBattleEffect326ModelInputs(uint GraphicsFlags, Action<NativeBattleEffect326ModelOperation> Execute);
public sealed record NativeBattleEffect326ModelDirectorStep(ushort Counter, int Result);

public static class NativeBattleEffect326ModelDirector
{
    public static NativeBattleEffect326ModelDirectorStep Advance62CEE0(NativeBattleModulePoolTask task, NativeBattleEffect326ModelInputs inputs)
    {
        if (task.Callback != 0x62CEE0 || task.Payload.IsDefault || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Effect326 model director requires its actual 36-byte task.");
        }
        int age = unchecked((short)task.ReadWord(12));
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (age == 0)
        {
            Emit(NativeBattleEffect326ModelService.Initialize);
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
                    Append(0x62E4F0);
                    Initialize(0x62E810);
                    Append(0x62EB50);
                }
            }
            DrawAdvance();
        }
        else if (age < 40)
        {
            Emit(NativeBattleEffect326ModelService.Scale, 4096);
            if (!paused)
            {
                if (age == 12)
                {
                    Select(1);
                }
                else if (age == 21)
                {
                    Append(0x62EE30);
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
                    Initialize(0x62F1C0);
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
                    Initialize(0x62F710);
                }
                else if (age == 106)
                {
                    Initialize(0x62FC20);
                }
            }
            DrawAdvance();
        }
        else if (age < 120)
        {
            if (!paused && age == 108)
            {
                Select(4);
                Initialize(0x630810);
            }
            DrawAdvance();
        }
        else if (age < 132)
        {
            if (!paused && age == 120)
            {
                Select(4);
            }
            Draw();
        }
        else if (age < 143)
        {
            if (!paused && age == 132)
            {
                Initialize(0x630A70);
            }
            DrawAdvance();
            if (!paused)
            {
                Emit(NativeBattleEffect326ModelService.CaptureBone, 0, 0);
            }
        }
        else if (age < 174)
        {
            if (!paused)
            {
                if (age == 143)
                {
                    Emit(NativeBattleEffect326ModelService.TranslateXZ, 210, unchecked((short)task.ReadWord(0x14)) + 1200);
                    Select(5);
                    Emit(NativeBattleEffect326ModelService.Flash, 4, 6, 12, 255);
                }
                if (age < 153)
                {
                    Emit(NativeBattleEffect326ModelService.AddZ, -450);
                }
                else if (age == 153)
                {
                    Emit(NativeBattleEffect326ModelService.SetMask, 3);
                }
                if (age == 146)
                {
                    Initialize(0x630400);
                }
            }
            DrawAdvance();
        }
        else if (age == 174)
        {
            Draw();
        }
        else if (age < 200)
        {
            if (!paused && age == 198)
            {
                Initialize(0x631150);
            }
            Emit(NativeBattleEffect326ModelService.SplitHeld, age - 174);
            Emit(NativeBattleEffect326ModelService.SetDistance, 128);
        }
        else if (age < 254)
        {
            if (!paused)
            {
                if (age == 222)
                {
                    Emit(NativeBattleEffect326ModelService.Flash, 2, 1, 4, 255);
                }
                if (age == 246)
                {
                    Initialize(0x630CB0);
                }
                Emit(NativeBattleEffect326ModelService.TargetPairFlags, age < 230 ? 8 : 0);
                if (age >= 230)
                {
                    Emit(NativeBattleEffect326ModelService.AddDistance, 18);
                }
            }
            Emit(NativeBattleEffect326ModelService.SplitAnimated, age - 200);
        }
        if (paused)
        {
            return new(task.ReadWord(12), 0);
        }
        ushort counter = unchecked((ushort)(task.ReadWord(12) + 1));
        if (unchecked((short)counter) >= 256)
        {
            Emit(NativeBattleEffect326ModelService.Complete);
            return new(counter, 2);
        }
        return new(counter, 0);

        void Emit(NativeBattleEffect326ModelService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));

        void Select(int animation) => Emit(NativeBattleEffect326ModelService.Select, animation);

        void Advance() => Emit(NativeBattleEffect326ModelService.Advance);

        void Draw() => Emit(NativeBattleEffect326ModelService.Draw);

        void Initialize(int callback) => Emit(NativeBattleEffect326ModelService.InitializeChild, callback);

        void Append(int callback) => Emit(NativeBattleEffect326ModelService.Append, callback);

        void DrawAdvance()
        {
            Draw();
            if (!paused)
            {
                Advance();
            }
        }
    }
}
