using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCompatibilityProjectionTests
{
    [Theory]
    [InlineData(Jsm.Opcode.MES, 2)]
    [InlineData(Jsm.Opcode.MESW, 2)]
    [InlineData(Jsm.Opcode.AMES, 4)]
    [InlineData(Jsm.Opcode.AMESW, 4)]
    [InlineData(Jsm.Opcode.RAMESW, 4)]
    [InlineData(Jsm.Opcode.ASK, 6)]
    [InlineData(Jsm.Opcode.AASK, 8)]
    public void PresentationOperandsDoNotBlockTextIdentity(Jsm.Opcode opcode, int count)
    {
        int[] left = Enumerable.Range(0, count).Select(index => Push(index == 1 ? 0 : index)).Append((int)opcode).ToArray();
        int[] right = Enumerable.Range(0, count).Select(index => Push(index == 1 ? 0 : 80 + index)).Append((int)opcode).ToArray();
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Equal([0, 1], result.ProvenOrdinalMatches.Select(match => match.EnglishOrdinal));
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.NotEqual(result.English.JsmSha256, result.Localized.JsmSha256);
        Assert.NotEqual(result.English.Contexts[0].Operands[0], result.Localized.Contexts[0].Operands[0]);
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
        Assert.NotNull(result.FirstDivergence);
        Assert.True(result.MessageIdentityEvidence!.StreamEqual);
        Assert.True(result.MessageIdentityEvidence.OrdinalIdentityProven);
    }

    [Fact]
    public void CompleteCalculatedArgumentsNormalizeAcrossDifferentExpressionLengths()
    {
        int[] left = [Push(0), Push(0), Push(10), Push(20), (int)Jsm.Opcode.AMES];
        int[] right = [Push(1), Push(0), Push(20), Push(22), Calculate(0), Push(50), (int)Jsm.Opcode.AMES];
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Equal(2, result.ProvenOrdinalMatches.Length);
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.Equal(result.English.Instructions.Length, result.MessageIdentityEvidence!.EnglishBoundaryOperationIndex);
        Assert.Equal(result.Localized.Instructions.Length, result.MessageIdentityEvidence.LocalBoundaryOperationIndex);
        Assert.NotEqual(result.MessageIdentityEvidence.EnglishBoundaryOperationIndex, result.MessageIdentityEvidence.LocalBoundaryOperationIndex);
        Assert.Null(result.MessageIdentityEvidence.EnglishPosition);
        Assert.Null(result.MessageIdentityEvidence.LocalPosition);
    }

    [Fact]
    public void ProjectedPrefixBoundariesRemainSeparateFromOriginalExecutableDivergence()
    {
        int[] left = new[] { Push(0), Push(0), Push(10), Push(20), (int)Jsm.Opcode.AMES }.Concat(Message(1)).ToArray();
        int[] right = new[] { Push(1), Push(0), Push(20), Push(22), Calculate(0), Push(50), (int)Jsm.Opcode.AMES }.Concat(Message(0)).ToArray();
        FieldCompatibilityResult result = Compare(left, right);
        FieldMessageIdentityEvidence evidence = Assert.IsType<FieldMessageIdentityEvidence>(result.MessageIdentityEvidence);
        Assert.True(evidence.ProjectionValidated);
        Assert.False(evidence.StreamEqual);
        Assert.False(evidence.OrdinalIdentityProven);
        Assert.Equal(2, evidence.MatchedTokens);
        Assert.Equal(6, evidence.EnglishBoundaryOperationIndex);
        Assert.Equal(8, evidence.LocalBoundaryOperationIndex);
        Assert.Equal(6, evidence.EnglishPosition!.OperationIndex);
        Assert.Equal(8, evidence.LocalPosition!.OperationIndex);
        Assert.Equal(1, result.FirstDivergence!.EnglishPosition!.OperationIndex);
        Assert.Equal(1, result.FirstDivergence.LocalPosition!.OperationIndex);
        Assert.Equal([0], result.ProvenOrdinalMatches.Select(match => match.EnglishOrdinal));
        Assert.Equal([1], result.EnglishSuffixContexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
        Assert.Equal([0], result.LocalSuffixContexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
    }

    [Fact]
    public void BranchTargetAndScriptEndRelocateOnlyThroughMatchingProjectionBoundaries()
    {
        int[] left = [Branch(Jsm.Opcode.JMP, 6), Push(0), Push(0), Push(10), Push(20), (int)Jsm.Opcode.AMES, (int)Jsm.Opcode.NOP];
        int[] right = [Branch(Jsm.Opcode.JMP, 8), Push(0), Push(0), Push(10), Push(20), Calculate(0), Push(40), (int)Jsm.Opcode.AMES, (int)Jsm.Opcode.NOP];
        // The branch reaches the retained NOP after the complete message span.
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Equal(2, result.ProvenOrdinalMatches.Length);
    }

    [Fact]
    public void TargetInsideArgumentSpanPreventsItsRemoval()
    {
        int[] left = [Branch(Jsm.Opcode.JMP, 3), Push(0), Push(0), Push(10), Push(20), (int)Jsm.Opcode.AMES];
        int[] right = [Branch(Jsm.Opcode.JMP, 3), Push(0), Push(0), Push(11), Push(20), (int)Jsm.Opcode.AMES];
        FieldCompatibilityResult result = Compare(left, right);
        Assert.NotEmpty(result.PotentialLocalOrdinals);
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void FaultingPresentationCalculationCannotEstablishTextIdentity()
    {
        int[] left = [Push(0), Push(0), Push(10), Push(0), Calculate(3), Push(20), (int)Jsm.Opcode.AMES];
        int[] right = [Push(0), Push(0), Push(10), Push(1), Calculate(3), Push(20), (int)Jsm.Opcode.AMES];
        FieldCompatibilityResult result = Compare(left, right);
        Assert.NotEmpty(result.PotentialLocalOrdinals);
    }

    [Fact]
    public void NonpresentationDifferenceStopsPrefixWithoutSuffixResynchronization()
    {
        int[] left = Message(0).Append((int)Jsm.Opcode.NOP).Concat(Message(1)).ToArray();
        int[] right = Message(0).Concat(Message(1)).ToArray();
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Equal([0], result.ProvenOrdinalMatches.Select(match => match.EnglishOrdinal));
        Assert.Contains(1, result.PotentialLocalOrdinals);
    }

    [Fact]
    public void ChangedMessageIdDoesNotNormalizeOrResynchronize()
    {
        FieldCompatibilityResult result = Compare(Message(0).Concat(Message(1)).ToArray(), Message(1).Concat(Message(1)).ToArray());
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1], result.PotentialLocalOrdinals);
    }

    [Fact]
    public void UnknownMessageIdExpressionRemainsExecutableEvidence()
    {
        int[] left = [Push(0), ((int)Jsm.Opcode.PSHM_W << 24) | 10, (int)Jsm.Opcode.MES];
        int[] right = [Push(0), ((int)Jsm.Opcode.PSHM_W << 24) | 11, (int)Jsm.Opcode.MES];
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1], result.PotentialLocalOrdinals);
    }

    [Fact]
    public void MissingArgumentsCannotBeReplacedByMessageMarker()
    {
        FieldCompatibilityResult result = Compare([Push(0), (int)Jsm.Opcode.MES], [Push(1), (int)Jsm.Opcode.MES]);
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1], result.PotentialLocalOrdinals);
    }

    [Fact]
    public void ChangedBranchDestinationIsNotAStorageRelocation()
    {
        int[] left = [Branch(Jsm.Opcode.JMP, 4), Push(0), Push(0), (int)Jsm.Opcode.MES, (int)Jsm.Opcode.NOP];
        int[] right = [Branch(Jsm.Opcode.JMP, 5), Push(0), Push(0), (int)Jsm.Opcode.MES, (int)Jsm.Opcode.NOP];
        FieldCompatibilityResult result = Compare(left, right);
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.NotEmpty(result.PotentialLocalOrdinals);
    }

    private static FieldCompatibilityResult Compare(int[] left, int[] right) => FieldCompatibilityAudit.Compare(Input(Script(left), "en"), Input(Script(right), "de"));

    private static FieldCompatibilityInput Input(byte[] script, string language) => new("fixture", language, "synthetic/" + language, script, [new(0, "a"), new(1, "b")]);

    private static int[] Message(int id) => [Push(0), Push(id), (int)Jsm.Opcode.MES];

    private static int Push(int value) => ((int)Jsm.Opcode.PSHN_L << 24) | (value & 0xFFFFFF);

    private static int Branch(Jsm.Opcode opcode, int displacement) => ((int)opcode << 24) | (displacement & 0xFFFFFF);

    private static int Calculate(int kind) => ((int)Jsm.Opcode.CAL << 24) | kind;

    private static byte[] Script(int[] body)
    {
        int[] operations = new[] { (int)Jsm.Opcode.LBL << 24 }.Concat(body).Append((int)Jsm.Opcode.RET).ToArray();
        byte[] data = new byte[14 + operations.Length * 4];
        data[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(14 + index * 4), operations[index]);
        }
        return data;
    }
}
