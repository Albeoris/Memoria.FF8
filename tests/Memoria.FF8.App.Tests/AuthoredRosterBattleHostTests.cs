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

    [Fact]
    public void Gunblade_routing_keeps_the_zero_contact_in_the_header_and_routes_damage_through_pending_graphics()
    {
        var original = RunRoutedGunblade(attached: false);
        var hosted = RunRoutedGunblade(attached: true);
        Assert.Equal((original.Presented, original.Applied), (hosted.Presented, hosted.Applied));
        Assert.Equal(original.Random, hosted.Random);
    }

    [Fact]
    public void Guardian_routing_keeps_the_initial_targets_and_publishes_the_routed_impact()
    {
        var original = RunRoutedGuardian(attached: false);
        var hosted = RunRoutedGuardian(attached: true);
        Assert.Equal(original.Contacts, hosted.Contacts);
        Assert.True((original.Presented, original.Applied) == (hosted.Presented, hosted.Applied),
            $"Original {original.Presented}/{original.Applied} [{original.Trace}]; attached {hosted.Presented}/{hosted.Applied} [{hosted.Trace}].");
        Assert.Equal(original.Random, hosted.Random);
    }

    [Fact]
    public void Renzokuken_routes_each_trigger_without_changing_its_original_contact()
    {
        var original = RunRoutedRenzokuken(attached: false);
        var hosted = RunRoutedRenzokuken(attached: true);
        Assert.Equal(original.Applied, hosted.Applied);
        Assert.Equal(original.Random, hosted.Random);
    }

    private static (int[] Applied, byte[] Random) RunRoutedRenzokuken(bool attached)
    {
        const string casterId = "party-0";
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition, recipientHpMultiplier: 64)) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            PrepareParty(current, 3);
            current.SetCharacterVitals(current.State.CharacterVitals!.WithBattleParty([0], 3));
        }, 89, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
        TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "routed Renzokuken entry");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        EncounterCompiledDamageRoute route = Assert.Single(battle.ReadDefinition().EncounterMod!.DamageRoutes).Value;
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == route.SourceActor);
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == Assert.Single(route.Recipients));
        BattleModelOwner originalTarget = battle.ReadPresentationModelOwner(source.Id);
        int originalHp = source.CurrentHp;
        caster.CurrentHp = 1;
        Ready(world, caster);
        byte crisis = 0;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            crisis = battle.RefreshNativeLimit(casterId);
            NativeBattleRandomState forecast = battle.Capture().Random!.Draw(out _).Draw(out byte gate);
            if (crisis > 0 && !NativeBattleRenzokukenRules.HasFinisher(crisis, gate))
            {
                break;
            }
            crisis = 0;
        }
        Assert.InRange(crisis, 1, 4);
        caster.CurrentHp = caster.MaximumHp;
        int eventsBefore = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: casterId, TargetId: source.Id, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16));
        NativeBattleActionPacketsState ReadPackets() => battle.ExpandedPresentationProcess?.ActionPackets ?? battle.NativeActionPackets;
        int[] RoutedDamage() => battle.Events.Skip(eventsBefore).Where(entry => entry is { Kind: "Damage", ActorId: casterId } && entry.TargetId == recipient.Id).Select(entry => entry.Amount).ToArray();
        TickUntil(world, () => RoutedDamage().Length > 0, "first routed Renzokuken trigger");
        NativeBattleActionPacketsState packets = ReadPackets();
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal((byte)5, header.Command);
        Assert.Equal(1, header.ReadContactCount());
        NativeBattleContactHalf selected = packets.ReadContact(header.FirstContact).Primary;
        Assert.Equal(originalTarget, selected.Recipient);
        Assert.Equal((0, (byte)0), (selected.PresentedAmount, selected.Sequence));
        Assert.Equal(originalHp, source.CurrentHp);
        TickUntil(world, () => battle.GetNativeRenzokuken(casterId) is null && battle.ActiveAction is null && battle.ExpandedPresentationProcess?.HasActiveAction != true, "routed Renzokuken completion");
        int[] applied = RoutedDamage();
        Assert.True(applied.Length > 1, $"Expected repeat routed hits. Applied {string.Join(", ", applied)} to starting HP {recipient.MaximumHp}.");
        if (attached)
        {
            int[] presented = battle.Events.Skip(eventsBefore).Where(entry => entry is { Kind: "EffectContact", ActorId: casterId } && entry.TargetId == recipient.Id).Select(entry => entry.Amount).ToArray();
            Assert.Equal(applied.Length, presented.Length);
            Assert.Equal(applied[..^1], presented[..^1]);
            Assert.True(presented[^1] >= applied[^1]);
        }
        return (applied, battle.Capture().Random!.Indexes.ToArray());
    }

    private static (NativeBattleContactHalf[] Contacts, int Presented, int Applied, byte[] Random, string Trace) RunRoutedGuardian(bool attached)
    {
        const ushort formation = 89;
        const int guardian = 1;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition)) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            ExpandedBattleGuardianTests.PrepareParty(current, learnedBoost: false, guardianId: guardian);
            current.SetCharacterVitals(current.State.CharacterVitals!.WithBattleParty([3], 3));
        }, formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "routed GF entry");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == Caster);
        var route = Assert.Single(battle.ReadDefinition().EncounterMod!.DamageRoutes).Value;
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == route.SourceActor);
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == Assert.Single(route.Recipients));
        BattleModelOwner sourceModel = battle.ReadPresentationModelOwner(source.Id);
        BattleModelOwner recipientModel = battle.ReadPresentationModelOwner(recipient.Id);
        int originalHp = source.CurrentHp;
        int recipientHp = recipient.CurrentHp;
        Ready(world, caster);
        NativeBattleRandomState readyRandom = battle.Capture().Random!;
        int eventsBefore = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Caster, TargetId: source.Id, Ability: BattleAbility.GuardianForce, ActionId: guardian));
        TickUntil(world, () => battle.GuardianForceCasts.Any(cast => cast.ActorId == Caster), "routed GF charge");
        for (int callback = 0; callback < 1200 && battle.GuardianForceCasts.Any(cast => cast.ActorId == Caster); callback++)
        {
            if (battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Empty(battle.GuardianForceCasts);
        NativeBattleRandomState chargedRandom = battle.Capture().Random!;
        NativeBattleRandomState beforeFormula = chargedRandom;
        for (int callback = 0; callback < 1200 && !Routed(); callback++)
        {
            if (battle.NativeGuardianBoost is { Phase: >= 4, Remaining: <= 2 } || battle.ExpandedPresentationProcess?.PendingGuardianBoost is not null)
            {
                beforeFormula = battle.Capture().Random!;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(Routed(), "The final GF formula did not route damage.");
        ExpandedBattlePresentationProcess? process = battle.ExpandedPresentationProcess;
        NativeBattleActionPacketsState packets = process?.ActionPackets ?? battle.NativeActionPackets;
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal((byte)254, header.Command);
        Assert.Equal((ushort)(64 + guardian), header.Action);
        NativeBattleContactHalf[] contacts = packets.ReadHeaderContacts(new BattleActionHeaderReference(0)).Select(packet => packet.Primary).ToArray();
        Assert.Equal(2, contacts.Length);
        NativeBattleContactHalf selected = Assert.Single(contacts, contact => contact.Recipient == sourceModel);
        Assert.Equal(0, selected.PresentedAmount);
        Assert.Equal((byte)0, selected.Sequence);
        Assert.Contains(contacts, contact => contact.Recipient == recipientModel);
        Assert.Equal(originalHp, source.CurrentHp);
        Assert.Equal((byte)100, battle.Capture().NativeActionScratch!.GuardianBoostPercent);
        IEnumerable<NativeBattleContactPacket> routedContacts = process is null ? battle.NativeGraphics!.PendingContacts :
            process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet);
        NativeBattleContactPacket routed = Assert.Single(routedContacts, packet => packet.Occurrence is null && packet.Primary.Recipient == recipientModel);
        Assert.Null(routed.Occurrence);
        Assert.True(routed.Primary.PresentedAmount > 0);
        int loss = recipientHp - recipient.CurrentHp;
        Assert.True(loss > 0);
        NativeBattleRandomState random = battle.Capture().Random!;
        string trace = $"ready {FormatRandom(readyRandom)}, charged {FormatRandom(chargedRandom)}, before formula {FormatRandom(beforeFormula)}, resolved {FormatRandom(random)}, source spirit {source.Spirit}";
        TickUntil(world, () => (process?.Graphics ?? battle.NativeGraphics!).Effects.Slots.Any(task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelOwner == recipientModel), "routed GF presentation");
        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && process?.HasActiveAction != true, "routed GF completion");
        return (contacts, routed.Primary.PresentedAmount, loss, random.Indexes.ToArray(), trace);

        bool Routed() => battle.Events.Skip(eventsBefore).Any(entry => entry.Kind == "Damage" && entry.ActorId == Caster && entry.TargetId == recipient.Id);
        static string FormatRandom(NativeBattleRandomState random) => $"{random.Lane}/{string.Join(",", random.Indexes)}";
    }

    private static (int Presented, int Applied, byte[] Random) RunRoutedGunblade(bool attached)
    {
        const ushort formation = 89;
        const string casterId = "party-0";
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition)) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, 2), formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
        TickUntil(world, () => battle.NativeEntryReady && battle.ActiveAction is null && battle.QueuedActions.Count == 0, "routed Gunblade entry");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        var route = Assert.Single(battle.ReadDefinition().EncounterMod!.DamageRoutes).Value;
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == route.SourceActor);
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == Assert.Single(route.Recipients));
        BattleModelOwner sourceModel = battle.ReadPresentationModelOwner(source.Id);
        BattleModelOwner recipientModel = battle.ReadPresentationModelOwner(recipient.Id);
        int originalHp = source.CurrentHp;
        int recipientHp = recipient.CurrentHp;
        Ready(world, caster);
        int eventsBefore = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: casterId, TargetId: source.Id) { ForcePhysical = true });
        TickUntil(world, () => battle.Events.Skip(eventsBefore).Any(entry => entry.Kind == "Damage" && entry.ActorId == casterId && entry.TargetId == recipient.Id), "final routed Gunblade damage");

        NativeBattleActionPacketsState packets = battle.ExpandedPresentationProcess?.ActionPackets ?? battle.NativeActionPackets;
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal(1, header.ReadContactCount());
        NativeBattleContactHalf selected = packets.ReadContact(header.FirstContact).Primary;
        Assert.Equal(sourceModel, selected.Recipient);
        Assert.Equal(0, selected.PresentedAmount);
        Assert.Equal((byte)0, selected.Sequence);
        int loss = recipientHp - recipient.CurrentHp;
        Assert.True(loss > 0);
        NativeBattleGraphicsState graphics = battle.ExpandedPresentationProcess?.Graphics ?? battle.NativeGraphics!;
        NativeBattleEffectTask damage = Assert.Single(graphics.Effects.Slots, task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelOwner == recipientModel);
        string digits = string.Concat(damage.Glyphs.TakeWhile(glyph => glyph != 0).Select(glyph => (char)('0' + glyph - 56)));
        int presented = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Math.Min(recipientHp, presented), loss);
        Assert.Equal(originalHp, source.CurrentHp);
        byte[] random = battle.Capture().Random!.Indexes.ToArray();
        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ExpandedPresentationProcess?.HasActiveAction != true, "routed Gunblade completion");
        if (attached)
        {
            BattleEvent contact = Assert.Single(battle.Events.Skip(eventsBefore), entry => entry is { Kind: "EffectContact", ActorId: casterId } && entry.TargetId == recipient.Id);
            Assert.Equal(presented, contact.Amount);
        }
        return (presented, loss, random);
    }

    private static BattleDefinition BindRoute(BattleDefinition definition, int recipientHpMultiplier = 1)
    {
        const string encounter = "fixture.routed-contact";
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).Take(2).ToArray();
        Assert.Equal(2, enemies.Length);
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(encounter, "Classic", ["original"]), []);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new(encounter, enemies[0].Id, 0, 0, [], true));
        EncounterCompiledDamageRoute route = new("contact-route", enemies[0].Id, [enemies[1].Id]);
        definition = definition with { EncounterMod = compiled with { DamageRoutes = compiled.DamageRoutes.Add(route.Id, route) } };
        if (recipientHpMultiplier == 1)
        {
            return definition;
        }
        int recipientHp = EncounterActorProperties.ScaleMaximumHp(enemies[1].MaximumHp, recipientHpMultiplier);
        return definition with { Combatants = definition.Combatants.Select(actor => actor.Id == enemies[1].Id ? actor with { MaximumHpMultiplier = recipientHpMultiplier, MaximumHp = recipientHp, CurrentHp = recipientHp } : actor).ToArray() };
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
