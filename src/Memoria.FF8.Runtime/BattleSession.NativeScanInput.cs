namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool CanQueueNativeScanInput => Presentation?.CanQueueScanInput == true;

    public void QueueNativeScanInput(BattleScanButton button)
    {
        (Presentation ?? throw new InvalidOperationException("Scan input requires its bound presentation.")).QueueScanInput(button);
    }

    public void PublishNativeScanMappedButtons(ushort mappedButtons)
    {
        Presentation?.PublishScanMappedButtons(mappedButtons);
    }
}
