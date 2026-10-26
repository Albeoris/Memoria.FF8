using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCompatibilityAuditTests
{
    [Fact]
    public void IdenticalScriptsProveAllSharedOrdinalsIncludingUnusedEmptyRows()
    {
        byte[] script = Script(Message(0));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "Hello", "{End}", "unused"), Input(script, "de", "Hallo", "{End}", "unbenutzt"));
        Assert.Equal(FieldCompatibilityKind.ByteIdentical, result.Kind);
        Assert.Equal([0, 1, 2], result.ProvenOrdinalMatches.Select(match => match.EnglishOrdinal));
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.Equal([1], result.Localized.EmptyOrdinals);
        Assert.Equal([1, 2], result.Localized.UnreferencedOrdinals);
        Assert.Contains("ByteIdentical", JsonSerializer.Serialize(result, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
    }

    [Fact]
    public void PaddingOnlyRelocationPreservesEveryEncodedOperation()
    {
        byte[] left = Script(Message(0));
        byte[] right = Script(Message(0), 8);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(left, "en", "a"), Input(right, "fr", "b"));
        Assert.Equal(FieldCompatibilityKind.StorageOnly, result.Kind);
        Assert.NotEqual(result.English.JsmSha256, result.Localized.JsmSha256);
        Assert.Single(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void ChangedOperandDoesNotResynchronizeAtLaterIdenticalMessages()
    {
        int[] left = Message(0).Concat(Message(1)).Concat(Message(2)).ToArray();
        int[] right = Message(0).Concat(Message(2)).Concat(Message(2)).ToArray();
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(left), "en", "a", "b", "c", "unused"), Input(Script(right), "ja", "a", "b", "c", "unused"));
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
        Assert.NotNull(result.FirstDivergence?.EnglishPosition);
        Assert.Equal([1, 2], result.EnglishSuffixContexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
        Assert.Equal([2, 2], result.LocalSuffixContexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
        Assert.Equal([1, 2, 3], result.PotentialLocalOrdinals);
        Assert.Equal([0], result.ProvenOrdinalMatches.Select(match => match.LocalOrdinal));
    }

    [Fact]
    public void MissingAndUndecodableResourcesKeepKnownCandidateBounds()
    {
        FieldCompatibilityInput missing = new("fixture", "ja", "missing", null, null, ["Decode failed"], 3);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(Message(0)), "en", "a", "b"), missing);
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.Equal([0, 1, 2], result.PotentialLocalOrdinals);
        Assert.True(result.CandidateCountKnown);
        Assert.Equal([0, 1], result.ImplicatedEnglishOrdinals);
        Assert.Null(result.FirstDivergence?.LocalPosition);
    }

    [Fact]
    public void DynamicMessageKeepsAllBankCandidatesAndNoExactCompatibility()
    {
        int[] body = [Push(0), ((int)Jsm.Opcode.PSHM_W << 24) | 42, (int)Jsm.Opcode.MES];
        byte[] script = Script(body);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "a", "b"), Input(script, "it", "c", "d"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        FieldCompatibilityContext context = Assert.Single(result.Localized.Contexts);
        Assert.True(context.Dynamic);
        Assert.Null(context.MessageId);
        Assert.Equal([0, 1], context.CandidateOrdinals);
        Assert.Equal(2, result.ContextOnlyProblems);
    }

    [Fact]
    public void ExtraUnusedBankRowsAreUnmatchedWithoutSemanticGuessing()
    {
        byte[] script = Script(Message(0));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "a"), Input(script, "es", "a", "extra"));
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
        Assert.Single(result.ProvenOrdinalMatches);
        Assert.Equal([1], result.PotentialLocalOrdinals);
        Assert.Empty(result.ImplicatedEnglishOrdinals);
    }

    [Fact]
    public void PrefixRowReusedAfterDivergenceRemainsCandidate()
    {
        int[] left = Message(0).Concat(Message(1)).Concat(Message(0)).ToArray();
        int[] right = Message(0).Concat(Message(2)).Concat(Message(0)).ToArray();
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(left), "en", "a", "b", "c"), Input(Script(right), "de", "d", "e", "f"));
        Assert.Contains(0, result.PotentialLocalOrdinals);
        Assert.Contains(0, result.ImplicatedEnglishOrdinals);
        Assert.Equal(5, result.FirstDivergence!.EnglishPosition!.OperationIndex);
    }

    [Fact]
    public void LaterScriptLengthDifferenceRetainsActualInstructionPrefix()
    {
        int[] left = Message(0).Concat(Message(1)).ToArray();
        int[] right = Message(0).Append((int)Jsm.Opcode.NOP).Concat(Message(1)).ToArray();
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(left), "en", "a", "b"), Input(Script(right), "de", "c", "d"));
        Assert.Equal(4, result.FirstDivergence!.EnglishPosition!.OperationIndex);
        Assert.Equal([1], result.PotentialLocalOrdinals);
        Assert.Equal([0], result.ProvenOrdinalMatches.Select(match => match.LocalOrdinal));
    }

    [Fact]
    public void EarlierCategoryDifferenceInvalidatesPrefixDespiteLaterOperandDivergence()
    {
        byte[] english = Script(Message(0).Concat(Message(1)).ToArray());
        byte[] localized = Script(Message(0).Concat(Message(2)).ToArray());
        localized[3] = 0;
        localized[0] = 1;
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(english, "en", "a", "b", "c"), Input(localized, "de", "d", "e", "f"));
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1, 2], result.PotentialLocalOrdinals);
        Assert.Equal(0, result.FirstDivergence!.StructuralBoundaryOperationIndex);
        Assert.Equal(5, result.FirstDivergence.EnglishPosition!.OperationIndex);
        Assert.Equal(2, result.EnglishSuffixContexts.Count(context => context.IsMessage));
    }

    [Fact]
    public void EarlierPointerFlagDifferenceInvalidatesPrefixDespiteLaterOperandDivergence()
    {
        byte[] english = Script(Message(0).Concat(Message(1)).ToArray());
        byte[] localized = Script(Message(0).Concat(Message(2)).ToArray());
        BinaryPrimitives.WriteUInt16LittleEndian(localized.AsSpan(10), 0x8000);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(english, "en", "a", "b", "c"), Input(localized, "de", "d", "e", "f"));
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1, 2], result.PotentialLocalOrdinals);
        Assert.Equal(0, result.FirstDivergence!.StructuralBoundaryOperationIndex);
        Assert.Equal(5, result.FirstDivergence.EnglishPosition!.OperationIndex);
    }

    [Fact]
    public void ExactIdentityKeepsPresentationEvidenceWithoutTextConflictCases()
    {
        int[] body = Message(0).Concat(new[] { Push(1), Push(0), (int)Jsm.Opcode.MES }).ToArray();
        byte[] script = Script(body);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "a"), Input(script, "fr", "b"));
        Assert.Equal(FieldCompatibilityKind.ByteIdentical, result.Kind);
        Assert.Single(result.ProvenOrdinalMatches);
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.Equal(0, result.ContextOnlyProblems);
        Assert.Empty(result.ReviewCases);
        Assert.Equal(2, result.Localized.Contexts.Select(context => context.Operands[0]).Distinct().Count());
    }

    [Fact]
    public void EventEngineGapRameswAndWindowChoiceContextRemainIndexed()
    {
        int[] body = Enumerable.Range(0, 5).Select(Push).Append((int)Jsm.Opcode.WINSIZE)
            .Concat(new[] { Push(0), Push(0), Push(50), Push(80), (int)Jsm.Opcode.RAMESW })
            .Concat(Enumerable.Range(0, 8).Select(index => Push(index == 1 ? 0 : index)))
            .Append((int)Jsm.Opcode.AASK).Append(Push(0)).Append((int)Jsm.Opcode.MESSYNC).ToArray();
        FieldCompatibilityInventory inventory = FieldCompatibilityAudit.Inspect(Input(Script(body), "en", "a"));
        Assert.Empty(inventory.Problems);
        Assert.Equal(["WINSIZE", "RAMESW", "AASK", "MESSYNC"], inventory.Contexts.Select(context => context.Opcode));
        Assert.All(inventory.Contexts, context => Assert.True(context.Position.ByteOffset > 0));
        Assert.Equal(8, inventory.Contexts[2].Operands.Length);
    }

    [Fact]
    public void TruncatedAndUnknownInstructionsCannotEstablishEquality()
    {
        byte[] malformed = [0, 1, 2];
        Assert.Equal(FieldCompatibilityKind.Incomplete, FieldCompatibilityAudit.Compare(Input(malformed, "en", "a"), Input(malformed, "de", "b")).Kind);
        byte[] unknown = Script([0x7FFFFF]);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(unknown, "en", "a"), Input(unknown, "de", "b"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.NotEmpty(result.PotentialLocalOrdinals);
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void ReviewCaseCountMatchesStableCaseInventoryAndDeduplicatesChoiceUses()
    {
        int[] choice = new[] { 0, 0, 1, 2, 1, 1 }.Select(Push).Append((int)Jsm.Opcode.ASK).ToArray();
        int[] body = choice.Concat(choice).ToArray();
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(body), "en", "a"), Input(Script(new[] { (int)Jsm.Opcode.NOP }.Concat(body).ToArray()), "fr", "b"));
        Assert.Equal(result.ReviewCases.Length, result.SemanticReviewCases);
        Assert.Equal(2, result.ReviewCases.Count(review => review.Kind == "ChoiceGroup"));
        Assert.All(result.ReviewCases.Where(review => review.Kind == "ChoiceGroup"), review => Assert.Equal(2, review.EnglishPositions.Length + review.LocalPositions.Length));
        Assert.Equal(result.ReviewCases.Length, result.ReviewCases.Select(review => review.Id).Distinct().Count());
    }

    [Fact]
    public void SameMessageIdWithDifferentObservedWindowStateSharesChoiceCase()
    {
        int[] choice = new[] { 0, 0, 1, 2, 1, 1 }.Select(Push).Append((int)Jsm.Opcode.ASK).ToArray();
        int[] windowA = new[] { 0, 10, 20, 100, 40 }.Select(Push).Append((int)Jsm.Opcode.WINSIZE).ToArray();
        int[] windowB = new[] { 0, 30, 40, 100, 40 }.Select(Push).Append((int)Jsm.Opcode.WINSIZE).ToArray();
        int[] body = windowA.Concat(choice).Concat(windowB).Concat(choice).ToArray();
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(body), "en", "a"), Input(Script(new[] { (int)Jsm.Opcode.NOP }.Concat(body).ToArray()), "fr", "b"));
        Assert.Equal(2, result.ReviewCases.Count(review => review.Kind == "ChoiceGroup"));
        Assert.Equal(2, result.Localized.Contexts.Where(context => context.IsMessage).Select(context => context.PresentationStateFingerprint).Distinct().Count());
        Assert.All(result.Localized.Contexts, context => Assert.StartsWith("EntryAndControlFlowStateUnresolved:", context.PresentationStateFingerprint));
    }

    [Fact]
    public void InstructionPointerDomainOverflowCannotEstablishCompatibility()
    {
        byte[] script = Script([]);
        Array.Resize(ref script, 14 + (ushort.MaxValue + 2) * 4);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "a"), Input(script, "de", "b"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.Contains(result.English.Problems, problem => problem.Contains("instruction-pointer domain", StringComparison.Ordinal));
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void DuplicateScriptLabelsCannotEstablishCompatibility()
    {
        byte[] script = new byte[34];
        script[3] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(6), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(14), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(16), 4);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(18), (int)Jsm.Opcode.LBL << 24);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(22), (int)Jsm.Opcode.RET);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(26), (int)Jsm.Opcode.LBL << 24);
        BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(30), (int)Jsm.Opcode.RET);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en", "a"), Input(script, "de", "b"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.Contains(result.English.Problems, problem => problem.Contains("script labels", StringComparison.Ordinal));
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    private static FieldCompatibilityInput Input(byte[] script, string language, params string[] texts) => new("fixture", language, "synthetic/" + language, script, texts.Select((text, ordinal) => new FieldCompatibilityText(ordinal, text)).ToArray());

    private static int[] Message(int id) => [Push(0), Push(id), (int)Jsm.Opcode.MES];

    private static int Push(int value) => ((int)Jsm.Opcode.PSHN_L << 24) | (value & 0xFFFFFF);

    private static byte[] Script(int[] body, int padding = 0)
    {
        int[] operations = new[] { (int)Jsm.Opcode.LBL << 24 }.Concat(body).Append((int)Jsm.Opcode.RET).ToArray();
        int operationsOffset = 14 + padding;
        byte[] data = new byte[operationsOffset + operations.Length * 4];
        data[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), (ushort)operationsOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(operationsOffset + index * 4), operations[index]);
        }
        return data;
    }
}
