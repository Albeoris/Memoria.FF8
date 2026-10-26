using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private NativeCharacterVitals? guardianSourceVitals;
    private string guardianSourceRevision = "";
    private GuardianForceAbilityApplyPreview? guardianPendingModReview;
    private string? guardianAcceptedModReview;
    private string? guardianAcceptedModState;

    private void EnsureGuardianSource()
    {
        if (guardianSourceVitals is not null)
        {
            return;
        }
        using GameAssets assets = new(options.GameDirectory);
        guardianSourceVitals = assets.ReadNewGameCharacterVitals();
        guardianSourceRevision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(guardianSourceVitals.MenuDefinitions)));
    }

    private void ConnectGuardianAbilityEditor()
    {
        resourceHost!.PrepareGuardianForcePublication = PrepareGuardianModPublication;
        gameMenus!.ConnectGuardianForceEditorAsync(ReadGuardianEditorContext, id => { EnsureLiveMenuCatalog(); return liveMenuCatalog!.GuardianForceName(id) ?? $"GF {id}"; }, id => { EnsureLiveMenuCatalog(); return liveMenuCatalog!.AbilityName(id) ?? ""; }, id => { EnsureLiveMenuCatalog(); return liveMenuCatalog!.AbilityDescription(id) ?? ""; }, keyboard.BindingPrompt, ReviewGuardianDraft, ApplyGuardianDraft, SaveGuardianDraft);
        gameMenus!.GuardianEditorEditionSupported = resourceHost.NativeGameplayModsSupported;
        gameMenus.ConnectGuardianPackageOwnership(ReviewGuardianPackageOwnership, ReviewRemoveMissingGuardianLayer);
        gameMenus.ConnectGuardianRecovery(ReviewRecoveredGuardianLayers);
        gameMenus.ConnectGuardianModReview(() => guardianPendingModReview?.FormatConsequences(LocalizeGuardianEditorText).ToArray(), AcceptGuardianModReview);
    }

    private GuardianForceEditorContext ReadGuardianEditorContext()
    {
        EnsureGuardianSource();
        NativeCharacterVitals? current = session.State.Scene == SceneKind.Title ? null : session.State.CharacterVitals;
        GuardianForceAbilityProfile? profile = current?.GuardianForceProfile;
        NativeCharacterVitals source = guardianSourceVitals!;
        if (profile is not null && profile.SourceFingerprint == guardianSourceRevision)
        {
            source = source with
            {
                MenuDefinitions = source.MenuDefinitions with
                {
                    Templates = profile.Source
                }
            };
        }
        else if (profile is null && current is not null && (current.GuardianForceOpaqueBaseline || !current.MenuDefinitions.Templates.IsDefault && current.MenuDefinitions.Templates.Length == 16 && !TemplateEquals(current.ResolveGuardianForceTemplates(), source.ResolveGuardianForceTemplates())))
        {
            // An observable imported custom definition has no recoverable package attribution.
            source = source with
            {
                MenuDefinitions = source.MenuDefinitions with
                {
                    Templates = current.ResolveGuardianForceTemplates()
                }
            };
        }
        ImmutableArray<string> excluded = profile?.ExcludedDocuments ?? (current?.GuardianForceOpaqueBaseline == true ? current.GuardianForceRecoveryProfile?.AppliedLayers.Select(layer => layer.Document.DocumentId).ToImmutableArray() ?? [] : []);
        return new(source, current, EffectiveGuardianLayers(resourceHost!.GuardianForceMods, resourceHost.Mods.Catalog, profile), profile?.LocalDocuments ?? [], excluded);
    }

    private static ImmutableArray<GuardianForceAbilityPatchLayer> EffectiveGuardianLayers(GuardianForceAbilityMods mods, ModCatalogSnapshot catalog, GuardianForceAbilityProfile? profile) => GuardianForceAbilityProfileCoordinator.ResolveLayers(mods, catalog, profile);

    private GuardianForceAbilityProfile GuardianProfile(GuardianForceEditorContext context, ImmutableArray<GuardianForceAbilityPatchLayer>? layers = null, ImmutableArray<GuardianForceAbilityPatchDocument>? locals = null, ImmutableArray<string>? exclusions = null)
    {
        var aliases = context.Current?.GuardianForceProfile?.LocalOwnershipAliases ?? ImmutableDictionary<string, string>.Empty;
        return new(context.Source.ResolveGuardianForceTemplates(), guardianSourceRevision, layers ?? context.Layers, locals ?? context.LocalDocuments, exclusions ?? context.ExcludedDocuments)
        {
            LocalOwnershipAliases = aliases
        };
    }

    private GuardianForceAbilityComposition ComposeGuardianProfile(GuardianForceAbilityProfile profile) => GuardianForceAbilityProfileCoordinator.Compose(profile, guardianSourceVitals!.MenuDefinitions.AbilityPoints);

    private GuardianForceEditorReview ReviewGuardianDraft(GuardianForceAbilityPatchDocument document, bool resetStartingStates, ImmutableArray<GuardianForceAbilityReset> editedStartingStates)
    {
        GuardianForceEditorContext context = ReadGuardianEditorContext();
        if (context.Current is null)
        {
            throw new InvalidOperationException("Start or load a playthrough before applying changes once.");
        }
        List<string> excluded = context.ExcludedDocuments.ToList();
        if (!excluded.Contains(document.DocumentId))
        {
            excluded.Add(document.DocumentId);
        }
        ImmutableArray<GuardianForceAbilityPatchDocument> local = context.LocalDocuments.Where(existing => existing.DocumentId != document.DocumentId).Append(document).ToImmutableArray();
        GuardianForceAbilityProfile profile = GuardianProfile(context, locals: local, exclusions: excluded.ToImmutableArray());
        GuardianForceAbilityComposition composed = ComposeGuardianProfile(profile);
        GuardianForceAbilityText[] unsuccessful = composed.Outcomes.Where(outcome => outcome.DocumentId == document.DocumentId && outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => outcome.MessageText!).ToArray();
        if (unsuccessful.Length > 0)
        {
            // A failed operation must not be saved locally to become active unexpectedly on a later refresh.
            HashSet<string> successful = composed.Outcomes.Where(outcome => outcome.DocumentId == document.DocumentId && outcome.Status is "Applied" or "AlreadySatisfied").Select(outcome => outcome.OperationId).ToHashSet();
            document = document with
            {
                Operations = document.Operations.Where(operation => successful.Contains(operation.Id)).ToImmutableArray()
            };
            profile = profile with
            {
                LocalDocuments = local.Where(existing => existing.DocumentId != document.DocumentId).Append(document).ToImmutableArray()
            };
            composed = ComposeGuardianProfile(profile);
        }
        IEnumerable<GuardianForceAbilityReset> resets = resetStartingStates ? editedStartingStates.Where(reset => composed.Templates[reset.GuardianForce].Any(entry => entry.Ability == reset.Ability) && document.Operations.Any(operation => operation.GuardianForce == reset.GuardianForce && operation.InitialLearned == reset.Learned && (operation.Kind == GuardianForceAbilityOperationKind.SetInitialState && operation.Ability == reset.Ability || operation.Kind == GuardianForceAbilityOperationKind.EnsureAbility && operation.Ability == reset.Ability) && composed.Outcomes.Any(outcome => outcome.DocumentId == document.DocumentId && outcome.OperationId == operation.Id && (outcome.Status == "Applied" || operation.Kind == GuardianForceAbilityOperationKind.SetInitialState && outcome.Status == "AlreadySatisfied")))) : [];
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(composed.Templates, profile, resets, guardianSourceRevision, resourceHost!.Mods.Catalog.Generation);
        if (unsuccessful.Length > 0)
        {
            ImmutableArray<GuardianForceAbilityText> prose = preview.ConsequenceTexts.AddRange(unsuccessful.Select(message => GuardianForceAbilityText.Create("guardian.editor.review.skipped", message)));
            preview = preview with
            {
                Consequences = prose.Select(text => text.Format()).ToImmutableArray(),
                ConsequenceTexts = prose
            };
        }
        return new(preview, document, resetStartingStates);
    }

    private GuardianForceEditorReview ReviewGuardianPackageOwnership(GuardianForceAbilityPatchDocument document)
    {
        GuardianForceEditorContext context = ReadGuardianEditorContext();
        GuardianForceAbilityPatchLayer? installed = resourceHost!.GuardianForceMods.Layers.FirstOrDefault(layer => layer.Document.DocumentId == document.DocumentId && resourceHost.Mods.InstalledPackages.Any(package => package.Id.Equals(layer.PackageId, StringComparison.OrdinalIgnoreCase) && package.RequestedEnabled && package.Status == ModEffectiveStatus.Active));
        if (context.Current is null || installed is null)
        {
            throw new InvalidOperationException("Enable this saved package in Installed Mods before transferring its GF contribution to package ownership.");
        }
        if (!GuardianForceAbilityMods.Serialize(installed.Document).SequenceEqual(GuardianForceAbilityMods.Serialize(document)))
        {
            throw new InvalidOperationException("Save the draft before transferring ownership to its installed package revision.");
        }
        GuardianForceAbilityProfile profile = GuardianProfile(context, locals: context.LocalDocuments.Where(local => local.DocumentId != document.DocumentId && local.DocumentId != context.Current?.GuardianForceProfile?.LocalOwnershipAliases.GetValueOrDefault(document.DocumentId)).ToImmutableArray(), exclusions: context.ExcludedDocuments.Where(id => id != document.DocumentId).ToImmutableArray());
        profile = profile with { LocalOwnershipAliases = profile.LocalOwnershipAliases.Remove(document.DocumentId) };
        GuardianForceAbilityComposition composition = ComposeGuardianProfile(profile);
        return new(session.PreviewGuardianForceAbilities(composition.Templates, profile, sourceRevision: guardianSourceRevision, modGeneration: resourceHost!.Mods.Catalog.Generation), document, false, true);
    }

    private GuardianForceEditorReview ReviewRemoveMissingGuardianLayer(string documentId)
    {
        GuardianForceEditorContext context = ReadGuardianEditorContext();
        GuardianForceAbilityPatchLayer layer = context.Layers.Single(layer => layer.Document.DocumentId == documentId);
        if (resourceHost!.GuardianForceMods.Layers.Any(installed => installed.Document.DocumentId == documentId))
        {
            throw new InvalidOperationException("Disable the installed package through Installed Mods instead.");
        }
        GuardianForceAbilityProfile profile = GuardianProfile(context, layers: context.Layers.Where(item => item.Document.DocumentId != documentId).ToImmutableArray());
        GuardianForceAbilityComposition composition = ComposeGuardianProfile(profile);
        return new(session.PreviewGuardianForceAbilities(composition.Templates, profile, sourceRevision: guardianSourceRevision, modGeneration: resourceHost.Mods.Catalog.Generation), layer.Document, false, true, "Remove retained missing GF contribution: " + layer.PackageId) { IntentText = GuardianForceAbilityText.Create("guardian.editor.review.remove-missing", layer.PackageId) };
    }

    private GuardianForceEditorReview ReviewRecoveredGuardianLayers()
    {
        GuardianForceEditorContext context = ReadGuardianEditorContext();
        GuardianForceAbilityProfile recovery = context.Current?.GuardianForceRecoveryProfile ?? throw new InvalidOperationException("No recovered GF profile is available.");
        HashSet<string> previouslyActive = recovery.AppliedLayers.Where(layer => !recovery.ExcludedDocuments.Contains(layer.Document.DocumentId)).Select(layer => layer.Document.DocumentId).ToHashSet(StringComparer.Ordinal);
        GuardianForceAbilityProfile profile = GuardianProfile(context, exclusions: context.ExcludedDocuments.Where(id => !previouslyActive.Contains(id)).ToImmutableArray());
        GuardianForceAbilityComposition composition = ComposeGuardianProfile(profile);
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(composition.Templates, profile, sourceRevision: guardianSourceRevision, modGeneration: resourceHost!.Mods.Catalog.Generation);
        ImmutableArray<GuardianForceAbilityText> prose = preview.ConsequenceTexts.Prepend(GuardianForceAbilityText.Create("guardian.editor.review.imported-baseline")).Concat(composition.Outcomes.Where(outcome => outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => GuardianForceAbilityText.Create("guardian.editor.review.package", outcome.PackageId, outcome.MessageText!))).ToImmutableArray();
        preview = preview with
        {
            Consequences = prose.Select(text => text.Format()).ToImmutableArray(),
            ConsequenceTexts = prose
        };
        return new(preview, new()
        {
            DocumentId = "recovery-review"
        }, false, true, "Reactivate supported native documents over the observed opaque baseline after fresh reconciliation.") { IntentText = GuardianForceAbilityText.Create("guardian.editor.review.reactivate") };
    }

    private string LocalizeGuardianEditorText(string id, string original) => SystemTextCatalog.Translate((key, fallback) => resourceHost?.Resources.Localize(key, fallback) ?? fallback, id, original);

    private string? GuardianMutationUnavailableReason
    {
        get
        {
            if (!session.CanApplyGuardianForceAbilities(out string? reason, LocalizeGuardianEditorText))
            {
                return reason;
            }
            if (session.State.Scene == SceneKind.Field && events is not null && (events.Scene.NativeTutorial is not null || events.Scene.NativeMenuTutorial is not null || events.HasPendingBattle || events.PartyMenu is not null && events.PartyMenu is not { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active } || events.PartyMenu is null && (!events.IsUserControlEnabled || events.IsDialogueBlocking || !events.Scene.MenuEnabled || events.PendingTripleTriad is not null)))
            {
                return GuardianForceAbilityText.Create("guardian.editor.boundary.idle").Format(LocalizeGuardianEditorText);
            }
            return null;
        }
    }

    private void RequireGuardianMutationBoundary()
    {
        if (GuardianMutationUnavailableReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private string ApplyGuardianDraft(GuardianForceEditorReview review)
    {
        RequireGuardianMutationBoundary();
        session.ApplyGuardianForceAbilities(review.Preview, guardianSourceRevision, resourceHost!.Mods.Catalog.Generation);
        guardianPendingModReview = null;
        gameMenus?.InvalidateLiveData();
        return review.PackageOwnership ? "Reviewed contribution ownership updated. Save the game to retain it." : "Applied to this playthrough. Save the game to keep it. Package enablement is unchanged.";
    }

    private async Task<GuardianForceAbilityPackageDocument?> SaveGuardianDraft(GuardianForceAbilityPatchDocument document, GuardianForceAbilityPackageDocument? original, bool saveAs)
    {
        EnsureGuardianSource();
        string primary = resourceHost!.Mods.PrimaryModsDirectory;
        string root = Path.Combine(primary, "Memoria");
        string? destination = original?.PackageDirectory;
        string? revision = original?.Revision;
        string localOwnerId = document.DocumentId;
        bool exportedIndependent = false;
        if (saveAs || destination is null)
        {
            try
            {
                destination = await ModelBrowserDialogs.DestinationAsync(Window.Handle, root, LocalizeUiText);
            }
            finally
            {
                DrainGuardianEditorDialogInput();
            }
            if (destination is null)
            {
                return null;
            }
            bool identityInstalledElsewhere = resourceHost.Mods.InstalledPackages.Where(package => package.Format == ModFormat.Memoria && File.Exists(Path.Combine(package.RootDirectory, GuardianForceAbilityMods.PayloadPath))).Any(package => GuardianForceAbilityPackageStore.Open(package).Document.DocumentId == document.DocumentId);
            if (original is not null || identityInstalledElsewhere)
            {
                document = GuardianForceAbilityPackageStore.IndependentCopy(document);
                exportedIndependent = true;
            }
            revision = null;
        }
        string packageId = "Memoria/" + Path.GetFileName(destination);
        bool installedDestination = string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination))), Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase) || resourceHost.Mods.InstalledPackages.Any(package => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(package.RootDirectory)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)), StringComparison.OrdinalIgnoreCase));
        if (installedDestination && gameMenus is not null && !gameMenus.ReviewGuardianPackagePlacement(packageId, document))
        {
            return null;
        }
        if (exportedIndependent && session.CharacterVitals?.GuardianForceProfile?.LocalDocuments.Any(local => local.DocumentId == localOwnerId) == true)
        {
            RequireGuardianMutationBoundary();
        }
        GuardianForceAbilityPreparedApply? ownership = exportedIndependent ? GuardianForceAbilityProfileCoordinator.PrepareIndependentLocalExport(session, localOwnerId, document.DocumentId, guardianSourceRevision, resourceHost.Mods.Catalog.Generation) : null;
        GuardianForceAbilityPackageDocument result = GuardianForceAbilityPackageStore.Save(primary, destination, document, revision, resourceHost.Mods.InstalledPackages);
        if (ownership is not null)
        {
            session.CommitPreparedGuardianForceAbilities(ownership);
        }
        resourceHost.Mods.Refresh();
        resourceHost.Pump(true);
        return result;
    }

    private void DrainGuardianEditorDialogInput()
    {
        keyboard.SuppressHeldUiInput();
        drainUiButtons = true;
    }

    private ModPreparationResult PrepareGuardianModPublication(GuardianForceAbilityMods mods, ModCatalogSnapshot catalog)
    {
        if (session.State.Scene == SceneKind.Title || session.State.CharacterVitals is null)
        {
            return ModPreparationResult.Accepted;
        }
        if (GuardianMutationUnavailableReason is { } boundaryReason)
        {
            return new(false, Diagnostics: [new("Memoria.GuardianForceBoundary", boundaryReason)]);
        }
        EnsureGuardianSource();
        GuardianForceEditorContext context = ReadGuardianEditorContext();
        GuardianForceAbilityProfile profile = GuardianProfile(context, layers: EffectiveGuardianLayers(mods, catalog, context.Current?.GuardianForceProfile));
        GuardianForceAbilityComposition composed = ComposeGuardianProfile(profile);
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(composed.Templates, profile, sourceRevision: guardianSourceRevision, modGeneration: catalog.Generation);
        string reviewKey = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(profile)));
        string stateKey = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(preview.Source)));
        if (preview.Consequences.Length > 0 && (guardianAcceptedModReview != reviewKey || guardianAcceptedModState != stateKey))
        {
            guardianPendingModReview = preview;
            return new(false, Diagnostics: [new("Memoria.GuardianForceReview", "GF progression or junction changes await review in Mods / GF changes review. The previous runtime generation remains active.")]);
        }
        try
        {
            GuardianForceAbilityPreparedApply prepared = session.PrepareGuardianForceAbilities(preview, guardianSourceRevision, catalog.Generation);
            return new(true, Diagnostics: GuardianForceAbilityProfileCoordinator.MissingLayerDiagnostics(catalog, context.Current?.GuardianForceProfile).AddRange(composed.Outcomes.Where(outcome => outcome.Status is not ("Applied" or "AlreadySatisfied")).Select(outcome => new ModDiagnostic("Memoria.GuardianForceConflict", outcome.Message, outcome.PackageId, GuardianForceAbilityMods.PayloadPath))), Commit: () =>
            {
                session.CommitPreparedGuardianForceAbilities(prepared);
                gameMenus?.InvalidateLiveData();
            });
        }
        catch (InvalidOperationException exception)
        {
            return new(false, Diagnostics: [new("Memoria.GuardianForceBoundary", exception.Message)]);
        }
    }

    private string AcceptGuardianModReview()
    {
        GuardianForceAbilityApplyPreview review = guardianPendingModReview ?? throw new InvalidOperationException("There are no pending GF changes.");
        if (session.State.CharacterVitals is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.CharacterVitals), JsonSerializer.SerializeToElement(review.Source)))
        {
            resourceHost!.Mods.Refresh();
            throw new InvalidOperationException("The playthrough changed. Refresh mods and review the updated consequences before accepting.");
        }
        guardianAcceptedModReview = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(review.Vitals.GuardianForceProfile)));
        guardianAcceptedModState = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(review.Source)));
        resourceHost!.Mods.Refresh();
        bool published = resourceHost.Pump(true);
        if (published)
        {
            guardianPendingModReview = null;
        }
        return published ? "Reviewed GF changes activated. Save your playthrough to retain the applied profile." : "The generation remains pending or rejected; see Installed Mods diagnostics.";
    }

    private GuardianForceAbilityProfile? synchronizedGuardianProfile;
    private GuardianForceAbilityMods? synchronizedGuardianMods;

    private void SynchronizeGuardianModProfile()
    {
        if (resourceHost is null || session.State.Scene == SceneKind.Title || session.State.CharacterVitals is null || resourceHost.Mods.IsReloadPending)
        {
            return;
        }
        GuardianForceAbilityProfile? profile = session.State.CharacterVitals.GuardianForceProfile;
        if (ReferenceEquals(profile, synchronizedGuardianProfile) && ReferenceEquals(resourceHost.GuardianForceMods, synchronizedGuardianMods))
        {
            return;
        }
        synchronizedGuardianProfile = profile;
        synchronizedGuardianMods = resourceHost.GuardianForceMods;
        if (profile is null && resourceHost.GuardianForceMods.Layers.Length == 0)
        {
            return;
        }
        EnsureGuardianSource();
        var layers = EffectiveGuardianLayers(resourceHost.GuardianForceMods, resourceHost.Mods.Catalog, profile);
        if (profile is not null && profile.SourceFingerprint == guardianSourceRevision && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(layers), JsonSerializer.SerializeToElement(profile.AppliedLayers)))
        {
            return;
        }
        ModPreparationResult result = PrepareGuardianModPublication(resourceHost.GuardianForceMods, resourceHost.Mods.Catalog);
        if (result.Success)
        {
            result.Commit?.Invoke();
        }
    }

    private static bool TemplateEquals(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> left, ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> right) => left.Zip(right).All(pair => pair.First.SequenceEqual(pair.Second));
}
