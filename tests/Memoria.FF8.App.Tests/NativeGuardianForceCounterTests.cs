using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGuardianForceCounterTests
{
    [Fact]
    public void AuthenticSavedRowsOwnBothCountersAndReceiptPublicationChecksTheCurrentBeforeRow()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().RebuildParty().Rest();
        Assert.All(vitals.GuardianForces, gf =>
        {
            Assert.Equal((ushort)0, gf.KillCount);
            Assert.Equal((ushort)0, gf.DeathCount);
        });
        NativeGuardianForceVital[] rows = vitals.GuardianForces.ToArray();
        rows[0] = rows[0] with { KillCount = ushort.MaxValue, DeathCount = ushort.MaxValue };
        vitals = vitals with { GuardianForces = rows };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        BattleEncounterOccurrence encounter = new("gf-counters", 512, new string('A', 64), SceneKind.Field, 999, 0, new string('B', 64), null);
        BattlePartyResult[] party = vitals.CombatParty.Where(row => row != 255).Select(row => new BattlePartyResult(row, vitals.Records[row].CharacterId, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].HitPoints.Minimum, vitals.Records[row].Status, vitals.Records[row].Status, [])).ToArray();
        BattleGuardianForceCounterChange receipt = new(0, ushort.MaxValue, 0, ushort.MaxValue, 0);
        BattleResult result = new(encounter, BattleOutcome.Victory, party, [], [], [], true, BattleRewardPolicy.DebugNoRewards, []) { GuardianForceCounterChanges = [receipt] };
        BattleResultOwners projected = BattleResultProjection.Apply(result, vitals, economy, new Dictionary<int, int>(), new Dictionary<int, BattleEnemyRewardDefinition>(), assets.ReadNativeBattleExperienceDefinition(), null, false);
        Assert.Equal((ushort)0, projected.CharacterVitals.GuardianForces[0].KillCount);
        Assert.Equal((ushort)0, projected.CharacterVitals.GuardianForces[0].DeathCount);
        Assert.Equal((ushort)65535, vitals.GuardianForces[0].KillCount);
        Assert.Throws<InvalidDataException>(() => BattleResultProjection.Apply(result, projected.CharacterVitals, economy, new Dictionary<int, int>(), new Dictionary<int, BattleEnemyRewardDefinition>(), assets.ReadNativeBattleExperienceDefinition(), null, false));

        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(encounter);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidDataException>(() => session.CommitBattleResult(encounter, projected.CharacterVitals, economy, null));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        session.CommitBattleResult(encounter, projected.CharacterVitals, economy, null, guardianForceCounters: [receipt]);
        Assert.Equal((ushort)0, session.State.CharacterVitals!.GuardianForces[0].DeathCount);
        Assert.Equal((ushort)0, session.State.CharacterVitals.GuardianForces[0].KillCount);
        Assert.Equal(receipt, Assert.Single(result.DeepCopy().GuardianForceCounterChanges));
    }
}
