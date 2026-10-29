using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private int authoringDrawGroup;
    private IReadOnlyList<AuthoringHit>? authoringCandidates;
    private readonly List<(UiRect Bounds, AuthoringHit Hit)> authoringCandidateButtons = [];
    private int authoringCandidateIndex;
    private float authoringCandidateX;
    private float authoringCandidateY;

    private void RegisterAuthoringHits(IReadOnlyList<LocalizationTextHit> hits)
    {
        if (!localizationWorkbench.Enabled || gameMenus?.IsOpen == true && gameMenus.ScreenId.StartsWith("localization-", StringComparison.Ordinal))
        {
            return;
        }
        int group = ++authoringDrawGroup;
        foreach (LocalizationTextHit hit in hits)
        {
            if (hit.Binding.Key.Length != 0 && !hit.Bounds.IsEmpty)
            {
                authoringHits.Add(new(hit.Binding, hit.Bounds) { DrawGroup = group, Terms = authoringTermSnapshot, NativeContext = AuthoringTextContext(hit.Binding), FontSize = hit.Size, FontFamily = hit.FontFamily, MaximumWidth = hit.MaximumWidth });
            }
        }
    }

    private bool PickAuthoringText(float x, float y)
    {
        AuthoringHit[] underPointer = authoringHits.Where(hit => hit.Bounds.Contains(x, y)).ToArray();
        if (underPointer.Length == 0)
        {
            return false;
        }
        int topmost = underPointer.Max(hit => hit.DrawGroup);
        AuthoringHit[] candidates = underPointer.Where(hit => hit.DrawGroup == topmost).DistinctBy(hit => (hit.Binding.Key, hit.Binding.Operation)).ToArray();
        if (candidates.Length == 1)
        {
            OpenAuthoringBinding(candidates[0]);
        }
        else
        {
            authoringCandidates = candidates;
            authoringCandidateIndex = 0;
            authoringCandidateX = x;
            authoringCandidateY = y;
            SynchronizeUiMoviePause();
        }
        return true;
    }

    private void HandleAuthoringCandidateCommand(UiEditCommand command)
    {
        if (authoringCandidates is not { } candidates)
        {
            return;
        }
        if (command == UiEditCommand.Cancel)
        {
            authoringCandidates = null;
            drainUiButtons = true;
            SynchronizeUiMoviePause();
        }
        else if (command == UiEditCommand.Up)
        {
            authoringCandidateIndex = Math.Max(0, authoringCandidateIndex - 1);
        }
        else if (command == UiEditCommand.Down)
        {
            authoringCandidateIndex = Math.Min(candidates.Count - 1, authoringCandidateIndex + 1);
        }
        else if (command == UiEditCommand.Confirm)
        {
            AuthoringHit hit = candidates[authoringCandidateIndex];
            authoringCandidates = null;
            OpenAuthoringBinding(hit);
        }
    }

    private bool PointerAuthoringCandidates(float x, float y, bool clicked)
    {
        if (authoringCandidates is null)
        {
            return false;
        }
        if (clicked)
        {
            foreach (var candidate in authoringCandidateButtons)
            {
                if (candidate.Bounds.Contains(x, y))
                {
                    authoringCandidates = null;
                    OpenAuthoringBinding(candidate.Hit);
                    break;
                }
            }
        }
        return true;
    }

    private void DrawAuthoringCandidates(UiRect viewport)
    {
        authoringCandidateButtons.Clear();
        if (authoringCandidates is not { } candidates || uiCanvas is null)
        {
            return;
        }
        float width = Math.Min(680, Math.Max(0, viewport.Width - 24));
        float height = Math.Min(candidates.Count * 32 + 36, Math.Max(0, viewport.Height - 24));
        float x = Math.Clamp(authoringCandidateX, viewport.X + 12, Math.Max(viewport.X + 12, viewport.Right - width - 12));
        float y = Math.Clamp(authoringCandidateY, viewport.Y + 12, Math.Max(viewport.Y + 12, viewport.Bottom - height - 12));
        uiCanvas.Fill(new(x, y, width, height), new UiColor(20, 28, 45, 245));
        uiCanvas.Text(AuthoringHostText("picking.choose", "Choose a text fragment — Escape cancels"), 17, new(x + 8, y + 4, width - 16, 28), new UiColor(255, 220, 235));
        for (int index = 0; index < candidates.Count && 36 + index * 32 < height; index++)
        {
            UiRect row = new(x + 6, y + 32 + index * 32, width - 12, 30);
            if (index == authoringCandidateIndex)
            {
                uiCanvas.Fill(row, new UiColor(65, 82, 112, 230));
            }
            uiCanvas.Text(candidates[index].Binding.Key, 16, row.Inset(4), new UiColor(240, 240, 245));
            authoringCandidateButtons.Add((row, candidates[index]));
        }
    }

    private bool IsEditingAuthoringOccurrence(Events.DialogueState dialogue)
    {
        if (!localizationWorkbench.IsEditing || editingAuthoringHit is not { Detached: false, Dialogue: { } selected })
        {
            return false;
        }
        return dialogue.Channel == selected.Channel && dialogue.MessageId == selected.MessageId && dialogue.Operation == selected.Operation && dialogue.ObjectId == selected.ObjectId && dialogue.ScriptId == selected.ScriptId;
    }
}
