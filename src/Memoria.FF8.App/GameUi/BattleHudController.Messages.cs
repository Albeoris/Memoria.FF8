using Memoria.FF8.Runtime;
using Memoria.FF8.Resources.Localization;

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
    private string? formattedBattleMessageBindings;
    private double battlePresentationSeconds;
    private DialogueDocument? commandError;
    private double commandErrorUntil;
    private BattleEvent? observedBattleMessage;
    private readonly string messageOccurrenceSession = Guid.NewGuid().ToString("N");
    private long messageOccurrenceSequence;
    private bool formattedOriginalCompatible;
    internal bool OriginalCompatibleLayout { get; set; }
    internal event Action<LocalizationTextBinding, string, string, string>? DialoguePresented;

    public string? BattleMessage => BattleMessageDocument?.Text;

    internal double BattlePresentationSeconds => battlePresentationSeconds;

    internal double BattleCallbackRate => session.NativeGraphics is null ? BattleSession.FramesPerSecond : NativeBattleGraphicsPresentationClock.CallbacksPerSecond;

    internal int GuardianFormSeconds(int remainingTicks) => checked((int)Math.Ceiling(remainingTicks / BattleCallbackRate));

    internal DialogueDocument? BattleMessageDocument
    {
        get
        {
            if (activeBattleMessage is not { } message || battleMessageFrames < message.NativeMessageDelayFrames)
            {
                return battlePresentationSeconds < commandErrorUntil ? commandError : null;
            }
            BoundLocalizationText? bound = boundBattleMessage?.Invoke(message);
            string displayed = bound?.Text ?? localizedBattleMessage?.Invoke(message) ?? message.Message;
            string bindings = DialogueTextFormatter.ExpandButtonBindings(displayed, NativeButtonBindingHint);
            if (!ReferenceEquals(message, formattedBattleMessageEvent) || bindings != formattedBattleMessageBindings || formattedOriginalCompatible != OriginalCompatibleLayout)
            {
                formattedBattleMessage = DialogueTextFormatter.Parse(displayed, TextNames(), buttonBindingHint: NativeButtonBindingHint, originalCompatible: OriginalCompatibleLayout);
                formattedBattleMessageEvent = message;
                formattedBattleMessageBindings = bindings;
                formattedOriginalCompatible = OriginalCompatibleLayout;
            }
            if (formattedBattleMessage is not null && bound is not null && (formattedBattleMessage.Binding is not { } retained || retained.Key != bound.Binding.Key || retained.Template != bound.Binding.Template || retained.ResourceFingerprint != bound.Binding.ResourceFingerprint))
            {
                formattedBattleMessage = formattedBattleMessage with { Binding = bound.Binding with { Text = formattedBattleMessage.Text, Arguments = [TextNames()] } };
            }
            return formattedBattleMessage;
        }
    }

    private Dictionary<int, string> TextNames()
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
        return names;
    }

    private string FormatBattleText(string encoded) => DialogueTextFormatter.FormatWithPrompts(encoded, TextNames(), buttonBindingHint: NativeButtonBindingHint);

    private void ShowCommandError(string message)
    {
        Message = message;
        commandError = DialogueTextFormatter.Parse(message, TextNames(), buttonBindingHint: NativeButtonBindingHint);
        commandErrorUntil = battlePresentationSeconds + 5;
    }

    public void AdvancePresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        AdvanceResults(elapsed);
        RefreshNativeLimitSelection();
        CollectBattleMessages();
        battlePresentationSeconds += elapsed.TotalSeconds;
        battleMessageFrames += elapsed.TotalSeconds * BattleCallbackRate;
        while (activeBattleMessage is { } message && battleMessageFrames >= message.NativeMessageDelayFrames + Math.Max(1, message.Amount))
        {
            battleMessageFrames -= message.NativeMessageDelayFrames + Math.Max(1, message.Amount);
            activeBattleMessage = battleMessages.TryDequeue(out BattleEvent? next) ? next : null;
        }
        if (activeBattleMessage is null)
        {
            battleMessageFrames = 0;
        }
        ObserveBattleMessage();
    }

    private void ObserveBattleMessage()
    {
        if (activeBattleMessage is not { } message || battleMessageFrames < message.NativeMessageDelayFrames || ReferenceEquals(observedBattleMessage, message))
        {
            return;
        }
        observedBattleMessage = message;
        if (BattleMessageDocument is { Binding: { } binding } document)
        {
            DialoguePresented?.Invoke(binding, "Battle/messages", messageOccurrenceSession + ":" + (++messageOccurrenceSequence).ToString(System.Globalization.CultureInfo.InvariantCulture), document.Text);
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
