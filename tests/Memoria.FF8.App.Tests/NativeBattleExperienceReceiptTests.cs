using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleExperienceReceiptTests
{
    [Fact]
    public void FrozenPhysicalAwardsPublishWithTheirActualPartyMappingAndPolicyMultipliers()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(new(0, 1, null, 10, 10, 10, 10, 5, 100, 0, 3));
        NativeBattleExperiencePartySlot[] slots = Slots(vitals);
        ushort[] hp = vitals.GuardianForces.Select(gf => gf.HitPoints).ToArray();
        BattleNativeExperienceReceipt receipt = new(banks.Distribute494D40(slots, hp, [], false), vitals.CombatParty.ToImmutableArray());
        string source = JsonSerializer.Serialize(receipt);
        BattleNativeExperienceReceipt restored = JsonSerializer.Deserialize<BattleNativeExperienceReceipt>(source)!;
        BattleExperienceAwards awards = restored.ProjectExperience(vitals, false, new() { ExperienceMultiplier = 2, GuardianExperienceMultiplier = 3 });
        Assert.Equal((ushort)120, awards.Characters[receipt.PartyRows[0]]);
        foreach (byte row in receipt.PartyRows.Where(row => row != 255 && row != receipt.PartyRows[0]))
        {
            Assert.Equal((ushort)40, awards.Characters[row]);
        }
        Assert.Equal((ushort)60, awards.GuardianForces[0]);
        Assert.Equal(source, JsonSerializer.Serialize(receipt));
        NativeBattleExperienceBanks noExperience = receipt.Banks.ClearExperience48CFF0();
        BattleExperienceAwards cleared = (receipt with { Banks = noExperience }).ProjectExperience(vitals, true, new());
        Assert.Empty(cleared.Characters);
        Assert.Empty(cleared.GuardianForces);
        Assert.Equal((ushort)3, noExperience.ReadWord(76));
        Assert.Throws<InvalidDataException>(() => receipt.ProjectExperience(vitals, true, new()));
    }

    [Fact]
    public void OriginalResultConsumerReadsEveryAcquiredGuardianBankIncludingAnUnjunctionedPhysicalAlias()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        NativeBattleExperienceBanks banks = NativeBattleExperienceBanks.Initialize48D020().Accrue494AF0(new(4, 1, null, 10, 10, 10, 10, 0, 100, 0, 0));
        banks = banks.Distribute494D40(Slots(vitals), vitals.GuardianForces.Select(gf => gf.HitPoints).ToArray(), [], false);
        BattleNativeExperienceReceipt receipt = new(banks, vitals.CombatParty.ToImmutableArray());
        BattleExperienceAwards awards = receipt.ProjectExperience(vitals, false, new());
        Assert.DoesNotContain(1, vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.Equal((ushort)40, awards.GuardianForces[1]);
        Assert.False(awards.GuardianForces.ContainsKey(0));
    }

    private static NativeBattleExperiencePartySlot[] Slots(NativeCharacterVitals vitals)
    {
        return vitals.CombatParty.Select(row => row == 255 ? new NativeBattleExperiencePartySlot(false, null, 0) : new NativeBattleExperiencePartySlot(true, vitals.Records[row].Status, (ushort)vitals.Records[row].Junction.GuardianForceAssignments.Aggregate(0, (mask, id) => mask | 1 << id))).ToArray();
    }
}
