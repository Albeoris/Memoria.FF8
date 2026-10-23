using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityProfileTests
{
    private static GuardianForceAbilityPatchLayer Layer(string name, int ability, string? id = null) => new("Memoria/" + name, new() { DocumentId = id ?? name, Operations = [new() { Id = "add", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = ability }] });
    private static GuardianForceAbilityProfile Profile(params GuardianForceAbilityPatchLayer[] layers) => new(Enumerable.Range(0, 16).Select(_ => ImmutableArray.Create(new GuardianForceAbilityTemplate(1, false, 10))).ToImmutableArray(), "source", layers.ToImmutableArray(), [], []);
    private static ModPackage Package(string name, bool enabled = true, ModEffectiveStatus status = ModEffectiveStatus.Active) => new("Memoria/" + name, name, ModFormat.Memoria, enabled, enabled, status, name);
    private static ModFile File(string name) => new("Memoria/" + name, ModFormat.Memoria, GuardianForceAbilityMods.PayloadPath, 1, () => []);

    [Fact]
    public void IndependentExportPreparationPreservesLocalOwnershipWithoutMutatingUntilCommit()
    {
        NativeCharacterVitals source = GuardianForceAbilityEditorTests.Source();
        GuardianForceAbilityPatchDocument local = new() { DocumentId = "local-owner" };
        GuardianForceAbilityProfile profile = new(source.ResolveGuardianForceTemplates(), "source", [], [local], ["local-owner"]);
        GameSession session = new(new GameState { CharacterVitals = source.ApplyGuardianForceTemplates(profile.Source) with { GuardianForceProfile = profile } });
        GuardianForceAbilityPreparedApply? prepared = GuardianForceAbilityProfileCoordinator.PrepareIndependentLocalExport(session, "local-owner", "independent-copy", "source", 7);
        Assert.NotNull(prepared);
        Assert.DoesNotContain("independent-copy", session.CharacterVitals!.GuardianForceProfile!.ExcludedDocuments);
        session.CommitPreparedGuardianForceAbilities(prepared);
        Assert.Equal("local-owner", session.CharacterVitals!.GuardianForceProfile!.LocalOwnershipAliases["independent-copy"]);
        Assert.Contains("independent-copy", session.CharacterVitals.GuardianForceProfile.ExcludedDocuments);
        Assert.Equal("local-owner", Assert.Single(session.CharacterVitals.GuardianForceProfile.LocalDocuments).DocumentId);
        Assert.Null(GuardianForceAbilityProfileCoordinator.PrepareIndependentLocalExport(session, "unrelated", "another-copy", "source", 7));
    }

    [Fact]
    public void DeliberateDisableAndIncompatibleEditionRemoveContribution()
    {
        var saved = Layer("saved", 2);
        var profile = Profile(saved);
        foreach (ModPackage package in new[] { Package("saved", false, ModEffectiveStatus.Disabled), Package("saved", true, ModEffectiveStatus.IncompatibleEdition) })
        {
            ModCatalogSnapshot catalog = new(1, [package], []);
            Assert.Empty(GuardianForceAbilityProfileCoordinator.ResolveLayers(GuardianForceAbilityMods.Empty, catalog, profile));
            Assert.Empty(GuardianForceAbilityProfileCoordinator.MissingLayerDiagnostics(catalog, profile));
        }
    }

    [Fact]
    public void MissingPackageAndMissingPayloadRetainLastKnownRevisionAndPosition()
    {
        var first = Layer("first", 2);
        var saved = Layer("saved", 3);
        var last = Layer("last", 4);
        var profile = Profile(first, saved, last);
        foreach (ModPackage[] packages in new[] { new[] { Package("first"), Package("last") }, new[] { Package("first"), Package("saved"), Package("last") } })
        {
            ModCatalogSnapshot catalog = new(1, packages, [File("first"), File("last")]);
            var resolved = GuardianForceAbilityProfileCoordinator.ResolveLayers(new([first, last]), catalog, profile);
            Assert.Equal(new[] { "first", "saved", "last" }, resolved.Select(layer => layer.Document.DocumentId));
            Assert.Same(saved.Document, resolved[1].Document);
            Assert.Single(GuardianForceAbilityProfileCoordinator.MissingLayerDiagnostics(catalog, profile));
        }
    }

    [Fact]
    public void PresentUpdatedPayloadReplacesSavedRevisionEvenWhenDocumentIdentityChanges()
    {
        var previous = Layer("saved", 2);
        var updated = Layer("saved", 3, "new-identity");
        ModCatalogSnapshot catalog = new(2, [Package("saved")], [File("saved")]);
        var resolved = GuardianForceAbilityProfileCoordinator.ResolveLayers(new([updated]), catalog, Profile(previous));
        Assert.Same(updated.Document, Assert.Single(resolved).Document);
        Assert.Empty(GuardianForceAbilityProfileCoordinator.MissingLayerDiagnostics(catalog, Profile(previous)));
    }

    [Fact]
    public void LocalOwnershipExcludesPackageButNeverOtherLayersAndCanTransferBack()
    {
        var selected = Layer("selected", 2);
        var unrelated = Layer("unrelated", 3);
        var profile = Profile(selected, unrelated) with
        {
            LocalDocuments = [selected.Document],
            ExcludedDocuments = [selected.Document.DocumentId]
        };
        ImmutableArray<byte> costs = Enumerable.Range(0, 116).Select(id => (byte)id).ToImmutableArray();
        var local = GuardianForceAbilityProfileCoordinator.Compose(profile, costs);
        Assert.Equal(new[] { 1, 3, 2 }, local.Templates[0].Select(entry => entry.Ability));
        Assert.Equal(new[] { "Memoria/unrelated", "Playthrough" }, local.Outcomes.Select(outcome => outcome.PackageId));
        var transferred = GuardianForceAbilityProfileCoordinator.Compose(profile with
        {
            LocalDocuments = [],
            ExcludedDocuments = []
        }, costs);
        Assert.Equal(new[] { 1, 2, 3 }, transferred.Templates[0].Select(entry => entry.Ability));
        Assert.Equal(2, transferred.Outcomes.Length);
    }

    [Fact]
    public void MissingLocalOwnedPackageIsRetainedAsProfileButExcludedFromComposition()
    {
        var saved = Layer("saved", 2);
        var profile = Profile(saved) with
        {
            LocalDocuments = [saved.Document],
            ExcludedDocuments = [saved.Document.DocumentId]
        };
        var resolved = GuardianForceAbilityProfileCoordinator.ResolveLayers(GuardianForceAbilityMods.Empty, new(2, [], []), profile);
        Assert.Single(resolved);
        var composition = GuardianForceAbilityProfileCoordinator.Compose(profile with
        {
            AppliedLayers = resolved
        }, Enumerable.Repeat((byte)10, 116).ToImmutableArray());
        Assert.Single(composition.Outcomes);
        Assert.Equal("Playthrough", composition.Outcomes[0].PackageId);
    }
}
