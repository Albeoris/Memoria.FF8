using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleEntryAdmissionTests
{
    [Fact]
    public void Installed_menu_item_uses_selected_extra_ally_and_the_available_area_donors()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState bootstrap = ExpandedBattleEffectMatrixTests.PrepareField(directory);
        GameSession session = new(bootstrap with { FieldId = 132, FieldName = "field_132", GlobalRandom = new(917) });
        session.RequestField(new FieldTransition(133, -105, -1751, 22, 128));
        session.SetDepartingFieldState(bootstrap.DepartingFieldState);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        for (int callback = 0; callback < 60 && !world.Events.CanAcceptMovementInput; callback++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(world.Events.CanAcceptMovementInput);
        SelectFourAllies(world, session);
        NativeEconomy economy = session.State.Economy!;
        NativeItemStock[] stocks = economy.Items.ToArray();
        int lampSlot = Array.FindIndex(stocks, stock => stock.ItemId == 168);
        if (lampSlot < 0)
        {
            lampSlot = Array.FindIndex(stocks, stock => stock.ItemId == 0);
        }
        stocks[lampSlot] = new(168, 1);
        session.SetEconomy(economy with { Items = stocks });
        session.SetExamTimer(session.ExamTimer!.SetDisplayEnabled4A6CC0(false));

        Assert.True(world.Apply(new(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.Equal((ushort)811, world.Events.PendingMenuBattle!.Formation);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = battle.ReadDefinition();
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(new[] { "party-0", "party-1", "party-2", "party-3" }, battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        Assert.Equal(new[] { 8, 9 }, definition.AddedEnemySources!.Keys);
        BattleCombatantDefinition original = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.NativeTemplate is not null && actor.InitialActive);
        Assert.Equal(0, original.SceneSlot);
        Assert.DoesNotContain(session.State.Economy!.Items, stock => stock.ItemId == 168 && stock.Count != 0);
        ReachReady(world, battle);
    }

    [Fact]
    public void Installed_reserve_identity_and_native_aliases_survive_area_additions()
    {
        const ushort formation = 164;
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        SelectFourAllies(world, session);
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = battle.ReadDefinition();
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(new[] { 8, 9 }, definition.AddedEnemySources!.Keys);
        BattleCombatantDefinition reserve = Assert.Single(definition.Combatants, actor => actor.SceneSlot == 3 && !actor.IsPlayer);
        Assert.Equal(71, reserve.ModelId);
        Assert.False(reserve.InitialActive);
        Assert.NotNull(reserve.NativeTemplate);
        Assert.Null(battle.Combatants.Single(actor => actor.Id == reserve.Id).Slot);
        BattleCombatant original = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 101 && actor.NativeActorIndex is not null);
        Assert.Equal(3, original.NativeActorIndex);
        Assert.NotEmpty(original.NativeAi!.Scripts[0]);
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot >= 8), actor => Assert.Null(actor.NativeActorIndex));
        ReachReady(world, battle);
    }

    internal static void ReachReady(GameWorld world, BattleSession battle)
    {
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(process.EntryReady);
        Assert.Same(process, battle.ExpandedPresentationProcess);
    }

    private static void SelectFourAllies(GameWorld world, GameSession session)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        session.SetCharacterVitals(vitals.Rest().WithBattleParty([0, 1, 2, 3], 4));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4 });
    }
}
