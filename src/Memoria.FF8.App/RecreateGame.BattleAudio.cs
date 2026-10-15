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
        battleAudio ??= new BattleAudioPresentation(fieldAudio.StopAll, fieldAudio.PlayBattleMusic, fieldAudio.PlayBattleImpact);
        if (battleAudio.Update(ActiveBattle))
        {
            resumeFieldMusic = true;
        }
    }
}
