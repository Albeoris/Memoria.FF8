using Memoria.AutoPlay;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Shared autoplay lifecycle around a game-native FFVII policy and host-owned presentation.</summary>
public sealed partial class Ff7AutoPlayController(Ff7Session session, Func<Ff7AutoplayDriver, Ff7AutoplayInput> chooseInput, Action? present = null, Action<Ff7SessionSave>? checkpoint = null, TimeProvider? clock = null, Action? advanceTick = null, Func<Ff7Dialogue, int?>? chooseDialogueSelection = null, Func<Ff7Dialogue, bool>? advanceDialoguePresentation = null, Func<bool>? hasReachedGoal = null, Action<bool>? advanceShop = null, Func<string?>? readPlanningProgress = null)
{
    private readonly Ff7AutoplayDriver driver = new(session);
    private readonly List<string> recent = [];
    private readonly AutoPlayIdleDeadline deadline = new(clock ?? TimeProvider.System, TimeSpan.FromSeconds(10));
    private readonly Ff7AutoPlayTransitionGuard transitions = new();
    private readonly Ff7AutoPlayProgressWatchdog progressWatchdog = new();
    private Ff7SessionSave lastSafe = session.Capture();

    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public bool Completed { get; private set; }
    public string? Error { get; private set; }
    public Exception? Failure { get; private set; }
    public string Status => $"{(Completed ? "Completed: " : "")}{(session.World is { } world ? $"World {world.Resources.World}, frame {world.Process.Tick}" : $"{session.Field.Name}, frame {session.Host.Process.Tick}")}, {Steps} steps";
    public long Steps { get; private set; }
    public IReadOnlyList<string> RecentActions => recent.ToArray();
    public Ff7AutoPlayPlanningProgress PlanningProgress => driver.PlanningProgress;

    public void Start()
    {
        Error = null;
        Failure = null;
        Completed = false;
        Running = true;
        Paused = false;
        deadline.Reset();
        transitions.Reset();
        progressWatchdog.Reset();
        CaptureCheckpoint();
    }

    public void Pause()
    {
        Paused = true;
        CaptureCheckpoint();
    }

    public void Resume()
    {
        if (Error is not null)
        {
            throw new InvalidOperationException("Restore the standalone checkpoint after correcting the native failure before resuming.");
        }
        Paused = false;
        deadline.Reset();
        progressWatchdog.Reset();
    }

    public void ResetIdleDeadline()
    {
        // Host pauses exclude elapsed wall time; they do not establish native progress.
        deadline.Reset();
    }

    public void SaveAndStop()
    {
        CaptureCheckpoint();
        Running = false;
    }

    public void Advance(int stepBudget = 256)
    {
        AutoPlayScheduler.Advance(stepBudget, () => Running && !Paused, () =>
        {
            try
            {
                if (UsesManualScene() || CompleteIfReached())
                {
                    return;
                }
                if (session.ChocoboRace is not null || session.Host.PendingMinigame?.Command is Memoria.FF7.Events.FieldMinigameCommand { Game: 1 })
                {
                    present?.Invoke();
                    lastSafe = session.Capture();
                    if (session.ChocoboRace is { } race)
                    {
                        string raceProgress = $"chocobo:{race.Phase}:{race.Countdown}:{race.FadeAlpha}:{race.DisplayedFadeAlpha}:{race.RecoveryCount}:" + string.Join(';', race.Racers.Select(racer => $"{racer.Path},{racer.Position},{racer.Stamina},{racer.Placement}"));
                        ObserveProgress(raceProgress);
                        session.SetChocoboInput(Ff7ChocoboAutoPlay.ChooseInput(race));
                    }
                    else
                    {
                        ObserveProgress("chocobo:pending");
                    }
                    long generation = session.FieldGeneration;
                    if (advanceTick is null)
                    {
                        session.AdvanceTick();
                    }
                    else
                    {
                        advanceTick();
                    }
                    Steps++;
                    if (session.FieldGeneration != generation)
                    {
                        CaptureCheckpoint();
                    }
                    return;
                }
                if (session.Shop is { Phase: not Ff7ShopPhase.Completed } shop)
                {
                    present?.Invoke();
                    lastSafe = session.Capture();
                    ObserveProgress($"shop:{shop.RequestId}:{shop.Phase}:{shop.Alpha}:{shop.DisplayedAlpha}:{shop.LastTransaction?.Transaction.Id}");
                    bool cancel = shop.Phase == Ff7ShopPhase.Interactive;
                    AutoPlayRecording.AppendRecent(recent, $"{session.Field.Name} shop {shop.ShopId}:{shop.Tick} {(cancel ? "cancel" : "native callback")}");
                    if (advanceShop is null)
                    {
                        session.AdvanceShop(cancel);
                        session.AdvanceClock(TimeSpan.FromSeconds(1d / 60));
                    }
                    else
                    {
                        advanceShop(cancel);
                    }
                    Steps++;
                    if (session.Shop?.Phase == Ff7ShopPhase.Completed)
                    {
                        CaptureCheckpoint();
                    }
                    return;
                }
                bool continuingTick = session.HasPendingTick;
                if (!continuingTick)
                {
                    present?.Invoke();
                    if (CompleteIfReached())
                    {
                        return;
                    }
                    ConfirmDefaultDialogueAndNames();
                }
                transitions.Observe(session);
                Ff7SessionSave observed = session.Capture();
                string progress = observed.World is { } world ? $"battles:{observed.Progress.CompletedBattles.Length}:{WorldProgress(world)}" : FieldProgress(observed);
                ObserveProgress(progress);
                driver.BeginPlanningStep();
                Ff7AutoplayInput input = continuingTick ? default : chooseInput(driver);
                if (driver.PlanningPending)
                {
                    return;
                }
                string action = continuingTick ? "resume native cycle" : input.ToString();
                AutoPlayRecording.AppendRecent(recent, AutoPlayRecording.LimitActionText($"{session.Field.Name}:{session.Host.Process.Tick} {action}"));
                long previousGeneration = session.FieldGeneration;
                lastSafe = observed;
                if (!continuingTick)
                {
                    driver.Apply(input);
                }
                if (advanceTick is null)
                {
                    session.AdvanceTick();
                }
                else
                {
                    advanceTick();
                }
                Steps++;
                if (UsesManualScene() || CompleteIfReached())
                {
                    return;
                }
                if (session.FieldGeneration != previousGeneration)
                {
                    CaptureCheckpoint();
                }
            }
            catch (Exception error)
            {
                Completed = false;
                Failure = error;
                Error = error.ToString();
                Running = false;
                try
                {
                    CaptureCheckpoint();
                }
                catch (Exception checkpointError)
                {
                    Error += Environment.NewLine + "Checkpoint writer: " + checkpointError;
                }
            }
        });
    }

    private bool UsesManualScene()
    {
        return session.Highway is not null || session.Host.Process.Menu is { Mode: 7, Completed: false };
    }

    private bool CompleteIfReached()
    {
        if (session.HasPendingTick || hasReachedGoal?.Invoke() != true)
        {
            return false;
        }
        SaveAndStop();
        Completed = Failure is null;
        return true;
    }

    private void CaptureCheckpoint()
    {
        using IDisposable suspended = deadline.Suspend();
        Ff7SessionSave snapshot;
        try
        {
            snapshot = session.Highway is null ? session.Capture() : lastSafe;
        }
        catch (Exception captureError)
        {
            snapshot = lastSafe;
            Failure ??= captureError;
            Error += Environment.NewLine + "Checkpoint capture; retained the last safe native boundary: " + captureError;
            Running = false;
        }
        if (snapshot.Battle is not null || snapshot.Process.GlobalCommand == Ff7GlobalCommand.Battle)
        {
            return;
        }
        checkpoint?.Invoke(snapshot);
    }

    private void ConfirmDefaultDialogueAndNames()
    {
        if (session.World is { } world)
        {
            foreach (Ff7Dialogue dialogue in world.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && world.CanConfirmDialogue(dialogue.RequestId)).ToArray())
            {
                Ff7Dialogue selected = chooseDialogueSelection?.Invoke(dialogue) is int selection ? Ff7DialogueCursor.Select(dialogue, selection) : dialogue;
                world.UpdateDialogue(selected);
                if (advanceDialoguePresentation?.Invoke(selected) == true)
                {
                    continue;
                }
                world.UpdateDialogue(Ff7DialogueCursor.Confirm(selected, FieldText.Read(world.Texts[dialogue.Text].Span)));
            }
            return;
        }
        if (session.Host.Process.Menu is { Completed: false, Mode: 6 } menu)
        {
            session.CompleteNamingMenu(menu.RequestId, session.Progress.Characters.Single(character => character.Id == menu.Parameter).Name);
        }
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            Ff7Dialogue selected = chooseDialogueSelection?.Invoke(dialogue) is int selection ? Ff7DialogueCursor.Select(dialogue, selection) : dialogue;
            session.Host.UpdateDialogue(selected);
            if (advanceDialoguePresentation?.Invoke(selected) == true)
            {
                continue;
            }
            Ff7Dialogue confirmed = Ff7DialogueCursor.Confirm(selected, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span));
            session.Host.UpdateDialogue(confirmed);
        }
    }
}
