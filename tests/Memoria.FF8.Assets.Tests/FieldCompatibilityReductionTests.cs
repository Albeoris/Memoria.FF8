using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCompatibilityReductionTests
{
    [Fact]
    public void UnresolvedChoicePresentationDoesNotInvalidateIdenticalBankOrdinals()
    {
        byte[] script = Script(Choice(0));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en"), Input(script, "de"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.True(result.Localized.StaticReferencesValidated);
        Assert.True(result.Localized.Contexts.Single(context => context.IsMessage).Dynamic);
        Assert.Equal([0, 1, 2, 3], result.ProvenOrdinalMatches.Select(match => match.LocalOrdinal));
        Assert.Empty(result.PotentialLocalOrdinals);
        Assert.Contains(result.ReviewCases, item => item.Kind == "DynamicContext");
        Assert.DoesNotContain(result.ReviewCases, item => item.Kind.StartsWith("Unmatched", StringComparison.Ordinal));
    }

    [Fact]
    public void UnresolvedLaterChoiceRetainsProofOfPrefixExclusiveRows()
    {
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(Message(0).Concat(Choice(1))), "en"), Input(Script(Message(0).Concat(Choice(2))), "fr"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.Equal([1, 2, 3], result.PotentialLocalOrdinals);
        Assert.Equal([0], result.ProvenOrdinalMatches.Select(match => match.LocalOrdinal));
        Assert.Contains(result.LocalSuffixContexts, context => context.IsMessage && context.MessageId == 2);
    }

    [Fact]
    public void UnknownSuffixTextIdStillBoundsTheEntireBank()
    {
        int[] left = Message(0).Concat(new[] { Push(0), Encode(Jsm.Opcode.PSHM_W, 20), (int)Jsm.Opcode.MES }).ToArray();
        int[] right = left.ToArray();
        right[4] = Encode(Jsm.Opcode.PSHM_W, 21);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(left), "en"), Input(Script(right), "it"));
        Assert.Equal([0, 1, 2, 3], result.PotentialLocalOrdinals);
        Assert.False(result.Localized.StaticReferencesValidated);
    }

    [Fact]
    public void PreparationOwnsSourceBytesAndBankEntries()
    {
        byte[] script = Script(Message(0));
        FieldCompatibilityText[] bank = [new(0, "Synthetic")];
        PreparedFieldCompatibility first = FieldCompatibilityAudit.Prepare(new("fixture", "en", "synthetic", script, bank));
        script[0] = 255;
        bank[0] = new(9, "Changed input");
        PreparedFieldCompatibility second = FieldCompatibilityAudit.Prepare(new("fixture", "de", "synthetic", Script(Message(0)), [new(0, "Other")]));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(first, second);
        Assert.Equal(FieldCompatibilityKind.ByteIdentical, result.Kind);
        Assert.Equal("Synthetic", result.English.Bank[0].Text);
    }

    [Fact]
    public void BranchIntoMessageOperandsCannotProveAnEarlierPushedId()
    {
        int[] body = [Push(1), Encode(Jsm.Opcode.JPF, 5), Push(0), Push(0), Push(1), Push(1), (int)Jsm.Opcode.AMESW];
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(Script(body), "en"), Input(Script(body), "ja"));
        Assert.Null(result.Localized.Contexts.Single(context => context.IsMessage).MessageId);
        Assert.Equal([0, 1, 2, 3], result.PotentialLocalOrdinals);
    }

    [Fact]
    public void InvalidReferencesAndMalformedScriptsStayInTheRepairQueue()
    {
        byte[] invalid = Script(Message(99));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(invalid, "en"), Input(invalid, "es"));
        Assert.False(result.Localized.StaticReferencesValidated);
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1, 2, 3], result.PotentialLocalOrdinals);
        byte[] malformed = [1, 2, 3];
        FieldCompatibilityResult broken = FieldCompatibilityAudit.Compare(Input(malformed, "en"), Input(malformed, "es"));
        Assert.False(broken.English.ScriptValidated);
        Assert.Equal([0, 1, 2, 3], broken.PotentialLocalOrdinals);
    }

    [Fact]
    public void EqualScriptsDoNotAbsorbUnmatchedBankExtras()
    {
        byte[] script = Script(Choice(0));
        FieldCompatibilityInput english = new("fixture", "en", "synthetic", script, [new(0, "A")]);
        FieldCompatibilityInput local = new("fixture", "fr", "synthetic", script, [new(0, "B"), new(1, "Extra")]);
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(english, local);
        Assert.Equal([1], result.PotentialLocalOrdinals);
        Assert.Single(result.ProvenOrdinalMatches);
    }

    [Fact]
    public void UnsupportedCalculationCannotEstablishScriptValidation()
    {
        byte[] script = Script(new[] { Push(1), Encode(Jsm.Opcode.CAL, 18) }.Concat(Message(0)));
        FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(Input(script, "en"), Input(script, "de"));
        Assert.Equal(FieldCompatibilityKind.Incomplete, result.Kind);
        Assert.False(result.Localized.ScriptValidated);
        Assert.Empty(result.ProvenOrdinalMatches);
        Assert.Equal([0, 1, 2, 3], result.PotentialLocalOrdinals);
    }

    private static FieldCompatibilityInput Input(byte[] script, string language) => new("fixture", language, "synthetic", script, Enumerable.Range(0, 4).Select(ordinal => new FieldCompatibilityText(ordinal, "Synthetic " + ordinal)).ToArray());
    private static int[] Message(int id) => [Push(0), Push(id), (int)Jsm.Opcode.MES];
    private static int[] Choice(int id) => [Push(0), Push(id), Push(0), Push(1), Push(0), Encode(Jsm.Opcode.PSHI_L, 0), Push(50), Push(80), (int)Jsm.Opcode.AASK];
    private static int Push(int value) => Encode(Jsm.Opcode.PSHN_L, value);
    private static int Encode(Jsm.Opcode opcode, int value) => ((int)opcode << 24) | (value & 0xFFFFFF);

    private static byte[] Script(IEnumerable<int> body)
    {
        int[] operations = new[] { Encode(Jsm.Opcode.LBL, 0) }.Concat(body).Append((int)Jsm.Opcode.RET).ToArray();
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
