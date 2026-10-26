using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldPresentationProjectionTests
{
    [Fact]
    public void DifferentMusicCommandsAndCompleteFadePacketsDisappearFromTextComparison()
    {
        int[] left = [Push(12), Command(Jsm.Opcode.MUSICLOAD), Push(0), Command(Jsm.Opcode.MUSICSTOP), Command(Jsm.Opcode.FADEOUT)];
        int[] right = [Push(127), Push(16), Command(Jsm.Opcode.CROSSMUSIC), Push(20), Push(30), Push(40), Command(Jsm.Opcode.DCOLADD), Command(Jsm.Opcode.COLSYNC)];
        FieldCompatibilityResult result = Compare(left.Concat(Message(0)).ToArray(), right.Concat(Message(0)).ToArray());
        Assert.True(result.MessageIdentityEvidence!.StreamEqual);
        Assert.True(result.MessageIdentityEvidence.OrdinalIdentityProven);
        Assert.NotEqual(result.English.JsmSha256, result.Localized.JsmSha256);
        Assert.Equal(FieldCompatibilityKind.Divergent, result.Kind);
    }

    [Fact]
    public void PurePresentationScriptRetainsOnlyMatchingScriptAnchors()
    {
        FieldCompatibilityResult result = Compare([Push(1), Command(Jsm.Opcode.MUSICLOAD), Command(Jsm.Opcode.FADEIN)], []);
        Assert.True(result.MessageIdentityEvidence!.StreamEqual);
        Assert.Equal(2, result.MessageIdentityEvidence.MatchedTokens);
    }

    [Theory]
    [InlineData(Jsm.Opcode.TCOLSUB, 4)]
    [InlineData(Jsm.Opcode.FCOLADD, 7)]
    [InlineData(Jsm.Opcode.EFFECTPLAY, 4)]
    [InlineData(Jsm.Opcode.EFFECTPLAY2, 3)]
    [InlineData(Jsm.Opcode.SETCAMERA, 1)]
    [InlineData(Jsm.Opcode.SHAKE, 4)]
    [InlineData(Jsm.Opcode.DISPBAR, 7)]
    [InlineData(Jsm.Opcode.BROKEN, 8)]
    [InlineData(Jsm.Opcode.SETBAR, 2)]
    public void CompletePresentationOperandPacketsDisappear(Jsm.Opcode opcode, int count)
    {
        int[] packet = Enumerable.Range(0, count).Select(Push).Append(Command(opcode)).ToArray();
        Assert.True(Compare(packet.Concat(Message(0)).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        if (count > 0)
        {
            Assert.False(Compare(packet.Skip(1).Concat(Message(0)).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        }
    }

    [Theory]
    [InlineData(Jsm.Opcode.DISPBAR, 7)]
    [InlineData(Jsm.Opcode.BROKEN, 8)]
    [InlineData(Jsm.Opcode.SETBAR, 2)]
    public void OnlyKnownSupportedBarIdsArePresentationOnly(Jsm.Opcode opcode, int count)
    {
        int[] suffix = Enumerable.Range(1, count - 1).Select(Push).Append(Command(opcode)).Concat(Message(0)).ToArray();
        Assert.True(Compare(new[] { Push(0) }.Concat(suffix).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        Assert.True(Compare(new[] { Push(1) }.Concat(suffix).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        Assert.False(Compare(new[] { Push(2) }.Concat(suffix).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        Assert.False(Compare(new[] { Command(Jsm.Opcode.PSHM_W, 42) }.Concat(suffix).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
    }

    [Fact]
    public void BranchesRelocateAcrossRemovedPresentationPrefix()
    {
        int[] left = [Command(Jsm.Opcode.JMP, 4), Push(1), Command(Jsm.Opcode.MUSICLOAD), Command(Jsm.Opcode.FADEIN)];
        int[] right = [Command(Jsm.Opcode.JMP, 1)];
        Assert.True(Compare(left.Concat(Message(0)).ToArray(), right.Concat(Message(0)).ToArray()).MessageIdentityEvidence!.StreamEqual);
    }

    [Fact]
    public void EntryAtRemovedSpanStartMapsToNextSemanticBoundary()
    {
        int[] left = [Command(Jsm.Opcode.JMP, 1), Push(1), Command(Jsm.Opcode.MUSICLOAD)];
        int[] right = [Command(Jsm.Opcode.JMP, 1)];
        Assert.True(Compare(left.Concat(Message(0)).ToArray(), right.Concat(Message(0)).ToArray()).MessageIdentityEvidence!.StreamEqual);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void InteriorEntryPreventsRemovalOfCompletePacket(int target)
    {
        int[] left = [Command(Jsm.Opcode.JMP, target), Push(100), Push(10), Command(Jsm.Opcode.CROSSMUSIC)];
        int[] right = [Command(Jsm.Opcode.JMP, target), Push(101), Push(10), Command(Jsm.Opcode.CROSSMUSIC)];
        Assert.False(Compare(left.Concat(Message(0)).ToArray(), right.Concat(Message(0)).ToArray()).MessageIdentityEvidence!.StreamEqual);
    }

    [Fact]
    public void SafeCalculatedAndUnknownReadArgumentsCanBePresentationOnly()
    {
        int[] left = [Push(10), Push(20), Command(Jsm.Opcode.CAL, 0), Command(Jsm.Opcode.MUSICLOAD)];
        int[] right = [Command(Jsm.Opcode.PSHM_W, 42), Command(Jsm.Opcode.MUSICLOAD)];
        Assert.True(Compare(left.Concat(Message(0)).ToArray(), right.Concat(Message(0)).ToArray()).MessageIdentityEvidence!.StreamEqual);
    }

    [Fact]
    public void UnknownDivisorAndIncompleteArgumentsRemainSignificant()
    {
        int[] uncertain = [Push(10), Command(Jsm.Opcode.PSHM_W, 42), Command(Jsm.Opcode.CAL, 3), Command(Jsm.Opcode.MUSICLOAD)];
        int[] incomplete = [Push(1), Command(Jsm.Opcode.CROSSMUSIC)];
        Assert.False(Compare(uncertain.Concat(Message(0)).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
        Assert.False(Compare(incomplete.Concat(Message(0)).ToArray(), Message(0)).MessageIdentityEvidence!.StreamEqual);
    }

    [Theory]
    [InlineData(Jsm.Opcode.WAIT)]
    [InlineData(Jsm.Opcode.MOVE)]
    [InlineData(Jsm.Opcode.KEYON)]
    [InlineData(Jsm.Opcode.POPM_B)]
    [InlineData(Jsm.Opcode.REQ)]
    [InlineData(Jsm.Opcode.MUSICSTATUS)]
    public void SemanticCommandsRemainSignificant(Jsm.Opcode opcode)
    {
        Assert.False(Compare(new[] { Push(1), Command(opcode) }.Concat(Message(0)).ToArray(), new[] { Push(2), Command(opcode) }.Concat(Message(0)).ToArray()).MessageIdentityEvidence!.StreamEqual);
    }

    [Fact]
    public void IgnoredPresentationCannotHideChangedMessageId()
    {
        FieldCompatibilityResult result = Compare(new[] { Command(Jsm.Opcode.FADEIN) }.Concat(Message(0)).ToArray(), new[] { Command(Jsm.Opcode.FADEOUT) }.Concat(Message(1)).ToArray());
        Assert.False(result.MessageIdentityEvidence!.StreamEqual);
        Assert.Empty(result.ProvenOrdinalMatches);
    }

    private static FieldCompatibilityResult Compare(int[] left, int[] right) => FieldCompatibilityAudit.Compare(Input(left, "en"), Input(right, "de"));

    private static FieldCompatibilityInput Input(int[] body, string language) => new("fixture", language, "synthetic/" + language, Script(body), [new(0, "a"), new(1, "b")]);

    private static int[] Message(int id) => [Push(0), Push(id), Command(Jsm.Opcode.MES)];

    private static int Push(int value) => Command(Jsm.Opcode.PSHN_L, value);

    private static int Command(Jsm.Opcode opcode, int parameter = 0) => (int)opcode <= (int)Jsm.Opcode.PREQEW ? ((int)opcode << 24) | (parameter & 0xFFFFFF) : (int)opcode;

    private static byte[] Script(int[] body)
    {
        int[] operations = new[] { Command(Jsm.Opcode.LBL) }.Concat(body).Append(Command(Jsm.Opcode.RET)).ToArray();
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
