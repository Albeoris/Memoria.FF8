using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private ushort condorHeld;
    private ushort condorPressed;

    public Ff7CondorRuntime? Condor { get; private set; }

    public void SetCondorInput(ushort held, ushort pressed)
    {
        condorHeld = held;
        condorPressed = pressed;
    }

    private void RestoreCondor(Ff7SessionSave save)
    {
        if (save.Condor is { } snapshot)
        {
            Condor = new(Ff7CondorResources.Load(installation), snapshot.Request, snapshot.Attack, Progress.Gil, snapshot.RandomSeed, snapshot);
        }
    }

    private void AdvanceCondor(FieldHostRequest request, bool advanceClock)
    {
        if (Condor is null)
        {
            Condor = new(Ff7CondorResources.Load(installation), request, Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xA9), Progress.Gil, Progress.BattleRng?.CrtSeed ?? 1);
            return;
        }
        Ff7CondorRuntime condor = Condor;
        if (condor.Request != request)
        {
            throw new InvalidDataException("The FFVII Condor module lost its unfinished field departure.");
        }
        if (advanceClock)
        {
            AdvanceNominalClockCallbacks();
        }
        condor.Advance(condorHeld, condorPressed);
        condorPressed = 0;
        Host.AdvanceNativeMusicClock();
        if (!condor.Finished)
        {
            return;
        }
        Ff7CondorSnapshot completed = condor.Capture();
        FieldVariables variables = new(Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteByte(FieldBank.Persistent0Byte, 0xAA, 0);
        variables.WriteByte(FieldBank.Persistent0Byte, 0xAF, checked((byte)completed.Enemies.Length));
        variables.WriteByte(FieldBank.Persistent0Byte, 0xAB, checked((byte)(completed.NextEnemy - completed.Enemies.Length)));
        variables.WriteByte(FieldBank.Persistent0Byte, 0xAC, condor.NativeFieldResult);
        Ff7Progress progress = Progress with { Gil = completed.Funds, BattleRng = (Progress.BattleRng ?? new(new byte[8])) with { CrtSeed = completed.RandomSeed } };
        CompleteMinigameFieldReturn(((FieldMinigameCommand)request.Command).Return, variables, progress);
        Condor = null;
        condorHeld = condorPressed = 0;
    }
}
