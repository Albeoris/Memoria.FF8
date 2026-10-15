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
    public event Action<MenuSound>? SoundRequested;
    public bool SelectingTarget { get; private set; }
    public string Message { get; private set; } = "Choose Attack when a character is ready.";
    public float TextScale { get; set; } = 1;
    public bool ShowInitiative { get; set; }
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
                string? before = SelectedTarget?.Id;
                selectedTargetId = Cycle(Targets, selectedTargetId, direction);
                if (before != selectedTargetId)
                {
                    SoundRequested?.Invoke(MenuSound.Move);
                }
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
        if (!SelectingTarget)
        {
            SelectingTarget = true;
            Message = "Select an enemy. Confirm: Attack   Cancel: Return";
            SoundRequested?.Invoke(MenuSound.Confirm);
            return;
        }
        if (SelectedTarget is not BattleCombatant target)
        {
            return;
        }
        attack(actor.Id, target.Id);
        SoundRequested?.Invoke(MenuSound.Confirm);
        SelectingTarget = false;
        Message = $"{actor.Name}: Attack → {target.Name}";
        selectedActorId = null;
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
