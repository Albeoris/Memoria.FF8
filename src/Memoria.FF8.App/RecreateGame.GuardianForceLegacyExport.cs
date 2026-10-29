using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private async Task SaveOriginalPlayerSlotWithGuardianForceReviewAsync(int slot)
    {
        GameState snapshot = session.PrepareSnapshot();
        NativeCharacterVitals targetSource = await ReviewOriginalGuardianForceExportAsync(snapshot);
        playerSaves.Save(slot, session, ValidatePlayerState, targetSource, allowGuardianForceLoss: true);
    }

    private async Task<NativeCharacterVitals> ReviewOriginalGuardianForceExportAsync(GameState snapshot)
    {
        using GameAssets assets = new(options.GameDirectory, resources: resourceHost?.Resources);
        NativeCharacterVitals targetSource = assets.ReadNewGameCharacterVitals();
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(snapshot, targetSource);
        if (plan.RequiresConfirmation)
        {
            bool confirmed;
            try
            {
                confirmed = await GuardianForceLegacyExportReview.ConfirmAsync(Window.Handle, plan, LocalizeUiText);
            }
            finally
            {
                DrainGuardianEditorDialogInput();
            }
            if (!confirmed)
            {
                throw new OperationCanceledException("Original save export cancelled.");
            }
        }
        return targetSource;
    }
}

internal static class GuardianForceLegacyExportReview
{
    internal static string LossText(NativeGuardianForceLegacyLoss loss, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(localizeText, id, original);
        string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);
        object?[] arguments = loss.DescriptionArguments.Cast<object?>().ToArray();
        return loss.DescriptionId switch
        {
            "attribution" => SystemText("save.gf-export.loss.attribution", "GF mod-layer identities, save-local contributions and their attribution cannot be retained in an original FFVIII save."),
            "passive" => FormatSystemText("save.gf-export.loss.passive", "GF {0}: the disabled Move-Find preference cannot be retained in an original FFVIII save.", arguments),
            "configuration" => FormatSystemText("save.gf-export.loss.configuration", "GF {0}: authored learning order, entries or conditions revert to the target game's definitions.", arguments),
            "unknown" => FormatSystemText("save.gf-export.loss.unknown", "GF {0}: unmapped high forgotten bits cannot be retained after learning structure changes.", arguments),
            "progress-clamped" => FormatSystemText("save.gf-export.loss.progress-clamped", "GF {0}: ability {1} AP {2} is clamped to the target byte range.", arguments),
            "remap" => FormatSystemText("save.gf-export.loss.remap", "GF {0}: ability {1} AP/forgotten state maps from slot {2} to {3}.", arguments),
            "progress-missing" => FormatSystemText("save.gf-export.loss.progress-missing", "GF {0}: ability {1} has no target learning slot; AP {2} and forgotten state cannot be preserved.", arguments),
            "dormant" => FormatSystemText("save.gf-export.loss.dormant", "GF {0}: dormant ability {1} learning history cannot be retained by the target save.", arguments),
            "capacity" => FormatSystemText("save.gf-export.loss.capacity", "GF {0}: learned ability {1} exceeds the target's twenty-two effective entries and is dropped.", arguments),
            "availability" => FormatSystemText("save.gf-export.loss.availability", "GF {0}: ability {1} is unavailable under the target game's learning conditions or twenty-two-entry limit.", arguments),
            "learning" => FormatSystemText("save.gf-export.loss.learning", "GF {0}: learning selection {1} is invalid in the target and is cleared.", arguments),
            "equipped-command" => FormatSystemText("save.gf-export.loss.equipped-command", "Character {0}: unavailable command {1} is cleared.", arguments),
            "equipped-passive" => FormatSystemText("save.gf-export.loss.equipped-passive", "Character {0}: unavailable passive {1} is cleared.", arguments),
            "equipped-junction" => FormatSystemText("save.gf-export.loss.equipped-junction", "Character {0}: unavailable magic junction slot {1} is cleared.", arguments),
            _ => loss.Description
        };
    }

    public static Task<bool> ConfirmAsync(nint owner, NativeGuardianForceLegacyExportPlan plan, Func<string, string, string>? localizeText = null)
    {
        string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(localizeText, id, original);
        string explanation = SystemText("save.gf-export.explanation", "Original FFVIII saves use the target game's GF definitions. Review every change below. The active Memoria game keeps its full GF configuration and progression.");
        return ModelBrowserDialogs.ShowConfirmationAsync(new GameConfirmationRequest(explanation + "\n\n" + string.Join("\n\n", plan.Losses.Select(loss => LossText(loss, localizeText))), SystemText("save.gf-export.title", "Original FFVIII GF conversion"), SystemText("save.gf-export.cancel", "Cancel"), SystemText("save.gf-export.export", "Export this copy"), SystemText("model.dialog.scroll-review", "Up / Down: scroll review")));
    }
}
