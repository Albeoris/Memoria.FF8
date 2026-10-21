using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResultPresentationTests
{
    [Fact]
    public void InstalledCharacterRowsProjectCurrentOwnersWithoutMutatingThem()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals before = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals after = before.DeepCopy();
        byte row = before.CombatParty.First(value => value != 255);
        NativeCharacterVital[] records = after.Records.ToArray();
        records[row] = records[row] with { Experience = records[row].Experience + 1000 };
        after = after with { Records = records };
        string original = JsonSerializer.Serialize(before);
        string projected = JsonSerializer.Serialize(after);

        BattleCharacterRewardPresentation[] rows = NativeBattleResultPresentation.BuildCharacters(before, after);

        Assert.Equal(before.CombatParty.Where(value => value != 255), rows.Select(value => value.Row));
        BattleCharacterRewardPresentation changed = Assert.Single(rows, value => value.Row == row);
        Assert.Equal(before.Records[row].Experience, changed.PreviousExperience);
        Assert.Equal(after.Records[row].Experience, changed.FinalExperience);
        NativeStatCurve curve = before.Definitions.Characters[before.Records[row].CharacterId];
        Assert.Equal(NativeStatDefinitions.Level(before.Records[row].Experience, curve), changed.PreviousLevel);
        Assert.Equal(NativeStatDefinitions.Level(after.Records[row].Experience, curve), changed.FinalLevel);
        Assert.Equal(original, JsonSerializer.Serialize(before));
        Assert.Equal(projected, JsonSerializer.Serialize(after));
    }

    [Fact]
    public void FractionalThresholdAndLevelHundredUseNativeExperienceCurve()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        byte row = original.CombatParty.First(value => value != 255);
        byte character = original.Records[row].CharacterId;
        NativeStatCurve curve = original.Definitions.Characters[character] with { ExperienceStep = 100, ExperienceFraction = 255 };
        NativeCharacterVital[] records = original.Records.ToArray();
        records[row] = records[row] with { Experience = 10098 };
        NativeCharacterVitals before = original with { Definitions = original.Definitions with { Characters = original.Definitions.Characters.SetItem(character, curve) }, Records = records };
        NativeCharacterVitals after = before.DeepCopy();
        after.Records[row] = after.Records[row] with { Experience = 10099 };

        BattleCharacterRewardPresentation result = Assert.Single(NativeBattleResultPresentation.BuildCharacters(before, after), value => value.Row == row);

        Assert.Equal((byte)10, result.PreviousLevel);
        Assert.Equal((byte)11, result.FinalLevel);
        Assert.Equal(1u, result.PreviousExperienceToNextLevel);
        Assert.Equal(1021u, result.ExperienceToNextLevel);
        after.Records[row] = after.Records[row] with { Experience = 1000000 };
        result = Assert.Single(NativeBattleResultPresentation.BuildCharacters(before, after), value => value.Row == row);
        Assert.Equal((byte)100, result.FinalLevel);
        Assert.Equal(0u, result.ExperienceToNextLevel);
    }

    [Fact]
    public void GuardianNoticesIncludeOnlyChangedAcquiredOwnersAndNewLearnedBits()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals before = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1);
        NativeGuardianForceVital[] previous = before.GuardianForces.ToArray();
        previous[0] = previous[0] with { LearnedBank = new NativeGuardianForceLearnedBank(1UL << 3, 0) };
        before = before with { GuardianForces = previous };
        NativeCharacterVitals after = before.DeepCopy();
        after.GuardianForces[0] = after.GuardianForces[0] with { Experience = 1000000, LearnedBank = new NativeGuardianForceLearnedBank((1UL << 3) | (1UL << 5), 1UL << 63) };
        string original = JsonSerializer.Serialize(before);
        string projected = JsonSerializer.Serialize(after);

        BattleGuardianForceRewardPresentation notice = Assert.Single(NativeBattleResultPresentation.BuildGuardianForces(before, after));

        Assert.Equal(0, notice.Id);
        Assert.Equal(NativeStatDefinitions.Level(before.GuardianForces[0].Experience, before.Definitions.GuardianForces[0]), notice.PreviousLevel);
        Assert.Equal((byte)100, notice.FinalLevel);
        Assert.Equal(new[] { 5, 127 }, notice.LearnedAbilityIds);
        Assert.Empty(NativeBattleResultPresentation.BuildGuardianForces(before, before));
        Assert.Equal(original, JsonSerializer.Serialize(before));
        Assert.Equal(projected, JsonSerializer.Serialize(after));
    }

    [Fact]
    public void ExistingSummaryConstructorDefaultsPresentationArraysToEmpty()
    {
        BattleRewardSummary summary = new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>(), new Dictionary<int, ushort>(), [], []);

        Assert.Empty(summary.CharacterGrowth);
        Assert.Empty(summary.GuardianForceGrowth);
    }
}
