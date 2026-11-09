using Memoria.AutoPlay;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Shared autoplay lifecycle around a game-native FFVII policy and host-owned presentation.</summary>
public sealed class Ff7AutoPlayController(Ff7Session session, Func<Ff7AutoplayDriver, Ff7AutoplayInput> chooseInput, Action? present = null, Action<Ff7SessionSave>? checkpoint = null, TimeProvider? clock = null, Action? advanceTick = null)
{
    private readonly Ff7AutoplayDriver driver = new(session);
    private readonly List<string> recent = [];
    private readonly AutoPlayIdleDeadline deadline = new(clock ?? TimeProvider.System, TimeSpan.FromSeconds(10));
    private readonly Ff7AutoPlayTransitionGuard transitions = new();
    private string? previousProgress;
    private Ff7SessionSave lastSafe = session.Capture();

    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public string? Error { get; private set; }
    public Exception? Failure { get; private set; }
    public string Status => $"{session.Field.Name}, frame {session.Host.Process.Tick}, {Steps} steps";
    public long Steps { get; private set; }
    public IReadOnlyList<string> RecentActions => recent.ToArray();

    public void Start()
    {
        Error = null;
        Failure = null;
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
                present?.Invoke();
                transitions.Observe(session);
                ConfirmDefaultDialogueAndNames();
                Ff7SessionSave observed = session.Capture();
                string progress = $"{session.FieldGeneration}:{session.Progress.CompletedBattles.Length}:{session.Host.Process.Movie?.Frame}:{session.Host.Process.CameraScroll?.Step}:{session.Host.Process.CameraViewOffset?.Step}:"
                    + string.Join(";", observed.Vm.Entities.Select(entity => $"{entity.ProgramCounter},{entity.Priority},{entity.WaitCounter}"))
                    + string.Join(";", observed.Process.Actors.Select(actor => $"{actor.X4096},{actor.Y4096},{actor.Z4096},{actor.Triangle},{actor.Ladder?.Step},{actor.Jump?.Step}"))
                    + string.Join(";", observed.Process.Dialogues.Select(dialogue => $"{dialogue.RequestId},{dialogue.TokenPosition},{dialogue.WaitTicks},{dialogue.Completed}"));
                if (progress != previousProgress)
                {
                    previousProgress = progress;
                    deadline.Reset();
                }
                if (deadline.Expired)
                {
                    throw new TimeoutException("FFVII autoplay made no native scene, dialogue or battle progress within its idle deadline.");
                }
                Ff7AutoplayInput input = chooseInput(driver);
                AutoPlayRecording.AppendRecent(recent, AutoPlayRecording.LimitActionText($"{session.Field.Name}:{session.Host.Process.Tick} {input}"));
                long previousGeneration = session.FieldGeneration;
                lastSafe = observed;
                driver.Apply(input);
                if (advanceTick is null)
                {
                    session.AdvanceTick();
                }
                else
                {
                    advanceTick();
                }
                Steps++;
                if (session.FieldGeneration != previousGeneration)
                {
                    CaptureCheckpoint();
                }
            }
            catch (Exception error)
            {
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
        if (session.Host.Process.Menu is { Completed: false, Mode: 6 } menu)
        {
            session.CompleteNamingMenu(menu.RequestId, session.Progress.Characters.Single(character => character.Id == menu.Parameter).Name);
        }
        foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
        {
            Ff7Dialogue confirmed = Ff7DialogueCursor.Confirm(dialogue, FieldText.Read(session.Field.Scripts.Texts[dialogue.Text].Span));
            if (confirmed.Completed)
            {
                session.Host.CompleteDialogue(dialogue.RequestId, confirmed.Selected);
            }
            else
            {
                session.Host.UpdateDialogue(dialogue.RequestId, confirmed.Page, confirmed.Selected, confirmed.TokenPosition, confirmed.WaitTicks, confirmed.AwaitingInput);
            }
        }
    }
}
