using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly Queue<BattleEvent> battleMessages = new();
    private int messageEventCursor;
    private bool collectedInitialMessages;
    private BattleEvent? activeBattleMessage;
    private double battleMessageFrames;

    public string? BattleMessage => activeBattleMessage is { } message && battleMessageFrames >= message.NativeMessageDelayFrames ? message.Message : null;

    public void AdvancePresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        CollectBattleMessages();
        battleMessageFrames += elapsed.TotalSeconds * BattleSession.FramesPerSecond;
        while (activeBattleMessage is { } message && battleMessageFrames >= message.NativeMessageDelayFrames + Math.Max(1, message.Amount))
        {
            battleMessageFrames -= message.NativeMessageDelayFrames + Math.Max(1, message.Amount);
            activeBattleMessage = battleMessages.TryDequeue(out BattleEvent? next) ? next : null;
        }
        if (activeBattleMessage is null)
        {
            battleMessageFrames = 0;
        }
    }

    private void CollectBattleMessages()
    {
        for (; messageEventCursor < session.Events.Count; messageEventCursor++)
        {
            BattleEvent message = session.Events[messageEventCursor];
            if (message.Kind == "NativeText" && (collectedInitialMessages || message.Frame >= session.Frame))
            {
                battleMessages.Enqueue(message);
            }
        }
        collectedInitialMessages = true;
        if (activeBattleMessage is null && battleMessages.TryDequeue(out BattleEvent? first))
        {
            activeBattleMessage = first;
            battleMessageFrames = 0;
        }
    }
}
