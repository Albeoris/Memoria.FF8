using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class CompletedGameContractTests
{
    [Fact]
    public void CompletedDataRoundTripsWithoutAnEndingVmAndOwnsItsProgression()
    {
        GameState state = CompletedSaveFixture.State();
        GameplaySaveComponentDocument document = CompletedGameData.Capture(state);
        Assert.Equal(GameplaySaveResumeKind.Completed, document.Resume.Kind);
        Assert.Null(document.Progression);
        state.CharacterVitals!.Records[0] = state.CharacterVitals.Records[0] with { Experience = 99999 };
        state.Economy!.Items[0] = new(17, 1);
        state.CardInventory!.Cards[0] = 0;
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        stream.Position = 0;
        SaveSummary summary = GameplaySaveCodec.ReadSummary(stream);
        Assert.True(summary.IsCompletedGame);
        Assert.Equal(SceneKind.Title, summary.Scene);
        stream.Position = 0;
        GameplaySaveComponentDocument loaded = GameplaySaveCodec.Read(stream);
        GameState transfer = CompletedGameData.ToTransferState(loaded, CompletedSaveFixture.State());
        Assert.Equal(12345u, transfer.CharacterVitals!.Records[0].Experience);
        Assert.Equal(43, transfer.Economy!.Items[0].Count);
        Assert.Equal((byte)0x87, transfer.CardInventory!.Cards[0]);
        Assert.Equal(document.CompletedGame!.Completion.Identity, transfer.CompletedGame!.Completion.Identity);
        Assert.Null(transfer.EventState);
        Assert.Null(transfer.Ff8Completion);
    }

    [Fact]
    public void OrdinaryLegacyDocumentsRemainOrdinary()
    {
        GameState state = CompletedSaveFixture.State() with { Ff8Completion = null };
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, 76, null, new(0, 0, 0, 0), null);
        GameplaySaveComponentDocument partial = GameplaySaveComponentDocument.Capture(resume, state.CharacterVitals!, state.Economy!, state.LimitProgress!);
        GameplaySaveComponentDocument full = GameplaySaveComponentDocument.CaptureProgression(state, resume) with { Version = 2 };
        foreach (GameplaySaveComponentDocument document in new[] { partial, full })
        {
            using MemoryStream stream = new();
            GameplaySaveCodec.Write(stream, document);
            stream.Position = 0;
            GameplaySaveComponentDocument loaded = GameplaySaveCodec.Read(stream);
            Assert.Null(loaded.CompletedGame);
            Assert.Null(loaded.NewGamePlus);
            Assert.Equal(document.Version, loaded.Version);
        }
    }

    [Fact]
    public void CompletedPayloadCannotBeUsedWithAnOrdinaryResumeOrAnOlderSchema()
    {
        GameplaySaveComponentDocument document = CompletedSaveFixture.Document();
        Assert.Throws<InvalidDataException>(() => (document with { CompletedGame = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (document with { Version = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (document with { Resume = new(1, GameplaySaveResumeKind.Field, 76, null, new(0, 0, 0, 0), null) }).Validate());
        Assert.Throws<InvalidOperationException>(() => CompletedGameData.Capture(CompletedSaveFixture.State() with { Ff8Completion = null }));
    }

    [Fact]
    public void CompletedChecksumFailureDoesNotYieldASource()
    {
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, CompletedSaveFixture.Document());
        byte[] bytes = stream.ToArray();
        bytes[^1] ^= 1;
        using MemoryStream corrupted = new(bytes);
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(corrupted));
    }
}
