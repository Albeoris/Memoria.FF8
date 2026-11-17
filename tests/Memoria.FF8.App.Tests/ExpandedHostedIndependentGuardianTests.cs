using System.Collections.Immutable;
using System.Reflection;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedIndependentGuardianTests
{
    private const string CasterId = "party-3";
    private const int MiniMogCommandKey = (38 << 16) | 255;
    private const int CallbackBudget = 3200;

    [Theory]
    [InlineData("MiniMog")]
    [InlineData("MiniMogUnavailable")]
    [InlineData("Chocobo")]
    [InlineData("ChocoboAway")]
    [InlineData("Phoenix")]
    public void Installed_independent_guardian_or_MiniMog_completes_on_the_attached_process(string route)
    {
        bool miniMog = route.StartsWith("MiniMog", StringComparison.Ordinal);
        bool phoenix = route == "Phoenix";
        int item = phoenix ? 31 : 30;
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState field = ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) };
        NativePocketStationState pocket = Assert.IsType<NativePocketStationState>(field.PocketStation);
        byte flags = route switch
        {
            "MiniMog" => 0x0D,
            "Chocobo" => 1,
            "ChocoboAway" => 3,
            _ => 0
        };
        byte[] name = new byte[12];
        FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("Kweh").CopyTo(name, 0);
        pocket = pocket with { Raw = pocket.Raw.SetItem(0, flags).SetItem(1, 20).SetItem(45, 1), ChocoboName = name.ToImmutableArray() };
        NativeEconomy economy = Assert.IsType<NativeEconomy>(field.Economy);
        economy = economy with { Gil = 10000 };
        if (!miniMog)
        {
            NativeItemStock[] items = economy.Items.ToArray();
            int existing = Array.FindIndex(items, stock => stock.ItemId == item);
            int slot = existing >= 0 ? existing : Array.FindIndex(items, stock => stock.ItemId == 0);
            items[slot] = new((byte)item, 2);
            economy = economy with { Items = items };
        }

        GameSession session = new(field with { Economy = economy, PocketStation = pocket });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: assets);
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, miniMog, phoenix));
            RunAction(world, session, route, item, miniMog, phoenix);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
    }

    private static void PrepareParty(GameSession session, bool miniMog, bool phoenix)
    {
        ExpandedBattleGuardianTests.PrepareParty(session, learnedBoost: false, guardianId: 0);
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        if (miniMog)
        {
            vitals = vitals.TeachGuardianForceAbility(0, 38).SetJunctionAbility(3, 0, 38, true);
            NativeGuardianForceVital[] guardians = vitals.GuardianForces.ToArray();
            guardians[0] = guardians[0] with { HitPoints = 1 };
            vitals = vitals with { GuardianForces = guardians };
        }
        else
        {
            int command = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4);
            Assert.True(command >= 0);
            NativeCharacterVital caster = vitals.Records[3];
            vitals.Records[3] = caster with { Junction = caster.Junction with { Commands = (uint)(20 + command) } };
        }
        if (phoenix)
        {
            NativeCharacterVital victim = vitals.Records[1];
            vitals.Records[1] = victim with { HitPoints = new(0, 0), Status = 1 };
        }
        session.SetCharacterVitals(vitals);
    }

    private static void RunAction(GameWorld world, GameSession session, string route, int item, bool miniMog, bool phoenix)
    {
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleSession child = Assert.IsType<BattleSession>(typeof(BattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(CasterId);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(owner, out _));
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        BattleDefinition definition = battle.ReadDefinition();
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable).ToArray();
        BattleCombatant target = miniMog ? caster : enemies[0];
        int oldResults = session.State.AppliedBattleResultIds.Length;
        int firstEvent = battle.Events.Count;
        uint oldGil = battle.Capture().NativeGil!.Gil;
        if (phoenix)
        {
            Assert.False(Assert.Single(battle.Combatants, actor => actor.Id == "party-1").IsAlive);
            foreach (BattleCombatant enemy in enemies)
            {
                enemy.CurrentHp = 1;
            }
        }

        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id,
            Ability: miniMog ? BattleAbility.NativeCommand : BattleAbility.Item, ActionId: miniMog ? MiniMogCommandKey : item));
        if (!miniMog)
        {
            Assert.Equal(1, battle.Items.Single(stock => stock.Id == item).Count);
        }

        HashSet<BattleActionHeaderReference> seenHeaders = [];
        List<NativeBattleActionHeaderSlot> headers = [];
        Dictionary<BattleContactOccurrence, BattleModelOwner> expected = [];
        List<NativeBattlePresentationReceipt> receipts = [];
        for (int frame = 0; frame < CallbackBudget && !Completed(battle, firstEvent); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            foreach (var header in child.NativeActionPackets.PublishedHeaders().Where(header => header.Slot.Source == owner))
            {
                if (seenHeaders.Add(header.Reference))
                {
                    headers.Add(header.Slot);
                    foreach (NativeBattleContactPacket contact in child.NativeActionPackets.ReadHeaderContacts(header.Reference))
                    {
                        expected.Add(contact.Occurrence!.Value, contact.Primary.Recipient);
                    }
                }
            }
            if (process.LastFrameServices is { } services)
            {
                receipts.AddRange(services.ReadOrderedReceipts());
            }
            Assert.Same(process, battle.Presentation);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
        }
        Assert.Single(battle.Events.Skip(firstEvent), entry => entry is { Kind: "ActionComplete", ActorId: CasterId });
        Assert.Null(battle.ActiveAction);
        Assert.Null(process.ActiveActionToken);
        NativeBattleContactPacket[] delivered = receipts.OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet).ToArray();
        Assert.Equal(expected.Count, delivered.Length);
        Assert.Equal(expected.Count, delivered.Select(packet => packet.Occurrence).Distinct().Count());
        Assert.All(delivered, contact => Assert.Equal(expected[contact.Occurrence!.Value], contact.Primary.Recipient));

        if (route is "MiniMogUnavailable" or "ChocoboAway")
        {
            AssertHeader(Assert.Single(headers), 0, 9, 0, 0);
            Assert.Empty(delivered);
        }
        else if (miniMog)
        {
            BattleNativeAction dance = Assert.Single(definition.NativeActions, action => action is { Command: 240, ActionId: 6 });
            AssertHeader(Assert.Single(headers), 38, (ushort)dance.KernelIndex, dance.MagicId, 4);
            Assert.Equal(battle.Combatants.Where(actor => actor.IsPlayer && actor.IsAlive && !actor.Untargetable).Select(actor => battle.ReadPresentationModelOwner(actor.Id)).ToHashSet(),
                delivered.Select(contact => contact.Primary.Recipient).ToHashSet());
            BattleGuardianForceVital guardian = Assert.Single(battle.GuardianForces, force => force.Id == 0);
            Assert.Equal(1 + 20 * guardian.MaximumHitPoints / 100, (int)guardian.HitPoints);
            uint fee = (uint)(100 * (battle.Combatants.Where(actor => actor.IsPlayer).Sum(actor => actor.Level) / 4));
            Assert.Equal(oldGil - fee, battle.Capture().NativeGil!.Gil);
        }
        else
        {
            ushort childId = phoenix ? Assert.Single(definition.NativeActions, action => action is { Command: 4, ActionId: 31 }).MagicId : (ushort)3;
            BattleNativeAction summon = Assert.Single(definition.NativeActions, action => action.Command == 244 && action.ActionId == childId);
            Assert.Equal(phoenix ? 2 : 1, headers.Count);
            AssertHeader(headers[0], 244, childId, summon.MagicId, enemies.Length);
            if (phoenix)
            {
                AssertHeader(headers[1], 0, 8, 0, 4);
                BattleCombatant revived = Assert.Single(battle.Combatants, actor => actor.Id == "party-1");
                Assert.Equal(revived.MaximumHp / 8, revived.CurrentHp);
                List<BattleEvent> events = battle.Events.ToList();
                int lastDamage = events.FindLastIndex(entry => entry.Kind == "Defeated");
                int revivedAt = events.FindIndex(firstEvent, entry => entry is { Kind: "EffectHealing", TargetId: "party-1" });
                Assert.InRange(lastDamage, firstEvent, revivedAt - 1);
                Assert.InRange(revivedAt, firstEvent, battle.Events.Count - 1);
            }
        }

        int? message = route switch { "MiniMog" => 54, "MiniMogUnavailable" => 72, "ChocoboAway" => 56, _ => null };
        if (message is int messageId)
        {
            string prefix = messageId == 56 ? "Kweh" + Assert.IsType<string>(definition.NativeGlobalMessages[11])[..1] : "";
            string expectedMessage = Assert.IsType<string>(definition.NativeGlobalMessages[messageId]);
            NativeBattlePresentationDescriptorText text = Assert.Single(receipts.OfType<NativeBattlePresentationDescriptorText>(), text => text.Message is { ActorId: CasterId, NativeMessageMode: 3 });
            Assert.Equal(prefix + expectedMessage, text.Message.Message);
            Assert.Single(battle.Events.Skip(firstEvent), entry => entry.Kind == "NativeText" && entry.Message == text.Message.Message);
            int textIndex = receipts.IndexOf(text);
            Assert.InRange(textIndex, 0, receipts.FindIndex(receipt => receipt is NativeBattlePresentationCompletionRequested { ActorId: CasterId }) - 1);
        }
        if (!miniMog)
        {
            Assert.Equal(1, battle.Items.Single(stock => stock.Id == item).Count);
        }
        if (route != "MiniMog")
        {
            Assert.Equal(oldGil, battle.Capture().NativeGil!.Gil);
        }

        BattleCombatant? survivor = battle.Combatants.FirstOrDefault(enemy => !enemy.IsPlayer && enemy.IsAlive && !enemy.Untargetable && !enemy.IsEjected);
        if (survivor is null)
        {
            for (int frame = 0; frame < 500 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            string remaining = string.Join(", ", battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => $"{actor.Id}:hp={actor.CurrentHp},alive={actor.IsAlive},active={actor.IsActive},hidden={actor.Hidden},untargetable={actor.Untargetable}"));
            Assert.True(battle.Outcome == BattleOutcome.Victory, $"Terminal did not finish. Outcome={battle.Outcome}; queued={battle.QueuedActions.Count}; terminalFinished={process.TerminalFinished}; {remaining}");
            Assert.True(process.TerminalFinished);
            Assert.Equal(oldResults, session.State.AppliedBattleResultIds.Length);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(oldResults + 1, session.State.AppliedBattleResultIds.Length);
        }
        else
        {
            int attackStart = battle.Events.Count;
            caster.Atb = BattleSession.AtbMaximum;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: survivor.Id));
            for (int frame = 0; frame < 1200 && !Completed(battle, attackStart); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.Presentation);
            }
            Assert.True(Completed(battle, attackStart));
        }
    }

    private static bool Completed(BattleSession battle, int start) => battle.Events.Skip(start).Any(entry => entry is { Kind: "ActionComplete", ActorId: CasterId });

    private static void AssertHeader(NativeBattleActionHeaderSlot header, byte command, ushort action, ushort effect, int count)
    {
        Assert.Equal(command, header.Command);
        Assert.Equal(action, header.Action);
        Assert.Equal(effect, header.Effect);
        Assert.Equal(count, header.ReadContactCount());
    }
}
