using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private BattleAudioPresentation? battleAudio;

    private void SynchronizeBattleAudio()
    {
        SynchronizeBattleAudio(ActiveBattle);
    }

    private void SynchronizeBattleAudio(BattleSession? active)
    {
        if (fieldAudio is null)
        {
            return;
        }
        battleAudio ??= new BattleAudioPresentation(fieldAudio.PlayBattleModuleSound, fieldAudio.StopSounds, fieldAudio.PlayBattleMusicCommand, cue => fieldAudio.PlayUiSound((GameUi.MenuSound)cue), fieldAudio.StopBattleSoundVoice);
        battleAudio.Update(active);
    }
}
