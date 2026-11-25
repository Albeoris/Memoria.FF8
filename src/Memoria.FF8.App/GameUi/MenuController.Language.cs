using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<GameLanguageInventory>? readLanguages;
    private Func<GameLanguageOffer, bool>? languageIsCurrent;
    private Func<bool>? canManageLanguages;
    private Action<GameLanguageOffer, bool, IProgress<LanguageAcquisitionProgress>>? applyLanguage;
    private Action<GameLanguageOffer>? removeLanguage;
    private Action? declineLanguageOffer;
    private Action? cancelLanguageChange;
    private string? offeredDisplayLanguage;
    private bool browsingDisplayLanguageOffer;
    private string? selectedLanguageOffer;
    private string? selectedLanguageReleaseVersion;
    private LanguageProgress? languageChangeProgress;
    private LanguageAcquisitionProgress? languageTransfer;
    private string? changingLanguageEdition;
    private string? languageFailure;
    private bool languageChangeApplying;
    private bool languageChangeCancelling;
    private bool languageRemovalConfirmed;
    private readonly HashSet<string> expandedLanguageGroups = new(StringComparer.OrdinalIgnoreCase);

    internal void ConnectLanguages(Func<GameLanguageInventory> read, Func<GameLanguageOffer, bool> isCurrent, Func<bool> canManage, Action<GameLanguageOffer, bool, IProgress<LanguageAcquisitionProgress>> apply, Action<GameLanguageOffer> remove, Action decline, Action cancel)
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
            TitleText = c => LanguageConfirmationTitle(),
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
    internal bool LanguageChangeFromConfig => ScreenId == "language-confirm" && _history.Count == 2 && _history.Last() == "config";
    private bool LanguageChangeInProgress => ScreenId == "language-confirm" && languageChangeProgress is not null;

    private string LanguageConfirmationTitle()
    {
        if (languageRemovalConfirmed)
        {
            return LanguageText("remove-confirm", "Remove this language package?");
        }
        return FindLanguageOffer(selectedLanguageOffer) is { } offer && languageIsCurrent!(offer)
            ? LanguageText("update-confirm", "Update this language edition?")
            : LanguageText("confirm", "Use this language edition?");
    }

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

    private HashSet<string> CurrentLanguageRows()
    {
        HashSet<string> rows = [];
        foreach (GameLanguageGroup group in readLanguages!().Groups)
        {
            foreach (GameLanguageOffer offer in group.Offers.Where(languageIsCurrent!))
            {
                rows.Add("language-group-" + group.DisplayTag);
                rows.Add("language-provider-" + LanguageOfferKey(offer));
            }
        }
        return rows;
    }

    private string LanguageOfferTitle(GameLanguageOffer offer)
    {
        return offer.Title switch
        {
            _ when offer.IsOriginal => LanguageText("original", "Original game"),
            "ILP retranslation" => LanguageText("provider-ilp-retranslation", "ILP retranslation"),
            "Succession (story rewrite)" => LanguageText("provider-succession", "Succession (story rewrite)"),
            _ => offer.Title
        };
    }

    private string LanguageOfferStatus(GameLanguageOffer offer)
    {
        if (languageIsCurrent!(offer))
        {
            string current = LanguageText("selected", "Current choice");
            return offer.CanUpdate ? $"{current} · {LanguageText("update-available", "Update available")}" : current;
        }
        return offer.CompatibilityRequirement is { } requirement
            ? LanguageCompatibilityStatus(requirement)
            : offer.Requirement ?? (offer.RequiresAcquisition ? LanguageText("download", "Download") : offer.IsInstalled ? LanguageText("installed", "Installed") : "");
    }

    private string LanguageCompatibilityStatus(GameLanguageRequirement requirement)
    {
        switch (requirement.Kind)
        {
            case GameLanguageRequirementKind.NativeBase:
                string[] bases = requirement.NativeVariants!.Select(variant => $"{LanguageEditionName(variant.Edition)} {NativeLanguageName(variant.Language)}").ToArray();
                string alternatives = bases.Aggregate((first, next) => FormatSystemText("language.native-base-or", "{0} or {1}", first, next));
                return FormatSystemText("language.requires-native-base", "Requires {0} game data.", alternatives);
            case GameLanguageRequirementKind.EngineVersion:
                return FormatSystemText("language.requires-engine-version", "Requires Memoria {0} or later.", requirement.MinimumEngineVersion);
            case GameLanguageRequirementKind.UnsupportedBase:
                return LanguageText("unsupported-base", "This provider has no supported native base.");
            case GameLanguageRequirementKind.UnknownEngineVersion:
                return LanguageText("unknown-engine-version", "The installed Memoria engine version is unknown.");
            case GameLanguageRequirementKind.UnreviewedMapping:
                return LanguageText("unreviewed-mapping", "The download has no verified mapping to a package language option.");
            case GameLanguageRequirementKind.JunctionBase:
                return LanguageText("junction-base", "Junction packages currently require Classic English game data.");
            case GameLanguageRequirementKind.MissingOption:
                return LanguageText("missing-option", "The installed release does not declare this language option.");
            default:
                return LanguageText("missing-dependencies", "Required language packages are missing or incompatible.");
        }
    }

    private static string NativeLanguageName(string language)
    {
        string? tag = LanguagePackages.DisplayTag(language);
        return tag is null ? language : System.Globalization.CultureInfo.GetCultureInfo(tag).EnglishName;
    }

    private string LanguageMemberName(string name, string? officialId)
    {
        return officialId is null ? name : LanguageText("package-name." + officialId, name);
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
            string source = offer.Source switch
            {
                "Installed game" => LanguageText("source-installed-game", "Installed game"),
                "Installed mod" => LanguageText("source-installed-mod", "Installed mod"),
                _ => offer.Source
            };
            details.Add(string.IsNullOrWhiteSpace(offer.Version) ? source : $"{source} · {offer.Version}");
        }
        if (offer.Profile is { } profile)
        {
            details.Add(FormatSystemText("language.base", "Native resources: {0} · {1}", LanguageEditionName(profile.Edition), NativeLanguageName(profile.ResourceLanguage)));
        }
        if (offer.Release is not null)
        {
            bool showDownload = offer.RequiresAcquisition || offer.CanUpdate && (ScreenId != "language-confirm" || SelectedEntry?.Id == "update");
            if (showDownload)
            {
                if (offer.DownloadMembers.Count > 1)
                {
                    foreach (GameLanguageDownloadMember member in offer.DownloadMembers)
                    {
                        string status = member.IsInstalled ? LanguageText("installed", "Installed") : member.Bytes is long length ? FormatOfficialSize(length) : LanguageText("unknown-size", "Unknown");
                        details.Add($"{LanguageMemberName(member.Name, member.OfficialId)} · {member.Version} · {status}");
                    }
                }
                string size = offer.DownloadBytes is long bytes ? FormatOfficialSize(bytes) : offer.PublishedSize ?? LanguageText("unknown-size", "Unknown");
                if (size.Length == 0)
                {
                    size = LanguageText("unknown-size", "Unknown");
                }
                details.Add(FormatSystemText("language.download-size", "Download size: {0}", size));
            }
        }
        return details.Where(detail => detail.Length > 0).ToArray();
    }

    private string LanguageEditionName(string edition) => edition switch
    {
        "Classic" => LanguageText("edition-classic", "Classic"),
        "Remastered" => LanguageText("edition-remastered", "Remastered"),
        _ => edition
    };

    private IReadOnlyList<MenuEntry> LanguageConfirmationEntries()
    {
        string cancel = LanguageText("cancel", "Cancel");
        if (LanguageChangeInProgress)
        {
            string cancelHelp = languageChangeCancelling ? LanguageChangeStatus() : LanguageText("cancel-progress", "Cancel the language change.");
            return [new("cancel", cancel, cancelHelp, Action: new("menu-back"), Enabled: !languageChangeCancelling)];
        }
        GameLanguageOffer? offer = FindLanguageOffer(selectedLanguageOffer);
        List<MenuEntry> entries = [new("cancel", cancel, "", Action: new("menu-back"))];
        if (offer is null)
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
        if (!languageIsCurrent!(offer))
        {
            entries.Insert(entries.Count - 1, new("apply", apply, help, Action: new("language-apply", key), Enabled: canManageLanguages() && offer.CanSelect && (!offer.RequiresAcquisition || unchangedRelease)));
        }
        if (offer.CanUpdate)
        {
            entries.Insert(entries.Count - 1, new("update", LanguageText("update", "Update and apply"), help, Action: new("language-update", key), Enabled: canManageLanguages() && offer.CanSelect && unchangedRelease));
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
                languageFailure = null;
                if (languageIsCurrent!(selected) && !selected.CanUpdate)
                {
                    break;
                }
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
            case "language-apply" or "language-update" when ScreenId == "language-confirm" && action.SubjectId == selectedLanguageOffer && !LanguageChangeInProgress && canManageLanguages!() && FindLanguageOffer(action.SubjectId) is { CanSelect: true } offer
                && (action.Kind == "language-update" || !languageIsCurrent!(offer))
                && (action.Kind == "language-apply" && !offer.RequiresAcquisition || (action.Kind == "language-apply" || offer.CanUpdate) && offer.Release is { } release && release.Version == selectedLanguageReleaseVersion):
                bool update = action.Kind == "language-update";
                languageFailure = null;
                languageChangeProgress = new();
                changingLanguageEdition = LanguageOfferTitle(offer);
                Message = "";
                _selections[ScreenId] = 0;
                applyLanguage!(offer, update, languageChangeProgress);
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
        if (LanguageChangeInProgress)
        {
            if (!languageChangeCancelling)
            {
                languageChangeCancelling = true;
                cancelLanguageChange!();
                _view = null;
            }
            return true;
        }
        if (IsLanguageScreen)
        {
            languageFailure = null;
        }
        return false;
    }

    private void RefreshLanguageChangeView()
    {
        if (LanguageChangeInProgress && languageChangeProgress!.Latest is { } progress && progress != languageTransfer)
        {
            languageTransfer = progress;
            _view = null;
        }
    }

    internal void BeginApplyingLanguage()
    {
        languageChangeApplying = true;
        _view = null;
    }

    private string LanguageChangeStatus()
    {
        if (languageChangeCancelling)
        {
            return LanguageText("cancelling", "Cancelling…");
        }
        if (languageChangeApplying)
        {
            return LanguageText("applying-selection", "Applying language…");
        }
        return languageTransfer?.Phase switch
        {
            LanguageAcquisitionPhase.Downloading => LanguageText("downloading", "Downloading language packages…"),
            LanguageAcquisitionPhase.Verifying => LanguageText("verifying", "Verifying language package…"),
            LanguageAcquisitionPhase.Installing => LanguageText("installing", "Installing language package…"),
            _ => LanguageText("applying", "Preparing language resources…")
        };
    }

    private UiElement BuildLanguageChangeDetails()
    {
        UiGrid details = new() { Gap = 8 * TextScale };
        details.ColumnDefinitions.Add(UiGridLength.Star());
        string package = !languageChangeApplying && languageTransfer?.MemberName is { Length: > 0 } name
            ? FormatSystemText("language.current-package", "Package: {0}", LanguageMemberName(name, languageTransfer.OfficialMemberId))
            : changingLanguageEdition ?? "";
        details.RowDefinitions.Add(UiGridLength.Auto);
        Place(details, Label(package, 20, UiTextFlow.Wrap), 0);

        LanguageAcquisitionProgress? transfer = languageChangeApplying ? null : languageTransfer;
        double receivedMiB = (transfer?.DownloadedBytes ?? 0) / 1048576d;
        double? fraction = transfer?.TotalBytes is > 0 ? Math.Clamp((double)transfer.DownloadedBytes / transfer.TotalBytes.Value, 0, 1) : null;
        UiElement bar;
        if (fraction is double value)
        {
            bar = new UiProgressBar { Value = (float)value };
        }
        else
        {
            bar = new LanguageActivityBar(languageChangeCancelling);
        }
        bar.Id = "language-progress";
        details.RowDefinitions.Add(UiGridLength.Pixel(12 * TextScale));
        Place(details, bar, 1);

        if (transfer is not null)
        {
            string text = fraction is double ratio
                ? FormatSystemText("language.download-progress", "{0:N1} / {1:N1} MiB · {2:P1}", receivedMiB, transfer.TotalBytes!.Value / 1048576d, ratio)
                : FormatSystemText("language.download-received", "{0:N1} MiB received", receivedMiB);
            details.RowDefinitions.Add(UiGridLength.Auto);
            Place(details, Label(text, 18, UiTextFlow.Wrap), 2);
        }
        return details;
    }

    internal void FinishLanguageChange(string? error, bool cancelled = false)
    {
        languageChangeProgress = null;
        languageTransfer = null;
        changingLanguageEdition = null;
        languageChangeApplying = false;
        languageChangeCancelling = false;
        if (error is null && !cancelled && ScreenId == "language-confirm")
        {
            browsingDisplayLanguageOffer = false;
            Back();
            Back();
        }
        if (error is null)
        {
            SetSystemMessage(() => cancelled ? LanguageText("cancelled", "Language change cancelled.") : LanguageText("applied", "Language applied."));
        }
        else
        {
            languageFailure = error;
            SetSystemMessage(() => LanguageText("change-failed", "Could not change the language."));
        }
        _view = null;
    }

    internal void ShowLanguageRemovalFailure(string error)
    {
        languageFailure = error;
        SetSystemMessage(() => LanguageText("remove-failed", "Could not remove the language package."));
        _view = null;
    }

    private sealed class LanguageProgress : IProgress<LanguageAcquisitionProgress>
    {
        private LanguageAcquisitionProgress? latest;

        internal LanguageAcquisitionProgress? Latest => Volatile.Read(ref latest);

        public void Report(LanguageAcquisitionProgress value)
        {
            Volatile.Write(ref latest, value);
        }
    }

    private sealed class LanguageActivityBar(bool stopped) : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            theme.Progress(canvas, Bounds, 0, Enabled);
            if (stopped)
            {
                return;
            }
            float position = (Environment.TickCount64 % 2000) / 1000f;
            if (position > 1)
            {
                position = 2 - position;
            }
            UiRect track = Bounds.Inset(1);
            float width = track.Width * 0.22f;
            theme.Progress(canvas, new UiRect(track.X + (track.Width - width) * position, track.Y, width, track.Height), 1, Enabled);
        }
    }
}
