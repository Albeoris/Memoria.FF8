using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldScriptCompatibilityTests
{
    [Fact]
    public void EarlierChangedScriptDoesNotImplicateLaterIdenticalMessage()
    {
        FieldCompatibilityResult result = Compare(Script(new Group(0, [Message(0), Message(1)])), Script(new Group(0, [[(int)Jsm.Opcode.NOP, .. Message(0)], Message(1)])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 1).StreamEqual);
        Assert.Contains(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 1);
        Assert.DoesNotContain(1, result.PotentialLocalOrdinals);
        Assert.DoesNotContain(1, result.ImplicatedEnglishOrdinals);
        Assert.DoesNotContain(result.LocalSuffixContexts, context => context.IsMessage && context.MessageId == 1);
    }

    [Fact]
    public void EarlierEqualScriptSurvivesLaterGroupInsertion()
    {
        FieldCompatibilityResult result = Compare(Script(new Group(0, [Message(0)])), Script(new Group(0, [Message(0)]), new Group(10, [Message(1)])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 0).StreamEqual);
        Assert.False(Evidence(result, 10, 10).Validated);
        Assert.Contains(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 0);
        Assert.DoesNotContain(0, result.PotentialLocalOrdinals);
        Assert.Contains(1, result.PotentialLocalOrdinals);
    }

    [Fact]
    public void MessageReusedInChangedScriptRemainsCandidateOnBothSides()
    {
        FieldCompatibilityResult result = Compare(Script(new Group(0, [Message(1), Message(1)])), Script(new Group(0, [Message(1), [(int)Jsm.Opcode.NOP, .. Message(1)]])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 0).StreamEqual);
        Assert.Contains(1, result.PotentialLocalOrdinals);
        Assert.Contains(1, result.ImplicatedEnglishOrdinals);
        Assert.DoesNotContain(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 1);
    }

    [Fact]
    public void ReuseOnOnlyOneSideStillPreventsOrdinalProofOnBothSides()
    {
        FieldCompatibilityResult result = Compare(Script(new Group(0, [Message(1), Message(2)])), Script(new Group(0, [Message(1), Message(1)])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 0).StreamEqual);
        Assert.Contains(1, result.PotentialLocalOrdinals);
        Assert.Contains(1, result.ImplicatedEnglishOrdinals);
        Assert.DoesNotContain(result.ProvenOrdinalMatches, match => match.EnglishOrdinal == 1 || match.LocalOrdinal == 1);
    }

    [Fact]
    public void ProvenMessageRowsDoNotRemoveDynamicNonMessageReviewCases()
    {
        int[] body = [Push(0), Encode(Jsm.Opcode.PSHM_B, 20), Push(20), Push(100), Push(40), (int)Jsm.Opcode.WINSIZE, .. Message(0)];
        byte[] english = Script(new Group(0, [body, []]));
        byte[] local = Script(new Group(0, [body, [(int)Jsm.Opcode.NOP]]));
        FieldCompatibilityText[] bank = [new(0, "Synthetic")];
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(new FieldCompatibilityInput("fixture", "en", "synthetic", english, bank), new FieldCompatibilityInput("fixture", "de", "synthetic", local, bank));

        AssertValidated(result);
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.True(Evidence(result, 0, 0).StreamEqual);
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.Empty(result.ImplicatedEnglishOrdinals);
        Assert.Equal(0, Assert.Single(result.ProvenOrdinalMatches).LocalOrdinal);
        Assert.Contains(result.English.Contexts, context => context.Opcode == "WINSIZE" && !context.IsMessage && context.Dynamic);
        Assert.Contains(result.ReviewCases, review => review.Kind == "DynamicContext" && !review.EnglishPositions.IsEmpty);
        Assert.Contains(result.ReviewCases, review => review.Kind == "DynamicContext" && !review.LocalPositions.IsEmpty);
    }

    [Fact]
    public void UnknownMessageInChangedScriptPreventsPruningOtherScriptRows()
    {
        int[] unknown = [Push(0), Encode(Jsm.Opcode.PSHM_W, 20), (int)Jsm.Opcode.MES];
        FieldCompatibilityResult result = Compare(Script(new Group(0, [unknown, Message(1)])), Script(new Group(0, [[(int)Jsm.Opcode.NOP, .. unknown], Message(1)])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 1).StreamEqual);
        Assert.False(result.Localized.StaticReferencesValidated);
        Assert.Equal([0, 1, 2, 3], result.PotentialLocalOrdinals);
        Assert.Equal([0, 1, 2, 3], result.ImplicatedEnglishOrdinals);
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void LocalBranchTargetsSurviveIndependentPriorScriptLengthChanges()
    {
        int[] branched = [Encode(Jsm.Opcode.JMP, 1), .. Message(1)];
        FieldCompatibilityResult result = Compare(Script(new Group(0, [Message(0), branched])), Script(new Group(0, [[(int)Jsm.Opcode.NOP, .. Message(0)], branched])));

        AssertValidated(result);
        FieldScriptIdentityEvidence evidence = Evidence(result, 0, 1);
        Assert.True(evidence.Validated);
        Assert.True(evidence.StreamEqual);
        Assert.NotEqual(evidence.EnglishBoundaryOperationIndex, evidence.LocalBoundaryOperationIndex);
        Assert.DoesNotContain(1, result.PotentialLocalOrdinals);
        Assert.DoesNotContain(1, result.ImplicatedEnglishOrdinals);
    }

    [Theory]
    [InlineData("pointer-flag")]
    [InlineData("category")]
    [InlineData("native-object-index")]
    public void NativeIdentityChangesPreventScriptProof(string change)
    {
        byte[] english = Script(new Group(10, [Message(1)]));
        byte[] local = Script(new Group(10, [[(int)Jsm.Opcode.NOP, .. Message(1)]]));
        if (change == "pointer-flag")
        {
            int pointers = BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(4));
            BinaryPrimitives.WriteUInt16LittleEndian(local.AsSpan(pointers), 0x8000);
        }
        else if (change == "category")
        {
            local[0] = 1;
            local[3] = 0;
        }
        else
        {
            local = Script(new Group(0, [[]]), new Group(10, [Message(1)]));
        }
        FieldCompatibilityResult result = Compare(english, local);

        AssertValidated(result);
        Assert.False(Evidence(result, 10, 10).Validated);
        Assert.Contains(1, result.PotentialLocalOrdinals);
        Assert.Contains(1, result.ImplicatedEnglishOrdinals);
        Assert.DoesNotContain(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossScriptBranchesRemainConservative(bool interiorEntry)
    {
        int displacement = interiorEntry ? 3 : 2;
        int[] branch = [Encode(Jsm.Opcode.JMP, displacement)];
        FieldCompatibilityResult result = Compare(Script(new Group(0, [branch, Message(1)])), Script(new Group(0, [[(int)Jsm.Opcode.NOP, .. branch], Message(1)])));

        AssertValidated(result);
        Assert.False(Evidence(result, 0, 0).Validated);
        Assert.Equal(!interiorEntry, Evidence(result, 0, 1).Validated);
        Assert.Equal(interiorEntry, result.PotentialLocalOrdinals.Contains(1));
        Assert.Equal(interiorEntry, result.ImplicatedEnglishOrdinals.Contains(1));
    }

    [Fact]
    public void FirstDifferenceForbidsSameScriptSuffixResynchronizationButOtherScriptsMatch()
    {
        int[] english = [.. Message(0), (int)Jsm.Opcode.NOP, .. Message(1)];
        int[] local = [.. Message(0), (int)Jsm.Opcode.NOP, (int)Jsm.Opcode.NOP, .. Message(1)];
        FieldCompatibilityResult result = Compare(Script(new Group(0, [english, Message(2)])), Script(new Group(0, [local, Message(2)])));

        AssertValidated(result);
        Assert.True(Evidence(result, 0, 0).Validated);
        Assert.False(Evidence(result, 0, 0).StreamEqual);
        Assert.True(Evidence(result, 0, 1).StreamEqual);
        Assert.Equal([0, 2], result.ProvenOrdinalMatches.Select(match => match.LocalOrdinal));
        Assert.Contains(1, result.PotentialLocalOrdinals);
        Assert.Contains(1, result.ImplicatedEnglishOrdinals);
        Assert.Equal([1], result.LocalSuffixContexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
    }

    private static FieldScriptIdentityEvidence Evidence(FieldCompatibilityResult result, int label, int script) => Assert.Single(result.ScriptIdentityEvidence, item => item.ObjectLabel == label && item.ScriptId == script);

    private static void AssertValidated(FieldCompatibilityResult result)
    {
        Assert.True(result.English.ScriptValidated, string.Join("; ", result.English.Problems));
        Assert.True(result.Localized.ScriptValidated, string.Join("; ", result.Localized.Problems));
    }

    private static FieldCompatibilityResult Compare(byte[] english, byte[] local) => FieldCompatibilityAudit.Compare(Input(english, "en"), Input(local, "de"));
    private static FieldCompatibilityInput Input(byte[] script, string language) => new("fixture", language, "synthetic", script, Enumerable.Range(0, 4).Select(ordinal => new FieldCompatibilityText(ordinal, "Synthetic " + ordinal)).ToArray());
    private static int[] Message(int id) => [Push(0), Push(id), (int)Jsm.Opcode.MES];
    private static int Push(int value) => Encode(Jsm.Opcode.PSHN_L, value);
    private static int Encode(Jsm.Opcode opcode, int value) => ((int)opcode << 24) | (value & 0xFFFFFF);
    private sealed record Group(int Label, int[][] Scripts);

    private static byte[] Script(params Group[] groups)
    {
        Group[] sorted = groups.OrderBy(group => group.Label).ToArray();
        int[][] scripts = sorted.SelectMany(group => group.Scripts.Select((body, index) => new[] { Encode(Jsm.Opcode.LBL, group.Label + index) }.Concat(body).Append((int)Jsm.Opcode.RET).ToArray())).ToArray();
        int pointersOffset = 8 + groups.Length * 2;
        int operationsOffset = pointersOffset + (scripts.Length + 1) * 2;
        byte[] data = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        data[3] = checked((byte)groups.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), checked((ushort)pointersOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), checked((ushort)operationsOffset));
        for (int index = 0; index < groups.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8 + index * 2), checked((ushort)((groups[index].Label << 7) | (groups[index].Scripts.Length - 1))));
        }
        int operationIndex = 0;
        for (int index = 0; index < scripts.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(pointersOffset + index * 2), checked((ushort)operationIndex));
            foreach (int operation in scripts[index])
            {
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(operationsOffset + operationIndex * 4), operation);
                operationIndex++;
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(pointersOffset + scripts.Length * 2), checked((ushort)operationIndex));
        return data;
    }
}
