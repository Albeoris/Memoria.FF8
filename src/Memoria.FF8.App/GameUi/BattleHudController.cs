using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Live battle input and presentation. Commands always pass through the battle session.</summary>
public sealed partial class BattleHudController
{
    private readonly BattleSession session;
    private readonly Action openDebug;
    private readonly Action advanceTurn;
    private readonly Action<string, string> attack;
    private readonly Action? complete;
    private readonly UiView view;
    private readonly Action<BattleInput>? execute;
    private readonly Func<BattleRewardSummary?>? previewRewards;
    private readonly Func<int, string> guardianForceName;
    private readonly Func<int, string> itemName;
    private readonly Func<int, string> cardName;
    private readonly IReadOnlyDictionary<int, string>? battleNames;
    private readonly bool hasGuardianForceNames;
    private bool completionRequested;
    private int commandIndex;
    private int actionIndex;
    private BattleEvent? lastNotification;
    public bool SelectingAction { get; private set; }
    public bool DrawCast { get; private set; }
    private string? selectedActorId;
    private string? selectedTargetId;

    public BattleHudController(BattleSession session, Action<string, string> attack, Action openDebug, Action advanceTurn, Action? complete = null, Action<BattleInput>? execute = null, Func<BattleRewardSummary?>? previewRewards = null, Func<int, string>? guardianForceName = null, Func<int, string>? itemName = null, Func<int, string>? cardName = null, IReadOnlyDictionary<int, string>? battleNames = null)
    {
        this.session = session;
        this.execute = execute;
        this.attack = attack;
        this.complete = complete;
        this.openDebug = openDebug;
        this.advanceTurn = advanceTurn;
        this.previewRewards = previewRewards;
        this.guardianForceName = guardianForceName ?? (id => $"GF {id}");
        this.itemName = itemName ?? (id => $"Item {id}");
        this.cardName = cardName ?? (id => $"Card {id}");
        this.battleNames = battleNames;
        hasGuardianForceNames = guardianForceName is not null;
        view = new UiView(new BattleHudElement(this)) { Cancel = CancelTarget };
        view.Resize(960, 672);
        RefreshNativeLimitSelection();
    }

    public BattleSession Session => session;
    public event Action<MenuSound>? SoundRequested;
    public bool SelectingTarget { get; private set; }
    public string Message { get; private set; } = "Choose a command when a character is ready.";
    public float TextScale { get; set; } = 1;
    public string GunbladeBindingHint { get; set; } = new KeyboardInput().BindingPrompt(ControlAction.RotateRight);
    internal string GuardianForceDisplayName(int id) => guardianForceName(id);
    public IReadOnlyList<BattleCombatant> Party => session.Combatants.Where(actor => actor.IsPlayer).ToArray();
    public IReadOnlyList<BattleAbility> Commands => GetPlayerCommandChoices();
    public BattleAbility SelectedCommand
    {
        get
        {
            IReadOnlyList<BattleAbility> commands = Commands;
            return commands.Count == 0 ? BattleAbility.Attack : commands[Math.Clamp(commandIndex, 0, commands.Count - 1)];
        }
    }
    public IReadOnlyList<BattleActionDefinition> ActionChoices
    {
        get
        {
            BattleAbility command = SelectedCommand;
            if (HasGuardianFormCommands)
            {
                return [];
            }
            if (command == BattleAbility.Attack || SelectedActor is not { } actor)
            {
                return [];
            }
            if (command == BattleAbility.Draw)
            {
                return DrawActionChoices();
            }
            if (command is BattleAbility.NativeCommand or BattleAbility.LimitBreak)
            {
                return session.GetNativePlayerActionChoices(actor.Id, command == BattleAbility.LimitBreak);
            }
            return session.ActionDefinitions.Where(action => command switch
            {
                BattleAbility.Magic => action.Ability == BattleAbility.Magic && actor.MagicStocks.Any(stock => stock.Id == action.Id && stock.Count > 0) && session.QuoteMagic(actor.Id, action.Id).Capacity > 0,
                BattleAbility.Item => action.Ability == BattleAbility.Item && session.Items.Any(stock => stock.Id == action.Id && stock.Count > 0),
                BattleAbility.GuardianForce => action.Ability == BattleAbility.GuardianForce && actor.EquipmentIds.Contains(action.Id) && action.Available && (session.GuardianForces.Count == 0 || session.GuardianForces.Any(gf => gf.Id == action.Id && gf.Acquired && gf.HitPoints > 0)) && !session.GuardianForceCasts.Any(cast => cast.Command.ActionId == action.Id),
                _ => false
            }).ToArray();
        }
    }
    public BattleActionDefinition? SelectedAction
    {
        get
        {
            IReadOnlyList<BattleActionDefinition> actions = ActionChoices;
            return actions.ElementAtOrDefault(Math.Clamp(actionIndex, 0, Math.Max(0, actions.Count - 1)));
        }
    }
    public IReadOnlyList<string> ChoiceLabels => SelectingGuardianFormAction ? ["Summon"] : HasGuardianFormCommands ? ["Attack", "Fire", "Hell Fire"] : SelectingDrawMode ? SelectedAction?.Id >= 64 ? ["Stock"] : ["Stock", "Cast"] : NativeDuelChoiceLabels ?? (SelectingAction ? ActionChoices.Select(ChoiceLabel).ToArray() : Commands.Select(CommandLabel).ToArray());
    public int ChoiceIndex => SelectingGuardianFormAction ? 0 : SelectingDrawMode ? DrawCast ? 1 : 0 : SelectingAction ? Math.Clamp(actionIndex, 0, Math.Max(0, ActionChoices.Count - 1)) : Math.Clamp(commandIndex, 0, Math.Max(0, Commands.Count - 1));
    public int ChoiceStart => Math.Clamp(ChoiceIndex - 1, 0, Math.Max(0, ChoiceLabels.Count - 4));
    public IReadOnlyList<BattleCombatant> Targets
    {
        get
        {
            BattleAbility command = SelectedCommand;
            if (HasGuardianFormCommands)
            {
                return GuardianFormTargets();
            }
            if (command == BattleAbility.Draw)
            {
                return DrawTargets();
            }
            BattleActionDefinition? action = command == BattleAbility.Attack ? null : SelectedAction;
            bool allies = action?.Allies == true;
            bool fixedSelf = action?.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags);
            string? actorId = fixedSelf ? SelectedActor?.Id : null;
            return session.Combatants.Where(actor => actor.IsPlayer == allies && (!fixedSelf || actor.Id == actorId) && (action is not null ? BattleSession.HasValidTargetLifeState(action, actor) : actor.IsAlive)).ToArray();
        }
    }
    public bool SelectingTargetGroup
    {
        get
        {
            if (!SelectingTarget || SelectingDrawSource)
            {
                return false;
            }
            BattleActionDefinition? action = HasGuardianFormCommands && SelectedActor is { } actor ? session.GetGuardianFormActionDefinition(actor.Id, SelectedGuardianFormCommand) : SelectedCommand == BattleAbility.Attack ? null : SelectedAction;
            return action?.NativeAction?.TargetFlags is byte flags ? (flags & 0x30) != 0x10 : action?.AllTargets == true;
        }
    }
    public string TargetGroupDescription => Targets.All(actor => !actor.IsPlayer) ? "All enemies" : Targets.All(actor => actor.IsPlayer) ? "All allies" : "All targets";
    public IReadOnlyList<BattleCombatant> VisibleTargets => Targets;
    public BattleCombatant? SelectedActor
    {
        get
        {
            BattleCombatant? selected = session.UsesNativeReadyMenu ? Party.FirstOrDefault(actor => actor.Id == session.SelectedPlayerCommandActorId && CanChooseCommand(actor)) : Party.FirstOrDefault(actor => actor.Id == selectedActorId && CanChooseCommand(actor));
            selected ??= session.ReadyPlayerCommandOrder.FirstOrDefault();
            selectedActorId = selected?.Id;
            SynchronizeCursorActor(selected);
            return selected;
        }
    }

    public BattleCombatant? SelectedTarget
    {
        get
        {
            IReadOnlyList<BattleCombatant> targets = Targets;
            BattleCombatant? selected = targets.FirstOrDefault(actor => actor.Id == selectedTargetId) ?? targets.FirstOrDefault();
            selectedTargetId = selected?.Id;
            return selected;
        }
    }

    public UiView BuildView()
    {
        if (guardianForceLearningMenu is { IsOpen: true })
        {
            return guardianForceLearningMenu.BuildView();
        }
        RefreshNativeLimitSelection();
        RefreshGuardianFormSelection();
        RefreshDrawSelection();
        PresentNativeShot();
        CollectBattleMessages();
        BattleEvent? notification = session.Events.LastOrDefault(item => item.Kind is "Scan" or "GunbladeTrigger");
        if (notification is not null && notification != lastNotification)
        {
            Message = notification.Message;
            lastNotification = notification;
        }
        if ((SelectingTarget || SelectingAction) && SelectedActor is null)
        {
            SelectingTarget = false;
            SelectingAction = false;
            SelectingDrawMode = false;
            selectedDrawSourceId = null;
            session.CloseNativeActionMenu();
            selectedMagicTargets.Clear();
        }
        return view;
    }

    public void Navigate(MenuInput input)
    {
        if (guardianForceLearningMenu is not null)
        {
            NavigateGuardianForceLearning(input);
            return;
        }
        RefreshNativeLimitSelection();
        RefreshGuardianFormSelection();
        if (RefreshDrawSelection())
        {
            return;
        }
        commandIndex = Math.Clamp(commandIndex, 0, Math.Max(0, Commands.Count - 1));
        actionIndex = Math.Clamp(actionIndex, 0, Math.Max(0, ActionChoices.Count - 1));
        if (input == MenuInput.Cancel && NativeShotActor is { } shotActor)
        {
            session.EndNativeShot(shotActor.Id);
            return;
        }
        if (NativeTimedLimitActive || CommandMenuHidden)
        {
            return;
        }
        if (session.Outcome != BattleOutcome.Active)
        {
            if (input == MenuInput.Confirm)
            {
                if (session.Outcome == BattleOutcome.Victory)
                {
                    ConfirmResult();
                }
                else
                {
                    Complete();
                }
            }
            return;
        }
        if (NavigateGuardianFormActions(input))
        {
            return;
        }
        if (input == MenuInput.Cancel)
        {
            CancelTarget();
        }
        else if (input == MenuInput.Confirm)
        {
            if (session.Outcome == BattleOutcome.Active)
            {
                Attack();
            }
            else
            {
                Complete();
            }
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.Left or MenuInput.Right or MenuInput.PageLeft or MenuInput.PageRight)
        {
            if (SelectingDrawMode)
            {
                if (SelectedAction?.Id < 64)
                {
                    DrawCast = !DrawCast;
                    SoundRequested?.Invoke(MenuSound.Move);
                }
                return;
            }
            if (SelectingAction && !SelectingTarget && SelectedCommand == BattleAbility.Magic && input is MenuInput.PageLeft or MenuInput.PageRight)
            {
                ChangeMagicCastCount(input == MenuInput.PageLeft ? -1 : 1);
                return;
            }
            if (SelectingAction && !SelectingTarget && SelectedCommand == BattleAbility.LimitBreak && SelectedActor?.NativeLimit?.Command == 16 && input is MenuInput.PageLeft or MenuInput.PageRight)
            {
                RerollNativeSlots();
                return;
            }
            int direction = input is MenuInput.Up or MenuInput.Left or MenuInput.PageLeft ? -1 : 1;
            if (SelectingTarget)
            {
                if (SelectingTargetGroup)
                {
                    return;
                }
                string? before = SelectedTarget?.Id;
                if (Targets.Count > 4 && input is MenuInput.Left or MenuInput.Right)
                {
                    int index = Targets.ToList().FindIndex(target => target.Id == selectedTargetId);
                    int columns = (Targets.Count + 3) / 4;
                    int column = (index / 4 + direction + columns) % columns;
                    selectedTargetId = Targets[Math.Min(column * 4 + index % 4, Targets.Count - 1)].Id;
                }
                else
                {
                    selectedTargetId = Cycle(Targets, selectedTargetId, direction);
                }
                if (before != selectedTargetId)
                {
                    SoundRequested?.Invoke(MenuSound.Move);
                }
            }
            else if (input is MenuInput.Up or MenuInput.Down)
            {
                int count = SelectingAction ? ActionChoices.Count : Commands.Count;
                if (count > 0)
                {
                    if (SelectingAction)
                    {
                        actionIndex = (actionIndex + direction + count) % count;
                    }
                    else
                    {
                        commandIndex = (commandIndex + direction + count) % count;
                        SelectNativeLimit(false);
                    }
                    SoundRequested?.Invoke(MenuSound.Move);
                }
            }
            else if (SelectingAction && SelectedCommand == BattleAbility.Draw)
            {
                return;
            }
            else if (!SelectingAction && input is MenuInput.Left or MenuInput.Right)
            {
                SelectNativeLimit(input == MenuInput.Right && (nativeCommandHeldButtons & 0x5000) == 0);
            }
            else
            {
                string? before = SelectedActor?.Id;
                string? next = Cycle(session.ReadyPlayerCommandOrder, before, direction);
                if (next is not null && before != next)
                {
                    SelectParty(next);
                }
            }
        }
    }

    public void Attack()
    {
        RefreshNativeLimitSelection();
        RefreshGuardianFormSelection();
        if (RefreshDrawSelection())
        {
            return;
        }
        if (NativeTimedLimitActive || CommandMenuHidden)
        {
            return;
        }
        if (session.Outcome != BattleOutcome.Active || SelectedActor is not BattleCombatant actor)
        {
            Message = "Waiting for a character's ATB gauge.";
            return;
        }
        if (ConfirmGuardianFormCommand(actor))
        {
            return;
        }
        if (ConfirmDrawSelection())
        {
            return;
        }
        if (SelectedCommand != BattleAbility.Attack && !SelectingAction && !SelectingTarget)
        {
            selectedMagicTargets.Clear();
            magicCastCount = 1;
            selectedDrawSourceId = null;
            PrepareNativeLimitChoices();
            SelectingAction = true;
            if (SelectedCommand is BattleAbility.Magic or BattleAbility.Item or BattleAbility.GuardianForce or BattleAbility.LimitBreak)
            {
                session.OpenNativeActionMenu();
            }
            RestoreActionCursor();
            Message = ActionChoices.Count == 0 ? "No available stock or junctioned GF for this command." : SelectedCommand == BattleAbility.Magic ? "Choose magic. Page Left/Right: number of casts." : SelectedCommand == BattleAbility.GuardianForce ? "Choose a Guardian Force." : $"Choose {SelectedCommand}.";
            if (SelectedCommand == BattleAbility.LimitBreak && actor.NativeLimit?.Command == 16)
            {
                Message = "Choose magic. Page Left/Right: Do over.";
            }
            if (NativeLimitDirectTarget)
            {
                SelectingTarget = true;
                RestoreTargetCursor();
                Message = "Select a target.";
            }
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (!SelectingTarget)
        {
            if (SelectedAction is { Available: false } || SelectedCommand == BattleAbility.Magic && SelectedAction is { } magicAction && !session.QuoteMagic(actor.Id, magicAction.Id).Allowed)
            {
                ShowCommandError("The selected action is unavailable.");
                return;
            }
            if (SelectedCommand != BattleAbility.Attack && SelectedAction is null)
            {
                return;
            }
            SelectingTarget = true;
            BeginMagicTargetSelection(actor);
            RestoreTargetCursor();
            Message = "Select a target.";
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (SelectedTarget is not BattleCombatant target)
        {
            return;
        }
        if (!CollectDrawTarget(target) || !CollectMagicTarget(target))
        {
            return;
        }
        BattleSmartAttackPreview? smartPreview = SelectedCommand == BattleAbility.Attack ? ConfirmedAttackPreview() : null;
        string name = smartPreview?.Name ?? (SelectedCommand == BattleAbility.Attack ? "Attack" : SelectedAction!.Name);
        CursorSelection cursor = CaptureCursorSelection(actor);
        try
        {
            if (SelectedCommand == BattleAbility.Attack)
            {
                if (execute is not null)
                {
                    execute(new BattleInput(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { ForcePhysical = ForcePhysical, SmartAttackPreview = smartPreview });
                }
                else
                {
                    attack(actor.Id, target.Id);
                }
            }
            else
            {
                (execute ?? throw new NotSupportedException("Battle action dispatcher is unavailable."))(WithDrawTarget(WithMagicTargets(new BattleInput(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: SelectedCommand, ActionId: SelectedAction!.Id, DrawCast: DrawCast))));
            }
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            if (selectedMagicTargets.Count == magicCastCount)
            {
                selectedMagicTargets.RemoveAt(selectedMagicTargets.Count - 1);
            }
            ShowCommandError(error.Message);
            Console.Error.WriteLine(error);
            return;
        }
        RememberCursorSelection(cursor);
        SoundRequested?.Invoke(MenuSound.Confirm);
        SelectingTarget = false;
        SelectingAction = false;
        session.CloseNativeActionMenu();
        selectedMagicTargets.Clear();
        selectedDrawSourceId = null;
        Message = $"{actor.Name}: {name} → {target.Name}";
        selectedActorId = null;
        RestoreCommandCursor(SelectedActor);
        nativeLimitCommandSelected = false;
        nativeLimitSelectionActorId = null;
    }

    public void Choose(int index)
    {
        RefreshNativeLimitSelection();
        RefreshGuardianFormSelection();
        if (RefreshDrawSelection())
        {
            return;
        }
        if (CommandMenuHidden)
        {
            return;
        }
        if (SelectingGuardianFormAction)
        {
            Attack();
            return;
        }
        if (SelectingDrawMode)
        {
            DrawCast = index == 1 && SelectedAction?.Id < 64;
        }
        else if (SelectingAction)
        {
            actionIndex = index;
        }
        else
        {
            commandIndex = index;
        }
        Attack();
    }

    private string ChoiceLabel(BattleActionDefinition action)
    {
        string label = action.DisplayName ?? action.Name;
        return label;
    }

    public void SelectParty(string actorId)
    {
        if (!NativeTimedLimitActive && !CommandMenuHidden && Party.Any(actor => actor.Id == actorId && CanChooseCommand(actor)))
        {
            SelectingGuardianFormAction = false;
            guardianFormMenuActorId = null;
            selectedMagicTargets.Clear();
            magicCastCount = 1;
            SelectingAction = false;
            SelectingDrawMode = false;
            selectedDrawSourceId = null;
            selectedTargetId = null;
            session.CloseNativeActionMenu();
            SelectingTarget = false;
            if (SelectedActor?.Id != actorId)
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
            session.SelectPlayerCommandActor(actorId);
            selectedActorId = actorId;
            nativeLimitSelectionActorId = actorId;
            nativeLimitCommandSelected = false;
            RestoreCommandCursor(SelectedActor);
        }
    }

    public void CycleCommandActor()
    {
        RefreshNativeLimitSelection();
        if (SelectingGuardianFormAction || NativeTimedLimitActive || CommandMenuHidden || session.Outcome != BattleOutcome.Active)
        {
            return;
        }
        string? next = Cycle(session.ReadyPlayerCommandOrder, SelectedActor?.Id, 1);
        if (next is not null)
        {
            SelectParty(next);
            Message = "Choose a command.";
        }
    }

    public void SelectTarget(string targetId, bool confirm)
    {
        if (RefreshDrawSelection())
        {
            return;
        }
        if (CommandMenuHidden)
        {
            return;
        }
        if (Targets.Any(actor => actor.Id == targetId))
        {
            if (SelectingTargetGroup)
            {
                if (confirm && SelectingTarget)
                {
                    Attack();
                }
                return;
            }
            if (SelectedTarget?.Id != targetId)
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
            selectedTargetId = targetId;
            if (confirm && SelectingTarget)
            {
                Attack();
            }
        }
    }

    public void AdvanceTurn()
    {
        if (session.Mode == BattleAtbMode.TurnBased && session.Outcome == BattleOutcome.Active)
        {
            advanceTurn();
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
    }

    public void OpenDebug()
    {
        SoundRequested?.Invoke(MenuSound.Confirm);
        openDebug();
    }

    public void Complete()
    {
        if (session.Outcome != BattleOutcome.Active && (!session.CanReturnToField || !completionRequested))
        {
            completionRequested = session.CanReturnToField;
            try
            {
                complete?.Invoke();
            }
            catch
            {
                completionRequested = false;
                throw;
            }
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
    }

    private void CancelTarget()
    {
        if (CommandMenuHidden)
        {
            return;
        }
        if (CancelGuardianFormSelection())
        {
            return;
        }
        if (SelectingDrawCastRecipient)
        {
            SelectingTarget = false;
            SelectingDrawMode = true;
            Message = "Stock or Cast?";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (SelectingDrawMode)
        {
            SelectingDrawMode = false;
            Message = "Choose magic to draw.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (SelectedCommand == BattleAbility.Draw && SelectingAction)
        {
            selectedTargetId = selectedDrawSourceId;
            selectedDrawSourceId = null;
            SelectingAction = false;
            SelectingTarget = true;
            session.CloseNativeActionMenu();
            Message = "Choose a source to draw from.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (SelectingTarget && selectedMagicTargets.Count != 0)
        {
            selectedMagicTargets.RemoveAt(selectedMagicTargets.Count - 1);
            Message = $"Magic ×{magicCastCount}: select target {selectedMagicTargets.Count + 1}/{magicCastCount}.";
            SoundRequested?.Invoke(MenuSound.Cancel);
            return;
        }
        if (SelectingTarget)
        {
            if (NativeLimitDirectTarget)
            {
                SelectingTarget = false;
                SelectingAction = false;
                session.CloseNativeActionMenu();
                SelectNativeLimit((nativeCommandHeldButtons & 0x2000) != 0 && (nativeCommandHeldButtons & 0x5000) == 0);
                Message = "Choose a command.";
                SoundRequested?.Invoke(MenuSound.Cancel);
                return;
            }
            if (SelectingAction)
            {
                SelectingTarget = false;
                selectedMagicTargets.Clear();
                Message = $"Choose {SelectedCommand}.";
                SoundRequested?.Invoke(MenuSound.Cancel);
                return;
            }
            SoundRequested?.Invoke(MenuSound.Cancel);
        }
        SelectingTarget = false;
        SelectingAction = false;
        session.CloseNativeActionMenu();
        selectedMagicTargets.Clear();
        Message = "Choose a command when a character is ready.";
    }

    private static string? Cycle(IReadOnlyList<BattleCombatant> actors, string? selectedId, int direction)
    {
        if (actors.Count == 0)
        {
            return null;
        }
        int index = actors.Select(actor => actor.Id).ToList().IndexOf(selectedId ?? "");
        return actors[(Math.Max(0, index) + direction + actors.Count) % actors.Count].Id;
    }

    private bool CanChooseCommand(BattleCombatant actor)
    {
        return session.CanChoosePlayerCommand(actor.Id);
    }
}
