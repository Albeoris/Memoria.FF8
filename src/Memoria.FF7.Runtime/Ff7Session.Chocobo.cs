using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private Ff7ChocoboCarryState? chocoboCarry;

    public Ff7ChocoboRuntime? ChocoboRace { get; private set; }

    public void SetChocoboInput(ushort held)
    {
        (ChocoboRace ?? throw new InvalidOperationException("The FFVII Chocobo module is not active.")).SetInput(held);
    }

    private void RestoreChocobo(Ff7SessionSave save)
    {
        chocoboCarry = save.ChocoboCarry;
        if (save.ChocoboRace is not { } race)
        {
            return;
        }
        ChocoboRace = new(race.Request, race.Parameters, Ff7ChocoboResources.LoadCourse(installation, race.Parameters[7] != 0), Ff7PcExecutableData.LoadChocoboData(installation), 0, race);
        BindChocoboAudio(ChocoboRace);
    }

    private void BindChocoboAudio(Ff7ChocoboRuntime race)
    {
        race.Audio += command => Host.SubmitNativeAudio(command);
    }

    private void AdvanceChocobo(FieldHostRequest request, bool advanceClock)
    {
        if (ChocoboRace is null)
        {
            byte[] parameters = Machine.Variables.CopyPersistent().AsSpan(0x200, 29).ToArray();
            if (parameters[8] != 0)
            {
                parameters[9] = 255;
            }
            Ff7ChocoboRuntime prepared = new(request, parameters, Ff7ChocoboResources.LoadCourse(installation, parameters[7] != 0), Ff7PcExecutableData.LoadChocoboData(installation), Progress.BattleRng?.CrtSeed ?? 1, carry: chocoboCarry);
            Machine.Variables.WriteByte(FieldBank.Persistent2Byte, 9, parameters[9]);
            ChocoboRace = prepared;
            BindChocoboAudio(prepared);
            Host.SubmitNativeAudio(new(0x23, 64, 0, 0, 0, 0));
            return;
        }
        Ff7ChocoboRuntime race = ChocoboRace;
        if (race.Request != request)
        {
            throw new InvalidDataException("The FFVII Chocobo race lost its native field departure.");
        }
        if (!race.Finished)
        {
            if (advanceClock)
            {
                AdvanceNominalClockCallbacks();
            }
            race.Advance();
            Host.AdvanceNativeMusicClock();
        }
        if (!race.Finished)
        {
            return;
        }
        Ff7ChocoboSnapshot completed = race.Capture();
        FieldVariables variables = new(Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteByte(FieldBank.Persistent2Byte, 0x19, checked((byte)(race.Player.Placement - 1)));
        Ff7Progress progress = Progress with { BattleRng = (Progress.BattleRng ?? new(new byte[8])) with { CrtSeed = race.RandomSeed } };
        CompleteMinigameFieldReturn(((FieldMinigameCommand)request.Command).Return, variables, progress);
        chocoboCarry = new(completed.Simulation, completed.Parameters[7] != 0, completed.RecoverySide, completed.RecoveryCount) { Camera = completed.Camera };
        ChocoboRace = null;
    }
}
