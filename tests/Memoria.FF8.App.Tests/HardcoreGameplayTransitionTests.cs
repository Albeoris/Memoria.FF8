using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HardcoreGameplayTransitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Profile_publication_can_refresh_GF_vitals_before_migration(bool shared)
    {
        GameSession session = CreateSession(shared);
        GameplayPreferences preferences = (session.State.GameplayPreferences with { Difficulty = BattleDifficulty.Hardcore }).NormalizeForDifficulty();
        MagicModePreview before = session.PreviewGameplayPreferences(preferences);
        GuardianForceAbilityProfile profile = new(session.CharacterVitals!.ResolveGuardianForceTemplates(), "profile", [], [], []);
        List<BattleDifficulty> published = [];

        MagicModePreview? review = HardcoreGameplayTransition.Apply(session, preferences, shared ? null : before, difficulty =>
        {
            published.Add(difficulty);
            session.SetCharacterVitals(session.CharacterVitals! with { GuardianForceProfile = profile });
            Assert.Throws<InvalidOperationException>(() => session.ApplyGameplayPreferences(before));
            return true;
        });

        Assert.Null(review);
        Assert.Equal([BattleDifficulty.Hardcore], published);
        Assert.Equal(BattleDifficulty.Hardcore, session.State.GameplayPreferences.Difficulty);
        Assert.Equal(profile, session.CharacterVitals!.GuardianForceProfile);
        Assert.True(session.CharacterVitals.MagicSystem.SharedMagicStock);
        Assert.True(session.CharacterVitals.MagicSystem.InfiniteMagic);
    }

    [Fact]
    public void Changed_migration_consequences_require_an_updated_review()
    {
        GameSession session = CreateSession(false);
        session.SetCharacterVitals(session.CharacterVitals!.ChangeMagicStock(1, 1, 20, MagicOperationReason.ScriptReward, out _));
        GameplayPreferences preferences = (session.State.GameplayPreferences with { Difficulty = BattleDifficulty.Hardcore }).NormalizeForDifficulty();
        MagicModePreview before = session.PreviewGameplayPreferences(preferences);
        List<BattleDifficulty> published = [];
        bool refreshed = false;
        bool Publish(BattleDifficulty difficulty)
        {
            published.Add(difficulty);
            if (difficulty == BattleDifficulty.Hardcore && !refreshed)
            {
                refreshed = true;
                session.SetCharacterVitals(session.CharacterVitals!.ChangeMagicStock(1, 1, 5, MagicOperationReason.ScriptReward, out _));
            }
            return true;
        }

        MagicModePreview updated = Assert.IsType<MagicModePreview>(HardcoreGameplayTransition.Apply(session, preferences, before, Publish));
        Assert.Equal(25, Assert.Single(updated.Moves).Quantity);
        Assert.Equal([BattleDifficulty.Hardcore, BattleDifficulty.Hard], published);
        Assert.Equal(BattleDifficulty.Hard, session.State.GameplayPreferences.Difficulty);
        Assert.Equal(25, session.CharacterVitals!.GetMagicCount(1, 1));
        Assert.Null(HardcoreGameplayTransition.Apply(session, preferences, updated, Publish));
        Assert.Equal(BattleDifficulty.Hardcore, session.State.GameplayPreferences.Difficulty);
        Assert.Equal(25, session.CharacterVitals!.GetMagicCount(0, 1));
    }

    [Fact]
    public void Unavailable_profile_restores_previous_difficulty_without_changing_progress()
    {
        GameSession session = CreateSession(false);
        string before = JsonSerializer.Serialize(session.State);
        List<BattleDifficulty> published = [];
        GameplayPreferences preferences = (session.State.GameplayPreferences with { Difficulty = BattleDifficulty.Hardcore }).NormalizeForDifficulty();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => HardcoreGameplayTransition.Apply(session, preferences, null, difficulty =>
        {
            published.Add(difficulty);
            return difficulty != BattleDifficulty.Hardcore;
        }));

        Assert.Equal(GameResourceHost.GameplayDifficultyProfilePending, exception.Message);
        Assert.Equal([BattleDifficulty.Hardcore, BattleDifficulty.Hard], published);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    private static GameSession CreateSession(bool shared)
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(Enumerable.Repeat((byte)1, 57)), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        GameplayPreferences preferences = new() { Difficulty = BattleDifficulty.Hard, SharedMagicStock = shared, InfiniteMagic = shared };
        vitals = MagicModeCoordinator.Preview(vitals, preferences).Vitals;
        return new(new GameState { Scene = SceneKind.Field, GameplayPreferences = preferences, CharacterVitals = vitals });
    }
}
