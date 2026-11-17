using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedMaximumRosterTests
{
    [Fact]
    public void Installed_maximum_roster_last_participants_attack_and_continue_on_the_attached_process()
    {
        const ushort formation = 92;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession field = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) });
        field.SetExamTimer(field.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(field, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, field, SelectAllAllies, formation, allies: 11, additionalEnemies: 10);
        Assert.Contains(formation, world.Field!.RandomEncounters!.Formations);
        var authored = world.BattleResources.LoadEncounter(formation);
        Assert.Equal(4, authored.Enemies.Count);

        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer && actor.IsAlive).OrderBy(actor => actor.Slot!.Value.Position).ToArray();
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive).OrderBy(actor => actor.Slot!.Value.Position).ToArray();
        Assert.Equal(11, allies.Length);
        Assert.Equal(14, enemies.Length);
        Assert.Equal(25, allies.Concat(enemies).Select(actor => actor.Id).Distinct().Count());
        Assert.Equal(25, allies.Concat(enemies).Select(actor => actor.Slot).Distinct().Count());
        Assert.Equal(10, battle.ReadDefinition().AddedEnemySources!.Count);
        Assert.Equal(authored.Enemies.OrderBy(enemy => enemy.Slot).Select(enemy => (SceneSlot: (int?)enemy.Slot, enemy.ModelId)), enemies.Take(4).Select(enemy => (enemy.SceneSlot, enemy.ModelId)));

        var (roster, aliases) = battle.ReadBattlePresentationPacketBindings();
        Assert.Equal(25, roster.Owners.Count());
        Assert.Equal(roster.Owners, process.Graphics.ActorModels.Owners);
        Assert.Equal(11, process.Graphics.ActorModels.Party.Entries.Count());
        Assert.Equal(14, process.Graphics.ActorModels.Enemies.Entries.Count());
        BattleCombatant caster = allies[^1];
        BattleCombatant target = enemies[^1];
        Assert.False(target.Untargetable);
        BattleModelOwner casterOwner = battle.ReadPresentationModelOwner(caster.Id);
        Assert.False(aliases.TryEncodeExact(casterOwner, out _));
        Assert.False(aliases.TryEncodeExact(battle.ReadPresentationModelOwner(target.Id), out _));

        // Poison forces the authored aftercare path after the physical contact.
        caster.NativeStatus0 |= 2;
        var first = AttackAndDrain(world, battle, process, caster, casterOwner, target);
        Assert.Equal(new uint[] { 0x47DE70, 0x47E120 }, first.Callbacks);
        Assert.NotNull(first.Aftercare);
        Assert.Equal(casterOwner, first.Aftercare.Primary.Recipient);
        Assert.True(first.Aftercare.Primary.PresentedAmount > 0);
        Assert.True(caster.IsAlive);
        Assert.False(caster.IsBusy);

        BattleCombatant nextTarget = enemies.First(enemy => enemy.IsAlive && !enemy.Untargetable);
        var next = AttackAndDrain(world, battle, process, caster, casterOwner, nextTarget);
        Assert.Equal(0x47DE70u, next.Callbacks[0]);
        Assert.NotEqual(first.CompletionToken, next.CompletionToken);
        Assert.Same(process, battle.Presentation);
    }

    private static void SelectAllAllies(GameSession session)
    {
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithExtendedRoster(true);
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        byte[] selected = Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount).Select(row => checked((byte)row)).ToArray();
        session.SetCharacterVitals(vitals.Rest().WithBattleParty(selected, selected.Length));
    }

    private static (NativeBattleContactPacket? Aftercare, uint[] Callbacks, long CompletionToken) AttackAndDrain(GameWorld world, BattleSession battle, BattlePresentationProcess process, BattleCombatant caster, BattleModelOwner casterOwner, BattleCombatant target)
    {
        BattleModelOwner targetOwner = battle.ReadPresentationModelOwner(target.Id);
        caster.Atb = BattleSession.AtbMaximum;
        int eventStart = battle.Events.Count;
        NativeBattleModelLifecycleServices? previous = process.LastFrameServices;
        List<NativeBattleContactPacket> contacts = [];
        List<NativeBattlePresentationCompletionRequested> completions = [];
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: target.Id));
        for (int callback = 0; callback < 1600; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.Presentation);
            if (process.LastFrameServices is { } services && !ReferenceEquals(services, previous))
            {
                previous = services;
                foreach (NativeBattlePresentationReceipt receipt in services.ReadOrderedReceipts())
                {
                    if (receipt is NativeBattlePresentationContactReceived contact)
                    {
                        contacts.Add(contact.Packet);
                    }
                    else if (receipt is NativeBattlePresentationCompletionRequested completion && completion.Owner == casterOwner)
                    {
                        Assert.Equal(caster.Id, completion.ActorId);
                        completions.Add(completion);
                    }
                }
            }
            if (process.ActiveActionToken is null && battle.ActiveAction is null && battle.Events.Skip(eventStart).Any(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id))
            {
                break;
            }
        }
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        Assert.Null(process.ActiveActionToken);
        Assert.Null(battle.ActiveAction);
        Assert.NotEmpty(completions);
        NativeBattleContactPacket hit = Assert.Single(contacts, packet => packet.Primary.Recipient == targetOwner);
        Assert.NotNull(hit.Occurrence);
        Assert.Equal(target.Id, Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "EffectContact" && item.ActorId == caster.Id && item.TargetId == target.Id).TargetId);
        return (contacts.SingleOrDefault(packet => packet.Primary.Recipient == casterOwner), completions.Select(receipt => receipt.Callback).ToArray(), completions[0].Token);
    }
}
