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
    private string? selectedActorId;
    private string? selectedTargetId;

    public BattleHudController(BattleSession session, Action<string, string> attack, Action openDebug, Action advanceTurn, Action? complete = null)
    {
        this.session = session;
        this.attack = attack;
        this.complete = complete;
        this.openDebug = openDebug;
        this.advanceTurn = advanceTurn;
        view = new UiView(new BattleHudElement(this)) { Cancel = CancelTarget };
        view.Resize(960, 672);
    }

    public BattleSession Session => session;
    public bool SelectingTarget { get; private set; }
    public string Message { get; private set; } = "Choose Attack when a character is ready.";
    public float TextScale { get; set; } = 1;
    public bool ShowInitiative { get; set; } = true;
    public Action<BattleCombatant, IUiCanvas, UiRect>? HeadRenderer { get; set; }
    public IReadOnlyList<BattleCombatant> Party => session.Combatants.Where(actor => actor.IsPlayer).ToArray();
    public IReadOnlyList<BattleCombatant> Targets => session.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive).ToArray();
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
        if (SelectingTarget && SelectedActor is null)
        {
            SelectingTarget = false;
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
                selectedTargetId = Cycle(Targets, selectedTargetId, direction);
            }
            else
            {
                selectedActorId = Cycle(Party.Where(actor => actor.IsReady && actor.IsAlive && !actor.IsBusy).ToArray(), selectedActorId, direction);
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
        if (!SelectingTarget)
        {
            SelectingTarget = true;
            Message = "Select an enemy. Confirm: Attack   Cancel: Return";
            return;
        }
        if (SelectedTarget is not BattleCombatant target)
        {
            return;
        }
        attack(actor.Id, target.Id);
        SelectingTarget = false;
        Message = $"{actor.Name}: Attack → {target.Name}";
        selectedActorId = null;
    }

    public void SelectParty(string actorId)
    {
        if (Party.Any(actor => actor.Id == actorId && actor.IsReady && actor.IsAlive && !actor.IsBusy))
        {
            selectedActorId = actorId;
        }
    }

    public void SelectTarget(string targetId, bool confirm)
    {
        if (Targets.Any(actor => actor.Id == targetId))
        {
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
        }
    }

    public void OpenDebug()
    {
        openDebug();
    }

    public void Complete()
    {
        if (session.Outcome != BattleOutcome.Active)
        {
            complete?.Invoke();
        }
    }

    private void CancelTarget()
    {
        SelectingTarget = false;
        Message = "Choose Attack when a character is ready.";
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
