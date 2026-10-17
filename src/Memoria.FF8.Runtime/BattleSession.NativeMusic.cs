namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleMusicProcessState? NativeMusicProcess { get; private set; }

    private void InitializeNativeMusic501C60()
    {
        NativeBattleMusicProcessState music = NativeMusicProcess ?? throw new NotSupportedException("Native500870/501C60 requires the transferred battle music process and its actual return-bank BYTE; fresh graphics initialization cannot invent it.");
        NativeMusicProcess = music.InitializeCommands501C60();
    }

    private void DrainNativeMusic501B60()
    {
        NativeBattleMusicProcessState music = NativeMusicProcess ?? throw new NotSupportedException("Native50065E/501B60 requires its retained music command owner on every admitted native callback; restore does not execute501C60.");
        var step = music.DrainCommands501B60Core(NativeGlobalSoundProcess, () => music.CurrentFieldWord ?? throw new NotSupportedException("Native501B60 song17 requires its actual current-field WORD1CD2FC0."));
        NativeMusicProcess = step.State;
        NativeGlobalSoundProcess = step.Sound;
        if (step.Receipt is { } receipt)
        {
            events.Add(new(Frame, "NativeMusicCommand", null, null, 0, $"Native501B60 consumed singleton command{receipt.Command}.") { NativeMusicCommand = receipt });
        }
    }
}
