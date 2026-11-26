using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudCursorMemoryTests
{
    [Theory]
    [InlineData(BattleAbility.Magic, false)]
    [InlineData(BattleAbility.Magic, true)]
    [InlineData(BattleAbility.Item, false)]
    [InlineData(BattleAbility.Item, true)]
    [InlineData(BattleAbility.GuardianForce, false)]
    [InlineData(BattleAbility.GuardianForce, true)]
    public void DrawCastPageDoesNotLeakIntoOtherCommands(BattleAbility ability, bool submitDraw)
    {
        List<BattleInput> inputs = [];
        BattleSession battle = Create();
        BattleHudController hud = Hud(battle, input =>
        {
            input.Validate();
            if (input.Ability != BattleAbility.Draw)
            {
                GameWorld.ExecuteBattleCommand(battle, input);
            }
            inputs.Add(input);
        });
        OpenDraw(hud);
        hud.Navigate(MenuInput.Right);
        Assert.True(hud.DrawCast);
        hud.Choose(0);
        if (submitDraw)
        {
            hud.SelectTarget("enemy", true);
        }
        else
        {
            hud.Navigate(MenuInput.Cancel);
            hud.Navigate(MenuInput.Cancel);
        }
        Submit(hud, ability, 0);
        BattleInput command = inputs.Last();
        Assert.Equal(ability, command.Ability);
        Assert.False(command.DrawCast);
        Assert.Null(command.DrawCastTargetId);
        BattleAttackCommand queued = ability == BattleAbility.GuardianForce ? Assert.Single(battle.GuardianForceCasts).Command : Assert.Single(battle.QueuedActions);
        Assert.Equal(ability, queued.Ability);
        Assert.Equal("enemy", queued.TargetId);
        Assert.False(queued.DrawCast);
        if (ability is BattleAbility.Magic or BattleAbility.Item)
        {
            BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(battle.Capture()));
            BattleAttackCommand restoredCommand = Assert.Single(restored.QueuedActions);
            Assert.Equal(queued.Ability, restoredCommand.Ability);
            Assert.Equal(queued.ActionId, restoredCommand.ActionId);
            Assert.Equal(queued.TargetId, restoredCommand.TargetId);
            Assert.False(restoredCommand.DrawCast);
            Assert.Null(restoredCommand.DrawCastTargetId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawRemembersSubmittedBlizzardAndReopensOnStockPage(bool cast)
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(Create(), inputs.Add);
        OpenDraw(hud);
        if (cast)
        {
            hud.Navigate(MenuInput.Right);
        }
        hud.Choose(1);
        if (cast)
        {
            hud.SelectTarget("enemy", true);
        }
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(2, input.ActionId);
        Assert.Equal(cast, input.DrawCast);
        Assert.Equal(BattleAbility.Draw, hud.SelectedCommand);
        OpenDraw(hud);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        Assert.False(hud.DrawCast);
        Assert.False(hud.SelectingTarget);
    }

    [Fact]
    public void InitialCursorResetsSubmittedCommandSpellAndDrawPage()
    {
        BattleHudController hud = new(Create(), (_, _) => { }, () => { }, () => { }, execute: _ => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        Assert.False(hud.CursorMemory);
        OpenDraw(hud);
        hud.Navigate(MenuInput.Right);
        hud.Choose(1);
        hud.SelectTarget("enemy", true);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenDraw(hud);
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
        Assert.False(hud.DrawCast);
    }

    [Fact]
    public void CharacterSwitchingRestoresEachAcceptedCommandAndAction()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.SelectParty("second");
        Submit(hud, BattleAbility.Item, 0);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.CycleCommandActor();
        Assert.Equal("second", hud.SelectedActor!.Id);
        Assert.Equal(BattleAbility.Item, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
    }

    [Fact]
    public void EachCommandRestoresItsOwnAcceptedAction()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        Submit(hud, BattleAbility.Item, 0);
        Submit(hud, BattleAbility.GuardianForce, 1);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        OpenCommand(hud, BattleAbility.Item);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        OpenCommand(hud, BattleAbility.GuardianForce);
        Assert.Equal(new[] { "First GF", "Second GF" }, hud.ChoiceLabels);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledOrRejectedActionDoesNotReplaceAcceptedMemory(bool reject)
    {
        bool rejecting = false;
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(Create(), input =>
        {
            if (rejecting)
            {
                throw new InvalidOperationException("Unavailable");
            }
            inputs.Add(input);
        });
        Submit(hud, BattleAbility.Magic, 1);
        OpenCommand(hud, BattleAbility.Magic);
        hud.Choose(0);
        if (reject)
        {
            rejecting = true;
            hud.SelectTarget("enemy", true);
            Assert.True(hud.SelectingTarget);
        }
        hud.Navigate(MenuInput.Cancel);
        hud.Navigate(MenuInput.Cancel);
        Assert.Single(inputs);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void CancelledDrawCastDoesNotReplaceAcceptedSpellMemory()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        OpenDraw(hud);
        hud.Choose(1);
        OpenDraw(hud);
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        Assert.True(hud.SelectingDrawCastRecipient);
        hud.Navigate(MenuInput.Cancel);
        hud.Navigate(MenuInput.Cancel);
        OpenDraw(hud);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        Assert.False(hud.DrawCast);
    }

    [Fact]
    public void NewCharacterAndSameCharacterReselectionUseTheirOwnMemory()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.SelectParty("second");
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.SelectParty("first");
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void RealQueuedActionKeepsMemoryWithItsSubmittingActorThroughHandoff()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition[] spells = [.. assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic && action.Id is 1 or 2).OrderBy(action => action.Id)];
        BattleSession battle = CreateHosted(game, assets, spells);
        BattleHudController hud = Hud(battle, input => battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        Submit(hud, BattleAbility.Magic, 1);
        hud.BuildView();
        Assert.Equal("second", hud.SelectedActor!.Id);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        WaitForNextCommand(battle);
        Assert.Equal(9, battle.Combatants.Single(actor => actor.Id == "first").MagicStocks.Single(stock => stock.Id == spells[1].Id).Count);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(spells[1].Id, hud.SelectedAction!.Id);
    }

    [Fact]
    public void DrawRestoresAcceptedSpellAcrossDuplicateSourceEntries()
    {
        BattleSession battle = Create(definition => definition with
        {
            Combatants =
            [
                .. definition.Combatants,
                definition.Combatants[2] with { Id = "reordered", NativeDrawSlots = [new(2, 0), new(1, 0), new(0, 0), new(0, 0)] },
                definition.Combatants[2] with { Id = "missing", NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ]
        });
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs.Add);
        OpenDraw(hud);
        Assert.Equal(new[] { "Fire", "Blizzard" }, hud.ChoiceLabels);
        hud.Choose(1);
        Assert.Equal(2, Assert.Single(inputs).ActionId);
        OpenDraw(hud);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Up);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        OpenDraw(hud);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
    }

    [Fact]
    public void DepletedAcceptedItemFallsBackToFirstRemainingChoiceAndNavigatesFromIt()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition[] items = [.. assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Item && action.Available && !action.Allies && action.NativeAction is { AttackType: 2, Power: > 0 } native && ExpandedBattleActionRules.SupportsPlayerAction(action.Ability, native)).OrderBy(action => action.Id).Take(3)];
        BattleSession battle = CreateHosted(game, assets, items);
        BattleHudController hud = Hud(battle, input => battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        Submit(hud, BattleAbility.Item, 1);
        Assert.DoesNotContain(battle.Items, stock => stock.Id == items[1].Id);
        WaitForNextCommand(battle);
        hud.SelectParty("first");
        OpenCommand(hud, BattleAbility.Item);
        Assert.Equal(new[] { items[0].Name, items[2].Name }, hud.ChoiceLabels);
        Assert.Equal(items[0].Id, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(items[2].Id, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Up);
        Assert.Equal(items[0].Id, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
    }

    [Fact]
    public void CursorPreferenceChangesApplyToNewSelectionsBetweenAcceptedTurns()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.CursorMemory = false;
        Submit(hud, BattleAbility.Item, 1);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        hud.CursorMemory = true;
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Choose(0);
        hud.SelectTarget("enemy", true);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(1, hud.SelectedAction!.Id);
    }

    private static BattleHudController Hud(BattleSession battle, Action<BattleInput> execute)
    {
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: execute, readPresentationUi: BattleHudTestPresentation.Visible) { CursorMemory = true };
    }

    private static void OpenCommand(BattleHudController hud, BattleAbility command)
    {
        hud.Choose(hud.Commands.ToList().IndexOf(command));
        Assert.True(hud.SelectingAction);
        Assert.Equal(command, hud.SelectedCommand);
    }

    private static void OpenDraw(BattleHudController hud)
    {
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Draw));
        Assert.True(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.False(hud.DrawCast);
    }

    private static void Submit(BattleHudController hud, BattleAbility command, int index)
    {
        OpenCommand(hud, command);
        hud.Choose(index);
        Assert.True(hud.SelectingTarget);
        hud.SelectTarget("enemy", true);
        Assert.False(hud.SelectingTarget);
    }

    private static BattleSession Create(Func<BattleDefinition, BattleDefinition>? transform = null)
    {
        BattleCombatantDefinition Player(string id) => new()
        {
            Id = id,
            Name = id,
            IsPlayer = true,
            MaximumHp = 1000,
            CurrentHp = 1000,
            InitialAtb = BattleSession.AtbMaximum,
            Speed = 100,
            Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Item, BattleAbility.GuardianForce, BattleAbility.Draw],
            MagicStocks = [new(1, 10), new(2, 10)],
            EquipmentIds = [1, 2],
            GuardianForceCompatibility = new ushort[16]
        };
        BattleDefinition definition = new()
        {
            Combatants =
            [
                Player("first"),
                Player("second"),
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeDrawSlots = [new(1, 0), new(2, 0), new(0, 0), new(0, 0)] }
            ],
            Items = [new(1, 10), new(2, 10)],
            NativeBattleSpeed = 0,
            NativeKnownMagic = BattleHudTestActions.KnownMagic,
            GuardianForces = [new(1, 1000, 1000, true), new(2, 1000, 1000, true)],
            ActionDefinitions =
            [
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false)),
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.Magic, 2, "Blizzard", BattleActionEffect.Damage, 10, false)),
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.Item, 1, "First item", BattleActionEffect.Damage, 10, false)),
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.Item, 2, "Second item", BattleActionEffect.Damage, 10, false)),
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.GuardianForce, 1, "First GF", BattleActionEffect.Damage, 10, false, AllTargets: true)),
                BattleHudTestActions.WithNativeRecord(new(BattleAbility.GuardianForce, 2, "Second GF", BattleActionEffect.Damage, 10, false, AllTargets: true))
            ]
        };
        return new(transform is null ? definition : transform(definition), BattleAtbMode.TurnBased);
    }

    private static BattleSession CreateHosted(GameAssets game, BattleAssets assets, BattleActionDefinition[] actions)
    {
        Assert.InRange(actions.Length, 2, 3);
        BattleDefinition source = NativeBattleTestDefinitions.CreateTargetGuardianDefinition(game, assets, 2, 201, out _);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleStock[] magic = actions.Where(action => action.Ability == BattleAbility.Magic).Select(action => new BattleStock(action.Id, 10)).ToArray();
        BattleStock[] items = actions.Where(action => action.Ability == BattleAbility.Item).Select((action, index) => new BattleStock(action.Id, index == 1 ? 1 : 10)).ToArray();
        BattleCombatantDefinition first = source.Combatants[0] with { Id = "first", Speed = 100, Abilities = [BattleAbility.Attack, actions[0].Ability], MagicStocks = magic, EquipmentIds = [] };
        BattleCombatantDefinition second = first with { Id = "second", Name = "Quistis", ModelId = 3, WeaponId = vitals.Records[3].Weapon, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 3) };
        BattleCombatantDefinition enemy = source.Combatants[1] with { NativeCombatantFlags = 8 };
        BattleCombatantDefinition[] actors = [first, second, enemy];
        BattleDefinition definition = source with
        {
            Combatants = actors,
            ActionDefinitions = actions,
            Items = items,
            GuardianForces = [],
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects(actions.Select(action => action.NativeAction!.MagicId).Distinct().ToArray())
        };
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(definition.EncounterId!.Value));
        BattleSession battle = NativeBattleTypedFixture.CreateBattle(definition, BattleAtbMode.TurnBased, graphics);
        for (int frame = 0; frame < 400 && (!battle.CanChoosePlayerCommand("first") || !battle.CanChoosePlayerCommand("second")); frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("first"));
        Assert.True(battle.CanChoosePlayerCommand("second"));
        return battle;
    }

    private static void WaitForNextCommand(BattleSession battle)
    {
        for (int frame = 0; frame < 1200 && !battle.CanChoosePlayerCommand("first"); frame++)
        {
            if (battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.CanChoosePlayerCommand("second"))
            {
                battle.AdvanceTurn();
            }
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand("first"));
    }
}
