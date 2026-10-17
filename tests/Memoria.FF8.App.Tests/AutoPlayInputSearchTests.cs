using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayInputSearchTests
{
    private static readonly ScriptedInputQuery Query = new(2, 3, 4, "KEYSCAN2", 3);

    [Fact]
    public void PlannedGlobalReplayStartsWithItsActualAlternativeAndColdPreservesTheDomain()
    {
        AutoPlayInputSearchFrame frame = Frame("root");
        frame.PreferredSelection = 1;
        AutoPlayInputSearch search = new() { Frames = [frame], ActiveFrameId = frame.Id };

        AutoPlayController.ValidateInputSearchLedger(search);
        Assert.Equal(1, AutoPlayController.NextInputSearchSelection(frame));
        AutoPlayInputSearch cold = JsonSerializer.Deserialize<AutoPlayInputSearch>(JsonSerializer.Serialize(search, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        AutoPlayController.ValidateInputSearchLedger(cold);
        AutoPlayInputSearchFrame restored = Assert.Single(cold.Frames);
        Assert.Equal(new[] { 0, 1 }, restored.Options);
        Assert.Equal(new[] { 1, 2 }, restored.Input!.Masks);
        Assert.Equal(1, AutoPlayController.NextInputSearchSelection(restored));
        restored.Trials.Add(new() { Selection = 1, Mask = 2 });
        Assert.Equal(0, AutoPlayController.NextInputSearchSelection(restored));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void PlannedReplayCannotInventAnAlternativeOutsideTheOriginalDomain(int preferred)
    {
        AutoPlayInputSearchFrame frame = Frame("root");
        frame.PreferredSelection = preferred;
        AutoPlayInputSearch search = new() { Frames = [frame], ActiveFrameId = frame.Id };

        Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateInputSearchLedger(search));
        Assert.Throws<InvalidDataException>(() => AutoPlayController.NextInputSearchSelection(frame));
    }

    [Fact]
    public void RepeatedDialogueStartsAfterItsLastActualPrefixSelectionAndRetainsEverySibling()
    {
        AutoPlayInputSearchFrame frame = new()
        {
            Key = "dialogue",
            Dialogue = new(0, 10, "Actual visible choice", 0, 0, 2, 5),
            Options = [2, 3, 4, 5],
            BeforeSelections = [new("dialogue", 2), new("other", 4), new("dialogue", 5)]
        };

        Assert.Equal(2, AutoPlayController.NextInputSearchSelection(frame));
        frame.Trials.Add(new() { Selection = 2 });
        Assert.Equal(3, AutoPlayController.NextInputSearchSelection(frame));
        frame.Trials.Add(new() { Selection = 3 });
        Assert.Equal(4, AutoPlayController.NextInputSearchSelection(frame));
        frame.Trials.Add(new() { Selection = 4 });
        Assert.Equal(5, AutoPlayController.NextInputSearchSelection(frame));
        Assert.False(frame.Exhausted);
        Assert.All(frame.Trials, trial => Assert.Equal(AutoPlayInputTrialOutcome.Pending, trial.Outcome));

        frame.BeforeSelections.Add(new("dialogue", 2));
        frame.Trials.Clear();
        Assert.Equal(3, AutoPlayController.NextInputSearchSelection(frame));
        AutoPlayInputSearchFrame cold = JsonSerializer.Deserialize<AutoPlayInputSearchFrame>(JsonSerializer.Serialize(frame, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        Assert.Equal(3, AutoPlayController.NextInputSearchSelection(cold));
    }

    [Fact]
    public void OnlyTheFirstActuallySampledDomainAndMaskPairIsNewKnowledgeAcrossOccurrences()
    {
        AutoPlayInputSearch search = new();
        AutoPlayInputSearchFrame first = Frame("first");
        AutoPlayInputSearchFrame later = Frame("later");
        AutoPlayInputSearchTrial trial = new() { Selection = 0, Mask = 1, Sampled = true, SampledQuery = Query };

        Assert.True(AutoPlayController.RecordInputSearchKnowledge(search, first, trial));
        Assert.False(AutoPlayController.RecordInputSearchKnowledge(search, later, trial));
        Assert.Equal(1, search.DistinctSampledPhysicalEdges);
        Assert.True(AutoPlayController.RecordInputSearchKnowledge(search, later, new() { Selection = 1, Mask = 2, Sampled = true, SampledQuery = Query }));
        Assert.Equal(2, search.DistinctSampledPhysicalEdges);
    }

    [Fact]
    public void UnsampledInputAndUnprovedQueriesDoNotCountAsNewKnowledge()
    {
        AutoPlayInputSearch search = new();
        AutoPlayInputSearchFrame frame = Frame("first");

        Assert.False(AutoPlayController.RecordInputSearchKnowledge(search, frame, new() { Selection = 0, Mask = 1 }));
        Assert.False(AutoPlayController.RecordInputSearchKnowledge(search, frame, new() { Selection = 0, Mask = 1, Sampled = true }));
        Assert.False(AutoPlayController.RecordInputSearchKnowledge(search, frame, new() { Selection = 0, Mask = 1, Sampled = true, SampledQuery = Query with { Operation = 99 } }));
        Assert.Empty(search.SampledPhysicalEdges);
        Assert.Equal(0, search.DistinctSampledPhysicalEdges);
    }

    [Fact]
    public void ColdKnowledgeAndReceiptBackedLegacyImportNeverRecountOldMasksAsProgress()
    {
        AutoPlayInputSearchFrame frame = Frame("first");
        AutoPlayInputSearchTrial sampled = new() { Selection = 0, Mask = 1, Sampled = true, SampledQuery = Query };
        frame.Trials.Add(sampled);
        frame.Trials.Add(new() { Selection = 1, Mask = 2 });
        AutoPlayInputSearch search = new() { Frames = [frame], TrialCount = 2 };
        Assert.True(AutoPlayController.RecordInputSearchKnowledge(search, frame, sampled));
        AutoPlayInputSearch cold = JsonSerializer.Deserialize<AutoPlayInputSearch>(JsonSerializer.Serialize(search, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        AutoPlayController.ValidateInputSearchLedger(cold);
        Assert.False(AutoPlayController.RecordInputSearchKnowledge(cold, cold.Frames[0], cold.Frames[0].Trials[0]));
        Assert.Equal(1, cold.DistinctSampledPhysicalEdges);

        cold.SampledPhysicalEdges.Clear();
        cold.DistinctSampledPhysicalEdges = 0;
        AutoPlayController.RestoreInputSearchKnowledge(cold, ledgerPresent: false);

        Assert.Equal(1, cold.DistinctSampledPhysicalEdges);
        Assert.False(AutoPlayController.RecordInputSearchKnowledge(cold, cold.Frames[0], cold.Frames[0].Trials[0]));
        Assert.True(AutoPlayController.RecordInputSearchKnowledge(cold, cold.Frames[0], new() { Selection = 1, Mask = 2, Sampled = true, SampledQuery = Query }));
    }

    [Fact]
    public void ColdLedgerKeepsTheFailedTrialAndSelectsADifferentPhysicalSiblingWithoutCoverageCredit()
    {
        AutoPlayInputSearchFrame frame = Frame("parent");
        frame.Trials.Add(new()
        {
            Selection = 0,
            Mask = 1,
            Sampled = true,
            SampledQuery = Query,
            Outcome = AutoPlayInputTrialOutcome.NativeLoss,
            Loss = new(1, 100, new string('a', 64), NativeFieldRuntimeProfile.FF8EnExecutableHash, 2, 3, 4, 10, 4)
        });
        frame.ActiveTrial = 0;
        AutoPlayRunState run = new() { InputSearch = new() { ActiveFrameId = frame.Id, TrialCount = 1, Frames = [frame] } };
        AutoPlayController.RecordInputSearchKnowledge(run.InputSearch, frame, frame.Trials[0]);
        AutoPlayRunState cold = JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(run, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;

        AutoPlayController.ValidateInputSearchLedger(cold.InputSearch!);

        AutoPlayInputSearchFrame restored = Assert.Single(cold.InputSearch!.Frames);
        Assert.Equal(1, AutoPlayController.NextInputSearchSelection(restored));
        Assert.Equal(2, restored.Input!.Masks[AutoPlayController.NextInputSearchSelection(restored)!.Value]);
        Assert.Equal(AutoPlayInputTrialOutcome.NativeLoss, Assert.Single(restored.Trials).Outcome);
        Assert.Empty(cold.Coverage.Choices);
    }

    [Fact]
    public void ARepeatedObservedContextRetainsItsDistinctGenuineChildOccurrence()
    {
        AutoPlayInputSearchFrame parent = Frame("parent");
        AutoPlayInputSearchFrame child = Frame("child");
        child.ParentId = parent.Id;
        child.ParentTrial = 0;
        parent.Trials.Add(new() { Selection = 0, Mask = 1, Sampled = true, SampledQuery = Query, Outcome = AutoPlayInputTrialOutcome.ReturnedToObservedContext, ChildId = child.Id });
        parent.ActiveTrial = 0;
        AutoPlayInputSearch search = new() { ActiveFrameId = parent.Id, TrialCount = 1, Frames = [parent, child] };
        AutoPlayController.RecordInputSearchKnowledge(search, parent, parent.Trials[0]);

        AutoPlayController.ValidateInputSearchLedger(search);

        Assert.Equal(parent.Key, child.Key);
        Assert.NotEqual(parent.Checkpoint, child.Checkpoint);
        Assert.Equal(1, AutoPlayController.NextInputSearchSelection(parent));
        Assert.Equal(0, AutoPlayController.NextInputSearchSelection(child));
        Assert.False(child.Exhausted);
    }

    [Theory]
    [InlineData("parentTrial")]
    [InlineData("childLink")]
    [InlineData("duplicateTrial")]
    [InlineData("mask")]
    [InlineData("domain")]
    [InlineData("sample")]
    [InlineData("loss")]
    public void CorruptColdEdgesAreRejectedBeforeAnyNativeRestore(string corruption)
    {
        AutoPlayInputSearchFrame parent = Frame("parent");
        AutoPlayInputSearchFrame child = Frame("child");
        child.ParentId = parent.Id;
        child.ParentTrial = 0;
        parent.Trials.Add(new() { Selection = 0, Mask = 1, Sampled = true, SampledQuery = Query, Outcome = AutoPlayInputTrialOutcome.ReachedNewBoundary, ChildId = child.Id });
        parent.ActiveTrial = 0;
        AutoPlayInputSearch search = new() { ActiveFrameId = child.Id, TrialCount = 1, Frames = [parent, child] };
        AutoPlayController.RecordInputSearchKnowledge(search, parent, parent.Trials[0]);
        switch (corruption)
        {
            case "parentTrial":
                child.ParentTrial = 5;
                break;
            case "childLink":
                parent.Trials[0].ChildId = "missing";
                break;
            case "duplicateTrial":
                parent.Trials.Add(new() { Selection = 0, Mask = 1 });
                search.TrialCount++;
                break;
            case "mask":
                parent.Trials[0].Mask = 2;
                break;
            case "domain":
                parent.Options.Reverse();
                break;
            case "sample":
                parent.Trials[0].SampledQuery = Query with { Operation = 99 };
                break;
            case "loss":
                parent.Trials[0].Outcome = AutoPlayInputTrialOutcome.NativeLoss;
                break;
        }

        Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateInputSearchLedger(search));
    }

    [Fact]
    public void ReciprocalButCyclicParentEdgesAreRejected()
    {
        AutoPlayInputSearchFrame first = Frame("first");
        AutoPlayInputSearchFrame second = Frame("second");
        first.ParentId = second.Id;
        first.ParentTrial = 0;
        second.ParentId = first.Id;
        second.ParentTrial = 0;
        first.Trials.Add(new() { Selection = 0, Mask = 1, ChildId = second.Id });
        second.Trials.Add(new() { Selection = 0, Mask = 1, ChildId = first.Id });
        AutoPlayInputSearch search = new() { TrialCount = 2, Frames = [first, second], ActiveFrameId = first.Id };

        Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateInputSearchLedger(search));
    }

    [Fact]
    public void AChangedNativeCheckpointIsRejectedByItsOriginalByteIdentity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-input-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "parent.json");
        try
        {
            File.WriteAllText(path, "original standalone bytes");
            AutoPlayInputSearchFrame frame = Frame("parent");
            frame.Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            File.WriteAllText(path, "changed standalone bytes");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateInputSearchCheckpoint(frame, directory));

            Assert.Contains("byte identity", exception.Message);
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportedCheckpointValidatesOwnersAfterReadingAcrossStreamBuffers(bool changedOwner)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-input-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "parent.json");
        try
        {
            AutoPlayInputSearchFrame frame = Frame("parent");
            frame.FieldId = 100;
            frame.ProgramHash = "observed-program";
            using (FileStream stream = File.Create(path))
            {
                JsonSerializer.Serialize(stream, new
                {
                    UnusedOwnerData = Enumerable.Repeat("unused owner data", 8192).ToArray(),
                    FieldId = frame.FieldId,
                    FieldButtonInput = new NativeFieldButtonInput(3, 0, 0, 42, changedOwner ? 101 : frame.FieldId, 0, 0, 0, 0),
                    EventState = new { ProgramHash = frame.ProgramHash, Frame = 42 }
                });
            }
            using (FileStream stream = File.OpenRead(path))
            {
                frame.Sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }

            if (changedOwner)
            {
                InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateImportedInputSearchCheckpoint(frame, directory));
                Assert.Contains("prepared input owner", exception.Message);
            }
            else
            {
                AutoPlayController.ValidateImportedInputSearchCheckpoint(frame, directory);
                File.AppendAllText(path, " ");
                InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AutoPlayController.ValidateImportedInputSearchCheckpoint(frame, directory));
                Assert.Contains("byte identity", exception.Message);
            }
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static AutoPlayInputSearchFrame Frame(string id)
    {
        return new()
        {
            Id = id,
            Checkpoint = id + ".json",
            Key = "input:100:observed",
            Input = new("observed", [1, 2], [Query]) { ObservedQueriesProven = true },
            Prepared = true,
            Options = [0, 1]
        };
    }
}
