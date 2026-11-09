using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7AutoPlayController? autoPlay;
    private string? autoPlayCheckpointPath;

    public string AutoPlayStatus => autoPlay?.Status ?? "Ready";

    public void ConfigureAutoPlayCheckpoint(string path)
    {
        autoPlayCheckpointPath = Path.GetFullPath(path);
    }

    public void ApplyAutoPlayCommand(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "start":
                if (Session is null || error is not null)
                {
                    throw new InvalidOperationException("Start or restore a valid FFVII session before automatic play.");
                }
                if (autoPlay is null || !autoPlay.Running)
                {
                    Ff7OpeningAutoPlayPolicy policy = new(Session);
                    autoPlay = new Ff7AutoPlayController(Session, policy.ChooseInput, checkpoint: SaveAutoPlayCheckpoint, advanceTick: () => Session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, advanceDialoguePresentation: AdvanceCommonDialogue);
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
                throw new ArgumentException("Automatic play commands: start, pause, stop.", nameof(command));
        }
    }

    private void SaveAutoPlayCheckpoint(Ff7SessionSave state)
    {
        string path = autoPlayCheckpointPath ?? throw new InvalidOperationException("Automatic play requires its host checkpoint destination.");
        AutoPlayCheckpoint.Save(path, () => envelope with { ActiveGame = SessionGame.Ff7, Ff7 = state with { AudioPresentation = CaptureAudioPresentation() } }, CombinedSessionCodec.Save);
    }

    private void AdvanceAutoPlay()
    {
        autoPlay!.Advance(1);
        if (autoPlay.Failure is Exception failure)
        {
            SetError(failure);
        }
    }
}
