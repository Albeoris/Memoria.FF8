using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly Queue<BattleEvent> battleMessages = new();
    private int messageEventCursor;
    private bool collectedInitialMessages;
    private BattleEvent? activeBattleMessage;
    private double battleMessageFrames;
    private BattleEvent? formattedBattleMessageEvent;
    private DialogueDocument? formattedBattleMessage;
    private double battlePresentationSeconds;
    private DialogueDocument? commandError;
    private double commandErrorUntil;

    public string? BattleMessage => BattleMessageDocument?.Text;

    internal double BattlePresentationSeconds => battlePresentationSeconds;

    internal DialogueDocument? BattleMessageDocument
    {
        get
        {
            if (activeBattleMessage is not { } message || battleMessageFrames < message.NativeMessageDelayFrames)
            {
                return battlePresentationSeconds < commandErrorUntil ? commandError : null;
            }
            if (!ReferenceEquals(message, formattedBattleMessageEvent))
            {
                Dictionary<int, string> names = battleNames?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
                foreach (BattleCombatant actor in session.Combatants.Where(actor => actor.IsPlayer && actor.ModelId is >= 0 and <= 10))
                {
                    names[GameNames.CharacterSubject(actor.ModelId)] = actor.Name;
                }
                for (int id = 0; id < 16; id++)
                {
                    int subject = GameNames.GuardianForceSubject(id);
                    if (hasGuardianForceNames)
                    {
                        names[subject] = guardianForceName(id);
                    }
                    else if (!names.ContainsKey(subject))
                    {
                        names[subject] = GameNames.DefaultName(subject);
                    }
                }
                formattedBattleMessage = DialogueTextFormatter.Parse(message.Message, names);
                formattedBattleMessageEvent = message;
            }
            return formattedBattleMessage;
        }
    }

    private void ShowCommandError(string message)
    {
        Message = message;
        commandError = DialogueTextFormatter.Parse(message, new Dictionary<int, string>());
        commandErrorUntil = battlePresentationSeconds + 5;
    }

    public void AdvancePresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        CollectBattleMessages();
        battlePresentationSeconds += elapsed.TotalSeconds;
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
