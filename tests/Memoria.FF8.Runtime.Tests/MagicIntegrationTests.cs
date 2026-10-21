using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicIntegrationTests
{
    [Fact]
    public void SharedRewardsUsePhysicalRecordZeroEvenDuringDreamAndWithReserveBank()
    {
        NativeCharacterVitals state = Vitals();
        state.Records[0] = state.Records[0] with { CharacterId = 8 };
        state = state with { CombatParty = [1, 255, 255], MagicSystem = state.MagicSystem with { SharedMagicStock = true } };
        NativeCharacterVitals result = state.AddEventMagic(1, 21, 15);
        Assert.Equal(15, result.GetMagicCount(1, 21));
        Assert.Equal(15, result.Records[0].MagicInventory[0].Count);
        Assert.All(result.Records[1].MagicInventory, stock => Assert.Equal(0, stock.Count));
        Assert.Throws<InvalidOperationException>(() => result.TransferMagic(0, 21, 1, 1, out _));
    }

    [Fact]
    public void SharedStoryDeparturesPreserveBankAndDormantStock()
    {
        NativeCharacterVitals state = Vitals();
        state.Records[0].MagicInventory[0] = new(21, 70);
        state.Records[6].MagicInventory[0] = new(21, 30);
        state.Records[7].MagicInventory[0] = new(22, 80);
        state = state with { MagicSystem = state.MagicSystem with { SharedMagicStock = true } };
        NativeCharacterVitals result = state.WithMemberFlags(6, false).WithMemberFlags(7, false);
        Assert.Equal(70, result.GetMagicCount(0, 21));
        Assert.Equal(30, result.Records[6].MagicInventory[0].Count);
        Assert.Equal(80, result.Records[7].MagicInventory[0].Count);
    }

    [Fact]
    public void NativeStoryCopyCarriesHistoryAndRetainsSourceHistory()
    {
        NativeCharacterVitals state = Vitals();
        state.Records[6].MagicInventory[0] = new(21, 30);
        state.MagicSystem.PersonalFieldMasks[6][21] = 5;
        state.MagicSystem.PersonalFieldMasks[5][21] = 2;
        state = state with { MagicSystem = state.MagicSystem with { HistoriesInitialized = true } };
        NativeCharacterVitals result = state.WithMemberFlags(6, false);
        Assert.Equal(30, result.GetMagicCount(5, 21));
        Assert.Equal(7, result.MagicSystem.PersonalFieldMasks[5][21]);
        Assert.Equal(5, result.MagicSystem.PersonalFieldMasks[6][21]);
    }

    [Fact]
    public void SharedCleanupRetainsRemoteBindingAndOpaqueTwentiethByte()
    {
        NativeCharacterVitals state = Vitals();
        state.Records[0].MagicInventory[0] = new(21, 100);
        state.Records[1].Junction.MagicJunctions[0] = 21;
        state.Records[1].Junction.MagicJunctions[19] = 56;
        state = state with { MagicSystem = state.MagicSystem with { SharedMagicStock = true } };
        NativeCharacterVitals result = state.NormalizeMagicInventories().RebuildMagicMenuRow(1);
        Assert.Equal(21, result.Records[1].Junction.MagicJunctions[0]);
        Assert.Equal(56, result.Records[1].Junction.MagicJunctions[19]);
    }

    [Fact]
    public void SharedFieldCasterUsesCurrentMagicThenPartyOrderAndUnavailableOwnerStaysReserved()
    {
        NativeCharacterVitals state = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 21, 100);
        state = state with { CombatParty = [2, 1, 255], MagicSystem = state.MagicSystem with { SharedMagicStock = true } };
        BattleNativePlayerStatRules Rules(int row)
        {
            byte[] character = new byte[36];
            for (int offset = 12; offset < 36; offset += 4)
            {
                character[offset + 1] = 1;
                character[offset + 3] = 1;
            }
            return new(state.Records[row].CharacterId, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), state.Records[row].Junction.MagicJunctions.ToImmutableArray(), 0, state.Definitions);
        }
        Assert.Equal(2, state.ResolveFieldMagicCaster(0, 21, Rules));
        state.Records[1].StatBonuses[2] = 20;
        Assert.Equal(1, state.ResolveFieldMagicCaster(0, 21, Rules));
        state.Records[3] = state.Records[3] with { MemberFlags = 0 };
        state.Records[3].Junction.MagicJunctions[0] = 21;
        Assert.Null(state.ResolveFieldMagicCaster(0, 21, Rules));
        Assert.Equal(3, MagicJunctionPolicy.GetOwner(state, 21));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void FieldNoOpDoesNotSpendTicketsButSuccessfulCastDoes(bool shared, bool infinite, bool smart)
    {
        NativeCharacterVitals state = Vitals();
        state.Records[0].MagicInventory[0] = new(21, 100);
        state.Records[0].Junction.MagicJunctions[0] = 21;
        state = state with { MagicSystem = state.MagicSystem with { SharedMagicStock = shared, InfiniteMagic = infinite, SmartAttack = smart, HistoriesInitialized = true } };
        byte[] spell = new byte[60];
        spell[7] = 3;
        spell[8] = 20;
        BattleNativePlayerStatRules rules = new(0, 1, new byte[36].ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(id => (id == 21 ? spell : new byte[60]).ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), state.Records[0].Junction.MagicJunctions.ToImmutableArray(), 0, state.Definitions);
        byte[] character = rules.CharacterRecord.ToArray();
        for (int offset = 12; offset <= 32; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 3] = 1;
        }
        rules = rules with { CharacterRecord = character.ToImmutableArray() };
        NativeCharacterVitals result = state.CastFieldMagic(0, 21, 1, rules, out bool used);
        Assert.True(used);
        Assert.Equal(infinite ? 100 : 99, result.GetMagicCount(0, 21));
        Assert.Equal(1, result.MagicSystem.PersonalFieldMasks[0][21]);
        Assert.Equal(1, result.MagicSystem.SharedFieldMasks[21]);
        Assert.Equal(0, result.MagicSystem.PersonalBattleMasks[0][21]);
        NativeCharacterVitals unchanged = result.CastFieldMagic(0, 21, 1, rules, out used);
        Assert.False(used);
        Assert.Same(result, unchanged);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void RefinementConsumesIngredientsAndClipsFinalBatchAcrossModes(bool shared, bool infinite, bool smart)
    {
        NativeCharacterVitals state = MagicSystemFixture.Create();
        const string kernelHash = "E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6";
        state = state with { KernelSha256 = kernelHash, MagicSystem = state.MagicSystem with { SharedMagicStock = shared, InfiniteMagic = infinite, SmartAttack = smart, HistoriesInitialized = true } };
        state.GuardianForces[0] = state.GuardianForces[0] with { LearnedBank = new(0, 1UL << (97 - 64)) };
        state = state.ChangeMagicStock(1, 1, 10, MagicOperationReason.ScriptReward, out _).ChangeMagicStock(1, 2, 99, MagicOperationReason.ScriptReward, out _);
        NativeRefinementRecipe recipe = new(5, 0, 1, 2, 2);
        NativeRefinementDefinitions definitions = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, kernelHash, Enumerable.Range(0, 24).Select(_ => new NativeRefinementAbility("Synthetic", 2, 0, 0)).ToImmutableArray(), new[] { 102, 143, 10, 12, 110 }.Select(count => Enumerable.Repeat(recipe, count).ToImmutableArray()).ToImmutableArray());
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = state.InitOutSha256, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        Assert.Equal(1, definitions.MaximumBatches(state, economy, null, 97, 0, 1));
        NativeRefinementResult result = definitions.Refine(state, economy, null, 97, 0, 1, 1);
        Assert.Equal(8, result.Vitals.GetMagicCount(1, 1));
        Assert.Equal(100, result.Vitals.GetMagicCount(1, 2));
        Assert.Equal(10, state.GetMagicCount(1, 1));
        Assert.Equal(0, definitions.MaximumBatches(result.Vitals, economy, null, 97, 0, 1));
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(result.Vitals, economy, null, 97, 0, 1, 1));
    }

    private static NativeCharacterVitals Vitals()
    {
        NativeStatCurve curve = new(1, 0, 1, 1, 100);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(1, 1), 0, new NativeCharacterJunction(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(123, 0x80, 60000, new NativeGuardianForceLearnedBank(1, 2), 1, ImmutableArray.CreateRange(new byte[21]), 3, 4)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(new byte[57]), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        vitals.Validate();
        return vitals;
    }

}
