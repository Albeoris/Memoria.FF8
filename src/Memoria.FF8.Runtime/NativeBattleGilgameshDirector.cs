namespace Memoria.FF8.Runtime;

public enum NativeBattleGilgameshDirectorService
{
    Camera,
    BeginWave,
    StartVibration,
    Sound,
    File,
    Upload,
    Initializer,
    AppendChild,
    AppendParticle,
    Volume,
    FadeTask,
    Title,
    QueueColor,
    StopVibrationIfActive,
    Stage
}

public sealed record NativeBattleGilgameshDirectorOperation(NativeBattleGilgameshDirectorService Service, int First = 0, int Second = 0, int Third = 0, int Fourth = 0, bool UseWorkBuffer = false);
public sealed record NativeBattleGilgameshDirectorInputs(uint GraphicsFlags, Action PublishWorldView, Func<int> Pending508500, Action<NativeBattleGilgameshDirectorOperation> Execute)
{
    public ushort Effect { get; init; } = 330;
}
public sealed record NativeBattleGilgameshDirectorStep(NativeBattleModulePoolTask Task, int Result);

/// <summary>592300's selected sword timeline and asynchronous admission boundaries.</summary>
public static class NativeBattleGilgameshDirector
{
    public static NativeBattleGilgameshDirectorStep Advance592300(NativeBattleModulePoolTask task, NativeBattleGilgameshDirectorInputs inputs)
    {
        if (task.Callback != 0x592300 || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native Gilgamesh director requires its original raw24h task.");
        }
        NativeBattleGilgameshProfile profile = NativeBattleGilgameshProfile.ForEffect(inputs.Effect);
        inputs.PublishWorldView();
        if ((inputs.GraphicsFlags & 0x201) != 0 && ((inputs.GraphicsFlags & 1) != 0 || inputs.Pending508500() < 0))
        {
            return new(task, 0);
        }
        int counter = unchecked((short)task.ReadWord(12));
        bool waitForFile = counter is 16 or 24 or 100 or 136 or 151 or 176 or 191 or 218 or 237 or 268 || counter == 253 && profile.HasAttackFile;
        if (waitForFile && inputs.Pending508500() < 0)
        {
            return new(task, 0);
        }
        switch (counter)
        {
            case 0:
                Emit(NativeBattleGilgameshDirectorService.Camera);
                Emit(NativeBattleGilgameshDirectorService.BeginWave, checked((int)profile.InitialStreamAddress));
                Emit(NativeBattleGilgameshDirectorService.StartVibration, 1, 128);
                Child(0x592BC0);
                break;
            case 1:
                Sound(0xCD0378);
                break;
            case 2:
                File(745, 0xE000);
                break;
            case 8:
                Initialize(0x592D00);
                Initialize(0x5904F0);
                break;
            case 16:
                Upload(0xE000);
                File(746, 0);
                break;
            case 18:
                Initialize(0x5931E0);
                break;
            case 24:
                File(747, 0x8000);
                break;
            case 44:
                Initialize(0x593550);
                break;
            case 76:
                Initialize(0x5904D0);
                Initialize(0x5947F0);
                Particle(0x5966B0);
                Child(0x595EE0);
                Initialize(0x592AB0);
                break;
            case 79:
            case 243:
                Volume();
                break;
            case 100:
                Upload(0x8000);
                break;
            case 101:
                Child(0x5939C0);
                break;
            case 113:
                Child(0x593770);
                break;
            case 121:
                Sound(0xCD037C);
                Initialize(0x594160);
                break;
            case 122:
                File(748, 0x8000);
                Child(0x593E80);
                break;
            case 123:
                Child(0x593C70);
                break;
            case 136:
                Upload(0x8000);
                File(749, 0x22000);
                break;
            case 151:
                File(750, 0, true);
                break;
            case 176:
            case 218:
            case 268:
                inputs.Execute(new(NativeBattleGilgameshDirectorService.BeginWave, UseWorkBuffer: true));
                break;
            case 181:
                Sound(0xCD0380);
                Volume();
                File(751, 0);
                Particle(0x596800);
                Child(0x596710);
                Emit(NativeBattleGilgameshDirectorService.FadeTask, 1, 92);
                break;
            case 191:
                File(752, 0, true);
                break;
            case 223:
                File(755, 0xE000);
                Particle(0x596A60);
                Child(0x596970);
                break;
            case 233:
                Emit(NativeBattleGilgameshDirectorService.Title);
                break;
            case 237:
                Upload(0xE000);
                break;
            case 238:
                if (profile.HasAttackFile)
                {
                    File(profile.AttackFile, 0xE000);
                }
                break;
            case 253:
                File(753, 0, true);
                break;
            case 263:
                Initialize(0x5943A0);
                break;
            case 273:
                Sound(checked((int)profile.ClosingSoundAddress));
                Volume();
                Upload(0xE000);
                Child(checked((int)profile.AttackCallback));
                break;
            case 279:
                Emit(NativeBattleGilgameshDirectorService.QueueColor, 2, 2, 12, 255);
                Child(0x596B30);
                break;
            case 280:
                Emit(NativeBattleGilgameshDirectorService.FadeTask, 1, 43);
                break;
            case 311:
                Initialize(0x592B30);
                break;
            case 324:
                Emit(NativeBattleGilgameshDirectorService.StopVibrationIfActive);
                break;
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        if (unchecked((short)task.ReadWord(12)) > 327)
        {
            Emit(NativeBattleGilgameshDirectorService.Stage, 0, 0);
            return new(task, 2);
        }
        return new(task, 0);

        void Emit(NativeBattleGilgameshDirectorService service, int first = 0, int second = 0, int third = 0, int fourth = 0) => inputs.Execute(new(service, first, second, third, fourth));
        void Initialize(int callback) => Emit(NativeBattleGilgameshDirectorService.Initializer, callback);
        void Child(int callback) => Emit(NativeBattleGilgameshDirectorService.AppendChild, callback);
        void Particle(int callback) => Emit(NativeBattleGilgameshDirectorService.AppendParticle, callback);
        void File(int id, int offset, bool work = false) => inputs.Execute(new(NativeBattleGilgameshDirectorService.File, id, offset, UseWorkBuffer: work));
        void Upload(int offset) => Emit(NativeBattleGilgameshDirectorService.Upload, offset);
        void Sound(int pointer) => Emit(NativeBattleGilgameshDirectorService.Sound, pointer, 0, 128);
        void Volume() => Emit(NativeBattleGilgameshDirectorService.Volume, 128, 0, 96);
    }
}
