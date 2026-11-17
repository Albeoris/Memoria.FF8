using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7AutoPlayController? autoPlay;
    private Ff7AutoPlayPolicy? autoPlayPolicy;
    private string? autoPlayCheckpointPath;

    public string AutoPlayStatus => autoPlay is null ? "Ready" : $"{autoPlay.Status} · {autoPlayPolicy?.Status}";
    public bool AutoPlayRunning => autoPlay?.Running == true;
    public bool AutoPlayPaused => autoPlay?.Paused == true;
    public string? AutoPlayError => autoPlay?.Error;

    public void ConfigureAutoPlayCheckpoint(string path)
    {
        autoPlayCheckpointPath = Path.GetFullPath(path);
    }

    public void ApplyAutoPlayCommand(string command, bool continueAttempt = false)
    {
        switch (command.ToLowerInvariant())
        {
            case "status":
                break;
            case "start":
                if (Session is null || error is not null)
                {
                    throw new InvalidOperationException("Start or restore a valid FFVII session before automatic play.");
                }
                if (Session.Highway is not null)
                {
                    return;
                }
                if (autoPlay is null || !autoPlay.Running)
                {
                    autoPlayPolicy?.Dispose();
                    autoPlayPolicy = new(Session, SaveAutoPlayChoiceCheckpoint, state =>
                    {
                        if (AutoPlayRunning && !AutoPlayPaused)
                        {
                            SaveAutoPlayCheckpoint(state);
                        }
                    });
                    if (!continueAttempt)
                    {
                        autoPlayPolicy.State.FrontierProgressKey = null;
                        autoPlayPolicy.State.FrontierIdleSteps = 0;
                    }
                    autoPlay = new Ff7AutoPlayController(Session, autoPlayPolicy.ChooseInput, present: PrepareAutoPlayDialogue, checkpoint: SaveAutoPlayCheckpoint, advanceTick: AdvanceFieldTick, chooseDialogueSelection: ChooseAutoPlayDialogueSelection, advanceDialoguePresentation: AdvanceCommonDialogue, advanceShop: cancel => Session.AdvanceShop(cancel), readPlanningProgress: () => autoPlayPolicy!.WorkProgress);
                    autoPlay.Start();
                }
                else
                {
                    autoPlay.Resume();
                }
                break;
            case "pause":
                autoPlay?.Pause();
                break;
            case "stop":
                autoPlay?.SaveAndStop();
                break;
            default:
                throw new ArgumentException("Automatic play commands: start, pause, stop, status.", nameof(command));
        }
    }

    public void SetAutoPlayDestination(ushort? field)
    {
        (autoPlayPolicy ?? throw new InvalidOperationException("Start automatic play before selecting its planning destination.")).SetDestinationField(field);
    }

    private void SaveAutoPlayCheckpoint(Ff7SessionSave state)
    {
        string path = autoPlayCheckpointPath ?? throw new InvalidOperationException("Automatic play requires its host checkpoint destination.");
        AutoPlayCheckpoint.Save(path, () => envelope with { ActiveGame = SessionGame.Ff7, Ff7 = state with { AudioPresentation = CaptureAudioPresentation() } }, CombinedSessionCodec.Save);
    }

    private void AdvanceAutoPlay()
    {
        if (ApplyPendingAutoPlayRestore())
        {
            return;
        }
        if (TryApplyAutoPlayFieldRecovery(Session!))
        {
            autoPlay!.ResetIdleDeadline();
            SaveAutoPlayCheckpoint(Session!.Capture());
            return;
        }
        autoPlay!.Advance(1);
        if (autoPlay.Failure is Exception failure)
        {
            SetError(failure);
        }
    }

    private int? ChooseAutoPlayDialogueSelection(Ff7Dialogue dialogue)
    {
        return dialogue.ChoiceReady && CanSelectCommonDialogueChoice(dialogue) ? autoPlayPolicy!.ChooseDialogueSelection(dialogue) : null;
    }

    private string AutoPlayChoiceDirectory()
    {
        string path = autoPlayCheckpointPath ?? throw new InvalidOperationException("Automatic play requires its host checkpoint destination.");
        return Path.Combine(Path.GetDirectoryName(path)!, "choices");
    }

    private string SaveAutoPlayChoiceCheckpoint(string key, Ff7SessionSave state)
    {
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".ffsave";
        string path = Path.Combine(AutoPlayChoiceDirectory(), name);
        AutoPlayCheckpoint.Save(path, () => envelope with { ActiveGame = SessionGame.Ff7, Ff7 = state with { AutoPlay = null, AudioPresentation = CaptureAudioPresentation() } }, CombinedSessionCodec.Save);
        return path;
    }

    private bool ApplyPendingAutoPlayRestore()
    {
        if (autoPlayPolicy?.State.PendingRestore is not Ff7AutoPlayRestoreRequest request)
        {
            return false;
        }
        Ff7AutoPlayState state = autoPlayPolicy.State.Clone();
        state.PendingRestore = null;
        CombinedSessionSave saved;
        using (FileStream input = File.OpenRead(request.Path))
        {
            saved = CombinedSessionCodec.Read(input);
        }
        Ff7SessionSave native = saved.Ff7 ?? throw new InvalidDataException("The automatic choice checkpoint contains no native FFVII continuation.");
        Continue(saved with { Ff7 = native with { AutoPlay = state } });
        ApplyAutoPlayCommand("start", continueAttempt: true);
        string root = AutoPlayChoiceDirectory();
        foreach (string retired in request.RetiredPaths ?? [])
        {
            string path = Path.GetFullPath(retired);
            if (!CanRetireAutoPlayCheckpoint(root, path))
            {
                continue;
            }
            File.Delete(path);
        }
        return true;
    }

    internal static bool CanRetireAutoPlayCheckpoint(string directory, string checkpoint)
    {
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(checkpoint);
        string name = Path.GetFileNameWithoutExtension(path);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(root, comparison) && Path.GetExtension(path).Equals(".ffsave", comparison) && name.Length == 64 && name.All(char.IsAsciiHexDigit);
    }

    private void ResetAutoPlay()
    {
        autoPlayPolicy?.Dispose();
        autoPlayPolicy = null;
        autoPlay = null;
    }
}
