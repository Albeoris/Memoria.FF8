using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Live battle input and presentation. Commands always pass through the battle session.</summary>
public sealed class BattleHudController
{
    private readonly BattleSession session;
    private readonly Action openDebug;
    private readonly Action advanceTurn;
    private readonly Action<string, string> attack;
    private readonly Action? complete;
    private readonly UiView view;
    private readonly Action<BattleInput>? execute;
    private int commandIndex;
    private int actionIndex;
    private BattleEvent? lastNotification;
    public bool SelectingAction { get; private set; }
    public bool DrawCast { get; private set; }
    private string? selectedActorId;
    private string? selectedTargetId;

    public BattleHudController(BattleSession session, Action<string, string> attack, Action openDebug, Action advanceTurn, Action? complete = null, Action<BattleInput>? execute = null)
    {
        this.session = session;
        this.execute = execute;
        this.attack = attack;
        this.complete = complete;
        this.openDebug = openDebug;
        this.advanceTurn = advanceTurn;
        view = new UiView(new BattleHudElement(this)) { Cancel = CancelTarget };
        view.Resize(960, 672);
    }

    public BattleSession Session => session;
    public event Action<MenuSound>? SoundRequested;
    public bool SelectingTarget { get; private set; }
    public string Message { get; private set; } = "Choose a command when a character is ready.";
    public float TextScale { get; set; } = 1;
    public IReadOnlyList<BattleCombatant> Party => session.Combatants.Where(actor => actor.IsPlayer).ToArray();
    public IReadOnlyList<BattleAbility> Commands => SelectedActor?.Abilities ?? [];
    public BattleAbility SelectedCommand => Commands.Count == 0 ? BattleAbility.Attack : Commands[Math.Clamp(commandIndex, 0, Commands.Count - 1)];
    public IReadOnlyList<BattleActionDefinition> ActionChoices => SelectedActor is not { } actor ? [] : session.ActionDefinitions.Where(action => SelectedCommand switch
    {
        BattleAbility.Magic => action.Ability == BattleAbility.Magic && actor.MagicStocks.Any(stock => stock.Id == action.Id && stock.Count > 0),
        BattleAbility.Item => action.Ability == BattleAbility.Item && session.Items.Any(stock => stock.Id == action.Id && stock.Count > 0),
        BattleAbility.GuardianForce => action.Ability == BattleAbility.GuardianForce && actor.EquipmentIds.Contains(action.Id) && action.Available,
        BattleAbility.Draw => action.Ability == BattleAbility.Magic && session.Combatants.Any(enemy => enemy.IsAlive && !enemy.IsPlayer && enemy.DrawSpells.Any(spell => spell.Id == action.Id)),
        _ => false
    }).ToArray();
    public BattleActionDefinition? SelectedAction => ActionChoices.ElementAtOrDefault(Math.Clamp(actionIndex, 0, Math.Max(0, ActionChoices.Count - 1)));
    public IReadOnlyList<string> ChoiceLabels => SelectingAction ? ActionChoices.Select(ChoiceLabel).ToArray() : Commands.Select(command => command == BattleAbility.GuardianForce ? "GF" : command.ToString()).ToArray();
    public int ChoiceIndex => SelectingAction ? actionIndex : commandIndex;
    public int ChoiceStart => Math.Clamp(ChoiceIndex - 1, 0, Math.Max(0, ChoiceLabels.Count - 3));
    public IReadOnlyList<BattleCombatant> Targets => session.Combatants.Where(actor =>
    {
        bool allies = SelectedCommand != BattleAbility.Attack && SelectedCommand != BattleAbility.Draw && SelectedAction?.Allies == true;
        bool revive = SelectedCommand != BattleAbility.Draw && SelectedAction?.Effect == BattleActionEffect.Revive;
        return actor.IsPlayer == allies && (revive ? !actor.IsAlive : actor.IsAlive);
    }).ToArray();
    public IReadOnlyList<BattleCombatant> VisibleTargets
    {
        get
        {
            IReadOnlyList<BattleCombatant> targets = Targets;
            int selected = targets.Select(actor => actor.Id).ToList().IndexOf(SelectedTarget?.Id ?? "");
            int start = Math.Clamp(selected - 1, 0, Math.Max(0, targets.Count - 3));
            return targets.Skip(start).Take(3).ToArray();
        }
    }
    public BattleCombatant? SelectedActor
    {
        get
        {
            BattleCombatant? selected = Party.FirstOrDefault(actor => actor.Id == selectedActorId && actor.IsAlive && actor.IsReady && !actor.IsBusy);
            selected ??= Party.FirstOrDefault(actor => actor.IsAlive && actor.IsReady && !actor.IsBusy);
            selectedActorId = selected?.Id;
            return selected;
        }
    }

    public BattleCombatant? SelectedTarget
    {
        get
        {
            BattleCombatant? selected = Targets.FirstOrDefault(actor => actor.Id == selectedTargetId) ?? Targets.FirstOrDefault();
            selectedTargetId = selected?.Id;
            return selected;
        }
    }

    public UiView BuildView()
    {
        BattleEvent? notification = session.Events.LastOrDefault(item => item.Kind is "Draw" or "Scan");
        if (notification is not null && notification != lastNotification)
        {
            Message = notification.Message;
            lastNotification = notification;
        }
        if (SelectingTarget && SelectedActor is null)
        {
            SelectingTarget = false;
            SelectingAction = false;
        }
        return view;
    }

    public void Navigate(MenuInput input)
    {
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
            int direction = input is MenuInput.Up or MenuInput.Left or MenuInput.PageLeft ? -1 : 1;
            if (SelectingTarget)
            {
                string? before = SelectedTarget?.Id;
                selectedTargetId = Cycle(Targets, selectedTargetId, direction);
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
                    }
                    SoundRequested?.Invoke(MenuSound.Move);
                }
            }
            else if (SelectingAction && SelectedCommand == BattleAbility.Draw)
            {
                DrawCast = !DrawCast;
            }
            else
            {
                string? before = SelectedActor?.Id;
                selectedActorId = Cycle(Party.Where(actor => actor.IsReady && actor.IsAlive && !actor.IsBusy).ToArray(), selectedActorId, direction);
                if (before != selectedActorId)
                {
                    SoundRequested?.Invoke(MenuSound.Move);
                }
            }
        }
    }

    public void Attack()
    {
        if (session.Outcome != BattleOutcome.Active || SelectedActor is not BattleCombatant actor)
        {
            Message = "Waiting for a character's ATB gauge.";
            return;
        }
        if (SelectedCommand != BattleAbility.Attack && !SelectingAction && !SelectingTarget)
        {
            SelectingAction = true;
            actionIndex = 0;
            Message = ActionChoices.Count == 0 ? "No available stock or junctioned GF for this command." : SelectedCommand == BattleAbility.Draw ? "Choose magic. Left/Right switches Stock/Cast." : SelectedCommand == BattleAbility.GuardianForce ? "Choose a Guardian Force." : $"Choose {SelectedCommand}.";
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (!SelectingTarget)
        {
            if (SelectedCommand != BattleAbility.Attack && SelectedAction is null)
            {
                return;
            }
            SelectingTarget = true;
            Message = "Select a target. Confirm: Execute   Cancel: Return";
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (SelectedTarget is not BattleCombatant target)
        {
            return;
        }
        string name = SelectedCommand == BattleAbility.Attack ? "Attack" : SelectedAction!.Name;
        try
        {
            if (SelectedCommand == BattleAbility.Attack)
            {
                attack(actor.Id, target.Id);
            }
            else
            {
                (execute ?? throw new NotSupportedException("Battle action dispatcher is unavailable."))(new BattleInput(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: SelectedCommand, ActionId: SelectedAction!.Id, DrawCast: DrawCast));
            }
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            Message = error.Message;
            return;
        }
        SoundRequested?.Invoke(MenuSound.Confirm);
        SelectingTarget = false;
        SelectingAction = false;
        Message = $"{actor.Name}: {name} → {target.Name}";
        selectedActorId = null;
        commandIndex = 0;
    }

    public void Choose(int index)
    {
        if (SelectingAction)
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
        return SelectedCommand switch
        {
            BattleAbility.Draw => label + (DrawCast ? " · Cast" : " · Stock"),
            BattleAbility.Magic => label + "  " + SelectedActor!.MagicStocks.Single(stock => stock.Id == action.Id).Count,
            BattleAbility.Item => label + "  " + session.Items.Single(stock => stock.Id == action.Id).Count,
            _ => label
        };
    }
    public void SelectParty(string actorId)
    {
        if (Party.Any(actor => actor.Id == actorId && actor.IsReady && actor.IsAlive && !actor.IsBusy))
        {
            if (SelectedActor?.Id != actorId)
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
            selectedActorId = actorId;
        }
    }

    public void SelectTarget(string targetId, bool confirm)
    {
        if (Targets.Any(actor => actor.Id == targetId))
        {
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
        if (session.Outcome != BattleOutcome.Active)
        {
            SoundRequested?.Invoke(MenuSound.Confirm);
            complete?.Invoke();
        }
    }

    private void CancelTarget()
    {
        if (SelectingTarget)
        {
            SoundRequested?.Invoke(MenuSound.Cancel);
        }
        SelectingTarget = false;
        SelectingAction = false;
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
}
