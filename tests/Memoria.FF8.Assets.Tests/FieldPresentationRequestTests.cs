using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldPresentationRequestTests
{
    [Theory]
    [InlineData("music")]
    [InlineData("bar")]
    [InlineData("read-only-loop")]
    public void RelocatedEquivalentPresentationRequestPreservesCallerMessageProof(string routine)
    {
        int[] callee = routine switch
        {
            "bar" => Bar(0),
            "read-only-loop" => [Encode(Jsm.Opcode.PSHM_B, 20), Encode(Jsm.Opcode.JPF, 4), .. Music(), Push(2), (int)Jsm.Opcode.WAIT, Encode(Jsm.Opcode.JMP, -7)],
            _ => Music()
        };
        FieldCompatibilityResult result = Relocated(callee, callee);

        AssertValidated(result);
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
        Assert.True(Caller(result).StreamEqual);
        Assert.Contains(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 0);
        Assert.DoesNotContain(0, result.PotentialLocalOrdinals);
        Assert.DoesNotContain(0, result.ImplicatedEnglishOrdinals);
    }

    [Fact]
    public void StateWritingDefaultDoesNotPoisonSeparatePresentationEntry()
    {
        int[] stateWriter = [Push(1), Encode(Jsm.Opcode.POPM_B, 20)];
        byte[] english = Script(new Group(0, [Call(1, 2)]), new Group(10, [stateWriter, Music()]));
        byte[] local = Script(new Group(0, [Call(2, 3)]), new Group(5, [[]]), new Group(20, [stateWriter, Music()]));
        FieldCompatibilityResult result = Compare(english, local);

        AssertValidated(result);
        Assert.True(Caller(result).StreamEqual);
        Assert.DoesNotContain(0, result.PotentialLocalOrdinals);
        Assert.Contains(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 0);
    }

    [Theory]
    [InlineData("priority")]
    [InlineData("wait-for-end")]
    [InlineData("unknown-script")]
    public void ChangedRequestContractCannotNormalizeToPresentationRequest(string change)
    {
        int[] localCall = change switch
        {
            "priority" => Call(2, 2, priority: 6),
            "wait-for-end" => Call(2, 2, opcode: Jsm.Opcode.REQEW),
            _ => [Push(5), Encode(Jsm.Opcode.PSHM_W, 20), Encode(Jsm.Opcode.REQ, 2), .. Message(0)]
        };
        FieldCompatibilityResult result = Relocated(Music(), Music(), localCall);

        AssertRejected(result);
    }

    [Fact]
    public void SelfRequestAndCrossObjectRequestRemainDistinct()
    {
        byte[] english = Script(new Group(0, [Call(0, 1), Music()]), new Group(10, [Music()]));
        byte[] local = Script(new Group(0, [Call(1, 2), Music()]), new Group(10, [Music()]));

        AssertRejected(Compare(english, local));
    }

    [Theory]
    [InlineData("store")]
    [InlineData("message")]
    [InlineData("random")]
    [InlineData("input")]
    [InlineData("escaping-branch")]
    [InlineData("escaping-request")]
    [InlineData("bar-alias")]
    public void CalleeWithUnapprovedEffectsCannotNormalizeCallerRequest(string effect)
    {
        int[] callee = effect switch
        {
            "store" => [.. Music(), Push(1), Encode(Jsm.Opcode.POPM_B, 20)],
            "message" => [.. Music(), .. Message(1)],
            "random" => [.. Music(), Push(10), (int)Jsm.Opcode.RND],
            "input" => [.. Music(), Push(16), (int)Jsm.Opcode.KEYSCAN],
            "escaping-branch" => [.. Music(), Encode(Jsm.Opcode.JMP, 2)],
            "escaping-request" => [.. Music(), Push(5), Push(0), Encode(Jsm.Opcode.REQ, 0)],
            _ => Bar(2)
        };

        AssertRejected(Relocated(callee, callee));
    }

    [Theory]
    [InlineData("condition")]
    [InlineData("wait")]
    public void ReadConditionAndWaitStructureRemainInCalleeSignature(string difference)
    {
        int[] english = [Encode(Jsm.Opcode.PSHM_B, 20), Encode(Jsm.Opcode.JPF, 4), .. Music(), Push(2), (int)Jsm.Opcode.WAIT];
        int[] local = english.ToArray();
        local[difference == "condition" ? 0 : 5] = difference == "condition" ? Encode(Jsm.Opcode.PSHM_B, 21) : Push(3);

        AssertRejected(Relocated(english, local));
    }

    [Fact]
    public void PurePresentationScriptsStayMarkedWhileRawDifferencesRemain()
    {
        byte[] english = Script(new Group(0, [Message(0)]), new Group(10, [Music()]));
        byte[] local = Script(new Group(0, [Message(0)]), new Group(10, [Bar(0)]));
        FieldCompatibilityResult result = Compare(english, local);

        AssertValidated(result);
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
        Assert.NotNull(result.FirstDivergence);
        Assert.True(Assert.Single(result.ScriptIdentityEvidence, evidence => evidence.ObjectLabel == 10).PresentationOnly);
        Assert.DoesNotContain(0, result.PotentialLocalOrdinals);
    }

    [Fact]
    public void EquivalentPresentationRequestDoesNotHideChangedMessageId()
    {
        FieldCompatibilityResult result = Relocated(Music(), Music(), Call(2, 2, messageId: 1));

        AssertValidated(result);
        Assert.False(Caller(result).StreamEqual);
        Assert.Contains(0, result.ImplicatedEnglishOrdinals);
        Assert.Contains(1, result.PotentialLocalOrdinals);
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    private static FieldCompatibilityResult Relocated(int[] englishCallee, int[] localCallee, int[]? localCall = null)
    {
        byte[] english = Script(new Group(0, [Call(1, 1)]), new Group(10, [englishCallee]), new Group(30, [[]]));
        byte[] local = Script(new Group(0, [localCall ?? Call(2, 2)]), new Group(5, [[]]), new Group(20, [localCallee]), new Group(30, [[]]));
        return Compare(english, local);
    }

    private static void AssertRejected(FieldCompatibilityResult result)
    {
        AssertValidated(result);
        Assert.False(Caller(result).StreamEqual);
        Assert.Contains(0, result.PotentialLocalOrdinals);
        Assert.Contains(0, result.ImplicatedEnglishOrdinals);
        Assert.DoesNotContain(result.ProvenOrdinalMatches, match => match.LocalOrdinal == 0);
    }

    private static void AssertValidated(FieldCompatibilityResult result)
    {
        Assert.True(result.English.ScriptValidated, string.Join("; ", result.English.Problems));
        Assert.True(result.Localized.ScriptValidated, string.Join("; ", result.Localized.Problems));
    }

    private static FieldScriptIdentityEvidence Caller(FieldCompatibilityResult result) => Assert.Single(result.ScriptIdentityEvidence, evidence => evidence.ObjectLabel == 0 && evidence.ScriptId == 0);
    private static FieldCompatibilityResult Compare(byte[] english, byte[] local) => FieldCompatibilityAudit.Compare(Input(english, "en"), Input(local, "de"));
    private static FieldCompatibilityInput Input(byte[] script, string language) => new("fixture", language, "synthetic", script, [new(0, "Synthetic zero"), new(1, "Synthetic one")]);
    private static int[] Call(int target, int pointer, int priority = 5, Jsm.Opcode opcode = Jsm.Opcode.REQ, int messageId = 0) => [Push(priority), Push(pointer), Encode(opcode, target), .. Message(messageId)];
    private static int[] Music() => [Push(0), Push(127), (int)Jsm.Opcode.MUSICVOL];
    private static int[] Bar(int id) => [Push(id), Push(210), Push(210), Push(0), Push(0), Push(80), Push(0), Push(80), (int)Jsm.Opcode.BROKEN];
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
