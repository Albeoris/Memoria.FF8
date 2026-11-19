using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CompletedGameCampaignTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-clear-envelope-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CompletedFf8PayloadIsDiscoveredInsideTheExistingEnvelopeWithoutChangingIt()
    {
        Directory.CreateDirectory(directory);
        GameplaySaveComponentDocument clear = CompletedSaveFixture.Document();
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, clear);
        CombinedSessionSave envelope = new() { ActiveGame = SessionGame.Ff8, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, Ff8Payload = payload.ToArray() };
        PlayerSaveStore store = new(Path.Combine(directory, "diagnostic.json"), directory, discoverOriginalSaves: false);
        CombinedSessionCodec.Save(store.GetPath(1), envelope);
        byte[] original = File.ReadAllBytes(store.GetPath(1));
        Assert.True(Assert.Single(store.ReadCompletedEntries()).IsCompletedGame);
        GameplaySaveComponentDocument decoded = CrossoverSaveAdapter.ReadFf8(CrossoverSaveAdapter.ReadEnvelope(store.GetPath(1))!);
        Assert.Equal(clear.CompletedGame!.Completion.Identity, decoded.CompletedGame!.Completion.Identity);
        Assert.Null(decoded.Progression);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(1)));
    }

    [Fact]
    public void ForeignCampaignCompletionCannotBecomeAnFf8Source()
    {
        Directory.CreateDirectory(directory);
        GameplaySaveComponentDocument clear = CompletedSaveFixture.Document();
        Assert.Throws<InvalidDataException>(() => (clear with { CompletedGame = clear.CompletedGame! with { Campaign = "ff7" } }).Validate());
        CombinedSessionSave foreign = new() { ActiveGame = SessionGame.Ff7 };
        Assert.False(CrossoverSaveAdapter.HasCompletedFf8(foreign));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
