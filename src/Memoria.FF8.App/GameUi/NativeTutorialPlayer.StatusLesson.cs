using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private GameSession? statusLesson;

    private NativeLimitProgress? StatusLessonProgress => statusLesson?.LimitProgress;

    private void InitializeStatusLesson()
    {
        if (definition.Id is >= 5 and <= 7)
        {
            statusLesson = new GameSession(new GameState { LimitProgress = catalog.CreateIsolatedLimitProgress(definition.Id) });
        }
    }

    private void ApplyStatusLessonAction(MenuAction action)
    {
        if (statusLesson is null || definition.Id != 7 || action.TargetId != $"character-{definition.Descriptor.CharacterId}" || !action.SubjectId.StartsWith("limit-", StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan("limit-".Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            throw new InvalidDataException("Invalid Angelo training action for the isolated lesson.");
        }
        statusLesson.SelectAngeloLearning(index);
    }

    private void RestoreStatusLesson(NativeTutorialSnapshot saved)
    {
        if (statusLesson?.LimitProgress is not { } original)
        {
            return;
        }
        if (saved.StatusProgress is { } progress)
        {
            if (progress.InitOutSha256 != original.InitOutSha256 || progress.PetPalsFlags is null || progress.Options978 is null)
            {
                throw new InvalidDataException("Saved tutorial Limit progress changed its initialization provenance or required fields.");
            }
            statusLesson.SetLimitProgress(progress);
            return;
        }

        byte training = original.TrainingSelection;
        if (saved.Settings.TryGetValue("Angelo learning", out string? selection) && (!byte.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out training) || training >= 8))
        {
            throw new InvalidDataException("Saved tutorial Angelo training is outside its native range.");
        }
        int options = original.Options978!.Value;
        if (saved.Settings.GetValueOrDefault("Auto") == "On")
        {
            options |= 1;
        }
        if (saved.Settings.GetValueOrDefault("Duel-Auto") == "On")
        {
            options |= 2;
        }
        if (saved.Settings.GetValueOrDefault("Indicator") == "Off")
        {
            options |= 0x8000;
        }
        statusLesson.SetLimitProgress(original with { TrainingSelection = training, Options978 = (ushort)options });

        // Reprepare the cursor when restoring the same script position into the regular menu.
        inputPrepared = false;
        actionDelay = 0;
    }

    private MenuNavigationState AdaptStatusLessonNavigation(NativeTutorialSnapshot saved, MenuNavigationState navigation)
    {
        if (statusLesson is null || saved.StatusProgress is not null || navigation.History is null || navigation.Selections is null)
        {
            return navigation;
        }
        string screen = (definition.Id, pc) switch
        {
            (5, > 19 and <= 28) or (6, > 10 and <= 12) or (7, > 17 and <= 19) => "status-stats",
            (5, > 39 and <= 75 or > 146 and <= 148) or (6, > 14 and <= 32 or > 53 and <= 55) or (7, > 21 and <= 46 or > 132 and <= 134) => "status-limit",
            (5, > 75 and <= 146) => "status-renzokuken",
            (6, > 32 and <= 53) => "status-duel",
            (7, > 46 and <= 87 or > 105 and <= 132) => "status-angelo",
            (7, > 87 and <= 105) => "status-angelo-warning",
            _ => "status"
        };
        string[] history = screen switch
        {
            "status" => [],
            "status-stats" or "status-limit" => ["status"],
            "status-angelo-warning" => ["status", "status-limit", "status-angelo"],
            _ => ["status", "status-limit"]
        };
        Dictionary<string, int> selections = navigation.Selections.Where(pair => !pair.Key.StartsWith("status", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (screen == "status-angelo-warning" && pc <= 96)
        {
            selections[screen] = 1;
        }
        return navigation with { ScreenId = screen, History = history, Selections = selections, PendingAngeloMove = screen == "status-angelo-warning" ? "limit-0" : null };
    }

    private string? StatusLessonMessage() => (definition.Id, textId) switch
    {
        (5, 2) => Menu.SystemText("tutorial.status.current.stats", "Stats shows the character's attributes, weapon, commands and equipped abilities."),
        (5, 3) => Menu.SystemText("tutorial.status.current.back", "Go back to the Status menu to see the other categories."),
        (5, 4) => Menu.SystemText("tutorial.status.current.limit-break", "Limit Break lists the character's special techniques."),
        (5, 5) => Menu.FormatSystemText("tutorial.status.current.renzokuken", "Renzokuken is {0}'s Limit Break.", Menu.Character?.Name),
        (5, 6) => Menu.SystemText("tutorial.status.current.finishers", "The Renzokuken screen lists the finishing moves that can follow the attack."),
        (5, 7) => Menu.SystemText("tutorial.status.current.open-renzokuken", "Choose Renzokuken to inspect its finishing moves."),
        (5, 8) => Menu.SystemText("tutorial.status.current.finisher-list", "The moves are listed across the top. The highlighted move has its details below."),
        (5, 9) => Menu.SystemText("tutorial.status.current.finisher-move", "Move the cursor to read about another finisher."),
        (5, 10) => Menu.SystemText("tutorial.status.current.finisher-description", "The description explains what the selected finishing move does."),
        (5, 11) => Menu.SystemText("tutorial.status.current.finisher-availability", "The details also show whether the move is available."),
        (5, 12) => Menu.SystemText("tutorial.status.current.equipped-weapon", "Available finishers depend on the equipped gunblade."),
        (5, 13) => Menu.SystemText("tutorial.status.current.unavailable-finisher", "An unavailable finisher can still be inspected here."),
        (5, 14) => Menu.SystemText("tutorial.status.current.compare-finishers", "Move between the entries to compare their descriptions and availability."),
        (5, 15) => Menu.SystemText("tutorial.status.current.return-from-renzokuken", "Go back to return to the Limit Break overview."),
        (6, 0) => Menu.FormatSystemText("tutorial.duel.current.status", "Open Limit Break in Status to see {0}'s Duel moves.", Menu.Character?.Name),
        (6, 1) => Menu.FormatSystemText("tutorial.duel.current.move-list", "Duel lists the moves {0} can perform during the Limit Break.", Menu.Character?.Name),
        (6, 2) => Menu.SystemText("tutorial.duel.current.open", "Choose Duel to inspect those moves."),
        (6, 3) => Menu.SystemText("tutorial.duel.current.move-details", "Highlight a move to read its details."),
        (6, 4) => Menu.FormatSystemText("tutorial.duel.current.learned", "The details show whether {0} has learned the selected move.", Menu.Character?.Name),
        (7, 2) => Menu.FormatSystemText("tutorial.angelo.current.limit-break", "Open {0} from {1}'s Limit Break screen.", StatusLessonAngeloName(), Menu.Character?.Name),
        (7, 3) => Menu.FormatSystemText("tutorial.angelo.current.trick-list", "{0}'s screen shows learned tricks and tricks that can be trained.", StatusLessonAngeloName()),
        (7, 4) => Menu.FormatSystemText("tutorial.angelo.current.open", "Choose {0} to see the list.", StatusLessonAngeloName()),
        (7, 5) => Menu.SystemText("tutorial.angelo.current.progress", "Complete marks learned tricks. The percentage beside an unfinished trick shows its training progress."),
        (7, 8) => Menu.FormatSystemText("tutorial.angelo.current.walking", "The training marker shows the current trick. Walking with {0} in the party advances its progress.", Menu.Character?.Name),
        (7, 10) => Menu.SystemText("tutorial.angelo.current.warning", "Selecting a learned trick opens a confirmation to stop the current training."),
        (7, 11) => Menu.SystemText("tutorial.angelo.current.resume-training", "Choose Yes to stop training, then select an unfinished trick to start training again."),
        _ => null
    };

    private string StatusLessonAngeloName() => Menu.FormatNativeTutorialText("{Char Angelo2}", names);
}
