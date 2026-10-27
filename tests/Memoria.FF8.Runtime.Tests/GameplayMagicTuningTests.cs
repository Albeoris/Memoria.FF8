using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayMagicTuningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unlimited_magic_keeps_stock_and_histories_and_supports_repeated_casts(bool limitedMode)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 4, 1, 1);
        vitals = MagicSystemFixture.Modes(vitals, infinite: limitedMode);
        vitals.MagicSystem.PersonalFieldMasks[4][1] = 15;
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { InfiniteMagic = limitedMode, Tuning = new GameplayTuning { UnlimitedMagic = true } }).Vitals;
        Assert.False(MagicCastingService.Quote(vitals, 4, 2, MagicCastContext.Field).Allowed);
        for (int action = 0; action < 3; action++)
        {
            string identity = $"unlimited-{action}";
            vitals = MagicCastingService.Reserve(vitals, 4, 1, MagicCastContext.Field, identity, 3);
            Assert.Equal(0, Assert.Single(vitals.MagicSystem.Reservations).TicketMask);
            vitals.ValidateMagicSystemConsistency();
            vitals = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
            vitals = MagicCastingService.Commit(vitals, identity, 3);
            Assert.Equal(1, vitals.GetMagicCount(4, 1));
            Assert.Equal(15, vitals.MagicSystem.PersonalFieldMasks[4][1]);
            Assert.Empty(vitals.MagicSystem.Reservations);
        }
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { InfiniteMagic = limitedMode }).Vitals;
        Assert.Equal(!limitedMode, MagicCastingService.Quote(vitals, 4, 1, MagicCastContext.Field).Allowed);
    }

    [Fact]
    public void Old_magic_state_defaults_to_consuming_stock_and_stock_operations_remain_ordinary()
    {
        JsonObject json = JsonSerializer.SerializeToNode(new MagicSystemState())!.AsObject();
        json.Remove("UnlimitedMagic");
        Assert.False(json.Deserialize<MagicSystemState>()!.UnlimitedMagic);
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100);
        vitals = vitals with { MagicSystem = vitals.MagicSystem with { UnlimitedMagic = true } };
        vitals = vitals.ChangeMagicStock(0, 1, 1, MagicOperationReason.DrawStock, out MagicStockMutation draw);
        Assert.Equal(0, draw.Accepted);
        vitals = vitals.ChangeMagicStock(0, 1, -1, MagicOperationReason.Discard, out MagicStockMutation discard);
        Assert.Equal(1, discard.Accepted);
        Assert.Equal(99, vitals.GetMagicCount(0, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dream_physical_rows_retain_junctions_through_menu_save_rebuild_and_exit(bool menu)
    {
        NativeCharacterVitals initial = MagicSystemFixture.Create().WithMemberFlags(1, true).WithMemberFlags(5, true);
        for (int row = 0; row < 8; row++)
        {
            initial = MagicSystemFixture.Stock(initial, row, row + 1, 50);
            initial = MagicJunctionPolicy.Assign(initial, row, 0, row + 1);
            initial.Records[row] = initial.Records[row] with { Junction = initial.Records[row].Junction with { Commands = 0x02030400, Abilities = 0x00000013 } };
        }
        initial = initial with { CombatParty = [5, 0, 1] };
        NativeJunctionResult entry = initial.PrepareJunction(menu ? 3 : 1, [0, 5, 1], 7);
        NativeCharacterVitals dream = entry.Vitals;
        if (menu)
        {
            dream = dream.EnterGuestSelectionMenu();
            dream = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(dream))!;
            dream = dream.CompleteGuestSelectionMenu([0, 1, 5], 7).Vitals;
        }
        dream = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(dream))!.RebuildParty();
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal(initial.Records[row].Junction, dream.Records[row].Junction);
            Assert.Equal(initial.Records[row].MagicInventory, dream.Records[row].MagicInventory);
        }
        Assert.Equal(8, dream.Records.SelectMany(row => row.Junction.GuardianForceAssignments).Distinct().Count());
        NativeJunctionResult exit = dream.PrepareJunction(0, [8, 9, 10], 7);
        Assert.False(exit.Vitals.DreamEnabled);
        Assert.Equal(new byte[] { 0, 5, 1 }, exit.FieldParty);
        Assert.Equal(initial.CombatParty, exit.Vitals.CombatParty);
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal(initial.Records[row].Junction, exit.Vitals.Records[row].Junction);
            Assert.Equal((byte)row, exit.Vitals.Records[row].CharacterId);
        }
    }
}
