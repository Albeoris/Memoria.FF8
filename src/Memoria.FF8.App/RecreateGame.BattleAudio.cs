using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private BattleAudioPresentation? battleAudio;

    private void SynchronizeBattleAudio()
    {
        if (fieldAudio is null)
        {
            return;
        }
        battleAudio ??= new BattleAudioPresentation(fieldAudio.PlayBattleImpact, fieldAudio.PlayBattleSequenceSound, fieldAudio.PlayBattleModuleSound, fieldAudio.StopSounds, fieldAudio.PlayBattleMusicCommand, cue => fieldAudio.PlayUiSound((GameUi.MenuSound)cue));
        BattleSession? active = ActiveBattle;
        battleAudio.Update(active, active is not null && presentedBattle == active ? battleScene?.CollectSounds(active) : null);
    }
}
