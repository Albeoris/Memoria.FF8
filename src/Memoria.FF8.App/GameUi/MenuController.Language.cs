using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<GameLanguageInventory>? readLanguages;
    private Func<GameLanguageOffer, bool>? languageIsCurrent;
    private Func<bool>? canManageLanguages;
    private Action<GameLanguageOffer, bool>? applyLanguage;
    private Action<GameLanguageOffer>? removeLanguage;
    private Action? declineLanguageOffer;
    private Action? cancelLanguageChange;
    private string? offeredDisplayLanguage;
    private bool browsingDisplayLanguageOffer;
    private string? selectedLanguageOffer;
    private string? selectedLanguageReleaseVersion;
    private bool languageChangePending;
    private bool languageRemovalConfirmed;
    private readonly HashSet<string> expandedLanguageGroups = new(StringComparer.OrdinalIgnoreCase);

    internal void ConnectLanguages(Func<GameLanguageInventory> read, Func<GameLanguageOffer, bool> isCurrent, Func<bool> canManage, Action<GameLanguageOffer, bool> apply, Action<GameLanguageOffer> remove, Action decline, Action cancel)
    {
        readLanguages = read;
        languageIsCurrent = isCurrent;
        canManageLanguages = canManage;
        applyLanguage = apply;
        removeLanguage = remove;
        declineLanguageOffer = decline;
        cancelLanguageChange = cancel;
        ScreenOpened += RefreshLanguagesOnOpen;
        Register(new MenuScreenDefinition
        {
            Id = "language",
            Title = "Language",
            TitleText = c => c.LanguageText("title", "Language"),
            Help = "Expand a language to see its available editions.",
            HelpText = c => c.LanguageText("help", "Expand a language to see its available editions."),
            Entries = _ => LanguageEntries(),
            Details = _ => LanguageDetails()
        });
        Register(new MenuScreenDefinition
        {
            Id = "language-confirm",
            Title = "Use this language edition?",
            Help = "",
            TitleText = c => c.LanguageText(languageRemovalConfirmed ? "remove-confirm" : "confirm", languageRemovalConfirmed ? "Remove this language package?" : "Use this language edition?"),
            Entries = _ => LanguageConfirmationEntries(),
            Details = _ => LanguageDetails()
        });
        Register(new MenuScreenDefinition
        {
            Id = "language-offer",
            Title = "Use your display language?",
            Help = "",
            TitleText = c => c.LanguageText("offer", "Use your display language?"),
            Entries = _ => LanguageOfferEntries()
        });
    }

    internal bool IsLanguageScreen => ScreenId is "language" or "language-confirm" or "language-offer";

    private string LanguageText(string key, string fallback)
    {
        return SystemText(key == "title" ? "title.language" : "language." + key, fallback);
    }

    private void RefreshLanguagesOnOpen(string screen)
    {
        if (screen == "language" && modLibrary is { } library && !library.View.Catalog.IsRefreshing && (library.View.Catalog.IsStale || library.View.Catalog.FetchedAt is null))
        {
            library.RefreshCatalog();
        }
    }

    internal void OfferDisplayLanguage(GameLanguageGroup group)
    {
        offeredDisplayLanguage = group.DisplayTag;
        Open("language-offer");
        SelectEntry("language-offer", "keep");
    }

    private IReadOnlyList<MenuEntry> LanguageOfferEntries()
    {
        GameLanguageGroup? group = readLanguages!().Groups.FirstOrDefault(group => group.DisplayTag == offeredDisplayLanguage);
        string help = FormatSystemText("language.offer.help", "Compatible editions are available for {0}.", group?.Label ?? offeredDisplayLanguage ?? "");
        return
        [
            new("view", LanguageText("offer.view", "View editions"), help, Action: new("language-view-offer"), Enabled: group?.Offers.Any(offer => offer.CanSelect) == true),
            new("keep", LanguageText("offer.decline", "Keep current language"), help, Action: new("language-decline"))
        ];
    }

    private IReadOnlyList<MenuEntry> LanguageEntries()
    {
        List<MenuEntry> rows = [];
        foreach (GameLanguageGroup group in readLanguages!().Groups)
        {
            bool expanded = expandedLanguageGroups.Contains(group.DisplayTag);
            rows.Add(new("language-group-" + group.DisplayTag, group.Label, expanded ? LanguageText("collapse", "Hide editions") : LanguageText("expand", "Show editions"), Action: new("language-expand", group.DisplayTag)));
            if (expanded)
            {
                foreach (GameLanguageOffer offer in group.Offers)
                {
                    string status = LanguageOfferStatus(offer);
                    string title = LanguageOfferTitle(offer);
                    string key = LanguageOfferKey(offer);
                    rows.Add(new("language-provider-" + key, title, status, Action: new("language-choose", key)));
                }
            }
        }
        bool refreshing = modLibrary?.View.Catalog.IsRefreshing == true;
        rows.Add(new("language-refresh", refreshing ? LanguageText("refreshing", "Refreshing language catalog…") : LanguageText("refresh", "Refresh language catalog"), "", Action: new("language-refresh"), Enabled: !refreshing && modLibrary is not null));
        return rows;
    }

    private GameLanguageOffer? FindLanguageOffer(string? id)
    {
        return id is null ? null : readLanguages!().Groups.SelectMany(group => group.Offers).FirstOrDefault(offer => LanguageOfferKey(offer) == id);
    }

    private static string LanguageOfferKey(GameLanguageOffer offer) => $"{offer.DisplayTag}/{offer.Id}/{offer.Option}/{offer.Profile?.Edition}/{offer.Profile?.ResourceLanguage}";

    private (int Depth, bool? Expanded) LanguageTreeRow(MenuEntry entry)
    {
        return entry.Action?.Kind switch
        {
            "language-expand" => (0, expandedLanguageGroups.Contains(entry.Action.SubjectId)),
            "language-choose" => (1, null),
            _ => (0, null)
        };
    }

    private string LanguageOfferTitle(GameLanguageOffer offer)
    {
        return offer.IsOriginal ? LanguageText("original", "Original game") : offer.Title;
    }

    private string LanguageOfferStatus(GameLanguageOffer offer)
    {
        if (languageIsCurrent!(offer))
        {
            return LanguageText("selected", "Current choice");
        }
        return offer.Requirement ?? (offer.RequiresAcquisition ? LanguageText("download", "Download") : offer.IsInstalled ? LanguageText("installed", "Installed") : "");
    }

    private ModLibraryInstallation? LanguageReceipt(GameLanguageOffer offer)
    {
        return offer.Release is not { } release ? null : modLibrary?.View.LanguageInstallations.FirstOrDefault(receipt => receipt.SourceId == release.SourceId && receipt.Id == release.Id);
    }

    private bool CanRemoveLanguage(GameLanguageOffer offer)
    {
        return !languageIsCurrent!(offer) && offer.InstalledPackage?.Status != ModEffectiveStatus.Active && LanguageReceipt(offer) is { CanUninstall: true, Pending: false, PendingRemoval: false };
    }

    private IReadOnlyList<string> LanguageDetails()
    {
        GameLanguageOffer? offer = FindLanguageOffer(ScreenId == "language-confirm" ? selectedLanguageOffer : SelectedEntry?.Action?.Kind == "language-choose" ? SelectedEntry.Action.SubjectId : null);
        if (offer is null)
        {
            return [LanguageText("help", "Expand a language to see its available editions.")];
        }
        GameLanguageGroup group = readLanguages!().Groups.First(group => group.DisplayTag == offer.DisplayTag);
        List<string> details = [group.Label, LanguageOfferTitle(offer), LanguageOfferStatus(offer)];
        if (!string.IsNullOrWhiteSpace(offer.Source))
        {
            details.Add(string.IsNullOrWhiteSpace(offer.Version) ? offer.Source : $"{offer.Source} · {offer.Version}");
        }
        if (offer.Profile is { } profile)
        {
            string? nativeTag = LanguagePackages.DisplayTag(profile.ResourceLanguage);
            string nativeName = nativeTag is null ? profile.ResourceLanguage : System.Globalization.CultureInfo.GetCultureInfo(nativeTag).EnglishName;
            details.Add(FormatSystemText("language.base", "Native resources: {0} · {1}", profile.Edition, nativeName));
        }
        if (offer.Release is { } release)
        {
            bool showDownload = offer.RequiresAcquisition || offer.CanUpdate && (ScreenId != "language-confirm" || SelectedEntry?.Id == "update");
            if (showDownload)
            {
                if (offer.DownloadMembers.Count > 1)
                {
                    foreach (GameLanguageDownloadMember member in offer.DownloadMembers)
                    {
                        string status = member.IsInstalled ? LanguageText("installed", "Installed") : member.Bytes is long length ? FormatOfficialSize(length) : LanguageText("unknown-size", "Unknown");
                        details.Add($"{member.Name} · {member.Version} · {status}");
                    }
                }
                string size = offer.DownloadBytes is long bytes ? FormatOfficialSize(bytes) : offer.PublishedSize ?? LanguageText("unknown-size", "Unknown");
                if (size.Length == 0)
                {
                    size = LanguageText("unknown-size", "Unknown");
                }
                details.Add(FormatSystemText("language.download-size", "Download size: {0}", size));
            }
            if (modLibrary?.View.LanguageJobs.LastOrDefault(job => CatalogIdentity(job.Entry) == CatalogIdentity(release) && job.Phase is not ("Cancelled" or "Installed")) is { } job)
            {
                details.Add(job.Phase + " · " + DownloadProgress(job));
                if (job.Error is not null)
                {
                    details.Add(job.Error);
                }
            }
        }
        return details.Where(detail => detail.Length > 0).ToArray();
    }

    private IReadOnlyList<MenuEntry> LanguageConfirmationEntries()
    {
        GameLanguageOffer? offer = FindLanguageOffer(selectedLanguageOffer);
        string cancel = LanguageText("cancel", "Cancel");
        List<MenuEntry> entries = [new("cancel", cancel, "", Action: new("menu-back"))];
        if (offer is null || languageChangePending)
        {
            return entries;
        }
        string help = canManageLanguages!() ? LanguageOfferStatus(offer) : LanguageText("title-only", "Return to the title screen to apply a language edition.");
        if (languageRemovalConfirmed)
        {
            entries.Insert(0, new("remove", LanguageText("remove", "Remove installed package"), help, Action: new("language-remove", LanguageOfferKey(offer)), Enabled: canManageLanguages() && CanRemoveLanguage(offer)));
            return entries;
        }
        string apply = offer.RequiresAcquisition ? LanguageText("download-apply", "Download and apply") : LanguageText("apply", "Apply");
        string key = LanguageOfferKey(offer);
        bool unchangedRelease = offer.Release?.Version == selectedLanguageReleaseVersion;
        entries.Insert(0, new("apply", apply, help, Action: new("language-apply", key), Enabled: canManageLanguages() && offer.CanSelect && (!offer.RequiresAcquisition || unchangedRelease)));
        if (offer.CanUpdate)
        {
            entries.Insert(1, new("update", LanguageText("update", "Update and apply"), help, Action: new("language-update", key), Enabled: canManageLanguages() && offer.CanSelect && unchangedRelease));
        }
        if (CanRemoveLanguage(offer))
        {
            entries.Insert(entries.Count - 1, new("remove", LanguageText("remove", "Remove installed package"), help, Action: new("language-ask-remove", key), Enabled: canManageLanguages()));
        }
        return entries;
    }

    private bool ExecuteLanguageAction(MenuAction action)
    {
        if (!IsOpen || !IsLanguageScreen || !action.Kind.StartsWith("language-", StringComparison.Ordinal))
        {
            return false;
        }
        switch (action.Kind)
        {
            case "language-expand" when ScreenId == "language":
                if (!expandedLanguageGroups.Add(action.SubjectId))
                {
                    expandedLanguageGroups.Remove(action.SubjectId);
                }
                break;
            case "language-refresh" when ScreenId == "language":
                modLibrary?.RefreshCatalog();
                break;
            case "language-view-offer" when ScreenId == "language-offer" && offeredDisplayLanguage is { } tag:
                offeredDisplayLanguage = null;
                browsingDisplayLanguageOffer = true;
                expandedLanguageGroups.Add(tag);
                Open("language");
                SelectEntry("language", "language-group-" + tag);
                break;
            case "language-decline" when ScreenId == "language-offer":
                Back();
                break;
            case "language-choose" when ScreenId == "language" && FindLanguageOffer(action.SubjectId) is { } selected:
                selectedLanguageOffer = action.SubjectId;
                selectedLanguageReleaseVersion = selected.Release?.Version;
                languageRemovalConfirmed = false;
                Push("language-confirm");
                SelectEntry("language-confirm", "cancel");
                break;
            case "language-ask-remove" when ScreenId == "language-confirm" && action.SubjectId == selectedLanguageOffer:
                languageRemovalConfirmed = true;
                break;
            case "language-remove" when ScreenId == "language-confirm" && languageRemovalConfirmed && action.SubjectId == selectedLanguageOffer && canManageLanguages!() && FindLanguageOffer(action.SubjectId) is { } removable && CanRemoveLanguage(removable):
                removeLanguage!(removable);
                languageRemovalConfirmed = false;
                break;
            case "language-apply" or "language-update" when ScreenId == "language-confirm" && action.SubjectId == selectedLanguageOffer && !languageChangePending && canManageLanguages!() && FindLanguageOffer(action.SubjectId) is { CanSelect: true } offer
                && (action.Kind == "language-apply" && !offer.RequiresAcquisition || (action.Kind == "language-apply" || offer.CanUpdate) && offer.Release is { } release && release.Version == selectedLanguageReleaseVersion):
                languageChangePending = true;
                Message = LanguageText("applying", "Preparing language resources…");
                applyLanguage!(offer, action.Kind == "language-update");
                _selections[ScreenId] = 0;
                break;
        }
        return true;
    }

    private bool BackFromLanguage()
    {
        if (!IsOpen)
        {
            return false;
        }
        if (ScreenId == "language-offer" && offeredDisplayLanguage is not null)
        {
            declineLanguageOffer!();
            offeredDisplayLanguage = null;
        }
        if (ScreenId == "language" && browsingDisplayLanguageOffer)
        {
            browsingDisplayLanguageOffer = false;
            declineLanguageOffer!();
        }
        if (ScreenId == "language-confirm" && languageChangePending)
        {
            cancelLanguageChange!();
            Message = LanguageText("cancelling", "Cancelling…");
            _view = null;
            return true;
        }
        return false;
    }

    internal void FinishLanguageChange(string? error, bool cancelled = false)
    {
        languageChangePending = false;
        if (error is null && !cancelled && ScreenId == "language-confirm")
        {
            browsingDisplayLanguageOffer = false;
            Back();
        }
        Message = cancelled ? LanguageText("cancelled", "Language change cancelled.") : error ?? LanguageText("applied", "Language applied.");
        _view = null;
    }
}
