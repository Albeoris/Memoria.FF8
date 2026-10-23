using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class GuardianForceAbilityModsTests
{
    private static ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> Source() => Enumerable.Range(0, 16).Select(_ => ImmutableArray.Create(new GuardianForceAbilityTemplate(1, false, 10, LearningPriority: 0), new GuardianForceAbilityTemplate(2, false, 20, PrerequisiteAbility: 1, LearningPriority: 1), new GuardianForceAbilityTemplate(3, true, 30, LearningPriority: 2))).ToImmutableArray();
    private static GuardianForceAbilityPatchOperation Op(string id, GuardianForceAbilityOperationKind kind, int ability = 1) => new() { Id = id, Kind = kind, GuardianForce = 0, Ability = ability };
    private static GuardianForceAbilityPatchLayer Layer(string id, params GuardianForceAbilityPatchOperation[] operations) => new("Memoria/" + id, new() { DocumentId = id, Operations = operations.ToImmutableArray() });

    [Fact]
    public void PackageAndPayloadJunctionsCannotEscapeDestination()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "Directory junctions require Windows.");
        string root = Path.Combine(Path.GetTempPath(), "gf-links-" + Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), "gf-outside-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "Memoria", "package");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Memoria"));
            Directory.CreateDirectory(outside);
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { Arguments = $"/c mklink /J \"{destination}\" \"{outside}\"", CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!)
            {
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.ValidateDestination(root, destination));
            Directory.Delete(destination);
            Directory.CreateDirectory(destination);
            string gameplay = Path.Combine(destination, "gameplay");
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { Arguments = $"/c mklink /J \"{gameplay}\" \"{outside}\"", CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!)
            {
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
            var document = Layer("test", Op("add", GuardianForceAbilityOperationKind.EnsureAbility, 4)).Document;
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.Save(root, destination, document));
            Directory.Delete(outside);
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.Save(root, destination, document));
            Assert.False(File.Exists(Path.Combine(outside, "gf-abilities.json")));
            Directory.Delete(gameplay);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, true);
            }
        }
    }
    [Fact]
    public void ExpandedCatalogAndLayerIsolationNeverAbsorbOtherContributions()
    {
        GuardianForceAbilityPatchOperation[] additions = Enumerable.Range(4, 100).Select(id => Op("add-" + id, GuardianForceAbilityOperationKind.EnsureAbility, id)).ToArray();
        var selected = Layer("selected", additions);
        var other = Layer("other", Op("unrelated", GuardianForceAbilityOperationKind.EnsureAbility, 110));
        var combined = GuardianForceAbilityComposer.Compose(Source(), [other, selected]);
        var isolated = GuardianForceAbilityComposer.Compose(Source(), [selected]);
        Assert.Equal(104, combined.Templates[0].Length);
        Assert.Equal(103, isolated.Templates[0].Length);
        Assert.DoesNotContain(isolated.Templates[0], entry => entry.Ability == 110);
        Assert.DoesNotContain("unrelated", Encoding.UTF8.GetString(GuardianForceAbilityMods.Serialize(selected.Document)));
    }

    [Fact]
    public void ProducerIdentityDoesNotFollowAnUnrelatedRecreatedAbility()
    {
        var first = Layer("first", Op("create", GuardianForceAbilityOperationKind.EnsureAbility, 4));
        var second = Layer("second", Op("replace", GuardianForceAbilityOperationKind.ReplaceAbility, 4) with
        {
            Replacement = 5
        }, Op("recreate", GuardianForceAbilityOperationKind.EnsureAbility, 4));
        var third = Layer("third", Op("toggle", GuardianForceAbilityOperationKind.SetInitialState, 4) with
        {
            InitialLearned = true,
            Producers = [new("first", "create", 4)]
        });
        var result = GuardianForceAbilityComposer.Compose(Source(), [first, second, third]);
        Assert.Equal("MissingProducer", result.Outcomes[^1].Status);
        Assert.False(result.Templates[0].Single(entry => entry.Ability == 4).InitialLearned);
    }

    [Fact]
    public void UnsupportedPreservedConditionCannotBeReplacedOrToggled()
    {
        var source = Source();
        source = source.SetItem(0, source[0].SetItem(0, source[0][0] with
        {
            SupportedCondition = false
        }));
        var layer = Layer("edit", Op("replace", GuardianForceAbilityOperationKind.ReplaceAbility) with
        {
            Replacement = 4
        }, Op("toggle", GuardianForceAbilityOperationKind.SetInitialState) with
        {
            InitialLearned = true
        });
        var result = GuardianForceAbilityComposer.Compose(source, [layer]);
        Assert.All(result.Outcomes, outcome => Assert.Equal("UnsupportedCondition", outcome.Status));
        Assert.Equal(source[0], result.Templates[0]);
    }
    [Fact]
    public void AddConflictsNeverResetAndReplacementRetargetsDependenciesAndUsesCatalogCost()
    {
        var add = Op("add", GuardianForceAbilityOperationKind.EnsureAbility, 4) with
        {
            InitialLearned = true
        };
        var secondAdd = add with
        {
            InitialLearned = false
        };
        var replace = Op("replace", GuardianForceAbilityOperationKind.ReplaceAbility) with
        {
            Replacement = 5
        };
        var conflictingReplace = replace with
        {
            Id = "later",
            Replacement = 6
        };
        ImmutableArray<byte> costs = Enumerable.Range(0, 116).Select(id => (byte)(id + 10)).ToImmutableArray();
        var result = GuardianForceAbilityComposer.Compose(Source(), [Layer("first", add, replace), Layer("second", secondAdd, conflictingReplace)], costs);
        Assert.Equal(new[] { 5, 2, 3, 4 }, result.Templates[0].Select(entry => entry.Ability));
        Assert.True(result.Templates[0][3].InitialLearned);
        Assert.Equal(15, result.Templates[0][0].RequiredPoints);
        Assert.Equal(14, result.Templates[0][3].RequiredPoints);
        Assert.Equal(5, result.Templates[0][1].PrerequisiteAbility);
        Assert.Equal("ConflictingValue", result.Outcomes[2].Status);
        Assert.Equal("MissingTarget", result.Outcomes[3].Status);
        Assert.Equal(1, Source()[0][0].Ability);
    }

    [Fact]
    public void DuplicateReplacementDestinationIsAtomicAndFailedProducerDoesNotOwnExistingRow()
    {
        var replace = Op("replace", GuardianForceAbilityOperationKind.ReplaceAbility) with
        {
            Replacement = 2
        };
        var toggle = Op("toggle", GuardianForceAbilityOperationKind.SetInitialState, 2) with
        {
            InitialLearned = true,
            ProducerOperationId = "replace"
        };
        var result = GuardianForceAbilityComposer.Compose(Source(), [Layer("test", replace, toggle)]);
        Assert.Equal(Source()[0], result.Templates[0]);
        Assert.Equal("ConflictingValue", result.Outcomes[0].Status);
        Assert.Equal("MissingProducer", result.Outcomes[1].Status);
    }

    [Fact]
    public void SubsetPermutationsComposeAndKeepPriorityAndUnrelatedRows()
    {
        var swap = Op("swap", GuardianForceAbilityOperationKind.OrderSubset) with
        {
            Order = [3, 1],
            ExpectedOrder = [1, 3]
        };
        var append = Op("append", GuardianForceAbilityOperationKind.EnsureAbility, 4);
        var other = Op("other", GuardianForceAbilityOperationKind.OrderSubset) with
        {
            Order = [4, 2],
            ExpectedOrder = [2, 4]
        };
        var result = GuardianForceAbilityComposer.Compose(Source(), [Layer("one", swap, append), Layer("two", other)]);
        Assert.Equal(new[] { 3, 4, 1, 2 }, result.Templates[0].Select(entry => entry.Ability));
        Assert.Equal(0, result.Templates[0][2].LearningPriority);
        Assert.Equal(3, result.Templates[0][1].LearningPriority);
        Assert.Equal(result.Templates[0], GuardianForceAbilityComposer.Compose(Source(), [Layer("one", swap, append), Layer("two", other)]).Templates[0]);
    }

    [Fact]
    public void AllOrderingProducersMustActuallyCreateTheirParticipants()
    {
        var add = Op("add", GuardianForceAbilityOperationKind.EnsureAbility, 1);
        var order = Op("order", GuardianForceAbilityOperationKind.OrderSubset) with
        {
            Order = [2, 1],
            Producers = [new("test", "add", 1)]
        };
        var result = GuardianForceAbilityComposer.Compose(Source(), [Layer("test", add, order)]);
        Assert.Equal("AlreadySatisfied", result.Outcomes[0].Status);
        Assert.Equal("MissingProducer", result.Outcomes[1].Status);
        Assert.Equal(1, result.Templates[0][0].Ability);
    }

    [Fact]
    public void MalformedOrDuplicateDocumentsFailPreparationAsWhole()
    {
        var document = Layer("same", Op("add", GuardianForceAbilityOperationKind.EnsureAbility, 4)).Document;
        byte[] bytes = GuardianForceAbilityMods.Serialize(document);
        ModCatalogSnapshot catalog = new(1, [], [new("Memoria/a", ModFormat.Memoria, GuardianForceAbilityMods.PayloadPath, 1, () => bytes), new("Memoria/b", ModFormat.Memoria, GuardianForceAbilityMods.PayloadPath, 1, () => bytes)]);
        Assert.False(GuardianForceAbilityMods.TryPrepare(catalog, out var result, out var diagnostics));
        Assert.Empty(result.Layers);
        Assert.Single(diagnostics);
        Assert.Throws<InvalidDataException>(() => GuardianForceAbilityMods.Serialize(document with { Operations = [Op("bad", GuardianForceAbilityOperationKind.EnsureAbility, 0)] }));
    }

    [Fact]
    public void DisabledPackageRoundtripPreservesUnknownMetadataAndUnrelatedFilesRejectsStaleWrites()
    {
        string root = Path.Combine(Path.GetTempPath(), "gf-authoring-" + Guid.NewGuid().ToString("N"));
        try
        {
            string destination = Path.Combine(root, "Memoria", "test");
            var document = GuardianForceAbilityMods.Parse(Encoding.UTF8.GetBytes("""{"schemaVersion":1,"documentId":"test","custom":{"nested":[1,2]},"operations":[]}"""));
            var saved = GuardianForceAbilityPackageStore.Save(root, destination, document);
            File.WriteAllText(Path.Combine(destination, "mod.ini"), "; preserve this comment\nenabled=false\n");
            Directory.CreateDirectory(Path.Combine(destination, "models"));
            File.WriteAllText(Path.Combine(destination, "models", "untouched.txt"), "unchanged");
            ModPackage package = new("Memoria/test", "test", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, destination);
            var reopened = GuardianForceAbilityPackageStore.Open(package);
            Assert.Equal(saved.Revision, reopened.Revision);
            Assert.Equal(new[] { 1, 2 }, reopened.Document.Metadata!["custom"].GetProperty("nested").EnumerateArray().Select(element => element.GetInt32()));
            var updated = GuardianForceAbilityPackageStore.Save(root, destination, reopened.Document with
            {
                Operations = [Op("add", GuardianForceAbilityOperationKind.EnsureAbility, 4)]
            }, reopened.Revision);
            Assert.Contains("preserve this comment", File.ReadAllText(Path.Combine(destination, "mod.ini")));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(destination, "models", "untouched.txt")));
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.Save(root, destination, document, saved.Revision));
            Assert.NotEqual(document.DocumentId, GuardianForceAbilityPackageStore.IndependentCopy(document).DocumentId);
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.ValidateDestination(root, Path.Combine(root, "MemoriaSibling", "test")));
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.ValidateDestination(root, Path.Combine(root, "Memoria")));
            Assert.Throws<InvalidDataException>(() => GuardianForceAbilityPackageStore.ValidateDestination(root, Path.Combine(destination, "nested")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
