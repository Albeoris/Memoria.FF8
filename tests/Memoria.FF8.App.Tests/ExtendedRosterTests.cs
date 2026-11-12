using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExtendedRosterTests
{
    [Fact]
    public void Saved_progress_survives_disable_and_reenable()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals enabled = original.WithExtendedRoster(true) with { CombatParty = [8, 6, 10] };
        Assert.All(Enumerable.Range(6, 5), row => Assert.True(enabled.IsRosterMember(row)));
        Assert.Equal(original.Records, enabled.Records.Take(NativeCharacterVitals.NativeRecordCount));

        enabled = enabled.ChangeMagicStock(8, 1, 5, MagicOperationReason.DrawStock, out MagicStockMutation stock);
        Assert.Equal(5, stock.Accepted);
        enabled = MagicCastingService.Reserve(enabled, 8, 1, MagicCastContext.Field, "extended-roster-cast");
        enabled = MagicCastingService.Commit(enabled, "extended-roster-cast");
        enabled = MagicCastingService.Release(enabled, "extended-roster-cast");
        Assert.Equal(4, enabled.GetMagicCount(8, 1));
        Assert.Equal(original.GetMagicCount(0, 1), enabled.GetMagicCount(0, 1));

        NativeCharacterVitals disabled = enabled.WithExtendedRoster(false);
        Assert.Equal(new byte[] { 0, 255, 255 }, disabled.CombatParty);
        Assert.All(Enumerable.Range(8, 3), row => Assert.False(disabled.IsRosterMember(row)));
        NativeCharacterVitals restored = GameplaySaveCharacters.Capture(disabled).Restore(original).WithExtendedRoster(true);
        Assert.Equal(4, restored.GetMagicCount(8, 1));
        Assert.Equal(enabled.Records[8], restored.Records[8]);
    }

    [Theory]
    [InlineData(6, 7)]
    [InlineData(7, 6)]
    public void Disabling_unbinds_all_five_and_keeps_the_story_member(int storyMember, int absentMember)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals enabled = original.WithExtendedRoster(true) with { CombatParty = [(byte)absentMember, 8, (byte)storyMember] };
        enabled.Records[storyMember] = enabled.Records[storyMember] with { MemberFlags = (ushort)(enabled.Records[storyMember].MemberFlags | 1) };
        enabled.Records[absentMember] = enabled.Records[absentMember] with { MemberFlags = (ushort)(enabled.Records[absentMember].MemberFlags & ~1) };
        for (int row = 6; row < enabled.Records.Length; row++)
        {
            NativeCharacterVital record = enabled.Records[row];
            record.MagicInventory[0] = new(1, (byte)row);
            record.Junction.MagicJunctions[0] = 1;
            enabled.Records[row] = record with { Junction = record.Junction with { GuardianForces = (ushort)(1 << row), Commands = 20, Abilities = 39 }, Experience = (uint)(3000 + row), HitPoints = new(17, 17), HitPointBonus = (ushort)row, Status = 0x80, KillCount = (ushort)row };
        }

        NativeCharacterVitals disabled = enabled.WithExtendedRoster(false);
        Assert.Equal(new byte[] { (byte)storyMember, 255, 255 }, disabled.CombatParty);
        Assert.True(disabled.IsRosterMember(storyMember));
        Assert.False(disabled.IsRosterMember(absentMember));
        NativeCharacterVitals reenabled = GameplaySaveCharacters.Capture(disabled).Restore(original).WithExtendedRoster(true);
        Assert.Equal(disabled.CombatParty, reenabled.CombatParty);
        for (int row = 6; row < enabled.Records.Length; row++)
        {
            NativeCharacterVital source = enabled.Records[row];
            Assert.Equal((byte)1, source.Junction.MagicJunctions[0]);
            Assert.Equal(source with { Junction = source.Junction.Reset() }, disabled.Records[row]);
            Assert.Equal(disabled.Records[row], reenabled.Records[row]);
        }
    }

    [Fact]
    public void Dream_uses_original_rows_and_restores_the_extended_party()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals enabled = original.WithExtendedRoster(true) with { CombatParty = [0, 8, 10] };
        NativeCharacterVital squall = enabled.Records[0];
        enabled.Records[0] = squall with { Junction = squall.Junction with { GuardianForces = 1 } };
        NativeJunctionResult dream = enabled.PrepareJunction(1, [0, 255, 255], 7);
        Assert.Equal((byte)0, dream.Vitals.FindPhysicalRow(8));
        Assert.Equal(new byte[] { 0, 255, 255 }, dream.Vitals.CombatParty);
        Assert.Equal(enabled.Records[8], dream.Vitals.Records[8]);
        Assert.Equal(enabled.Records[10], dream.Vitals.Records[10]);

        NativeCharacterVitals restored = GameplaySaveCharacters.Capture(dream.Vitals).Restore(original);
        Assert.Equal(enabled.CombatParty, restored.PrepareJunction(0, [8, 255, 255], 7).Vitals.CombatParty);
        NativeCharacterVitals disabled = restored.WithExtendedRoster(false);
        Assert.Equal((byte)0, disabled.FindPhysicalRow(8));
        Assert.Equal(restored.Records[0].Junction, disabled.Records[0].Junction);
        Assert.Equal(new byte[] { 0, 255, 255 }, disabled.PrepareJunction(0, [8, 255, 255], 7).Vitals.CombatParty);
    }

    [Fact]
    public void Permanent_guests_project_their_original_battle_models_and_saved_owners()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using BattleAssets battleAssets = new(assets.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithExtendedRoster(true) with { CombatParty = [8, 9, 10] };
        GameState state = new() { CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance() };
        BattleCombatantDefinition[] party = BattlePartyProjection.Create(state, null, LiveMenuCatalog.ReadInstalled(assets), battleAssets);
        Assert.Equal(new[] { 8, 9, 10 }, party.Select(actor => actor.ModelId));
        Assert.Equal(new int?[] { 8, 9, 10 }, party.Select(actor => actor.MagicRecordId));
        BattleDefinition definition = new()
        {
            Combatants = [.. party, new() { Id = "enemy", Name = "Enemy", MaximumHp = 500, CurrentHp = 500 }],
            MagicVitals = vitals,
            NativeActions = battleAssets.NativeActions.ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        Assert.Equal(new int?[] { 8, 9, 10 }, BattleSession.Restore(battle.Capture()).Capture().Definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId));
    }
}
