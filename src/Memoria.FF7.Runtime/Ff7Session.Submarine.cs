using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private ushort submarineHeld;
    private ushort submarinePressed;
    public Ff7SubmarineRuntime? Submarine { get; private set; }

    public void SetSubmarineInput(ushort held, ushort pressed)
    {
        submarineHeld = held;
        submarinePressed = pressed;
    }

    private void ConfigureSubmarineSound(Ff7SubmarineRuntime runtime)
    {
        runtime.SoundRequested += sound => Host.SubmitNativeAudio(new(0x2B, 64, sound));
    }

    private void RestoreSubmarine(Ff7SessionSave save)
    {
        if (save.Submarine is { } snapshot)
        {
            Submarine = new(Ff7SubmarineResources.Load(installation), snapshot.Request, snapshot.Mission, snapshot.RandomSeed, snapshot);
            ConfigureSubmarineSound(Submarine);
        }
    }

    private void AdvanceSubmarine(FieldHostRequest request, bool advanceClock)
    {
        if (Submarine is null)
        {
            Submarine = new(Ff7SubmarineResources.Load(installation), request, Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x5D), Progress.BattleRng?.CrtSeed ?? 1);
            ConfigureSubmarineSound(Submarine);
            return;
        }
        Ff7SubmarineRuntime submarine = Submarine;
        if (submarine.Request != request)
        {
            throw new InvalidDataException("The FFVII submarine module lost its unfinished native field departure.");
        }
        if (advanceClock)
        {
            AdvanceNominalClockCallbacks();
        }
        submarine.Advance(submarineHeld, submarinePressed);
        submarinePressed = 0;
        Host.AdvanceNativeMusicClock();
        if (!submarine.Finished)
        {
            return;
        }
        FieldVariables variables = new(Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteWord(FieldBank.Persistent3Word, 0x5E, unchecked((short)submarine.NativeFieldResult));
        Ff7Progress progress = Progress with { BattleRng = (Progress.BattleRng ?? new(new byte[8])) with { CrtSeed = submarine.RandomSeed } };
        CompleteOwnedMinigameFieldReturn(request, variables, progress);
        Submarine = null;
        submarineHeld = submarinePressed = 0;
    }
}
