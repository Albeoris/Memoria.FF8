using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OmegaAchievementTests
{
    [Theory]
    [InlineData(127, true)]
    [InlineData(126, false)]
    public void CurrentTutorialAvailabilityDeterminesProofIndependentlyOfFieldVictory(int tutorial, bool expected)
    {
        GameState state = new()
        {
            Scene = SceneKind.Field,
            EventState = JsonSerializer.SerializeToElement(new { Scene = new { UnlockedTutorials = new[] { tutorial } } }),
            EventMemory = NativeEventMemory.Empty
        };
        OriginalAchievementObservedEvent fact = OriginalAchievementObservedEvent.Create(OriginalAchievementState.Observe(state), null);
        Assert.Equal(expected, fact.Progress.OmegaProof);
        using AchievementService service = new(OriginalAchievementCatalog.CreateRegistry(), new Repository());
        service.Record(fact);
        Assert.Equal(expected, service.Get(OriginalAchievementCatalog.Id("omega")).Unlocked);
    }

    [Fact]
    public void MissingTutorialOwnerAndTheFieldVictoryBitCannotFabricateProof()
    {
        GameState state = new() { Scene = SceneKind.Field };
        Assert.Null(OriginalAchievementState.Observe(state).OmegaProof);
        state = state with { EventMemory = state.EventMemory.Write(374, 1, NativeScalarValue.Exact(128)) };
        Assert.Null(OriginalAchievementState.Observe(state).OmegaProof);
        GameSession session = new(state);
        session.ObserveOriginalAchievements();
        Assert.Empty(session.State.PendingOriginalAchievementObservations);
    }

    [Fact]
    public void VersionOnePendingReceiptRetainsItsIdentityWithTheExtendedProjection()
    {
        OriginalAchievementProgress progress = new() { MaximumGil = true, CardWins = 9 };
        object?[] legacy = [null, null, null, null, true, null, null, null, null, 9L, null, null, null];
        string legacyId = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(legacy)));
        string legacyContext = "original-state/v1/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("unbound")));
        OriginalAchievementObservedEvent fact = new(new(legacyId), new(legacyContext), progress, null);
        GameState serialized = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(new GameState { PendingOriginalAchievementObservations = [fact] }))!;
        GameSession restored = new(serialized);
        Assert.Equal(fact, Assert.Single(restored.State.PendingOriginalAchievementObservations));
        fact.Validate();
        OriginalAchievementObservedEvent omega = OriginalAchievementObservedEvent.Create(progress with { OmegaProof = true }, null);
        Assert.Contains("/v2/", omega.ContextId.Value);
        omega.Validate();
        Assert.Throws<InvalidDataException>(() => (omega with { Progress = progress }).Validate());
    }

    private sealed class Repository : IAchievementRepository
    {
        private AchievementProfile profile = new();
        public AchievementProfile Load() => profile;
        public void Save(AchievementProfile value) => profile = value;
        public void Dispose()
        {
        }
    }
}
