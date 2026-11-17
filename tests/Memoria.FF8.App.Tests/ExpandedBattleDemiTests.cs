using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleDemiTests
{
    private const string CasterId = "party-0";
    private const string AddedAllyId = "party-3";
    private const byte DemiSpell = 13;
    private const ushort DemiEffect = 107;
    private const int InitialStock = 5;

    [Theory]
    [InlineData(1, 0u)]
    [InlineData(2, 0x20000u)]
    [InlineData(3, 0x40000u)]
    public void Installed_Demi_repeated_headers_deliver_one_contact_each_on_the_attached_roster(int casts, uint repetitionStatus)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareParty);
        BattleActionDefinition demi = Assert.Single(world.BattleResources.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == DemiSpell);
        Assert.Equal("Demi", demi.Name);
        Assert.True(demi.NativeAction is { Command: 2, KernelSection: 1, MagicId: DemiEffect, AttackType: 8, HitCount: 1, TargetFlags: 0x54 });
        Assert.False(demi.AllTargets);
        Assert.True(BattleSession.CanSelectEitherTargetSide(demi));
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        Assert.True(battle.SupportsExpandedAction(demi));
        Assert.Same(process, battle.Presentation);

        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == CasterId);
        BattleCombatant ally = battle.Combatants.Single(actor => actor.Id == AddedAllyId);
        NativeBattleModelAliases aliases = process.ActionPackets.ModelAliases;
        BattleCombatant enemy = battle.Combatants.First(actor => !actor.IsPlayer && BattleSession.IsValidSelectedActionTarget(caster, demi, actor) && !aliases.TryEncodeExact(battle.ReadPresentationModelOwner(actor.Id), out _));
        Assert.False(aliases.TryEncodeExact(battle.ReadPresentationModelOwner(ally.Id), out _));
        Assert.False(aliases.TryEncodeExact(battle.ReadPresentationModelOwner(enemy.Id), out _));
        Assert.Equal(0u, ally.NativeCombatantFlags & 0x10000);
        BattleCombatant[] targets = casts switch
        {
            1 => [ally],
            2 => [enemy, ally],
            3 => [ally, enemy, ally],
            _ => throw new ArgumentOutOfRangeException(nameof(casts))
        };
        Assert.All(targets, target => Assert.True(BattleSession.IsValidSelectedActionTarget(caster, demi, target)));
        int allyHp = ally.CurrentHp;
        int enemyHp = enemy.CurrentHp;
        bool enemyImmune = (enemy.NativeCombatantFlags & 0x10000) != 0;
        caster.NativeStatus1 |= repetitionStatus;
        caster.Atb = BattleSession.AtbMaximum;
        Assert.Contains(casts, battle.GetPlayerMagicCastCounts(caster.Id, DemiSpell));
        Assert.Equal(InitialStock, battle.GetAvailableMagicCasts(caster.Id, DemiSpell));

        string[] targetIds = targets.Select(target => target.Id).ToArray();
        BattleModelOwner[] targetOwners = targetIds.Select(battle.ReadPresentationModelOwner).ToArray();
        BattleModelOwner sourceOwner = battle.ReadPresentationModelOwner(caster.Id);
        int start = battle.Events.Count;
        BattleInput spell = new(BattleInputKind.Action, ActorId: caster.Id, TargetId: targetIds[0], Ability: BattleAbility.Magic, ActionId: DemiSpell);
        if (casts > 1)
        {
            spell = spell with { MagicTargets = [.. targetIds] };
        }
        world.ApplyBattleInput(spell);
        BattleContactOccurrence[]? expectedOccurrences = null;
        List<NativeBattleContactPacket> delivered = [];
        HashSet<long> tokens = [];
        for (int callback = 0; callback < 1800 && !CommandCompleted(battle, start); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            NativeBattleActionPacketsState packets = process.ActionPackets;
            if (expectedOccurrences is null)
            {
                var headers = packets.PublishedHeaders().Where(entry => entry.Slot.Source == sourceOwner && entry.Slot.Effect == DemiEffect).ToArray();
                if (headers.Length > 0)
                {
                    Assert.Equal(casts, headers.Length);
                    foreach (var header in headers)
                    {
                        Assert.Equal((ushort)DemiSpell, header.Slot.Action);
                        Assert.Equal(1, header.Slot.ReadContactCount());
                        Assert.Equal((byte)0, header.Slot.ReadRepeatModeMinusOne());
                    }
                    expectedOccurrences = headers.Select(header => packets.ResolveContact(header.Reference, 0)).ToArray();
                    Assert.Equal(casts, expectedOccurrences.Distinct().Count());
                    Assert.Equal(targetOwners, expectedOccurrences.Select(occurrence => packets.ReadContact(occurrence).Primary.Recipient));
                }
            }
            if (process.LastStepActionToken is long token)
            {
                tokens.Add(token);
                delivered.AddRange(process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));
            }
        }
        Assert.True(CommandCompleted(battle, start), "Demi did not complete on the attached process within the callback budget.");
        Assert.NotNull(expectedOccurrences);
        Assert.Equal(expectedOccurrences.Select(occurrence => (BattleContactOccurrence?)occurrence), delivered.Select(packet => packet.Occurrence));
        Assert.Equal(targetOwners, delivered.Select(packet => packet.Primary.Recipient));
        Assert.Equal(targetIds, battle.Events.Skip(start).Where(item => item.Kind == "EffectContact" && item.ActorId == caster.Id).Select(item => item.TargetId));
        Assert.Single(battle.Events.Skip(start), item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        Assert.Equal(casts, tokens.Count);
        Assert.All(tokens, token => Assert.True(process.IsActionComplete(token)));
        Assert.True(ally.CurrentHp < allyHp);
        if (casts > 1 && enemyImmune)
        {
            Assert.Equal(enemyHp, enemy.CurrentHp);
            Assert.Equal(0, Assert.Single(delivered, packet => packet.Primary.Recipient == battle.ReadPresentationModelOwner(enemy.Id)).Primary.PresentedAmount);
        }
        Assert.Equal(InitialStock - casts, battle.GetAvailableMagicCasts(caster.Id, DemiSpell));

        string nextTarget = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive).Id;
        caster.Atb = BattleSession.AtbMaximum;
        int nextStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: nextTarget));
        for (int callback = 0; callback < 1200 && !CommandCompleted(battle, nextStart); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
        }
        Assert.True(CommandCompleted(battle, nextStart), "The next ordinary attack did not complete on the same attached process.");
        Assert.Equal(nextTarget, Assert.Single(battle.Events.Skip(nextStart), item => item.Kind == "EffectContact" && item.ActorId == caster.Id).TargetId);
        Assert.Equal(InitialStock - casts, battle.GetAvailableMagicCasts(caster.Id, DemiSpell));
    }

    private static bool CommandCompleted(BattleSession battle, int start)
    {
        return battle.Events.Skip(start).Any(item => item.Kind == "ActionComplete" && item.ActorId == CasterId);
    }

    private static void PrepareParty(GameSession session)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        NativeCharacterVital caster = vitals.Records[0];
        int magic = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2);
        Assert.True(magic >= 0);
        NativeMagicStock[] stocks = caster.MagicInventory.ToArray();
        int slot = Array.FindIndex(stocks, stock => stock.SpellId == DemiSpell);
        if (slot < 0)
        {
            slot = Array.FindIndex(stocks, stock => stock.SpellId == 0);
        }
        Assert.True(slot >= 0);
        stocks[slot] = new(DemiSpell, InitialStock);
        vitals.Records[0] = caster with { MagicInventory = stocks, Junction = caster.Junction with { Commands = (uint)(20 + magic) } };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2, 3], 4));
    }
}
