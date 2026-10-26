namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect187DirectorService
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
    Contacts
}

public sealed record NativeBattleEffect187DirectorOperation(NativeBattleEffect187DirectorService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0, bool UseWorkBuffer = false);
public sealed record NativeBattleEffect187DirectorInputs(uint GraphicsFlags, Action PublishWorldView, Func<int> Pending508500, Func<bool> StreamReady, Action<NativeBattleEffect187DirectorOperation> Execute);
public sealed record NativeBattleEffect187DirectorStep(NativeBattleModulePoolTask Task, int Result);

public static class NativeBattleEffect187Director
{
    public static NativeBattleEffect187DirectorStep Advance6474C0(NativeBattleModulePoolTask task, NativeBattleEffect187DirectorInputs inputs)
    {
        if (task.Callback != NativeBattleEffect187Profile.MainCallback || task.Payload.IsDefault || task.Payload.Length != 24)
        {
            throw new InvalidDataException("Effect187 director requires its original 36-byte task.");
        }
        inputs.PublishWorldView();
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(task, 0);
        }
        int age = unchecked((short)task.ReadWord(12));
        if (age is 13 or 22 or 117 or 127 or 163 or 208 && inputs.Pending508500() < 0)
        {
            return new(task, 0);
        }
        switch (age)
        {
            case 0:
                Emit(NativeBattleEffect187DirectorService.Camera, 0xE41F28, 0x24FE880, 0x24FD8D8, 0x24FD990);
                Stream(0xFA54A4, false);
                Emit(NativeBattleEffect187DirectorService.Vibration, 0xE41CE8, 1, 128, 0x24FD6D4);
                break;
            case 1:
                Sound(0xE41E40);
                File(0x225, 0x14000, false);
                break;
            case 6:
                Emit(NativeBattleEffect187DirectorService.Initialize, 0x647960);
                break;
            case 10:
                Emit(NativeBattleEffect187DirectorService.Append, 0x647F80);
                break;
            case 13:
                Emit(NativeBattleEffect187DirectorService.Upload, 0x14000);
                File(0x226, 0, false);
                break;
            case 22:
                File(0x227, 0, true);
                break;
            case 40:
                Emit(NativeBattleEffect187DirectorService.Initialize, 0x6483B0);
                break;
            case 52:
                Emit(NativeBattleEffect187DirectorService.Append, 0x649D00);
                break;
            case 56:
            case 138:
            case 173:
            case 185:
            case 197:
            case 212:
                Emit(NativeBattleEffect187DirectorService.Play, 128, 0, 96);
                break;
            case 111:
                Sound(0xE41E44);
                break;
            case 117:
                File(0x228, 0x14000, false);
                break;
            case 127:
                Emit(NativeBattleEffect187DirectorService.Upload, 0x14000);
                break;
            case 133:
            case 163:
                Stream(0, true);
                break;
            case 141:
                File(0x229, 0, true);
                break;
            case 168:
                Sound(0xE41E48);
                break;
            case 170:
                if (inputs.StreamReady())
                {
                    File(0x22A, 0, true);
                }
                break;
            case 208:
                Stream(0, true);
                Emit(NativeBattleEffect187DirectorService.Initialize, 0x648820);
                break;
            case 252:
                Emit(NativeBattleEffect187DirectorService.Append, 0x649CC0);
                break;
            case 286:
                Emit(NativeBattleEffect187DirectorService.StopVibrationIfActive);
                break;
        }
        if (age < 6)
        {
            Emit(NativeBattleEffect187DirectorService.Stage, unchecked(age * 682));
        }
        else if (age == 6)
        {
            Emit(NativeBattleEffect187DirectorService.StageGroupFlags, 0xFD, 0);
        }
        else if (age == 40)
        {
            Emit(NativeBattleEffect187DirectorService.StageGroupFlags, 0xFF, 2);
            Emit(NativeBattleEffect187DirectorService.Stage, 0xA00);
        }
        else if (age >= 273)
        {
            Emit(NativeBattleEffect187DirectorService.Stage, unchecked((289 - age) * 160));
        }
        if (age == 287)
        {
            Emit(NativeBattleEffect187DirectorService.Contacts);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        if (unchecked((short)task.ReadWord(12)) >= 289)
        {
            Emit(NativeBattleEffect187DirectorService.Stage, 0);
            return new(task, 2);
        }
        return new(task, 0);

        void Emit(NativeBattleEffect187DirectorService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));

        void Sound(int address) => Emit(NativeBattleEffect187DirectorService.Sound, address, 0, 128);

        void File(int id, int offset, bool work) => inputs.Execute(new(NativeBattleEffect187DirectorService.File, id, offset, UseWorkBuffer: work));

        void Stream(int address, bool work) => inputs.Execute(new(NativeBattleEffect187DirectorService.Stream, address, 0x24FD6D0, UseWorkBuffer: work));
    }
}
