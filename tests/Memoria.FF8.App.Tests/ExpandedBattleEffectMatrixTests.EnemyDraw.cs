using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private const int OchuDrawEffect = 37;
    private const ushort OchuDrawAction = 194;
    private const int OchuStockSlot = 17;
    private const uint HasMagicAtEntry = 1u << 30;

    [Theory]
    [InlineData(530)]
    [InlineData(632)]
    public void Installed_Ochu_defers_Draw_from_the_eleventh_ally_until_the_original_contact(ushort formation)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        (string status, string detail) = RunExpandedEnemyDraw(directory, PrepareField(directory), assets, formation);
        Assert.True(status == "logical", detail);
    }

    [Fact]
    public void Installed_Vysage_queues_Draw_Cast_from_original_medium_band_script()
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        const int formation = 590;
        const byte fire = 1;
        const string casterId = "enemy-10";
        string ally = CasterId;
        BattleEnemyPlacement prototype = Assert.Single(assets.LoadEncounter(formation).Enemies);
        Assert.Equal(39, prototype.ModelId);
        GameState field = PrepareField(directory);
        for (byte seed = 0; seed < 24; seed++)
        {
            using GameWorld world = StartBattle(directory, field, formation, nativeSeed: seed, prepareParty: records =>
            {
                for (int row = 0; row < records.Length; row++)
                {
                    NativeMagicStock[] slots = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
                    if (row == 10)
                    {
                        slots[17] = new(fire, 100);
                    }
                    records[row] = records[row] with { MagicInventory = slots };
                }
            }, prepareVitals: vitals => PrepareHigherBandParty(directory, vitals, 33));

            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
            Assert.NotNull(caster.NativeAi);
            if (caster.NativeAi.LevelBand != 1)
            {
                continue;
            }
            Assert.Equal(assets.ReadEnemyAi(39, caster.Level).Scripts[1], caster.NativeAi.Scripts[1]);
            caster.Atb = BattleSession.AtbMaximum;
            for (int frame = 0; frame < 240 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                BattleAttackCommand? cast = battle.QueuedActions.FirstOrDefault(action => action.ActorId == casterId && action is { NativeQueuedCommand: 2, NativeSequence: 11, ActionId: fire });
                if (cast is null)
                {
                    continue;
                }
                BattleSnapshot pending = battle.Capture();
                Assert.Equal(new BattlePendingEnemyDraw(casterId, fire, 1), pending.PendingEnemyDraw);
                Assert.True(pending.QueuedActions.Any(action => action.ActorId == casterId && action is { NativeQueuedCommand: 8, ActionId: 48 } && action.TargetId == ally) ||
                    pending.ActiveAction is { ActorId: casterId, NativeQueuedCommand: 8, ActionId: 48 } active && active.TargetId == ally);
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
                int start = battle.Events.Count;
                for (int tick = 0; tick < 240 && !battle.Events.Skip(start).Any(IsCastContact); tick++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    restored.Tick();
                }
                Assert.Equal(cast.TargetId, Assert.Single(battle.Events.Skip(start), IsCastContact).TargetId);
                BattleEvent debit = Assert.Single(battle.Events.Skip(start), entry => entry.Kind == "EnemyDrawStock");
                Assert.Equal(1, debit.Amount);
                Assert.Equal(99, battle.Combatants.Single(actor => actor.Id == debit.TargetId).NativeMagicSlots[17].Count);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                return;
            }
        }
        Assert.Fail("The original Vysage turn did not queue Draw Cast with a stocked ally.");

        static bool IsCastContact(BattleEvent entry) => entry is { Kind: "EffectContact", ActorId: casterId, NativeSequence: 11, Message: "Fire" };
    }

    private static (string Status, string Detail) RunExpandedEnemyDraw(string directory, GameState field, BattleAssets assets, ushort formation)
    {
        BattleEncounter scene = assets.LoadEncounter(formation);
        Assert.Equal(105, scene.StageId);
        BattleEnemyPlacement prototype = Assert.Single(scene.Enemies);
        Assert.Equal(45, prototype.ModelId);
        Assert.Equal(0, prototype.Slot);
        Assert.Equal(255, prototype.Level);
        Assert.False(prototype.Hidden || prototype.InitiallySuspended || prototype.Untargetable);
        BattleNativeAction draw = Assert.Single(assets.NativeActions, action => action is { Command: 8, ActionId: OchuDrawAction });
        Assert.Equal(OchuDrawEffect, draw.MagicId);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action is { Ability: BattleAbility.Magic, Id: 1 });
        using GameWorld world = StartBattle(directory, field, formation, fire, prepareHigherBand: true, prepareParty: records =>
        {
            NativeMagicStock stock = records[10].MagicInventory[0];
            for (int row = 0; row < records.Length; row++)
            {
                NativeMagicStock[] slots = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
                if (row == 10)
                {
                    slots[OchuStockSlot] = stock;
                }
                records[row] = records[row] with { MagicInventory = slots };
            }
        });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        BattleCombatant ochu = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-10");
        Assert.Equal(10, caster.Slot!.Value.Position);
        Assert.Equal(10, ochu.Slot!.Value.Position);
        Assert.Equal(prototype.ModelId, ochu.ModelId);
        Assert.InRange(ochu.Level, 30, 100);
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(ochu.NativeAi);
        Assert.Equal(HigherEnemyBand, ai.LevelBand);
        Assert.Equal(assets.ReadEnemyAi(ochu.ModelId, ochu.Level).Scripts[2], ai.Scripts[2]);
        Assert.Equal(new byte[] { 38, 0, 200, 0, 46, 12, 3, 41 }, ai.Scripts[2][27..35]);
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + 3];
        Assert.Equal(draw.Command, ability.Command);
        Assert.Equal(draw.ActionId, ability.Id);
        Assert.Equal((byte)12, ability.Sequence);
        Assert.Equal(new NativeMagicStock((byte)fire.Id, InitialStock), caster.NativeMagicSlots[OchuStockSlot]);
        BattleSnapshot entered = battle.Capture();
        Assert.Null(entered.PendingEnemyDraw);
        Assert.Equal(CasterId, Assert.Single(entered.Combatants, actor => (actor.NativeStatus!.Battle & HasMagicAtEntry) != 0).Id);

        caster.Atb = BattleSession.AtbMaximum;
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        int command = hud.Commands.ToList().IndexOf(BattleAbility.Magic);
        Assert.InRange(command, 0, hud.Commands.Count - 1);
        hud.Choose(command);
        int spell = hud.ActionChoices.ToList().FindIndex(action => action.Id == fire.Id);
        Assert.InRange(spell, 0, hud.ActionChoices.Count - 1);
        hud.Choose(spell);
        Assert.True(hud.SelectingTarget);
        hud.SelectTarget(ochu.Id, false);
        hud.Attack();
        Assert.Contains(battle.QueuedActions, action => action.ActorId == caster.Id && action.TargetId == ochu.Id && action.Ability == BattleAbility.Magic && action.ActionId == fire.Id);

        for (int frame = 0; frame < 320 && (battle.ExpandedPresentationProcess?.ActiveActionToken is not null || !battle.QueuedActions.Any(IsOchuDraw)); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        BattleAttackCommand response = Assert.Single(battle.QueuedActions, IsOchuDraw);
        Assert.Equal(caster.Id, response.TargetId);
        Assert.Equal(caster.Id, Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(response.TargetRequest.Selection).Actors.Select(actor => actor.ActorId)));
        Assert.Equal(ability.Sequence, response.NativeSequence);
        Assert.Contains(battle.Events, entry => entry is { Kind: "EffectContact", Ability: BattleAbility.Magic, ActorId: CasterId } && entry.TargetId == ochu.Id);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EnemyDrawStock");
        Assert.Equal(new NativeMagicStock((byte)fire.Id, InitialStock - 1), caster.NativeMagicSlots[OchuStockSlot]);
        BattleSnapshot before = battle.Capture();
        Assert.Equal(new BattlePendingEnemyDraw(ochu.Id, (byte)fire.Id, 1), before.PendingEnemyDraw);
        NativeBattleParticipantSnapshot participant = Assert.IsType<NativeBattleParticipantSnapshot>(before.Combatants.Single(actor => actor.Id == ochu.Id).NativeParticipant);
        Assert.Equal(caster.Id, participant.LastAttackerId);
        Assert.Equal<byte?>(1, participant.LastAction?.AttackType);
        Assert.Equal(caster.Id, Assert.IsType<BattlePreviousTarget.Actor>(participant.PreviousTarget).Reference.ActorId);
        Assert.Equal<byte?>((byte)fire.Id, participant.DrawnSpell);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(restored.Capture()));

        int liveEventStart = battle.Events.Count;
        int restoredEventStart = restored.Events.Count;
        for (int frame = 0; frame < 160 && !battle.Events.Skip(liveEventStart).Any(IsOchuCompletion); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            restored.Tick();
        }

        BattleEvent[] events = battle.Events.Skip(liveEventStart).ToArray();
        BattleEvent debit = Assert.Single(events, entry => entry.Kind == "EnemyDrawStock");
        Assert.Equal(ochu.Id, debit.ActorId);
        Assert.Equal(caster.Id, debit.TargetId);
        Assert.Equal(1, debit.Amount);
        BattleEvent contact = Assert.Single(events, entry => entry.Kind == "EffectContact" && entry.ActorId == ochu.Id);
        Assert.Equal(caster.Id, contact.TargetId);
        Assert.Equal(ability.Sequence, contact.NativeSequence);
        Assert.Equal(contact.Frame, debit.Frame);
        Assert.Single(events, IsOchuCompletion);
        Assert.Equal(debit, Assert.Single(restored.Events.Skip(restoredEventStart), entry => entry.Kind == "EnemyDrawStock"));
        Assert.Equal(new NativeMagicStock(0, 0), caster.NativeMagicSlots[OchuStockSlot]);
        BattleSnapshot after = battle.Capture();
        Assert.Null(after.PendingEnemyDraw);
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
        return ("logical", $"Formation {formation}, Ochu callback 2 command {response.NativeQueuedCommand}/{response.ActionId}, sequence {response.NativeSequence}, removed one Fire stock from {caster.Id} at its single contact and restored from the queued JSON boundary.");

        bool IsOchuDraw(BattleAttackCommand action) => action.ActorId == ochu.Id && action is { NativeAiCallbackStage: 2, NativeQueuedCommand: 8, ActionId: OchuDrawAction };

        bool IsOchuCompletion(BattleEvent entry) => entry.ActorId == ochu.Id && entry.Kind == "ActionComplete" && entry.TargetId == caster.Id;
    }
}
