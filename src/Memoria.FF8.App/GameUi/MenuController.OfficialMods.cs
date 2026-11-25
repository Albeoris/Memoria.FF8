using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private IEnumerable<RemoteModEntry> OfficialEntries => OrdinaryCatalogEntries.Where(entry => entry.Official is not null);

    private bool ApplicableOfficialComponent(RemoteModEntry entry)
    {
        string[] profile = modLibrarySnapshot!.TargetProfile.Split('/');
        return profile.Length >= 2 && OfficialModCatalog.TargetMatches(entry.Official!.Target, profile[0], profile[1]);
    }

    private static bool MatchesOfficialComponent(ModPackage package, OfficialModComponent component)
    {
        return OfficialModCatalog.SameComponent(package.Official?.Id, component.Id) || OfficialModCatalog.SameComponent(package.Descriptor?.Id, component.Id)
            || package.IsBundled && (package.Id == component.RuntimePackageId || component.Id == OfficialModCatalog.GirlNextDoor.Id && package.Id == OfficialModCatalog.LegacyGirlNextDoorRuntimePackageId);
    }

    private NativeModDescriptor? InstalledOfficialDescriptor(OfficialModComponent component)
    {
        return OrdinaryInstalledMods.FirstOrDefault(package => MatchesOfficialComponent(package, component))?.Descriptor;
    }

    private RemoteModEntry PresentCatalogEntry(RemoteModEntry entry)
    {
        return entry.Official is { } component && InstalledOfficialDescriptor(component) is { } descriptor
            ? entry with { Name = descriptor.Name, Description = descriptor.DescriptionForLanguage(interfaceLanguage?.Invoke()) }
            : entry;
    }

    private string MemoriaAvailability(string status)
    {
        RemoteModEntry[] applicable = OfficialEntries.Where(ApplicableOfficialComponent).ToArray();
        if (applicable.Length == 0)
        {
            return status;
        }
        int local = OrdinaryInstalledMods.Count(package => package is { Format: ModFormat.Memoria } && (package.Descriptor is { } descriptor ? descriptor.Group.Equals(ModRuntime.MemoriaPackageId, StringComparison.OrdinalIgnoreCase) : package.IsBundled)
            && package.Status != ModEffectiveStatus.IncompatibleEdition && !applicable.Any(entry => MatchesOfficialComponent(package, entry.Official!)));
        int installed = local + applicable.Count(entry => ManagedMod(entry) is { Available: true, Pending: false }
            || OrdinaryInstalledMods.Any(package => MatchesOfficialComponent(package, entry.Official!) && package.Status != ModEffectiveStatus.IncompatibleEdition));
        int cached = applicable.Count(entry => ManagedMod(entry) is not { Available: true, Pending: false } && (EntryJob(entry) is { CanInstall: true } || ManagedMod(entry) is { Pending: true }));
        int total = local + applicable.Length;
        int available = installed + cached;
        List<string> badges = [];
        if (installed > 0)
        {
            badges.Add(status);
        }
        if (available == 0)
        {
            badges.Add(SystemText("mods.official.not-downloaded", "Not downloaded"));
        }
        else if (available < total)
        {
            badges.Add(FormatSystemText("mods.official.partial", "Partially downloaded · {0} of {1} components available locally", available, total));
        }
        else if (installed == 0)
        {
            badges.Add(SystemText("mods.official.downloaded", "Downloaded · Ready to install"));
        }
        if (cached > 0)
        {
            badges.Add(FormatSystemText("mods.official.local-counts", "{0} installed · {1} downloaded", installed, cached));
        }
        if (applicable.Any(CatalogUpdateAvailable))
        {
            badges.Add(SystemText("mods.official.update", "Update available"));
        }
        return badges.Count == 0 ? status : string.Join(" · ", badges);
    }

    private void AddOfficialSettingsRows(List<ModSettingsRow> rows)
    {
        foreach (RemoteModEntry entry in OfficialEntries)
        {
            OfficialModComponent component = entry.Official!;
            ModLibraryInstallation? installation = ManagedMod(entry);
            ModLibraryJob? job = EntryJob(entry);
            BehaviorModSetting? toggle = modSettings.FirstOrDefault(setting => setting.Behavior == "component" && (setting.Parameter == component.SettingsKey
                || OfficialModCatalog.SameComponent(setting.Descriptor?.Id, component.Id) || OfficialModCatalog.SameComponent(component.Id, OfficialModCatalog.GirlNextDoor.Id) && setting.Parameter == OfficialModCatalog.LegacyGirlNextDoorSettingsKey));
            NativeModDescriptor? installed = InstalledOfficialDescriptor(component);
            bool hasToggle = toggle is not null || installed is not null;
            if (hasToggle && installation is not { Pending: true, Available: true } && installation is not { Available: false } && installation is not { Version.Length: 0 } && !CatalogUpdateAvailable(entry))
            {
                continue;
            }
            string label = installed?.Name ?? (toggle is null ? component.Name : ModSettingLabel(toggle));
            string description = installed?.DescriptionForLanguage(interfaceLanguage?.Invoke()) ?? (toggle is null ? component.Description : ModSettingDescription(toggle));
            string action = "";
            string state = CatalogStatus(entry);
            if (job is { CanInstall: true } && installation is not { Pending: true })
            {
                action = "enable";
                state = SystemText("mods.official.install-enable", "Install & enable");
            }
            else if (job is { CanPause: true } or { CanResume: true } or { CanRetry: true } or { CanCancel: true })
            {
                action = "transfer";
                state += " · " + DownloadProgress(job);
            }
            else if (installation is { Pending: true })
            {
                if (installation.PendingEnable)
                {
                    action = "cancel-enable";
                    state += " · " + SystemText("mods.official.cancel-enable", "Cancel enable request");
                }
            }
            else if (CanDownload(entry))
            {
                action = hasToggle || installation is not null ? "download" : "enable";
                state = installation is { Available: false } or { Version.Length: 0 }
                    ? SystemText("mods.official.download-repair", "Download & check repair")
                    : action == "download" ? SystemText("mods.official.download-update", "Download update") : SystemText("mods.official.download-enable", "Download & enable");
                state += " · " + FormatOfficialSize(entry.Official!.Latest!.Artifact.Size);
            }
            rows.Add(new(new("official-" + component.Id, label + " · " + state, description + "\n" + (entry.CompatibilityError ?? CatalogStatus(entry)), Action: new("official-setting", CatalogIdentity(entry), Value: action), Enabled: action.Length > 0)));
            if (HasModPreview(entry))
            {
                rows.Add(new(new("official-preview-" + component.Id, "Preview changes · " + label, "Compare this component with the original game.", Action: new("official-setting", CatalogIdentity(entry), Value: "preview"))));
            }
        }
        foreach (RemoteModEntry entry in OfficialEntries.Where(entry => ManagedMod(entry) is { CanUninstall: true, Pending: false } or { PendingRemoval: true }))
        {
            bool pending = ManagedMod(entry)!.PendingRemoval;
            string state = pending ? "Uninstall pending | Waiting to apply" : "Uninstall when available";
            rows.Add(new(new("official-uninstall-" + entry.Official!.Id, entry.Name + " \u00b7 " + state, "Removes this managed download when its resource directory is available. Current game resources remain available until you exit.", Action: new("official-setting", CatalogIdentity(entry), Value: "uninstall"), Enabled: !pending)));
        }
        AddBatchRow(rows, false);
        AddBatchRow(rows, true);
    }

    private static string FormatOfficialSize(long bytes)
    {
        return $"{bytes / 1048576d:0.0} MiB";
    }

    private RemoteModEntry[] OfficialBatch(bool updates) => OfficialEntries.Where(entry => CanDownload(entry) && EntryJob(entry) is null
        && (updates ? ManagedMod(entry) is { Available: true, Pending: false } && CatalogUpdateAvailable(entry)
            : ManagedMod(entry) is null && !OrdinaryInstalledMods.Any(package => MatchesOfficialComponent(package, entry.Official!))))
        .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    private void AddBatchRow(List<ModSettingsRow> rows, bool updates)
    {
        RemoteModEntry[] entries = OfficialBatch(updates);
        if (entries.Length == 0)
        {
            return;
        }
        string action = updates ? "updates" : "missing";
        string label = updates ? SystemText("mods.official.update-installed", "Update installed") : SystemText("mods.official.download-missing", "Download missing");
        rows.Add(new(new("official-batch-" + action, label, string.Join(", ", entries.Select(entry => entry.Name)), Action: new("official-batch", action))));
    }

    private bool ExecuteOfficialSettingsAction(MenuAction action)
    {
        if (modLibrary is null)
        {
            return false;
        }
        if (action.Kind == "official-batch")
        {
            RemoteModEntry[] entries = OfficialBatch(action.SubjectId == "updates");
            if (entries.Length > 0)
            {
                string[] summary = entries.Select(entry => entry.Name + " · " + FormatOfficialSize(entry.Official!.Latest!.Artifact.Size)).ToArray();
                string title = FormatSystemText("mods.official.confirm", "Download {0} components · {1}", entries.Length, FormatOfficialSize(entries.Sum(entry => entry.Official!.Latest!.Artifact.Size)));
                Register(new MenuScreenDefinition
                {
                    Id = "official-download-confirm",
                    Title = title,
                    Help = string.Join("\n", summary),
                    Entries = _ => [new("official-confirm", SystemText("mods.official.download", "Download"), title, Action: new("official-confirm")), new("official-cancel", SystemText("mods.official.cancel", "Cancel"), "", Action: new("menu-back"))],
                    Details = _ => summary
                });
                officialDownloadSelection = entries;
                Push("official-download-confirm");
            }
            return true;
        }
        if (action.Kind == "official-confirm")
        {
            foreach (RemoteModEntry entry in officialDownloadSelection)
            {
                modLibrary.Queue(entry);
            }
            officialDownloadSelection = [];
            Back();
            return true;
        }
        if (action.Kind != "official-setting")
        {
            return false;
        }
        RemoteModEntry? selected = OfficialEntries.FirstOrDefault(entry => CatalogIdentity(entry) == action.SubjectId);
        if (action.Value == "preview" && selected is not null && HasModPreview(selected))
        {
            OpenModPreview(selected);
            return true;
        }
        if (selected is null)
        {
            return true;
        }
        if (action.Value == "enable" && CanDownload(selected))
        {
            modLibrary.DownloadAndEnable(selected);
        }
        else if (action.Value == "download" && CanDownload(selected))
        {
            modLibrary.Queue(selected);
        }
        else if (action.Value == "transfer" && EntryJob(selected) is { } job)
        {
            selectedDownloadId = job.Id;
            Push("mods-downloads");
        }
        else if (action.Value == "uninstall" && ManagedMod(selected) is { CanUninstall: true, Pending: false })
        {
            modLibrary.Uninstall(selected);
        }
        else if (action.Value == "cancel-enable")
        {
            modLibrary.CancelPendingEnable(selected);
        }
        return true;
    }

    private RemoteModEntry[] officialDownloadSelection = [];
}
