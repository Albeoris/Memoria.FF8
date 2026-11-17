using Memoria.FF8.Runtime;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private readonly Queue<BattleEvent> battleMessages = new();
    private int messageEventCursor;
    private bool collectedInitialMessages;
    private BattleEvent? activeBattleMessage;
    private TimeSpan battleMessageElapsed;
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
    private bool showActorDialogue = true;
    internal bool ShowActorDialogue
    {
        get => showActorDialogue;
        set
        {
            if (showActorDialogue == value)
            {
                return;
            }
            showActorDialogue = value;
            PruneBattleMessages();
        }
    }
    private bool showEnemySpellNames;
    public bool ShowEnemySpellNames
    {
        get => showEnemySpellNames;
        set
        {
            if (showEnemySpellNames == value)
            {
                return;
            }
            showEnemySpellNames = value;
            PruneBattleMessages();
        }
    }

    private bool CanPresentBattleMessage(BattleEvent message)
    {
        if (message.IsEnemyActionTitle)
        {
            return ShowEnemySpellNames;
        }
        bool actorDialogue = message.Kind == "NativeText" && message.ActorId is not null &&
            (message.NativeTextIdentity is null || message.NativeAiHeapMessage is not null ||
                message.NativeTextIdentity.File.EndsWith("_dialogue_battle", StringComparison.Ordinal));
        return ShowActorDialogue || !actorDialogue;
    }

    private void PruneBattleMessages()
    {
        BattleEvent[] retained = battleMessages.Where(CanPresentBattleMessage).ToArray();
        battleMessages.Clear();
        foreach (BattleEvent message in retained)
        {
            battleMessages.Enqueue(message);
        }
        if (activeBattleMessage is { } active && !CanPresentBattleMessage(active))
        {
            activeBattleMessage = battleMessages.TryDequeue(out BattleEvent? next) ? next : null;
            battleMessageElapsed = TimeSpan.Zero;
        }
    }
    internal event Action<LocalizationTextBinding, string, string, string>? DialoguePresented;

    public string? BattleMessage => BattleMessageDocument?.Text;

    internal bool BattleMessageIsActionTitle => activeBattleMessage is { NativeMessageMode: 1 } message && CanPresentBattleMessage(message) && battleMessageElapsed >= BattlePresentationTiming.ElapsedForCallbacks(message.NativeMessageDelayFrames);

    internal double BattlePresentationSeconds => battlePresentationSeconds;

    internal double BattleCallbackRate => BattlePresentationTiming.CallbacksPerSecond;

    internal int GuardianFormSeconds(int remainingTicks) => checked((int)Math.Ceiling(BattlePresentationTiming.ElapsedForCallbacks(remainingTicks).TotalSeconds));

    internal DialogueDocument? BattleMessageDocument
    {
        get
        {
            if (activeBattleMessage is not { } message || !CanPresentBattleMessage(message) || battleMessageElapsed < BattlePresentationTiming.ElapsedForCallbacks(message.NativeMessageDelayFrames))
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
        AdvanceCommandRowTransition(elapsed);
        CollectBattleMessages();
        battlePresentationSeconds += elapsed.TotalSeconds;
        battleMessageElapsed += elapsed;
        while (activeBattleMessage is { } message && battleMessageElapsed >= MessageDuration(message))
        {
            battleMessageElapsed -= MessageDuration(message);
            activeBattleMessage = battleMessages.TryDequeue(out BattleEvent? next) ? next : null;
        }
        if (activeBattleMessage is null)
        {
            battleMessageElapsed = TimeSpan.Zero;
        }
        ObserveBattleMessage();
    }

    private static TimeSpan MessageDuration(BattleEvent message)
    {
        return BattlePresentationTiming.ElapsedForCallbacks((long)message.NativeMessageDelayFrames + Math.Max(1, message.Amount));
    }

    private void ObserveBattleMessage()
    {
        if (activeBattleMessage is not { } message || battleMessageElapsed < BattlePresentationTiming.ElapsedForCallbacks(message.NativeMessageDelayFrames) || ReferenceEquals(observedBattleMessage, message))
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
            if (message.Kind == "NativeText" && CanPresentBattleMessage(message) && (collectedInitialMessages || message.Frame >= session.Frame))
            {
                battleMessages.Enqueue(message);
            }
        }
        collectedInitialMessages = true;
        if (activeBattleMessage is null && battleMessages.TryDequeue(out BattleEvent? first))
        {
            activeBattleMessage = first;
            battleMessageElapsed = TimeSpan.Zero;
        }
    }
}
