using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IModService? modService;
    private IDisposable? modSubscription;

    internal void ConnectMods(IModService service)
    {
        modSubscription?.Dispose();
        modService = service;
        modSubscription = service.Subscribe(_ => _view = null);
        Register(new MenuScreenDefinition
        {
            Id = "mods",
            Title = "Mods",
            Help = "Enable texture packages. Changes apply to the current scene after validation.",
            MainCommand = true,
            Entries = controller => controller.ModEntries(),
            Details = controller => controller.ModDetails(),
            BuildContent = (controller, panel) => panel.Children.Add(controller.BuildModContent())
        });
    }

    internal void RefreshModView()
    {
        if (ScreenId == "mods")
        {
            _view = null;
        }
    }

    private IReadOnlyList<MenuEntry> ModEntries()
    {
        IModService service = modService!;
        List<MenuEntry> entries =
        [
            new("mod-auto", $"[{(service.AutoEnableNewMods ? "x" : " ")}] Automatically enable new mods", "Keep explicit disables; preserve existing mods when turning off.", Action: new MenuAction("mod-auto")),
            new("mod-refresh", "Refresh", "Rescan packages in both game and application Mods directories and retry rejected changes.", Action: new MenuAction("mod-refresh"))
        ];
        foreach (ModPackage package in service.Catalog.Packages)
        {
            ModEffectiveStatus effectiveStatus = package.Status != ModEffectiveStatus.IncompatibleEdition && service.Diagnostics.Any(diagnostic => diagnostic.PackageId?.Equals(package.Id, StringComparison.OrdinalIgnoreCase) == true && diagnostic.Code is "replacement-failed" or "reload-failed") ? ModEffectiveStatus.ReloadFailure : package.Status;
            string status = effectiveStatus switch
            {
                ModEffectiveStatus.Active => "Active",
                ModEffectiveStatus.Disabled => "Disabled",
                ModEffectiveStatus.IncompatibleEdition => "Incompatible edition",
                ModEffectiveStatus.NoUsableEntries => "No usable entries",
                _ => "Reload failed"
            };
            entries.Add(new MenuEntry(package.Id, $"[{(package.RequestedEnabled ? "x" : " ")}] {package.Name}   ·   {package.Format}   ·   {status}", status, Action: new MenuAction("mod-toggle", package.Id), Enabled: package.Status != ModEffectiveStatus.IncompatibleEdition));
        }
        return entries;
    }

    private IReadOnlyList<string> ModDetails()
    {
        IModService service = modService!;
        return [service.IsReloadPending ? "Reload pending" : $"{service.Catalog.Packages.Count} packages", "Replacement priority: Demaster, Tonberry, original", .. service.Diagnostics.Take(4).Select(diagnostic => diagnostic.Message)];
    }

    private UiElement BuildModContent()
    {
        UiGrid page = Grid([1.1f, 5.6f, 2.4f, 0.7f], [1]);
        Place(page, Header(), 0);
        Place(page, Panel(null, Commands(42, 18)), 1);
        Place(page, Panel(null, Details()), 2);
        Place(page, Footer(), 3);
        return page;
    }

    private bool ExecuteModAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("mod-", StringComparison.Ordinal))
        {
            return false;
        }
        IModService service = modService ?? throw new InvalidOperationException("The mod service is not connected.");
        ModOperationResult result;
        switch (action.Kind)
        {
            case "mod-auto":
                result = service.SetAutoEnableNewMods(!service.AutoEnableNewMods);
                break;
            case "mod-toggle":
                ModPackage package = service.Catalog.Packages.Single(package => package.Id == action.SubjectId);
                result = service.SetEnabled(package.Id, !package.RequestedEnabled);
                break;
            case "mod-refresh":
                service.Refresh();
                result = ModOperationResult.Completed;
                break;
            default:
                throw new NotSupportedException($"Unknown mod action {action.Kind}.");
        }
        Message = result.Success ? "Change queued; the published textures remain active until validation succeeds." : result.Error ?? "The change could not be saved.";
        _view = null;
        return true;
    }
}
