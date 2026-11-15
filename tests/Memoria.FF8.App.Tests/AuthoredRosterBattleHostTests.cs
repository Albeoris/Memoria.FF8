using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AuthoredRosterBattleHostTests
{
    private const string Caster = "party-3";
    private const byte Seed = 4;

    [Theory]
    [InlineData(87, 1)]
    [InlineData(89, 2)]
    [InlineData(91, 3)]
    [InlineData(92, 3)]
    public void Attached_host_runs_the_original_party_and_authored_formation(ushort formation, int partySize)
    {
        BattleTrace ordinary = Run(formation, partySize, attached: false);
        BattleTrace hosted = Run(formation, partySize, attached: true);
        Assert.Equal(ordinary.Party, hosted.Party);
        Assert.Equal(ordinary.Enemies, hosted.Enemies);
        Assert.Equal(ordinary.EntryRandom, hosted.EntryRandom);
        Assert.Equal(ordinary.SettledEntryRandom, hosted.SettledEntryRandom);
        Assert.Equal(ordinary.OpeningDamage, hosted.OpeningDamage);
        Assert.Equal(ordinary.AttackPacket, hosted.AttackPacket);
        Assert.True(ordinary.AttackDamage == hosted.AttackDamage && ordinary.AttackRandom.SequenceEqual(hosted.AttackRandom),
            $"Attack raw/hosted damage {ordinary.AttackDamage}/{hosted.AttackDamage}. Original: {string.Join(" | ", ordinary.Trace)}. Attached: {string.Join(" | ", hosted.Trace)}.");
        Assert.Equal(ordinary.AttackRandom, hosted.AttackRandom);
        Assert.Equal(ordinary.DrawPacket, hosted.DrawPacket);
        Assert.Equal(ordinary.DrawAmount, hosted.DrawAmount);
        Assert.Equal(ordinary.DrawRandom, hosted.DrawRandom);
        Assert.Equal(ordinary.EnemyPacket, hosted.EnemyPacket);
        Assert.Equal(ordinary.EnemyDamage, hosted.EnemyDamage);
        Assert.Equal(ordinary.EnemyRandom, hosted.EnemyRandom);
    }

    private sealed record ActionPacket(byte Command, ushort Action, byte Sequence, ushort Effect, int Contacts);

    private sealed record BattleTrace(string[] Party, string[] Enemies, byte[] EntryRandom, byte[] SettledEntryRandom,
        (string? Source, string? Target, int Amount)[] OpeningDamage, ActionPacket AttackPacket, int AttackDamage, byte[] AttackRandom,
        ActionPacket DrawPacket, int DrawAmount, byte[] DrawRandom, ActionPacket EnemyPacket, (string? Target, int Amount)[] EnemyDamage, byte[] EnemyRandom, string[] Trace);

    private static BattleTrace Run(ushort formation, int partySize, bool attached)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, partySize), formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal(attached, battle.ExpandedPresentationProcess is not null);
        Assert.Equal(attached, battle.UsesExpandedRoster);
        BattleCombatant[] participants = battle.Combatants.Where(actor => actor.IsActive).ToArray();
        BattleCombatant[] party = participants.Where(actor => actor.IsPlayer).ToArray();
        BattleCombatant[] enemies = participants.Where(actor => !actor.IsPlayer).ToArray();
        Assert.Equal(partySize, party.Length);
        Assert.Equal(world.BattleResources.LoadEncounter(formation).Enemies.Count, enemies.Length);
        Assert.All(participants, actor => Assert.NotNull(actor.NativeActorIndex));
        byte[] entryRandom = battle.Capture().Random!.Indexes.ToArray();
        List<string> trace = [];
        int capturedEventCount = 0;

        void Trace(string boundary)
        {
            NativeBattleRandomState random = battle.Capture().Random!;
            string events = string.Join(",", battle.Events.Skip(capturedEventCount).Where(entry => entry.Kind is "Draw" or "Damage" or "ActionComplete").Select(entry => $"{entry.Kind}/{entry.ActorId}/{entry.TargetId}/{entry.Amount}"));
            string gauges = string.Join(",", participants.Select(actor => $"{actor.Id}/{actor.Atb}/{actor.AiTurnCount}/{actor.NativeCombatantFlags:X}/{actor.IsBusy}"));
            trace.Add($"{boundary}={random.Indexes[random.Lane]}:{events}[{gauges}]");
            capturedEventCount = battle.Events.Count;
        }

        TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0, $"formation {formation} entry");
        byte[] settledEntryRandom = battle.Capture().Random!.Indexes.ToArray();
        var openingDamage = battle.Events.Where(entry => entry.Kind == "Damage").Select(entry => (entry.ActorId, entry.TargetId, entry.Amount)).ToArray();
        Trace("entry");
        BattleCombatant caster = Assert.Single(party, member => member.Id == Caster);
        BattleCombatant target = enemies.First(enemy => enemy.NativeDrawSlots.Any(slot => slot.Id is > 0 and < 64));
        int spell = target.NativeDrawSlots.First(slot => slot.Id is > 0 and < 64).Id;
        Ready(world, caster);
        Trace("draw ready");
        int beforeDraw = battle.Events.Count;
        int stock = battle.GetAvailableMagicCasts(Caster, spell);
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Caster, TargetId: target.Id, Ability: BattleAbility.Draw, ActionId: spell));
        ActionPacket drawPacket = AdvanceAction(world, beforeDraw, Caster, 6, $"formation {formation} Draw");
        int drawAmount = battle.Events.Skip(beforeDraw).Where(entry => entry.Kind == "Draw" && entry.ActorId == Caster).Sum(entry => entry.Amount);
        Assert.Equal(stock + drawAmount, battle.GetAvailableMagicCasts(Caster, spell));
        byte[] drawRandom = battle.Capture().Random!.Indexes.ToArray();
        Trace("draw complete");

        BattleCombatant attackTarget = enemies.OrderByDescending(enemy => enemy.CurrentHp).First(enemy => enemy.IsAlive);
        Ready(world, caster);
        Trace("attack ready");
        int beforeAttack = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Caster, TargetId: attackTarget.Id) { ForcePhysical = true });
        ActionPacket attackPacket = AdvanceAction(world, beforeAttack, Caster, 1, $"formation {formation} Attack");
        int attackDamage = battle.Events.Skip(beforeAttack).Where(entry => entry.Kind == "Damage" && entry.ActorId == Caster && entry.TargetId == attackTarget.Id).Sum(entry => entry.Amount);
        Assert.True(attackDamage > 0);
        byte[] attackRandom = battle.Capture().Random!.Indexes.ToArray();
        Trace("attack complete");

        target = enemies.First(enemy => enemy.IsAlive && enemy.IsActive && !enemy.Untargetable);
        Trace("enemy ready");
        int beforeEnemy = battle.Events.Count;
        byte turns = target.AiTurnCount;
        target.Atb = BattleSession.AtbMaximum;
        ActionPacket enemyPacket = AdvanceAction(world, beforeEnemy, target.Id, null, $"formation {formation} enemy turn");
        Assert.True(target.AiTurnCount > turns);
        var enemyDamage = battle.Events.Skip(beforeEnemy).Where(entry => entry.Kind == "Damage" && entry.ActorId == target.Id).Select(entry => (entry.TargetId, entry.Amount)).ToArray();
        byte[] enemyRandom = battle.Capture().Random!.Indexes.ToArray();
        Trace("enemy complete");

        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ExpandedPresentationProcess?.HasActiveAction != true, $"formation {formation} ready for final attack");
        BattleCombatant finalTarget = enemies.First(enemy => enemy.IsAlive && enemy.IsActive && !enemy.Untargetable);
        foreach (BattleCombatant enemy in enemies.Where(enemy => enemy != finalTarget))
        {
            enemy.CurrentHp = 0;
            enemy.NativeStatus0 |= 1;
            enemy.Atb = 0;
        }
        finalTarget.CurrentHp = 1;
        Ready(world, caster);
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Caster, TargetId: finalTarget.Id) { ForcePhysical = true });
        TickUntil(world, () => battle.CanReturnToField && (battle.ExpandedPresentationProcess?.TerminalFinished ?? true), $"formation {formation} victory");
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(SceneKind.Field, session.State.Scene);

        return new(party.Select(actor => actor.Id).ToArray(), enemies.Select(actor => actor.Id).ToArray(), entryRandom, settledEntryRandom, openingDamage,
            attackPacket, attackDamage, attackRandom, drawPacket, drawAmount, drawRandom, enemyPacket, enemyDamage, enemyRandom, trace.ToArray());
    }

    private static ActionPacket AdvanceAction(GameWorld world, int start, string actorId, byte? command, string action)
    {
        BattleSession battle = world.Battle!;
        BattleModelOwner owner = battle.ReadPresentationModelOwner(actorId);
        NativeBattleActionHeaderSlot? header = null;
        TickUntil(world, () =>
        {
            NativeBattleActionPacketsState packets = battle.ExpandedPresentationProcess?.ActionPackets ?? battle.NativeActionPackets;
            header ??= packets.PublishedHeaders().Select(entry => entry.Slot).FirstOrDefault(slot => slot.Source == owner && (command is null || slot.Command == command));
            return header is not null && Completed(battle, start, actorId);
        }, action);
        NativeBattleActionHeaderSlot admitted = Assert.IsType<NativeBattleActionHeaderSlot>(header);
        return new(admitted.Command, admitted.Action, admitted.Sequence, admitted.Effect, admitted.ReadContactCount());
    }

    private static void PrepareParty(GameSession session, int size)
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals!.Rest();
        byte[] party = size switch
        {
            1 => [3, 255, 255],
            2 => [0, 3, 255],
            _ => [0, 1, 3]
        };
        NativeCharacterVital[] records = vitals.Records.ToArray();
        int draw = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6);
        Assert.True(draw >= 0);
        foreach (byte row in party.Where(row => row != byte.MaxValue))
        {
            records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1), Status = 0 };
        }
        records[3] = records[3] with { Junction = records[3].Junction with { Commands = checked((uint)(20 + draw)) } };
        session.SetCharacterVitals((vitals with { CombatParty = party, Records = records }).RebuildParty().Rest());
    }

    private static bool Completed(BattleSession battle, int start, string actorId)
    {
        return battle.Events.Skip(start).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actorId);
    }

    private static void Ready(GameWorld world, BattleCombatant caster)
    {
        caster.Atb = BattleSession.AtbMaximum;
        BattleSession battle = world.Battle!;
        TickUntil(world, () => battle.CanChoosePlayerCommand(caster.Id), $"readiness for {caster.Id}");
        if (battle.SupportsPlayerLimitSelection(caster.Id) && !battle.HasNativeLimitSelection(caster.Id))
        {
            battle.SelectPlayerCommandActor(caster.Id);
        }
    }

    private static void TickUntil(GameWorld world, Func<bool> complete, string action)
    {
        for (int frame = 0; frame < 1200 && !complete(); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(complete(), $"Timed out at {action}. {string.Join(", ", world.Battle!.Events.TakeLast(12).Select(entry => entry.Kind + "/" + entry.ActorId))}");
    }
}
