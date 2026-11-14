using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedPartySaveSummaryTests
{
    private static readonly byte[] Party = [2, 0, 1, 5, 3, 9, 4, 10, 7, 8, 6];
    private static readonly GameplaySaveResumeDescriptor Resume = new(1, GameplaySaveResumeKind.Field, 106, 105, new(10, 20, 7, 192), null);

    [Theory]
    [InlineData(4, false, 0, 0u, false, 4)]
    [InlineData(5, false, 0, 0u, false, 5)]
    [InlineData(11, false, 0, 0u, false, 11)]
    [InlineData(3, false, 0, 0u, false, 3)]
    [InlineData(0, false, 0, 0u, false, 3)]
    [InlineData(11, true, 0, 0u, false, 3)]
    [InlineData(11, false, 0, 0x800u, false, 3)]
    [InlineData(11, false, 450, 0u, false, 3)]
    [InlineData(11, false, 850, 0u, true, 3)]
    [InlineData(11, false, 894, 0u, true, 11)]
    public void SavedSummaryUsesTheSameEffectivePartyInBothFormats(int capacity, bool dream, int story, uint flags, bool worldEntry, int expectedCount)
    {
        GameState state = State(capacity, dream, story, flags, worldEntry);
        int[] expected = Party.Take(expectedCount).Select(row => (int)row).ToArray();
        Assert.Equal(expected, SaveSummary.CapturePartyPortraits(state));

        using MemoryStream semantic = new();
        GameplaySaveCodec.Write(semantic, GameplaySaveComponentDocument.CaptureProgression(state, Resume));
        semantic.Position = 0;
        Assert.Equal(expected, GameplaySaveCodec.ReadSummary(semantic).PartyPortraits);

        using FileStream snapshot = TemporarySnapshot();
        SnapshotStorage.Write(snapshot, state, new JsonSerializerOptions());
        snapshot.Position = 0;
        Assert.Equal(expected, SnapshotStorage.ReadSummary(snapshot, new JsonSerializerOptions())!.PartyPortraits);
    }

    [Theory]
    [InlineData(0, 450, 3)]
    [InlineData(450, 894, 3)]
    [InlineData(894, 450, 11)]
    public void SemanticSummaryUsesTheCanonicalWordOverlayWhenTheSameAddressIsPresent(int worldStory, int genericStory, int expectedCount)
    {
        GameState state = State(11, false, worldStory, 0, true);
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(state, Resume);
        GameplaySaveProgression progression = document.Progression!;
        GameplaySaveStory story = progression.Story with { Cells = [.. progression.Story.Cells, new(256, 2, genericStory)] };
        using MemoryStream semantic = new();
        GameplaySaveCodec.Write(semantic, document with { Progression = progression with { Story = story } });
        semantic.Position = 0;
        GameState canonical = state with { EventMemory = story.RestoreEventMemory() };
        int[] portraits = GameplaySaveCodec.ReadSummary(semantic).PartyPortraits;
        Assert.Equal(expectedCount, portraits.Length);
        Assert.Equal(SaveSummary.CapturePartyPortraits(canonical), portraits);
    }

    [Fact]
    public void OlderMetadataAndMissingAdditionalMembersKeepTheThreeNativePortraits()
    {
        GameState state = State(11, false, 0, 0, false);
        state = state with { CharacterVitals = state.CharacterVitals! with { AdditionalCombatParty = null } };
        int[] expected = [2, 0, 1];
        using MemoryStream semantic = new();
        GameplaySaveCodec.Write(semantic, GameplaySaveComponentDocument.CaptureProgression(state, Resume));
        semantic.Position = 0;
        Assert.Equal(expected, GameplaySaveCodec.ReadSummary(semantic).PartyPortraits);
        using FileStream snapshot = TemporarySnapshot();
        WriteHeader(snapshot, new(SceneKind.Field, 106, "", null, expected, null, null, null));
        Assert.Equal(expected, SnapshotStorage.ReadSummary(snapshot, new JsonSerializerOptions())!.PartyPortraits);
    }

    [Fact]
    public void AChecksummedSnapshotWithOversizedPortraitMetadataIsRejected()
    {
        using FileStream snapshot = TemporarySnapshot();
        WriteHeader(snapshot, new(SceneKind.Field, 106, "", null, new int[NativeCharacterVitals.ExtendedRecordCount + 1], null, null, null));
        Assert.Throws<InvalidDataException>(() => SnapshotStorage.ReadSummary(snapshot, new JsonSerializerOptions()));
    }

    private static GameState State(int capacity, bool dream, int story, uint flags, bool worldEntry)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create().WithExtendedRoster(true);
        NativeCharacterVital[] records = vitals.Records.Select((record, row) => record with { CharacterId = (byte)row, MemberFlags = 1 }).ToArray();
        return GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = Resume.FieldId,
            PreviousFieldId = Resume.PreviousFieldId,
            CharacterVitals = (vitals with { Records = records }).WithBattleParty(Party, 11) with { DreamEnabled = dream },
            Economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = vitals.InitOutSha256, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 },
            LimitProgress = new(vitals.InitOutSha256, 0, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
            GameplayPreferences = new() { BattleAllyCount = capacity },
            WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = flags },
            EventMemory = !worldEntry && story != 0 ? NativeEventMemory.Empty.Write(256, 2, NativeScalarValue.Exact(story)) : NativeEventMemory.Empty,
            WorldEntryMemory = worldEntry ? NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, story) : null
        };
    }

    private static FileStream TemporarySnapshot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"memoria-party-summary-{Guid.NewGuid():N}.save");
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
    }

    private static void WriteHeader(FileStream stream, SaveSummary summary)
    {
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(summary);
        byte[] payload = [0];
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("MFF8SAVE"u8);
        writer.Write(1);
        writer.Write(metadata.Length);
        writer.Write((long)payload.Length);
        writer.Write(SHA256.HashData(metadata));
        writer.Write(SHA256.HashData(payload));
        writer.Write(metadata);
        writer.Write(payload);
        writer.Flush();
        stream.Position = 0;
    }
}
