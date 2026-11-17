using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleMassTargetTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(1)]
    public void InstalledOilboyleSonicWaveDamagesEachLivingAllyOnce(int livingAllyCount)
    {
        const string actionName = "Sonic Wave";
        using GameWorld world = StartBattle(79, null, false, out GameSession session, livingAllyCount);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer && actor.IsAlive).ToArray();
        Assert.Equal(livingAllyCount, allies.Length);
        Assert.Equal(11, battle.Combatants.Count(actor => !actor.IsPlayer));
        Assert.All(battle.ReadDefinition().Combatants.Where(actor => !actor.IsPlayer), enemy =>
        {
            Assert.Equal(99, enemy.ModelId);
            Assert.NotNull(enemy.NativeAi);
        });
        Assert.Contains(battle.ReadDefinition().NativeActions, action => action.Command == 8 && action.ActionId == 182 && action.Name == actionName);
        BattlePresentationProcess? presentation = null;
        uint? sonicSound = null;
        HashSet<string> tintedTargets = [];
        bool drewNativeOverlay = false;
        bool finished = false;
        for (int turn = 0; turn < 24 && !finished; turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            for (int frame = 0; frame < 150; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.Presentation is { ActiveActionToken: not null, Graphics.OriginalEffect: { ReturnedDescriptor: > 0 } identity } current)
                {
                    Assert.Equal(NativeBattleSonic139Profile.Effect, identity.RequestedEffect);
                    Assert.Equal(NativeBattleSonic139Profile.Entry, identity.Binding.Entry);
                    Assert.Equal(NativeBattleSonic139Profile.Initializer, identity.Binding.Initializer);
                    sonicSound = Assert.IsType<NativeBattleSonic139Resources>(identity.Binding.Sonic139Resources).Sound;
                    presentation ??= current;
                    Assert.Same(presentation, current);
                    drewNativeOverlay |= current.PreparedPresentation?.Primitives?.LastCommands.Any(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Opcode is >= 0x20 and < 0x80) == true;
                    foreach (BattleModelEntry<BattleModelOwner> entry in current.Graphics.ActorModels.Entries.Where(entry => (entry.State.Flags0 & 0x800) != 0))
                    {
                        tintedTargets.Add(Assert.IsType<BattlePartyActorModel>(entry.Owner).Actor.ActorId);
                    }
                }
                if (Contacts().Length > 0 && battle.ActiveAction is null)
                {
                    finished = true;
                    break;
                }
            }
        }

        Assert.True(finished);
        BattleEvent first = Assert.IsType<BattleEvent>(Contacts().FirstOrDefault());
        BattleEvent[] contacts = Contacts();
        Assert.Equal(allies.Select(actor => actor.Id).Order(), contacts.Select(entry => entry.TargetId).Order());
        Assert.All(contacts, contact =>
        {
            Assert.Equal(first.ActorId, contact.ActorId);
            Assert.Equal(BattleAbility.EnemyAbility, contact.Ability);
            Assert.Equal((byte)11, contact.NativeSequence);
            Assert.True(contact.Amount > 0);
        });
        BattleEvent[] damage = battle.Events.Where(entry => entry.ActorId == first.ActorId && entry.Kind == "Damage" && entry.Ability == BattleAbility.EnemyAbility && entry.NativeSequence == 11).ToArray();
        Assert.Equal(contacts.Select(entry => (entry.TargetId, entry.Amount)).Order(), damage.Select(entry => (entry.TargetId, entry.Amount)).Order());
        Assert.All(allies, ally => Assert.InRange(ally.CurrentHp, 1, ally.MaximumHp - 1));
        Assert.Equal(11 - livingAllyCount, battle.Combatants.Count(actor => actor.IsPlayer && !actor.IsAlive));
        Assert.NotNull(presentation);
        Assert.Null(presentation.ActiveActionToken);
        Assert.True(drewNativeOverlay);
        Assert.Equal(allies.Select(actor => actor.Id).Order(), tintedTargets.Order());
        Assert.DoesNotContain(presentation.Graphics.ActorModels.Entries, entry => (entry.State.Flags0 & 0x800) != 0);
        Assert.True(damage.Max(entry => entry.Frame) < contacts.Min(entry => entry.Frame));
        Assert.Single(battle.Events, entry => entry.ActorId == first.ActorId && entry.Kind == "NativeModuleSound" && entry.NativeModuleSound?.Sound == sonicSound);
        Assert.Equal(allies.Select(actor => actor.Id), contacts.Select(entry => entry.TargetId));
        Assert.All(contacts.Zip(contacts.Skip(1)), pair => Assert.Equal(4L, pair.Second.Frame - pair.First.Frame));

        BattleEvent[] Contacts() => battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.Message == actionName).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledMegaPotionHealsEachLivingAllyAndConsumesOneItem(bool includeKnockedOutAlly)
    {
        const byte itemId = 6;
        using GameWorld world = StartBattle(0, itemId, includeKnockedOutAlly, out _);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer && actor.IsAlive).ToArray();
        Assert.Equal(includeKnockedOutAlly ? 10 : 11, allies.Length);
        BattleCombatant caster = Assert.Single(allies, ally => ally.Id == "party-10");
        BattleActionDefinition item = Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == itemId);
        Assert.Equal("Mega-Potion", item.Name);
        Assert.True(item.Allies && item.AllTargets && battle.SupportsExpandedAction(item));
        BattleActionDefinition megalixir = Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Name == "Megalixir");
        Assert.True(battle.SupportsExpandedAction(megalixir));
        Assert.Equal(2, Stock());
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Item));
        hud.Choose(hud.ActionChoices.ToList().FindIndex(action => action.Id == item.Id));
        Assert.True(hud.SelectingTargetGroup);
        Assert.Equal(allies.Select(ally => ally.Id), hud.Targets.Select(target => target.Id));
        Assert.True(hud.CanChangeTargetSide);
        hud.ChangeTargetSide();
        Assert.All(hud.Targets, target => Assert.False(target.IsPlayer));
        hud.ChangeTargetSide();
        Assert.Equal(allies.Select(ally => ally.Id), hud.Targets.Select(target => target.Id));
        hud.Attack();
        Assert.True(caster.IsBusy);
        int[] before = [];
        for (int frame = 0; frame < 600 && caster.IsBusy; frame++)
        {
            before = allies.Select(ally => ally.CurrentHp).ToArray();
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        Assert.False(caster.IsBusy);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.Item).ToArray();
        Assert.Equal(allies.Select(actor => actor.Id), contacts.Select(entry => entry.TargetId));
        Assert.Single(contacts.Select(entry => entry.Frame).Distinct());
        Assert.All(contacts, contact => Assert.Equal(1000, contact.Amount));
        Assert.Equal(before.Select(hp => hp + 1000), allies.Select(ally => ally.CurrentHp));
        Assert.Equal(1, Stock());
        Assert.Equal(includeKnockedOutAlly ? 1 : 0, battle.Combatants.Count(actor => actor.IsPlayer && !actor.IsAlive));

        int Stock() => battle.Items.Single(stock => stock.Id == item.Id).Count;
    }

    [Fact]
    public void MegaPotionKeepsItsGroupWhenTheSelectedAnchorFallsDuringWindup()
    {
        const byte itemId = 6;
        using GameWorld world = StartBattle(0, itemId, false, out _);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-10");
        BattleCombatant anchor = Assert.Single(battle.Combatants, actor => actor.Id == "party-9");
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = caster.Id, TargetId = anchor.Id, Ability = BattleAbility.Item, ActionId = itemId });
        Assert.True(caster.IsBusy);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction?.Phase);
        anchor.CurrentHp = 0;
        anchor.NativeStatus0 |= 1;
        for (int frame = 0; frame < 600 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }

        Assert.False(caster.IsBusy);
        Assert.False(anchor.IsAlive);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id).ToArray();
        Assert.Equal(10, contacts.Length);
        Assert.DoesNotContain(contacts, contact => contact.TargetId == anchor.Id);
        Assert.Equal(1, battle.Items.Single(stock => stock.Id == itemId).Count);
    }

    private static GameWorld StartBattle(ushort formation, byte? itemId, bool includeKnockedOutAlly, out GameSession session, int? sonicLivingAllyCount = null)
    {
        const ushort fieldId = 133;
        string directory = TestResources.GameDirectory;
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        session = new(initial.State with
        {
            FieldId = fieldId,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initial.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory);
        try
        {
            world.EnsureField();
            world.Apply(new(PlayerActionKind.Wait, Frames: 4));
            session.SetCharacterVitals(initial.State.CharacterVitals! with { CombatParty = [0, 1, 2] });
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 11, AdditionalEnemies = 10 });
            NativeCharacterVitals vitals = session.State.CharacterVitals!.WithExtendedRoster(true).Rest();
            NativeCharacterVital[] records = vitals.Records.Select(record => record with
            {
                MemberFlags = (ushort)(record.MemberFlags | 1),
                BonusSources = null,
                HitPointBonus = 2400
            }).ToArray();
            vitals = vitals with { Records = records };
            if (itemId is byte id)
            {
                byte itemCommand = checked((byte)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4)));
                records[10] = records[10] with { Junction = records[10].Junction with { Commands = itemCommand } };
                NativeEconomy economy = session.State.Economy!;
                NativeItemStock[] items = economy.Items.ToArray();
                int existing = Array.FindIndex(items, stock => stock.ItemId == id);
                items[existing >= 0 ? existing : Array.FindIndex(items, stock => stock.ItemId == 0)] = new(id, 2);
                session.SetEconomy(economy with { Items = items });
            }
            for (int row = 0; row < records.Length; row++)
            {
                ushort hp = itemId is null ? vitals.GetMagicMaximumHitPoints(row) : (ushort)100;
                records[row] = records[row] with { HitPoints = new NativeHpWord(hp, hp), Status = 0 };
            }
            if (includeKnockedOutAlly)
            {
                vitals.Records[9] = vitals.Records[9] with { HitPoints = new NativeHpWord(0, 0), Status = 1 };
            }
            if (sonicLivingAllyCount is int livingAllyCount)
            {
                for (int row = livingAllyCount; row < records.Length; row++)
                {
                    records[row] = records[row] with { HitPoints = new NativeHpWord(0, 0), Status = 1 };
                }
            }
            byte[] selectedRows = Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount).Select(row => (byte)row).ToArray();
            session.SetCharacterVitals(vitals.WithBattleParty(selectedRows, NativeCharacterVitals.ExtendedRecordCount));
            string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
            session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, fieldId, world.Events.Frame, formation, source)));
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.True(battle.UsesExpandedRoster);
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }
}
