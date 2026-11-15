using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedPartyMenuEndpointTests
{
    private static readonly byte[] PartyOrder = [0, 1, 2, 7, 3, 9, 4, 8, 5, 10, 6];

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    public void Field_cure_and_potion_reach_the_last_selected_party_member(int capacity)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = CreateArrivingField(directory);
        using GameWorld world = new(session, directory, sharedAssets: game);
        byte recipient = PartyOrder[capacity - 1];
        PrepareParty(world, session, capacity);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(game);
        int cure = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == "Cure");
        int potion = Enumerable.Range(1, 255).Single(id => catalog.ItemName(id) == "Potion");
        NativeFieldItem item = Assert.IsType<NativeFieldItem>(catalog.FieldItem(potion));
        Assert.True(item.SupportsCharacterUse);
        NativeEconomy economy = session.State.Economy!;
        NativeItemStock[] items = economy.Items.ToArray();
        int slot = Array.FindIndex(items, stock => stock.ItemId == potion);
        if (slot < 0)
        {
            slot = Array.FindIndex(items, stock => stock.ItemId == 0);
        }
        Assert.InRange(slot, 0, items.Length - 1);
        items[slot] = new((byte)potion, 3);
        session.SetEconomy(economy with { Items = items });

        NativeCharacterVitals vitals = session.State.CharacterVitals!.ChangeMagicStock(0, cure, 3, MagicOperationReason.ScriptReward, out _);
        vitals = Wound(vitals, recipient);
        session.SetCharacterVitals(vitals);
        byte[] fieldParty = vitals.CombatParty.ToArray();
        int[] fieldBindings = world.Events.Scene.Party.ToArray();
        MenuController menu = new(() => LiveMenuData.Build(session.State, catalog: catalog), action =>
        {
            PlayerAction input = action.Kind switch
            {
                "magic-use" => NativeMagicMenuProjection.PlayerAction(action),
                "item-use" => new(PlayerActionKind.UseFieldItem, int.Parse(action.SubjectId.AsSpan("item-".Length), CultureInfo.InvariantCulture), Quantity: int.Parse(action.TargetId.AsSpan("character-".Length), CultureInfo.InvariantCulture)),
                _ => throw new InvalidOperationException($"Unexpected live menu action {action.Kind}.")
            };
            PlayerActionResult result = world.Apply(input);
            Assert.True(result.Accepted, result.Detail);
        });

        var beforeCure = Health(vitals);
        int stock = vitals.GetMagicInventory(0).Where(entry => entry.SpellId == cure).Sum(entry => entry.Count);
        BattleNativePlayerStatRules rules = world.BattleResources.ReadPlayerNativeStatRules(vitals, 0);
        NativeCharacterVitals expectedCure = vitals.CastFieldMagic(0, cure, recipient, rules, out bool actuallyHeals);
        Assert.True(actuallyHeals);
        menu.Open("magic-use");
        menu.SelectEntry("magic-use", $"magic-0-{cure}");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("magic-target", menu.ScreenId);
        ChooseTargetWithKeyboard(menu, recipient);
        vitals = session.State.CharacterVitals!;
        Assert.Equal(expectedCure.Records[recipient].HitPoints, vitals.Records[recipient].HitPoints);
        Assert.Equal(expectedCure.Records[recipient].Status, vitals.Records[recipient].Status);
        Assert.Equal(stock - 1, vitals.GetMagicInventory(0).Where(entry => entry.SpellId == cure).Sum(entry => entry.Count));
        Assert.Equal(beforeCure.Where((_, row) => row != recipient), Health(vitals).Where((_, row) => row != recipient));

        vitals = Wound(vitals, recipient);
        session.SetCharacterVitals(vitals);
        var beforePotion = Health(vitals);
        var expectedPotion = item.Use(vitals, session.State.Economy!, potion, recipient);
        Assert.True(expectedPotion.Changed);
        menu.InvalidateLiveData();
        menu.Open("item-use");
        menu.SelectEntry("item-use", $"item-{potion}");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("item-target", menu.ScreenId);
        ChooseTargetWithKeyboard(menu, recipient);
        vitals = session.State.CharacterVitals!;
        Assert.Equal(expectedPotion.Vitals.Records[recipient].HitPoints, vitals.Records[recipient].HitPoints);
        Assert.Equal(expectedPotion.Vitals.Records[recipient].Status, vitals.Records[recipient].Status);
        Assert.Equal(2, session.State.Economy!.Items.Where(entry => entry.ItemId == potion).Sum(entry => entry.Count));
        Assert.Equal(beforePotion.Where((_, row) => row != recipient), Health(vitals).Where((_, row) => row != recipient));
        Assert.Equal(fieldParty, vitals.CombatParty);
        Assert.Equal(PartyOrder[..capacity], vitals.GetBattlePartyRows(capacity));
        Assert.Equal(fieldBindings, world.Events.Scene.Party);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    public void Status_and_junction_cycle_to_last_party_member_and_exchange_real_packages(int capacity)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = CreateArrivingField(directory);
        using GameWorld world = new(session, directory, sharedAssets: game);
        PrepareParty(world, session, capacity);
        byte recipient = PartyOrder[capacity - 1];
        NativeCharacterVitals source = session.State.CharacterVitals!.AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0);
        source = source.ChangeMagicStock(0, 1, 40, MagicOperationReason.ScriptReward, out _);
        source = source.ChangeMagicStock(recipient, 2, 60, MagicOperationReason.ScriptReward, out _);
        session.SetCharacterVitals(source);
        int[] fieldBindings = world.Events.Scene.Party.ToArray();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(game);
        List<PlayerAction> applied = [];
        MenuController menu = new(() => LiveMenuData.Build(session.State, catalog: catalog), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            PlayerActionResult result = world.Apply(input);
            Assert.True(result.Accepted, result.Detail);
            applied.Add(input);
        });
        foreach (string screen in new[] { "status", "junction-magic" })
        {
            menu.Open(screen);
            menu.Execute(new MenuAction("select-character", "character-0"));
            for (int position = 1; position < capacity; position++)
            {
                menu.Navigate(MenuInput.PageRight);
                Assert.Equal($"character-{PartyOrder[position]}", menu.Character?.Id);
            }
        }

        menu.Open("switch");
        menu.SelectEntry("switch", "switch-junction");
        menu.Navigate(MenuInput.Confirm);
        menu.SelectEntry("switch-junction", "character-0");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("switch-junction-target", menu.ScreenId);
        menu.SelectEntry("switch-junction-target", $"character-{recipient}");
        menu.Navigate(MenuInput.Confirm);
        if (menu.ScreenId == "switch-junction-confirm")
        {
            menu.SelectEntry("switch-junction-confirm", "yes");
            menu.Navigate(MenuInput.Confirm);
        }

        Assert.Equal(new PlayerAction(PlayerActionKind.ExchangeJunctions, 0, recipient.ToString(CultureInfo.InvariantCulture)), Assert.Single(applied));
        NativeCharacterVitals next = session.State.CharacterVitals!;
        Assert.Equal(source.Records[0].Junction, next.Records[recipient].Junction);
        Assert.Equal(source.Records[recipient].Junction, next.Records[0].Junction);
        Assert.Equal(source.Records[0].MagicInventory, next.Records[recipient].MagicInventory);
        Assert.Equal(source.Records[recipient].MagicInventory, next.Records[0].MagicInventory);
        Assert.Equal(source.CombatParty, next.CombatParty);
        Assert.Equal(PartyOrder[..capacity], next.GetBattlePartyRows(capacity));
        Assert.Equal(fieldBindings, world.Events.Scene.Party);
    }

    private static GameSession CreateArrivingField(string directory)
    {
        GameState bootstrap = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        GameSession session = new(bootstrap with { FieldId = 132, FieldName = "field_132", GlobalRandom = new(917) });
        session.RequestField(new FieldTransition(133, -105, -1751, 22, 128));
        session.SetDepartingFieldState(bootstrap.DepartingFieldState);
        return session;
    }

    private static void PrepareParty(GameWorld world, GameSession session, int capacity)
    {
        world.EnsureField();
        for (int callback = 0; callback < 60 && !world.Events.CanAcceptMovementInput; callback++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(world.Events.CanAcceptMovementInput);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = capacity });
        NativeCharacterVitals vitals = session.State.CharacterVitals!.WithExtendedRoster(true);
        NativeCharacterVital[] records = vitals.Records.Select((record, row) => record with { CharacterId = (byte)row, MemberFlags = 1 }).ToArray();
        session.SetCharacterVitals((vitals with { Records = records, CombatParty = PartyOrder[..3] }).WithBattleParty(PartyOrder[..capacity], capacity));
    }

    private static NativeCharacterVitals Wound(NativeCharacterVitals vitals, int row)
    {
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[row] = records[row] with { HitPoints = new(1, 1), Status = 0 };
        return vitals with { Records = records };
    }

    private static (NativeHpWord HitPoints, ushort Status)[] Health(NativeCharacterVitals vitals)
    {
        return vitals.Records.Select(record => (record.HitPoints, record.Status)).ToArray();
    }

    private static void ChooseTargetWithKeyboard(MenuController menu, int row)
    {
        string id = $"character-{row}";
        Assert.Contains(menu.Entries, entry => entry.Id == id && entry.Enabled);
        menu.SelectEntry(menu.ScreenId, "character-0");
        for (int position = 0; position < menu.Entries.Count && menu.SelectedEntry?.Id != id; position++)
        {
            menu.Navigate(MenuInput.Down);
        }
        Assert.Equal(id, menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
    }
}
