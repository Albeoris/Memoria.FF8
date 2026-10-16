using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGuardianForceLearningReviewTests
{
    [Fact]
    public void EveryOriginalGfCurveChangesLevelAtItsExactNativeThreshold()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        foreach (NativeStatCurve curve in source.Definitions.GuardianForces)
        {
            Assert.Equal(1, NativeStatDefinitions.Level(0, curve));
            for (int level = 2; level <= 100; level++)
            {
                uint threshold = Threshold(curve, level);
                Assert.Equal(level - 1, NativeStatDefinitions.Level(threshold - 1, curve));
                Assert.Equal(level, NativeStatDefinitions.Level(threshold, curve));
            }
        }
    }

    [Fact]
    public void AllOriginalLearningTablesCompleteAtTheirApCostWithoutCarryingExcessToTheNextTarget()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        for (int id = 0; id < 16; id++)
        {
            NativeCharacterVitals current = source.AcquireGuardianForce(id);
            NativeGuardianForceVital[] rows = current.GuardianForces.ToArray();
            rows[id] = rows[id] with { Experience = Threshold(current.Definitions.GuardianForces[id], 100) };
            current = current with { GuardianForces = rows };
            int completed = 0;
            while (current.GuardianForceAbilities(id).FirstOrDefault(entry => !entry.Learned && entry.Ability != 0) is { } target)
            {
                Assert.True(completed++ < 21);
                Assert.InRange(target.RequiredPoints, 1, 255);
                current = current.SelectGuardianForceLearning(id, target.Ability);
                Assert.Same(current, current.AwardGuardianForceAbilityPoints(id, 0));
                int remaining = target.RequiredPoints - target.Progress;
                Assert.True(remaining > 0);
                NativeCharacterVitals partial = current.AwardGuardianForceAbilityPoints(id, checked((ushort)(remaining - 1)));
                Assert.False(partial.GuardianForces[id].LearnedBank.Has(target.Ability));
                Assert.Equal(target.RequiredPoints - 1, partial.GuardianForces[id].AbilityProgress[target.LearningIndex]);
                string preserved = JsonSerializer.Serialize(partial);
                NativeCharacterVitals learned = partial.AwardGuardianForceAbilityPoints(id, ushort.MaxValue);
                Assert.Equal(preserved, JsonSerializer.Serialize(partial));
                Assert.True(learned.GuardianForces[id].LearnedBank.Has(target.Ability));
                Assert.Equal(255, learned.GuardianForces[id].AbilityProgress[target.LearningIndex]);
                for (int index = 0; index < 21; index++)
                {
                    if (index != target.LearningIndex)
                    {
                        Assert.Equal(partial.GuardianForces[id].AbilityProgress[index], learned.GuardianForces[id].AbilityProgress[index]);
                    }
                }
                HashSet<int> available = learned.GuardianForceAbilities(id).Where(entry => !entry.Learned).Select(entry => entry.Ability).ToHashSet();
                int expected = learned.MenuDefinitions.Learning[id].FirstOrDefault(rule => rule.Ability != 0 && available.Contains(rule.Ability))?.Ability ?? 0;
                Assert.Equal(expected, learned.GuardianForces[id].Learning);
                current = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(learned))!;
                current.Validate();
            }
            Assert.True(completed > 0);
            Assert.Equal(0, current.GuardianForces[id].Learning);
        }
    }

    [Fact]
    public void GfMenuUsesTheSameTwentyTwoEntryLearnedBankProjectionAsTheNativeLearningMenu()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals current = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeGuardianForceVital[] rows = current.GuardianForces.ToArray();
        // Component fixture models a learned bank exceeding4ACB70's display limit.
        rows[0] = rows[0] with { LearnedBank = new NativeGuardianForceLearnedBank(ulong.MaxValue, ulong.MaxValue) };
        current = current with { GuardianForces = rows };
        MenuGuardianForce menu = Assert.Single(LiveMenuData.Build(new GameState { CharacterVitals = current }).GuardianForces);
        Assert.Equal(22, menu.Abilities.Count);
        Assert.Equal(22, menu.LearningChoices.Count);
        Assert.All(menu.LearningChoices, choice => Assert.True(choice.Learned));
        Assert.Equal(Enumerable.Range(0, 22).Select(id => $"Learned ability {id}"), menu.Abilities);
    }

    private static uint Threshold(NativeStatCurve curve, int level)
    {
        return (uint)(10 * (level - 1) * curve.ExperienceStep + (level - 1) * (level - 1) * curve.ExperienceFraction / 256);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original kernel/init resources are required for GF learning checks.");
        return new GameAssets(directory);
    }
}
