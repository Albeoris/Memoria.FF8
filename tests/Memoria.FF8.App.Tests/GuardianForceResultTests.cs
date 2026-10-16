using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceResultTests
{
    [Fact]
    public void ObservedGfHpChangeIsPureDuringPreviewAndCommittedOnlyAgainstItsOriginalOwner()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).RebuildParty().Rest();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        ushort before = vitals.GuardianForces[0].HitPoints;
        ushort after = checked((ushort)(before - 10));
        BattlePartyResult[] party = vitals.CombatParty.Where(row => row != 255).Select(row => new BattlePartyResult(row, vitals.Records[row].CharacterId, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].Status, vitals.Records[row].Status, [])).ToArray();
        BattleEncounterOccurrence encounter = new("gf-hp-component", 512, new string('A', 64), SceneKind.Field, 999, 0, new string('B', 64), null);
        BattleResult result = new(encounter, BattleOutcome.Victory, party, [], [], [], true, BattleRewardPolicy.DebugNoRewards, []) { GuardianForceChanges = [new(0, before, after)] };
        string unchanged = JsonSerializer.Serialize(vitals);
        BattleResultOwners Project(NativeCharacterVitals source)
        {
            return BattleResultProjection.Apply(result, source, economy, new Dictionary<int, int>(), new Dictionary<int, BattleEnemyRewardDefinition>(), assets.ReadNativeBattleExperienceDefinition(), null, false);
        }
        BattleResultOwners preview = Project(vitals);
        Assert.Equal(unchanged, JsonSerializer.Serialize(vitals));
        Assert.Equal(after, preview.CharacterVitals.GuardianForces[0].HitPoints);
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(Project(vitals)));
        Assert.Throws<InvalidDataException>(() => Project(preview.CharacterVitals));
    }
}
