using Memoria.AutoPlay;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Shared autoplay lifecycle around a game-native FFVII policy and host-owned presentation.</summary>
public sealed class Ff7AutoPlayController(Ff7Session session, Func<Ff7AutoplayDriver, Ff7AutoplayInput> chooseInput, Action? present = null, Action<Ff7SessionSave>? checkpoint = null, TimeProvider? clock = null, Action? advanceTick = null, Func<Ff7Dialogue, int?>? chooseDialogueSelection = null, Func<Ff7Dialogue, bool>? advanceDialoguePresentation = null, Func<bool>? hasReachedGoal = null, Action<bool>? advanceShop = null)
{
    private readonly Ff7AutoplayDriver driver = new(session);
    private readonly List<string> recent = [];
    private readonly AutoPlayIdleDeadline deadline = new(clock ?? TimeProvider.System, TimeSpan.FromSeconds(10));
    private readonly Ff7AutoPlayTransitionGuard transitions = new();
    private string? previousProgress;
    private Ff7SessionSave lastSafe = session.Capture();

    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public bool Completed { get; private set; }
    public string? Error { get; private set; }
    public Exception? Failure { get; private set; }
    public string Status => $"{(Completed ? "Completed: " : "")}{(session.World is { } world ? $"World {world.Resources.World}, frame {world.Process.Tick}" : $"{session.Field.Name}, frame {session.Host.Process.Tick}")}, {Steps} steps";
    public long Steps { get; private set; }
    public IReadOnlyList<string> RecentActions => recent.ToArray();

    public void Start()
    {
        Error = null;
        Failure = null;
        Completed = false;
        Running = true;
        Paused = false;
        deadline.Reset();
        transitions.Reset();
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
    }

    public void ResetIdleDeadline()
    {
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
                if (CompleteIfReached())
                {
                    return;
                }
                if (session.Shop is { Phase: not Ff7ShopPhase.Completed } shop)
                {
                    present?.Invoke();
                    lastSafe = session.Capture();
                    string shopProgress = $"shop:{shop.RequestId}:{shop.Phase}:{shop.Tick}";
                    if (previousProgress != shopProgress)
                    {
                        previousProgress = shopProgress;
                        deadline.Reset();
                    }
                    if (deadline.Expired)
                    {
                        throw new TimeoutException("FFVII autoplay made no native shop progress within its idle deadline.");
                    }
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
                string progress = $"{session.FieldGeneration}:{session.Progress.CompletedBattles.Length}:{session.Host.Process.Movie?.Frame}:{session.Host.Process.CameraScroll?.Step}:{session.Host.Process.CameraViewOffset?.Step}:"
                    + string.Join(";", observed.Vm.Entities.Select(entity => $"{entity.ProgramCounter},{entity.Priority},{entity.WaitCounter}"))
                    + string.Join(";", observed.Process.Actors.Select(actor => $"{actor.X4096},{actor.Y4096},{actor.Z4096},{actor.Triangle},{actor.Ladder?.Step},{actor.Jump?.Step}"))
                    + string.Join(";", observed.Process.Dialogues.Select(dialogue => $"{dialogue.RequestId},{dialogue.TokenPosition},{dialogue.WaitTicks},{dialogue.Completed}"));
                if (observed.World is { } world)
                {
                    progress = $"world:{world.WorldId}:{world.MovementEnabled}:{world.MainState}:{world.FadeAlpha}:"
                        + string.Join(";", world.Entities.Select(entity => $"{entity.Model},{entity.X},{entity.Y},{entity.Z},{entity.TerrainResolved}"))
                        + string.Join(";", world.Dialogues.Select(dialogue => $"{dialogue.RequestId},{dialogue.TokenPosition},{dialogue.WaitTicks},{dialogue.Completed}"));
                }
                if (progress != previousProgress)
                {
                    previousProgress = progress;
                    deadline.Reset();
                }
                if (deadline.Expired)
                {
                    throw new TimeoutException("FFVII autoplay made no native scene, dialogue or battle progress within its idle deadline.");
                }
                Ff7AutoplayInput input = continuingTick ? default : chooseInput(driver);
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
                if (CompleteIfReached())
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
            snapshot = session.Capture();
        }
        catch (Exception captureError)
        {
            snapshot = lastSafe;
            Failure ??= captureError;
            Error += Environment.NewLine + "Checkpoint capture; retained the last safe native boundary: " + captureError;
            Running = false;
        }
        checkpoint?.Invoke(snapshot);
    }

    private void ConfirmDefaultDialogueAndNames()
    {
        if (session.World is { } world)
        {
            foreach (Ff7Dialogue dialogue in world.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && world.CanConfirmDialogue(dialogue.RequestId)).ToArray())
            {
                if (advanceDialoguePresentation?.Invoke(dialogue) == true)
                {
                    continue;
                }
                Ff7Dialogue selected = chooseDialogueSelection?.Invoke(dialogue) is int selection ? Ff7DialogueCursor.Select(dialogue, selection) : dialogue;
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
            if (advanceDialoguePresentation?.Invoke(dialogue) == true)
            {
                continue;
            }
            Ff7Dialogue selected = chooseDialogueSelection?.Invoke(dialogue) is int selection ? Ff7DialogueCursor.Select(dialogue, selection) : dialogue;
            Ff7Dialogue confirmed = Ff7DialogueCursor.Confirm(selected, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span));
            session.Host.UpdateDialogue(confirmed);
        }
    }
}
