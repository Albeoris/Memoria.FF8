namespace Memoria.FF8.Runtime;

public enum NativeBattleShivaDirectorService
{
    Camera,
    BeginWave,
    StartVibration,
    Sound,
    File,
    Upload,
    Initializer,
    AppendChild,
    Volume,
    StopVibrationIfActive,
    Stage,
    Contacts
}

public sealed record NativeBattleShivaDirectorOperation(NativeBattleShivaDirectorService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0, bool UseWorkBuffer = false);
public sealed record NativeBattleShivaDirectorInputs(uint GraphicsFlags, Action PublishWorldView, Func<int> Pending508500, Func<bool> SoundReady, Action<NativeBattleShivaDirectorOperation> Execute);
public sealed record NativeBattleShivaDirectorStep(NativeBattleModulePoolTask Task, int Result);

/// <summary>5C0F30's primary timeline, including its original asynchronous waits and contact point.</summary>
public static class NativeBattleShivaDirector
{
    public static NativeBattleShivaDirectorStep Advance5C0F30(NativeBattleModulePoolTask task, NativeBattleShivaDirectorInputs inputs)
    {
        if (task.Callback != 0x5C0F30 || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native Shiva primary requires its original24h task.");
        }
        inputs.PublishWorldView();
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(task, 0);
        }
        int counter = unchecked((short)task.ReadWord(0x0C));
        if (counter is 15 or 41 or 72 or 132 or 157 or 225 && inputs.Pending508500() < 0)
        {
            return new(task, 0);
        }
        switch (counter)
        {
            case 0:
                Emit(NativeBattleShivaDirectorService.Camera, 0xD32604, 48, 0x22BD018, 0x22BC198);
                BeginWave(0xE9AD20, false);
                Emit(NativeBattleShivaDirectorService.StartVibration, 0xD32344, 1, 128);
                Child(0x5C4A90);
                break;
            case 1:
                Sound(0xD32488);
                Child(0x5C1390);
                File(0x21F, 0x10000);
                break;
            case 15:
                Emit(NativeBattleShivaDirectorService.Upload, 0x10000);
                File(0x220, 0);
                break;
            case 19:
                Child(0x5C42A0);
                break;
            case 22:
                Emit(NativeBattleShivaDirectorService.Initializer, 0x5C1E80);
                break;
            case 40:
            case 191:
                Volume();
                break;
            case 41:
                Child(0x5C4AD0);
                Child(0x5C2DE0);
                break;
            case 42:
                Emit(NativeBattleShivaDirectorService.Initializer, 0x5C4820);
                break;
            case 43:
                Child(0x5C4550);
                break;
            case 62:
                if (inputs.SoundReady())
                {
                    File(0x221, 0, true);
                }
                break;
            case 72:
            case 132:
                BeginWave(0, true);
                break;
            case 77:
                if (inputs.SoundReady())
                {
                    File(0x222, 0, true);
                }
                break;
            case 102:
                Sound(0xD3248C);
                Volume();
                break;
            case 147:
                Sound(0xD32490);
                File(0x223, 0x10000);
                break;
            case 157:
                Emit(NativeBattleShivaDirectorService.Upload, 0x10000);
                File(0x224, 0, true);
                break;
            case 225:
                BeginWave(0xE9AD20, false);
                break;
            case 249:
                Sound(0xD32494);
                Volume();
                break;
            case 272:
                Emit(NativeBattleShivaDirectorService.StopVibrationIfActive);
                break;
        }
        if (counter <= 16)
        {
            Emit(NativeBattleShivaDirectorService.Stage, unchecked(counter << 7));
        }
        else if (counter >= 259)
        {
            Emit(NativeBattleShivaDirectorService.Stage, unchecked((275 - counter) << 7));
        }
        if (counter == 265)
        {
            Emit(NativeBattleShivaDirectorService.Contacts);
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        if (unchecked((short)task.ReadWord(0x0C)) > 275)
        {
            Emit(NativeBattleShivaDirectorService.Stage, 0);
            return new(task, 2);
        }
        return new(task, 0);

        void Emit(NativeBattleShivaDirectorService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));
        void BeginWave(int address, bool work) => inputs.Execute(new(NativeBattleShivaDirectorService.BeginWave, address, 0x22BD0E4, UseWorkBuffer: work));
        void Child(int callback) => Emit(NativeBattleShivaDirectorService.AppendChild, callback);
        void File(int id, int offset, bool work = false) => inputs.Execute(new(NativeBattleShivaDirectorService.File, id, offset, UseWorkBuffer: work));
        void Sound(int address) => Emit(NativeBattleShivaDirectorService.Sound, address, 0, 128);
        void Volume() => Emit(NativeBattleShivaDirectorService.Volume, 128, 0, 96);
    }
}
