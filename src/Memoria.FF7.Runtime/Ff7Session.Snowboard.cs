using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private ushort snowboardHeld;
    private ushort snowboardPressed;

    public Ff7SnowboardRuntime? Snowboard { get; private set; }

    public void SetSnowboardInput(ushort held, ushort pressed)
    {
        snowboardHeld = held;
        snowboardPressed = pressed;
    }

    private void AdvanceSnowboard(FieldHostRequest request, bool advanceClock)
    {
        if (Snowboard is null)
        {
            Ff7SnowboardRuntime prepared = new(installation, request, Progress.BattleRng?.CrtSeed ?? 1);
            prepared.Sound += (channel, sample, volume) =>
            {
                if (channel is < 1 or > 4)
                {
                    throw new NotSupportedException("The snowboard sound request has no native channel.");
                }
                Host.SubmitNativeAudio(new(0x27 + channel, 64, sample));
                Host.SubmitNativeAudio(new(0x9F + channel, volume == 0 ? 127 : volume));
            };
            Snowboard = prepared;
            Host.SubmitNativeAudio(new(0xBA, 64));
            Host.SubmitNativeAudio(new(0xB8, 127));
            Host.SubmitNativeAudio(new(0xBC, 0));
            Host.SubmitNativeAudio(new(0x10, 0x41));
            Host.SubmitNativeAudio(new(0xC0, 127));
            return;
        }
        Ff7SnowboardRuntime snowboard = Snowboard;
        if (snowboard.Request != request)
        {
            throw new InvalidDataException("The snowboard module lost its native field request owner.");
        }
        if (advanceClock)
        {
            AdvanceNominalClockCallbacks();
        }
        snowboard.Advance(snowboardHeld, snowboardPressed);
        snowboardPressed = 0;
        Host.AdvanceNativeMusicClock();
        if (!snowboard.Finished)
        {
            return;
        }
        FieldVariables variables = new(Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteByte(FieldBank.Persistent1Byte, 0xCE, snowboard.NativeResult);
        Ff7Progress progress = Progress with { BattleRng = (Progress.BattleRng ?? new(new byte[8])) with { CrtSeed = snowboard.RandomSeed } };
        CompleteMinigameFieldReturn(((FieldMinigameCommand)request.Command).Return, variables, progress);
        Snowboard = null;
        snowboardHeld = snowboardPressed = 0;
    }
}
