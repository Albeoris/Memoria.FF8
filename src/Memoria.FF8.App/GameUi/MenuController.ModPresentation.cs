using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string ModStateText(ModPackageView view, bool disabled = false) => view.CommittedStatus == ModEffectiveStatus.Active ? SystemText("mods.state.active", "Active") : disabled || !view.Package.RequestedEnabled ? SystemText("mods.state.disabled", "Disabled") : SystemText("mods.state.not-active", "Not active");

    private string ModStatusText(ModPackageView view)
    {
        string state = view.CommittedStatus == ModEffectiveStatus.Active ? SystemText("mods.state.active", "Active") : SystemText("mods.state.not-active", "Not active");
        return view.PresentationKind switch
        {
            ModPackagePresentationKind.Basic => ModStateText(view),
            ModPackagePresentationKind.Incompatible => SystemText("mods.state.incompatible", "Incompatible edition"),
            ModPackagePresentationKind.NoUsableEntries => view.CommittedStatus == ModEffectiveStatus.Active ? SystemText("mods.state.active-no-update", "Active · no usable update") : SystemText("mods.state.no-entries", "No usable entries"),
            ModPackagePresentationKind.LoadFailed => FormatSystemText("mods.state.load-failed", "{0} · load failed", state),
            ModPackagePresentationKind.SettingNotSaved => FormatSystemText("mods.state.setting-not-saved", "{0} · setting not saved", ModStateText(view, true)),
            ModPackagePresentationKind.ReviewRequired => FormatSystemText("mods.state.review-required", "{0} · review required", state),
            ModPackagePresentationKind.ChangesBlocked => FormatSystemText("mods.state.blocked", "{0} · changes blocked", state),
            ModPackagePresentationKind.DisableFailed => FormatSystemText("mods.state.disable-failed", "{0} · disable failed", state),
            ModPackagePresentationKind.UpdateFailed => FormatSystemText("mods.state.update-failed", "{0} · update failed", state),
            ModPackagePresentationKind.EnableFailed => FormatSystemText("mods.state.enable-failed", "{0} · enable failed", state),
            ModPackagePresentationKind.ActiveUpdating => SystemText("mods.state.updating", "Active · updating…"),
            ModPackagePresentationKind.ActiveDisabling => SystemText("mods.state.disabling", "Active · disabling…"),
            ModPackagePresentationKind.Enabling => SystemText("mods.state.enabling", "Enabling…"),
            _ => view.StatusText
        };
    }

    private string? ModProblemText(ModPackageView view) => view.ProblemKind switch
    {
        ModPackageProblemKind.Incompatible => SystemText("mods.problem.incompatible", "This format is incompatible with the selected game-data edition."),
        ModPackageProblemKind.NoUsableEntries => SystemText("mods.problem.no-entries", "This package has no usable entries for the current configuration."),
        ModPackageProblemKind.LoadFailed => SystemText("mods.problem.load-failed", "This package could not be loaded."),
        ModPackageProblemKind.ReviewRequired => SystemText("mods.problem.review-required", "Saved changes require GF review before activation."),
        ModPackageProblemKind.ChangesBlocked => SystemText("mods.problem.blocked", "Saved changes cannot be committed at the current gameplay boundary."),
        ModPackageProblemKind.ApplyFailed => SystemText("mods.problem.apply-failed", "Saved changes could not be applied. The previous runtime configuration remains in use."),
        ModPackageProblemKind.Pending => SystemText("mods.problem.pending", "The saved request is waiting for runtime publication."),
        _ => view.Problem
    };

    private string[] ModFilterLabels() => [SystemText("mods.filter.all", "All mods"), SystemText("mods.filter.enabled", "Enabled in settings"), SystemText("mods.filter.disabled", "Disabled in settings"), SystemText("mods.filter.attention", "Needs attention")];

    private string[] ModFormatLabels() => [SystemText("mods.format.all", "All formats"), "Tonberry", "Demaster", "Junction", "Memoria"];
}
