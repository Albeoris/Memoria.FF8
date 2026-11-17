using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Transient audio follows committed battle events without becoming saved simulation state.</summary>
internal sealed class BattleAudioPresentation(Action<NativeBattleModuleSound> playModule, Action stopSounds, Action<NativeBattleMusicCommandReceipt> playMusicCommand, Action<int> playUiSound, Action<byte> stopSoundVoice)
{
    private BattleSession? battle;
    private IReadOnlyList<BattleEvent> receipts = [];
    private int consumedEvents;

    public void Update(BattleSession? active)
    {
        if (!ReferenceEquals(battle, active))
        {
            battle = active;
            receipts = active?.Events ?? [];
            consumedEvents = receipts.Count;
        }
        while (consumedEvents < receipts.Count)
        {
            BattleEvent entry = receipts[consumedEvents++];
            if (entry.NativeUiSound is int uiSound)
            {
                playUiSound(uiSound);
            }
            if (entry.NativeMusicCommand is { } musicCommand)
            {
                playMusicCommand(musicCommand);
            }
            if (entry.NativeStopSoundVoice is byte voice)
            {
                if (voice >= 32)
                {
                    throw new InvalidDataException("Native sound stop requires a physical voice below32.");
                }
                stopSoundVoice(voice);
            }
            if (entry.NativeStopAllSounds)
            {
                stopSounds();
            }
            if (entry.NativeModuleSound is { } module)
            {
                playModule(module);
            }
        }
    }
}
