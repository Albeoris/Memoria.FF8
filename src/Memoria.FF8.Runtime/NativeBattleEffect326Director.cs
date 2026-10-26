namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect326DirectorService
{
    Camera,
    Stream,
    Vibration,
    Sound,
    File,
    Upload,
    Initialize,
    Append,
    Play,
    StopVibrationIfActive,
    Stage,
    StageGroupFlags,
    Contacts,
    ActorAnimation,
    Flash
}

public enum NativeBattleEffect326SourceBase
{
    Literal,
    Common,
    Work
}

public sealed record NativeBattleEffect326DirectorOperation(NativeBattleEffect326DirectorService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0, NativeBattleEffect326SourceBase SourceBase = NativeBattleEffect326SourceBase.Literal);
public sealed record NativeBattleEffect326DirectorInputs(uint GraphicsFlags, Action PublishWorldView, Func<int> Pending508500, Action<NativeBattleEffect326DirectorOperation> Execute);
public sealed record NativeBattleEffect326DirectorStep(NativeBattleModulePoolTask Task, int Result);

public static class NativeBattleEffect326Director
{
    public static NativeBattleEffect326DirectorStep Advance62A9C0(NativeBattleModulePoolTask task, NativeBattleEffect326DirectorInputs inputs)
    {
        inputs.PublishWorldView();
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(task, 0);
        }
        int age = unchecked((short)task.ReadWord(12));
        if (age is 13 or 22 or 117 or 127 or 163 && inputs.Pending508500() < 0)
        {
            return new(task, 0);
        }
        switch (age)
        {
            case 0:
                Emit(NativeBattleEffect326DirectorService.Camera);
                Stream(0xEECC2C, NativeBattleEffect326SourceBase.Literal);
                Emit(NativeBattleEffect326DirectorService.Vibration, 0xE194E4, 1, 128, 0x24F1060);
                break;
            case 1:
                Sound(0xE1964C);
                File(737, 0x14800, NativeBattleEffect326SourceBase.Common);
                break;
            case 6:
                Initialize(0x62B010);
                break;
            case 10:
                Append(0x62B630);
                break;
            case 13:
                Emit(NativeBattleEffect326DirectorService.Upload, 0x14800);
                File(738, 0, NativeBattleEffect326SourceBase.Common);
                break;
            case 22:
                File(739, 0xEECC2C, NativeBattleEffect326SourceBase.Literal);
                break;
            case 40:
                Initialize(0x62BA60);
                break;
            case 52:
                Append(0x62CEE0);
                break;
            case 56:
            case 138:
            case 198:
                Play();
                break;
            case 111:
                Sound(0xE19650);
                break;
            case 117:
                File(740, 0x14800, NativeBattleEffect326SourceBase.Common);
                break;
            case 127:
                Emit(NativeBattleEffect326DirectorService.Upload, 0x14800);
                break;
            case 133:
                Stream(0xEECC2C, NativeBattleEffect326SourceBase.Literal);
                break;
            case 141:
                File(741, 0, NativeBattleEffect326SourceBase.Work);
                break;
            case 163:
                Stream(0, NativeBattleEffect326SourceBase.Work);
                break;
            case 170:
                Emit(NativeBattleEffect326DirectorService.ActorAnimation, 52);
                break;
            case 171:
                Sound(0xE19654);
                break;
            case 195:
                File(742, 0x14800, NativeBattleEffect326SourceBase.Common);
                break;
            case 218:
                Emit(NativeBattleEffect326DirectorService.ActorAnimation, 53);
                if (inputs.Pending508500() < 0)
                {
                    return new(task, 0);
                }
                File(743, 0, NativeBattleEffect326SourceBase.Work);
                break;
            case 252:
                Sound(0xE19658);
                if (inputs.Pending508500() < 0)
                {
                    return new(task, 0);
                }
                Stream(0, NativeBattleEffect326SourceBase.Work);
                File(744, 0xEECC30, NativeBattleEffect326SourceBase.Literal);
                break;
            case 322:
                Sound(0xE1965C);
                Emit(NativeBattleEffect326DirectorService.Upload, 0x14800);
                Emit(NativeBattleEffect326DirectorService.ActorAnimation, 1);
                Initialize(0x62BED0);
                break;
            case 323:
                Append(0x62C550);
                Append(0x62C8F0);
                break;
            case 349:
                Emit(NativeBattleEffect326DirectorService.Flash, 2, 1, 2, 255);
                break;
            case 352:
                Play();
                Initialize(0x62C180);
                break;
            case 382:
                Sound(0xE19660);
                Append(0x62CA80);
                Append(0x62CC00);
                Append(0x62CD40);
                Emit(NativeBattleEffect326DirectorService.StageGroupFlags, 0xFD, 0);
                break;
            case 407:
                Emit(NativeBattleEffect326DirectorService.StopVibrationIfActive);
                break;
        }
        if (age < 6)
        {
            Emit(NativeBattleEffect326DirectorService.Stage, unchecked(age * 682));
        }
        else if (age == 6)
        {
            Emit(NativeBattleEffect326DirectorService.StageGroupFlags, 0xFD, 0);
        }
        else if (age == 40)
        {
            Emit(NativeBattleEffect326DirectorService.StageGroupFlags, 0xFF, 2);
            Emit(NativeBattleEffect326DirectorService.Stage, 2560);
        }
        else if (age is >= 354 and < 370)
        {
            Emit(NativeBattleEffect326DirectorService.Stage, unchecked((370 - age) * 160));
        }
        if (age == 408)
        {
            Emit(NativeBattleEffect326DirectorService.Contacts);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        if (unchecked((short)task.ReadWord(12)) >= 410)
        {
            Append(0x62CEA0);
            Emit(NativeBattleEffect326DirectorService.StageGroupFlags, 0xFF, 2);
            Emit(NativeBattleEffect326DirectorService.Stage, 0);
            return new(task, 2);
        }
        return new(task, 0);

        void Emit(NativeBattleEffect326DirectorService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));
        void Initialize(int callback) => Emit(NativeBattleEffect326DirectorService.Initialize, callback);
        void Append(int callback) => Emit(NativeBattleEffect326DirectorService.Append, callback);
        void Sound(int source) => Emit(NativeBattleEffect326DirectorService.Sound, source, 0, 128);
        void Play() => Emit(NativeBattleEffect326DirectorService.Play, 128, 0, 96);
        void File(int id, int destination, NativeBattleEffect326SourceBase sourceBase) => inputs.Execute(new(NativeBattleEffect326DirectorService.File, id, destination, SourceBase: sourceBase));
        void Stream(int source, NativeBattleEffect326SourceBase sourceBase) => inputs.Execute(new(NativeBattleEffect326DirectorService.Stream, source, 0x24F0F84, SourceBase: sourceBase));
    }
}
