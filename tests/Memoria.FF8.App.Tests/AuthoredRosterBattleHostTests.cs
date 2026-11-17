using System.Collections.Immutable;
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
        BattleTrace observed = Run(formation, partySize);
        Assert.Equal((byte)1, observed.AttackPacket.Command);
        Assert.Equal((byte)6, observed.DrawPacket.Command);
        Assert.True(observed.AttackDamage > 0, string.Join(" | ", observed.Trace));
        Assert.True(observed.DrawAmount > 0);
    }

    [Fact]
    public void Gunblade_routing_keeps_the_zero_contact_in_the_header_and_routes_damage_through_pending_graphics()
    {
        var hosted = RunRoutedGunblade();
        Assert.InRange(hosted.Applied, 1, hosted.Presented);
    }

    [Fact]
    public void Guardian_routing_keeps_the_initial_targets_and_publishes_the_routed_impact()
    {
        var hosted = RunRoutedGuardian();
        Assert.InRange(hosted.Applied, 1, hosted.Presented);
    }

    [Theory]
    [InlineData(329, 52, 326, true)]
    [InlineData(330, 53, 218, false)]
    public void Seifer_original_command_236_uses_the_authored_self_target(ushort action, byte sequence, ushort effect, bool firstTurn)
    {
        var hosted = RunSeiferAction(action, sequence, effect, firstTurn);
        NativeBattleContactHalf contact = Assert.Single(hosted.Contacts);
        if (!firstTurn)
        {
            Assert.Equal(0, contact.PresentedAmount);
            Assert.Equal(0, contact.SavedStatuses4 & 1);
            Assert.Equal(0, hosted.Hp);
            Assert.Equal(1, hosted.Status & 1);
        }
    }

    private static (NativeBattleContactHalf[] Contacts, byte[] Random, int Hp, ushort Status) RunSeiferAction(ushort action, byte sequence, ushort effect, bool firstTurn)
    {
        const ushort formation = 795;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            if (firstTurn)
            {
                PrepareParty(current, 1);
                current.SetNativeOdin();
            }
            else
            {
                DiagnosticBattlePreparation preparation = new([new(0, true)], [new(0, 0, [], [0]) { MagicCommand = new(0, 20), HitPoints = 486, Level = 7 }], true);
                current.SetCharacterVitals(GameWorld.PrepareDiagnosticCharacters(current.State.CharacterVitals!, preparation, game.ReadNativeBattleExperienceDefinition()).Vitals);
            }
        }, formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.Normal, NativeSeed: firstTurn ? Seed : (byte)0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant seifer = Assert.Single(battle.Combatants, actor => actor is { IsPlayer: false, ModelId: 128 });
        BattleCombatant companion = Assert.Single(battle.Combatants, actor => actor is { IsPlayer: false, ModelId: 82 });
        Assert.True(companion.Suspended);
        Assert.Equal(3, seifer.NativeActorIndex);
        Assert.Equal(4, companion.NativeActorIndex);
        BattleModelOwner caster = battle.ReadPresentationModelOwner(seifer.Id);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        Assert.Equal(firstTurn ? 2 : 0, battle.ReadDefinition().NativeSavedBattleFlags.GetValueOrDefault() & 2);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: seifer.Id);
        TickUntil(world, () => battle.NativeEntryReady && !seifer.IsBusy, "Seifer entry");
        NativeBattleAiDefinition ai = battle.GetCombatantDefinition(seifer.Id).NativeAi!;
        int slot = firstTurn ? 1 : 2;
        int pc = firstTurn ? 31 : 66;
        Assert.Equal(new byte[] { 4, 200, 12, checked((byte)slot) }, ai.Scripts[1].AsSpan(pc, 4).ToArray());
        BattleDiagnosticEnemyAbility selection = new(128, 0, ai.LevelBand, firstTurn ? -1 : slot, 236, action, sequence, firstTurn ? null : (byte)200)
        {
            UseTurnScript = firstTurn,
            TurnScriptSha256 = firstTurn ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ai.Scripts[1])) : null
        };
        seifer.Atb = BattleSession.AtbMaximum;
        TickUntil(world, () => battle.CanSubmitDiagnosticEnemyAbility(seifer.Id, selection), "original Seifer turn readiness");
        Assert.Equal(0, seifer.AiTurnCount);
        int eventStart = battle.Events.Count;
        battle.SubmitDiagnosticEnemyAbility(seifer.Id, selection, effect);
        NativeBattleContactHalf[]? contacts = null;
        BattleActionHeaderReference? actionHeader = null;
        HashSet<string> trace = [];
        bool entered = false;
        bool retired = false;
        HashSet<BattleActionHeaderReference> deathCallbacks = [];
        for (int frame = 0; frame < 1500 && !retired && world.Battle is not null; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleActionPacketsState packets = process.ActionPackets;
            foreach (var (reference, header) in packets.PublishedHeaders())
            {
                trace.Add($"header {header.Command}/{header.Action}/{header.Sequence}/{header.Effect}");
                if (header.Command == 236 && header.Action == action)
                {
                    Assert.Equal(firstTurn ? 1 : 0, seifer.AiTurnCount);
                    Assert.Equal(sequence, header.Sequence);
                    Assert.Equal(caster, header.Source);
                    contacts = packets.ReadHeaderContacts(reference).Select(packet => packet.Primary).ToArray();
                    Assert.Equal(caster, Assert.Single(contacts).Recipient);
                    actionHeader = reference;
                }
                if (!firstTurn && header is { Command: 8, Action: 341 } && header.Source == caster)
                {
                    deathCallbacks.Add(reference);
                }
            }
            NativeBattleGraphicsState graphics = process.Graphics;
            if (graphics.OriginalEffect is { Parameters: { } parameters } native)
            {
                trace.Add($"effect {native.RequestedEffect}/{packets.ReadHeader(parameters.Header).Action}");
                entered |= actionHeader == parameters.Header && native.RequestedEffect == effect;
            }
            retired = entered && (firstTurn || deathCallbacks.Count != 0) && Completed(battle, eventStart, seifer.Id) &&
                battle.ActiveAction is null && !process.HasActiveAction;
        }
        Assert.True(retired, $"Seifer turn {seifer.AiTurnCount}: {string.Join(", ", trace)}");
        if (firstTurn)
        {
            Assert.True(seifer.IsAlive);
        }
        else
        {
            Assert.Single(deathCallbacks);
            Assert.Single(battle.EnemyDefeats, defeat => defeat.AllocationId == seifer.Id);
        }
        return (contacts!, battle.Capture().Random!.Indexes.ToArray(), seifer.CurrentHp, seifer.NativeStatus0);
    }

    [Theory]
    [InlineData(2, 0x20000u)]
    [InlineData(3, 0x40000u)]
    public void Player_repeat_Fire_owns_one_native_graphics_group_and_spends_only_casts(int casts, uint nativeStatus)
    {
        var hosted = RunPlayerRepeatMagic(casts, nativeStatus);
        Assert.Equal(casts, hosted.Damage.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Player_repeat_Fire_publishes_reflectors_in_one_stock_free_secondary_group(int reflectionMask)
    {
        var hosted = RunPlayerRepeatMagic(2, 0x20000, reflectionMask);
        (string? Target, int Amount)[] reflectedDamage = reflectionMask == 3 ? [("party-3", 108), ("party-0", 102)] : [("party-0", 108)];
        Assert.Equal(reflectedDamage, hosted.Damage.Where(hit => hit.Target?.StartsWith("party-", StringComparison.Ordinal) == true));
    }

    [Theory]
    [InlineData(2, 0x20000u, 0)]
    [InlineData(3, 0x40000u, 0)]
    [InlineData(2, 0x20000u, 1)]
    [InlineData(2, 0x20000u, 3)]
    public void Player_repeat_Fira_consumes_each_contiguous_direct_and_reflected_occurrence(int casts, uint nativeStatus, int reflectionMask)
    {
        const int fira = 2;
        var hosted = RunPlayerRepeatMagic(casts, nativeStatus, reflectionMask, fira);
        Assert.NotEmpty(hosted.Contacts);
    }

    [Fact]
    public void Player_repeat_Fira_rechecks_Triple_before_dispatch_and_retires_the_first_header()
    {
        const int fira = 2;
        var hosted = RunPlayerRepeatMagic(3, 0x40000, spell: fira, loseRepeatStatus: true);
        Assert.Single(hosted.Damage);
    }

    [Fact]
    public void Player_repeat_Esuna_publishes_its_reflected_cure_contacts()
    {
        const int esuna = 27;
        var hosted = RunPlayerRepeatMagic(2, 0x20000, reflectionMask: 3, spell: esuna);
        Assert.Equal(2, hosted.Reflections.Length);
    }

    [Fact]
    public void Player_repeat_Full_cure_heals_both_targets_and_presents_the_first_header()
    {
        const int fullCure = 51;
        var hosted = RunPlayerRepeatMagic(2, 0x20000, spell: fullCure, firstHeaderHealing: true);
        Assert.Equal(2, hosted.Contacts.Length);
    }

    private static (NativeBattleContactHalf[] Contacts, NativeBattleContactHalf[] Reflections, (string? Target, int Amount)[] Damage, byte[] Random) RunPlayerRepeatMagic(int casts, uint nativeStatus, int reflectionMask = 0, int spell = 1, bool firstHeaderHealing = false, bool loseRepeatStatus = false)
    {
        const string casterId = "party-0";
        const int firstInjuredHp = 23;
        const int secondInjuredHp = 31;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => definition with
        {
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { MaximumHpMultiplier = 32, MaximumHp = actor.MaximumHp * 32, CurrentHp = actor.MaximumHp * 32 }).ToArray()
        });
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            PrepareParty(current, 2);
            NativeCharacterVitals vitals = current.State.CharacterVitals!;
            NativeCharacterVital[] records = vitals.Records.ToArray();
            NativeMagicStock[] inventory = records[0].MagicInventory.ToArray();
            inventory[0] = new(checked((byte)spell), 10);
            int magic = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2);
            Assert.True(magic >= 0);
            records[0] = records[0] with { MagicInventory = inventory, Junction = records[0].Junction with { Commands = checked((uint)(20 + magic)) } };
            if (firstHeaderHealing)
            {
                records[0] = records[0] with { HitPoints = new(firstInjuredHp, firstInjuredHp) };
                records[3] = records[3] with { HitPoints = new(secondInjuredHp, secondInjuredHp) };
            }
            current.SetCharacterVitals(vitals with { Records = records });
        }, 89, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        TickUntil(world, () => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null, "repeat magic entry");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterId);
        BattleCombatant[] targets = battle.Combatants.Where(actor => actor.IsPlayer == firstHeaderHealing && actor.IsActive).ToArray();
        Assert.Equal(2, targets.Length);
        int contactsPerCast = firstHeaderHealing ? targets.Length : 1;
        if (firstHeaderHealing)
        {
            Assert.Equal(new[] { firstInjuredHp, secondInjuredHp }, targets.Select(target => target.CurrentHp));
            Assert.Equal(targets.Select(target => target.CurrentHp), targets.Select(target => battle.ReadPresentedVital(target.Id).HitPoints));
        }
        for (int index = 0; index < targets.Length; index++)
        {
            if ((reflectionMask & (1 << index)) != 0)
            {
                targets[index].NativeStatus1 |= 0x80;
            }
        }
        caster.NativeStatus1 |= nativeStatus;
        Ready(world, caster);
        Assert.Contains(casts, battle.GetNativeMagicCastCounts(casterId, spell));
        Assert.Equal(10, battle.GetAvailableMagicCasts(casterId, spell));
        ImmutableArray<string> targetIds = Enumerable.Range(0, casts).Select(index => targets[index % targets.Length].Id).ToImmutableArray();
        BattleModelOwner owner = battle.ReadPresentationModelOwner(casterId);
        ushort effect = battle.ReadDefinition().NativeActions.Single(action => action.Command == 2 && action.ActionId == spell).MagicId;
        NativeBattleContactHalf[] contacts = [];
        NativeBattleContactHalf[] reflections = [];
        BattleModelOwner[] reflectors = targets.Where((_, index) => (reflectionMask & (1 << index)) != 0).Select(target => battle.ReadPresentationModelOwner(target.Id)).ToArray();
        HashSet<BattleActionHeaderReference> entered = [];
        BattleEvent[] committedPrefix = battle.Events.ToArray();
        List<BattleEvent> observed = [];
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticEventPublished += observed.Add;
        battle.DiagnosticResolution += resolutions.Add;
        int eventStart = committedPrefix.Length;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: casterId, TargetId: targetIds[0], Ability: BattleAbility.Magic, ActionId: spell) { MagicTargets = targetIds });
        int performedCasts = loseRepeatStatus ? 1 : casts;
        if (loseRepeatStatus)
        {
            caster.NativeStatus1 &= ~nativeStatus;
        }
        TickUntil(world, () =>
        {
            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            NativeBattleActionPacketsState packets = process.ActionPackets;
            var headers = packets.PublishedHeaders().Where(entry => entry.Slot.Command == 2 && entry.Slot.Action == spell && entry.Slot.Source == owner).ToArray();
            if (headers.Length == performedCasts)
            {
                Assert.Equal(performedCasts - 1, headers[0].Slot.ReadRepeatModeMinusOne());
                Assert.All(headers, entry => Assert.Equal(contactsPerCast, entry.Slot.ReadContactCount()));
                Assert.Equal(headers.Select((_, index) => headers[0].Reference.Advance(index)), headers.Select(entry => entry.Reference));
                NativeBattleContactPacket[] produced = headers.SelectMany(entry => packets.ReadHeaderContacts(entry.Reference)).ToArray();
                Assert.Equal(produced.Select((_, index) => headers[0].Slot.FirstContact.Advance(index)), produced.Select(packet => packet.Occurrence!.Value.AsCursor()));
                contacts = produced.Select(packet => packet.Primary).ToArray();
            }
            if (reflectionMask != 0)
            {
                var reflectedHeaders = packets.PublishedHeaders().Where(entry => entry.Slot.Command == 247 && entry.Slot.Action == spell).ToArray();
                if (reflectedHeaders.Length == reflectors.Length)
                {
                    Assert.Equal(reflectors, reflectedHeaders.Select(entry => entry.Slot.Source));
                    Assert.Equal(reflectors.Length - 1, reflectedHeaders[0].Slot.ReadRepeatModeMinusOne());
                    Assert.Equal(reflectedHeaders.Select((_, index) => reflectedHeaders[0].Reference.Advance(index)), reflectedHeaders.Select(entry => entry.Reference));
                    Assert.Equal(reflectedHeaders.Select((_, index) => reflectedHeaders[0].Slot.FirstContact.Advance(index)), reflectedHeaders.Select(entry => packets.ReadContact(entry.Slot.FirstContact).Occurrence!.Value.AsCursor()));
                    Assert.All(reflectedHeaders, entry =>
                    {
                        Assert.Equal(owner, entry.Slot.Caster);
                        Assert.Equal(0, entry.Slot.Sequence);
                        Assert.Equal(1, entry.Slot.ReadContactCount());
                    });
                    if (headers.Length == performedCasts)
                    {
                        Assert.Equal(headers[^1].Reference.Advance(1), reflectedHeaders[0].Reference);
                    }
                    reflections = reflectedHeaders.Select(entry => packets.ReadContact(entry.Slot.FirstContact).Primary).ToArray();
                }
            }
            NativeBattleGraphicsState graphics = process.Graphics;
            if (graphics.OriginalEffect is { Parameters: { } parameters } original && original.RequestedEffect == effect &&
                (parameters.Source == owner || reflectors.Contains(parameters.Source)))
            {
                entered.Add(parameters.Header);
            }
            return Completed(battle, eventStart, casterId) && battle.ActiveAction is null && !process.HasActiveAction;
        }, "native repeat magic completion");
        IEnumerable<string> expectedRecipients = firstHeaderHealing ? Enumerable.Range(0, performedCasts).SelectMany(_ => targets.Select(target => target.Id)) : targetIds.Take(performedCasts);
        Assert.Equal(expectedRecipients.Select(battle.ReadPresentationModelOwner), contacts.Select(contact => contact.Recipient));
        Assert.Equal(reflectors.Length != 0 ? 2 : 1, entered.Count);
        Assert.Equal(reflectors.Length, reflections.Length);
        Assert.Equal(10 - performedCasts, battle.GetAvailableMagicCasts(casterId, spell));
        var damage = battle.Events.Skip(eventStart).Where(entry => entry is { Kind: "Damage", ActorId: casterId }).Select(entry => (entry.TargetId, entry.Amount)).ToArray();
        if (firstHeaderHealing)
        {
            Assert.All(targets, target =>
            {
                Assert.Equal(target.MaximumHp, target.CurrentHp);
                Assert.Equal(target.MaximumHp, battle.ReadPresentedVital(target.Id).HitPoints);
            });
        }
        else if (reflectionMask == 0)
        {
            Assert.Equal(targetIds.Take(performedCasts), damage.Select(entry => entry.TargetId));
        }
        BattleEvent[] presented = battle.Events.Skip(eventStart).Where(entry => entry is { Kind: "EffectContact", ActorId: casterId }).ToArray();
        if (firstHeaderHealing)
        {
            Assert.Equal(targets.Select(target => target.Id), presented.Select(entry => entry.TargetId));
        }
        else
        {
            Assert.Equal(performedCasts + reflectors.Length, presented.Length);
        }
        BattleSnapshot snapshot = battle.Capture();
        Assert.All(snapshot.Combatants.Where(actor => targetIds.Take(performedCasts).Contains(actor.Id)), actor => Assert.Equal(0, actor.NativeParticipant!.RemainingHits));
        for (int index = 0; index < committedPrefix.Length; index++)
        {
            Assert.Same(committedPrefix[index], battle.Events[index]);
        }
        Assert.Equal(battle.Events.Skip(eventStart), observed);
        Assert.Single(observed.Where(entry => entry is { Kind: "ActionComplete", ActorId: casterId }));
        if (spell == 1 && reflectionMask != 0)
        {
            Assert.Contains(resolutions, value => value is { Kind: "damage", ActualAmount: > 0 } && value.ActualTargetId.StartsWith("party-", StringComparison.Ordinal));
        }
        return (contacts, reflections, damage, snapshot.Random!.Indexes.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Renzokuken_routes_damage_without_changing_its_original_contact(bool withFinisher)
    {
        var hosted = RunRoutedRenzokuken(withFinisher);
        Assert.NotEmpty(hosted.Applied);
    }

    private static (int[] Applied, byte[] Random) RunRoutedRenzokuken(bool withFinisher)
    {
        const string casterId = "party-0";
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition, recipientHpMultiplier: 64));
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
            byte? finisher = null;
            if (crisis > 0 && NativeBattleRenzokukenRules.HasFinisher(crisis, gate))
            {
                _ = forecast.Draw(out byte roll);
                finisher = NativeBattleRenzokukenRules.Finisher(caster.NativePlayerStatRules!.WeaponRecord[2], roll);
            }
            if (crisis > 0 && (withFinisher ? finisher == 0 : finisher is null))
            {
                break;
            }
            crisis = 0;
        }
        Assert.InRange(crisis, 1, 4);
        caster.CurrentHp = caster.MaximumHp;
        int eventsBefore = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: casterId, TargetId: source.Id, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16));
        NativeBattleActionPacketsState ReadPackets() => battle.Presentation!.ActionPackets;
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
        BattleNativeRenzokukenState state = Assert.IsType<BattleNativeRenzokukenState>(battle.GetNativeRenzokuken(casterId));
        Assert.Equal(withFinisher ? (byte?)0 : null, state.FinisherActionId);
        int initialHits = Assert.Single(battle.ReadDefinition().NativeRenzokukenResources, resource => resource.MagicId == state.MagicId).Timeline.InputThresholds.Length;
        if (withFinisher)
        {
            TickUntil(world, () => RoutedDamage().Length == initialHits + 1, "routed Rough Divide formula");
            packets = ReadPackets();
            NativeBattleActionHeaderSlot finisher = Assert.Single(packets.PublishedHeaders(), entry => entry.Slot.Command == 250).Slot;
            Assert.Equal(ushort.MaxValue, finisher.Action);
            Assert.Equal(1, finisher.ReadContactCount());
            selected = packets.ReadContact(finisher.FirstContact).Primary;
            Assert.Equal(originalTarget, selected.Recipient);
            Assert.Equal((0, (byte)0), (selected.PresentedAmount, selected.Sequence));
        }
        TickUntil(world, () => battle.GetNativeRenzokuken(casterId) is null && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, "routed Renzokuken completion");
        int[] applied = RoutedDamage();
        Assert.Equal(initialHits + (withFinisher ? 1 : 0), applied.Length);
        Assert.Equal(originalHp, source.CurrentHp);
        int[] presented = battle.Events.Skip(eventsBefore).Where(entry => entry is { Kind: "EffectContact", ActorId: casterId } && entry.TargetId == recipient.Id).Select(entry => entry.Amount).ToArray();
        Assert.Equal(applied.Length, presented.Length);
        Assert.Equal(applied[..^1], presented[..^1]);
        Assert.True(presented[^1] >= applied[^1]);
        return (applied, battle.Capture().Random!.Indexes.ToArray());
    }

    private static (NativeBattleContactHalf[] Contacts, int Presented, int Applied, byte[] Random, string Trace) RunRoutedGuardian()
    {
        const ushort formation = 89;
        const int guardian = 1;
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition));
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
            if (battle.NativeGuardianBoost is { Phase: >= 4, Remaining: <= 2 } || battle.Presentation?.PendingGuardianBoost is not null)
            {
                beforeFormula = battle.Capture().Random!;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(Routed(), "The final GF formula did not route damage.");
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        NativeBattleActionPacketsState packets = process.ActionPackets;
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
        IEnumerable<NativeBattleContactPacket> routedContacts = process.LastFrameServices!.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet);
        NativeBattleContactPacket routed = Assert.Single(routedContacts, packet => packet.Occurrence is null && packet.Primary.Recipient == recipientModel);
        Assert.Null(routed.Occurrence);
        Assert.True(routed.Primary.PresentedAmount > 0);
        int loss = recipientHp - recipient.CurrentHp;
        Assert.True(loss > 0);
        NativeBattleRandomState random = battle.Capture().Random!;
        string trace = $"ready {FormatRandom(readyRandom)}, charged {FormatRandom(chargedRandom)}, before formula {FormatRandom(beforeFormula)}, resolved {FormatRandom(random)}, source spirit {source.Spirit}";
        TickUntil(world, () => (process.Graphics).Effects.Slots.Any(task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelOwner == recipientModel), "routed GF presentation");
        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && !process.HasActiveAction, "routed GF completion");
        return (contacts, routed.Primary.PresentedAmount, loss, random.Indexes.ToArray(), trace);

        bool Routed() => battle.Events.Skip(eventsBefore).Any(entry => entry.Kind == "Damage" && entry.ActorId == Caster && entry.TargetId == recipient.Id);
        static string FormatRandom(NativeBattleRandomState random) => $"{random.Lane}/{string.Join(",", random.Indexes)}";
    }

    private static (int Presented, int Applied, byte[] Random) RunRoutedGunblade()
    {
        const ushort formation = 89;
        const string casterId = "party-0";
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindRoute(definition));
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

        NativeBattleActionPacketsState packets = battle.Presentation!.ActionPackets;
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal(1, header.ReadContactCount());
        NativeBattleContactHalf selected = packets.ReadContact(header.FirstContact).Primary;
        Assert.Equal(sourceModel, selected.Recipient);
        Assert.Equal(0, selected.PresentedAmount);
        Assert.Equal((byte)0, selected.Sequence);
        int loss = recipientHp - recipient.CurrentHp;
        Assert.True(loss > 0);
        NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
        NativeBattleEffectTask damage = Assert.Single(graphics.Effects.Slots, task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelOwner == recipientModel);
        string digits = string.Concat(damage.Glyphs.TakeWhile(glyph => glyph != 0).Select(glyph => (char)('0' + glyph - 56)));
        int presented = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Math.Min(recipientHp, presented), loss);
        Assert.Equal(originalHp, source.CurrentHp);
        byte[] random = battle.Capture().Random!.Indexes.ToArray();
        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Presentation?.HasActiveAction != true, "routed Gunblade completion");
        BattleEvent contact = Assert.Single(battle.Events.Skip(eventsBefore), entry => entry is { Kind: "EffectContact", ActorId: casterId } && entry.TargetId == recipient.Id);
        Assert.Equal(presented, contact.Amount);
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

    private static BattleTrace Run(ushort formation, int partySize)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, partySize), formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.NotNull(battle.Presentation);
        Assert.NotNull(battle.Presentation);
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

        TickUntil(world, () => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Presentation?.HasActiveAction != true, $"formation {formation} ready for final attack");
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
        TickUntil(world, () => battle.CanReturnToField && (battle.Presentation?.TerminalFinished ?? true), $"formation {formation} victory");
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
            NativeBattleActionPacketsState packets = battle.Presentation!.ActionPackets;
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
