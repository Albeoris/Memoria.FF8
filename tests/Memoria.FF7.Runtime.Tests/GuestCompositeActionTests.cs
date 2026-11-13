using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class GuestCompositeActionTests
{
    public static void Run()
    {
        Ff7BattleGuestCompositeState empty = JsonSerializer.Deserialize<Ff7BattleGuestCompositeState>(JsonSerializer.Serialize(new Ff7BattleGuestCompositeState([])))!;
        Check(!empty.Contacts.IsDefault && !empty.SettledContacts.IsDefault && empty.NextContact == 0, "A fresh composite contact owner must serialize as explicit empty collections.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP composite action host requires MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[300];
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Guid encounter = Guid.NewGuid();
        Ff7BattleSession initial = Create();
        initial.BindGuestVitals(new(encounter, new('A', 64), 0, 0, 0, 0, 400, 400, 6, 14, 24, 17));
        Ff7BattleSnapshot seed = initial.Capture();
        seed.Actors[0] = seed.Actors[0] with { Gauge = ushort.MaxValue - 1 };
        int enemyIndex = Array.FindIndex(seed.Actors, actor => actor.Actor.Slot == 4);
        seed.Actors[enemyIndex] = seed.Actors[enemyIndex] with { Actor = seed.Actors[enemyIndex].Actor with { HitPoints = 25, MaxHitPoints = 25 } };
        CheckRepeatedContacts();
        CheckRejectedPreparation();
        CheckRandomSchedule();
        CheckOutOfOrderContact();
        CheckPresentedPermutation();
        CheckHistoricalSettledPrefix();
        CheckSourceDrain();
        CheckSelfDrain();
        CheckReflectedConvergence();
        CheckMixedReflectedGroupOrder();
        CheckReflectionTransitionOrder();
        CheckSourceFailure();

        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);

        (Ff7BattleSession Battle, Probe Source) Start(Ff7BattleGuestCompositeTargetPolicy policy = Ff7BattleGuestCompositeTargetPolicy.Selected, string? failure = null, Ff7BattleSnapshot? snapshot = null, Action<Probe>? configure = null, ushort targetMask = 0x10, bool group = false)
        {
            Ff7BattleSession battle = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot ?? seed)));
            Probe source = new(encounter, policy: policy, failure: failure);
            configure?.Invoke(source);
            source.CaptureRandom = () => battle.Capture().Random;
            battle.BindGuestAttack(source);
            for (int tick = 0; tick < 200 && battle.ReadyPlayer != 0; tick++)
            {
                battle.Advance();
            }
            Check(battle.ReadyPlayer == 0, "The source gauge did not reach its ready command boundary.");
            Check(battle.Submit(0, Ff7BattleCommand.Magic, 1, targetMask, group), "The ready guest could not queue its composite action.");
            for (int tick = 0; tick < 200 && battle.Action is null && battle.GuestAbilityFailure is null && !source.SawSourceFailure; tick++)
            {
                battle.Advance(waitForCommand: true);
            }
            return (battle, source);
        }

        void CheckRepeatedContacts()
        {
            (Ff7BattleSession battle, Probe source) = Start();
            Ff7BattleGuestCompositeState schedule = battle.Action?.GuestComposite ?? throw new InvalidOperationException("The action did not publish its occurrence owner.");
            Check(schedule.Contacts.Length == 6 && schedule.Contacts.Select(contact => contact.Ordinal).SequenceEqual(Enumerable.Range(0, 6)), "Repeated hits were deduplicated by their recipient.");
            for (int index = 1; index < schedule.Contacts.Length; index++)
            {
                Ff7BattleActionVitals previous = schedule.Contacts[index - 1].HitDescription.PreparedContact!.Receipt.Target;
                Ff7BattleActionVitals expected = previous with { OriginSavedStatuses = Ff7GuestBattleStatuses.ToOrigin(previous.Statuses, previous.OriginSavedStatuses) };
                Ff7BattleActionVitals actual = schedule.Contacts[index].HitDescription.PreparedContact!.TargetBefore;
                Check(actual == expected, $"The next occurrence did not use the preceding occurrence's projected vitals. Expected {JsonSerializer.Serialize(expected)}; actual {JsonSerializer.Serialize(actual)}.");
            }
            Ff7BattleGuestPreparedContact knockout = schedule.Contacts.Select(contact => contact.HitDescription.PreparedContact!).First(contact => (contact.Receipt.AddedStatuses & Ff7NativeBattleActionResolver.Death) != 0);
            Check(knockout.Receipt.Target.Hp == 0 && (knockout.Receipt.Target.Statuses & Ff7NativeBattleActionResolver.Death) != 0 && (Ff7GuestBattleStatuses.ToOrigin(knockout.Receipt.Target.Statuses, knockout.Receipt.Target.OriginSavedStatuses) & 1) != 0, "Projected KO must publish host death and derive the origin saved-bank death alias for the next occurrence.");
            Check(battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 25, "Packet preparation applied damage before a presentation contact.");
            Ff7BattleSession cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture())));
            Probe restored = source.Restore();
            cold.BindGuestAttack(restored);
            for (int index = 0; index < 8; index++)
            {
                battle.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The composite contact cursor diverged after JSON restore.");
                if (battle.Action?.GuestComposite is { } active)
                {
                    Check(active.NextContact == Math.Min(index + 1, 6), "An occurrence was skipped or settled more than once.");
                }
            }
            Check(source.Published == 6 && source.Commits == 1 && battle.Action is null && battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 0, "The composite action did not finish its contacts and source transaction exactly once.");
        }

        void CheckRejectedPreparation()
        {
            (Ff7BattleSession battle, Probe source) = Start(failure: "Unsupported geometry.");
            Check(battle.GuestAbilityFailure?.Reason == "Unsupported geometry." && battle.Action is null && source.SourcePreparations == 0 && source.Commits == 0 && source.Invocation is null, "An unsupported action consumed or retained its source preparation.");
            Check(battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 25 && battle.ReadyPlayer == 0, "Rejected preparation consumed the actor's ready turn or applied a speculative contact.");
            Check(source.SourceStock == 100 && JsonSerializer.Serialize(battle.Capture().Random) == JsonSerializer.Serialize(source.RandomBeforePreparation), "Rejected capability preflight retained speculative source stock or RNG draws.");
        }

        void CheckRandomSchedule()
        {
            (Ff7BattleSession battle, Probe source) = Start(Ff7BattleGuestCompositeTargetPolicy.RandomSelected, configure: probe => probe.Attack = probe.Attack with { Formula = 0x22, FixedDamage = null, BaseDamage = 40 });
            Ff7BattleGuestCompositeState schedule = battle.Action!.GuestComposite!;
            Check(schedule.Contacts.Length == 6 && source.RandomBeforePreparation is not null, "The random action lost its native repetition schedule.");
            Ff7BattleRng expected = new(kernel[2].Data.Span, source.RandomBeforePreparation!);
            expected.NextByte();
            ushort pool = (ushort)battle.Actors.Where(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints != 0 && (actor.Actor.Statuses & 1) == 0 && (actor.Actor.Conditions & 8) != 0).Aggregate(0, (mask, actor) => mask | (1 << actor.Actor.Slot));
            int[] targets = Enumerable.Range(0, 6).Select(_ => System.Numerics.BitOperations.TrailingZeroCount(Ff7NativeBattleTargets.ResolveNumericalPass(pool, 0x80, 0, expected))).ToArray();
            Check(schedule.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).SequenceEqual(targets), "Random target decisions did not precede damage RNG in host order.");
        }

        void CheckOutOfOrderContact()
        {
            (Ff7BattleSession battle, Probe source) = Start();
            source.Reorder = true;
            bool rejected = false;
            try
            {
                battle.Advance(waitForCommand: true);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected && battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 25, "An out-of-order contact published a numerical result.");
        }

        void CheckPresentedPermutation()
        {
            (Ff7BattleSession battle, Probe source) = Start(snapshot: DrainSeed(), configure: probe =>
            {
                probe.Repetitions = 3;
                probe.PresentationOrder = [2, 0, 1];
            });
            battle.Advance(waitForCommand: true);
            Check(battle.Action!.GuestComposite is { NextContact: 1 } first && first.SettledContacts.SequenceEqual([2]) && battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 90, "The host did not settle the native presentation's first actual ordinal against current recipient vitals.");
            Ff7BattleSnapshot retained = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
            Ff7BattleSession duplicate = Create(retained);
            Probe duplicateSource = source.Restore();
            duplicateSource.PresentationOrder = [2, 2, 1];
            duplicate.BindGuestAttack(duplicateSource);
            bool rejected = false;
            try
            {
                duplicate.Advance(waitForCommand: true);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected && duplicate.Action!.GuestComposite!.NextContact == 1 && duplicate.Action.GuestComposite.SettledContacts.SequenceEqual([2]) && duplicate.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 90, "An already settled native ordinal applied its receipt twice after JSON restore.");
            Ff7BattleSession cold = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(retained)));
            cold.BindGuestAttack(source.Restore());
            for (int step = 0; step < 3; step++)
            {
                battle.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The native ordinal permutation diverged after JSON restore.");
            }
            Check(battle.Action is null && source.Published == 3 && source.SourceStock == 99 && battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 70, "The presentation permutation did not settle every receipt once under its single command owner.");
        }

        void CheckHistoricalSettledPrefix()
        {
            (Ff7BattleSession battle, Probe source) = Start();
            battle.Advance(waitForCommand: true);
            System.Text.Json.Nodes.JsonNode json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(battle.Capture()))!;
            json["Action"]!["GuestComposite"]!.AsObject().Remove("SettledContacts");
            Ff7BattleSession older = Create(JsonSerializer.Deserialize<Ff7BattleSnapshot>(json.ToJsonString()));
            older.BindGuestAttack(source.Restore());
            battle.Advance(waitForCommand: true);
            older.Advance(waitForCommand: true);
            Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(older.Capture()) && older.Action!.GuestComposite!.SettledContacts.SequenceEqual([0, 1]), "An absent settled ordinal collection did not recover the previously recorded prefix without replay.");
        }

        Ff7BattleSnapshot DrainSeed()
        {
            Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(seed))!;
            snapshot.Actors[0] = snapshot.Actors[0] with { Actor = snapshot.Actors[0].Actor with { HitPoints = 100 } };
            snapshot.Actors[enemyIndex] = snapshot.Actors[enemyIndex] with { Actor = snapshot.Actors[enemyIndex].Actor with { HitPoints = 100, MaxHitPoints = 100 } };
            return snapshot;
        }

        void CheckSourceDrain()
        {
            (Ff7BattleSession battle, Probe source) = Start(snapshot: DrainSeed(), configure: probe => probe.Attack = probe.Attack with { GuestDrainStatusAttack = 200 });
            Ff7BattleGuestCompositeState schedule = battle.Action!.GuestComposite!;
            for (int index = 0; index < 6; index++)
            {
                Ff7BattleGuestPreparedContact prepared = schedule.Contacts[index].HitDescription.PreparedContact!;
                Check(prepared.SourceBefore.Hp == 100 + index * 10 && prepared.Receipt.Source.Hp == 110 + index * 10 && prepared.Receipt.SourceHitPointDamage == -10, "Projected source drain did not update the next occurrence's source exactly once.");
                Check(prepared.TargetBefore.Hp == 100 - index * 10 && prepared.Receipt.Target.Hp == 90 - index * 10, "Drain contacts did not retain their sequential recipient vitals.");
                battle.Advance(waitForCommand: true);
                Check(battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints == prepared.Receipt.Source.Hp && battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == prepared.Receipt.Target.Hp, "Actual contact publication lost or doubled a source drain receipt.");
            }
            battle.Advance(waitForCommand: true);
            Check(battle.Action is null && source.SourceStock == 99 && source.Commits == 1, "Repeated drain spent source stock more than once or retained its queue owner.");
        }

        void CheckSelfDrain()
        {
            (Ff7BattleSession battle, Probe source) = Start(Ff7BattleGuestCompositeTargetPolicy.Source, snapshot: DrainSeed(), configure: probe => probe.Attack = probe.Attack with { GuestDrainStatusAttack = 200 }, targetMask: 1);
            Ff7BattleGuestCompositeState schedule = battle.Action!.GuestComposite!;
            foreach (Ff7BattleGuestCompositeContact contact in schedule.Contacts)
            {
                Ff7BattleGuestPreparedContact prepared = contact.HitDescription.PreparedContact!;
                Check(prepared.Invocation.TargetSlot == 0 && prepared.SourceBefore.Hp == 100 && prepared.TargetBefore.Hp == 100 && prepared.Receipt.Source.Hp == 100 && prepared.Receipt.Target.Hp == 100 && prepared.Receipt.SourceHitPointDamage == -10, "A self-drain occurrence did not combine target damage and source recovery in one owner.");
                battle.Advance(waitForCommand: true);
                Check(battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints == 100, "Self-drain applied its source recovery twice or lost the target damage.");
            }
            battle.Advance(waitForCommand: true);
            Check(battle.Action is null && source.Published == 6 && source.SourceStock == 99, "The self-targeted series failed to release its single source transaction.");
        }

        void CheckReflectedConvergence()
        {
            Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(seed))!;
            int[] enemySlots = snapshot.Actors.Where(actor => actor.Actor.Slot >= 4).Select(actor => actor.Actor.Slot).Order().ToArray();
            Check(enemySlots.Length >= 2, "The original opening formation must supply two physical reflectors.");
            foreach (int slot in enemySlots)
            {
                int index = Array.FindIndex(snapshot.Actors, actor => actor.Actor.Slot == slot);
                snapshot.Actors[index] = snapshot.Actors[index] with { ReflectionCount = 3, Actor = snapshot.Actors[index].Actor with { Statuses = snapshot.Actors[index].Actor.Statuses | Ff7NativeBattleStatuses.Reflect } };
            }
            ushort mask = (ushort)enemySlots.Aggregate(0, (targets, slot) => targets | (1 << slot));
            (Ff7BattleSession battle, Probe source) = Start(snapshot: snapshot, configure: probe =>
            {
                probe.Group = true;
                probe.Repetitions = 1;
                probe.Attack = probe.Attack with { Reflectable = true };
            }, targetMask: mask, group: true);
            Ff7BattleGuestCompositeState schedule = battle.Action!.GuestComposite!;
            Check(schedule.Contacts.Length == enemySlots.Length && schedule.Contacts.All(contact => contact.Reflected && contact.HitDescription.PreparedContact!.Invocation.TargetSlot == 0), "Distinct reflectors did not retain their converging recipient occurrences.");
            Check(enemySlots.All(slot => battle.Actors.Single(actor => actor.Actor.Slot == slot).ReflectionCount == 3), "Reflection packet planning consumed a live reflection counter before contact.");
            for (int index = 0; index < schedule.Contacts.Length; index++)
            {
                Ff7BattleGuestPreparedContact prepared = schedule.Contacts[index].HitDescription.PreparedContact!;
                Check(prepared.TargetBefore.Hp == 400 - index * 10 && prepared.Receipt.Target.Hp == 390 - index * 10, "A converging reflection reused its recipient's original vitals.");
                battle.Advance(waitForCommand: true);
                Check(battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints == prepared.Receipt.Target.Hp, "A reflected occurrence was deduplicated or applied twice.");
                for (int reflector = 0; reflector < enemySlots.Length; reflector++)
                {
                    Ff7BattleActorState actual = battle.Actors.Single(actor => actor.Actor.Slot == enemySlots[reflector]);
                    Check(actual.ReflectionCount == (reflector <= index ? 2 : 3) && actual.Actor.HitPoints == snapshot.Actors.Single(actor => actor.Actor.Slot == actual.Actor.Slot).Actor.HitPoints, "Reflection publication changed another occurrence's counter or damaged its reflector.");
                }
            }
            battle.Advance(waitForCommand: true);
            Check(battle.Action is null && source.Published == enemySlots.Length && source.SourceStock == 99 && source.Commits == 1, "The converging reflected group did not settle under one source cost and queue owner.");
        }

        void CheckMixedReflectedGroupOrder()
        {
            Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(seed))!;
            int[] slots = snapshot.Actors.Where(actor => actor.Actor.Slot >= 4).Select(actor => actor.Actor.Slot).Order().ToArray();
            Check(slots.Length == 2, "The original opening formation must supply one reflector and one direct recipient.");
            int reflectorIndex = Array.FindIndex(snapshot.Actors, actor => actor.Actor.Slot == slots[0]);
            snapshot.Actors[reflectorIndex] = snapshot.Actors[reflectorIndex] with { ReflectionCount = 3, Actor = snapshot.Actors[reflectorIndex].Actor with { Statuses = snapshot.Actors[reflectorIndex].Actor.Statuses | Ff7NativeBattleStatuses.Reflect } };
            ushort mask = (ushort)slots.Aggregate(0, (targets, slot) => targets | (1 << slot));
            (Ff7BattleSession battle, Probe source) = Start(snapshot: snapshot, configure: probe =>
            {
                probe.Group = true;
                probe.Repetitions = 1;
                probe.Attack = probe.Attack with { Formula = 0x22, FixedDamage = null, BaseDamage = 120, Reflectable = true };
            }, targetMask: mask, group: true);
            Ff7BattleGuestCompositeState schedule = battle.Action!.GuestComposite!;
            Check(schedule.Contacts.Length == 2 && schedule.Contacts[0].HitDescription.PreparedContact!.Invocation.TargetSlot == slots[1] && !schedule.Contacts[0].Reflected && schedule.Contacts[1].HitDescription.PreparedContact!.Invocation.TargetSlot == 0 && schedule.Contacts[1].Reflected, "A reflected group lost breadth-first direct-then-bounced contact order.");
            Ff7BattleRng expectedRandom = new(kernel[2].Data.Span, source.RandomBeforePreparation!);
            expectedRandom.NextByte();
            Ff7BattleGuestPhysicalHit description = source.DescribeMagic(1);
            Ff7BattleActionReceipt ignoredReflector = Resolve(slots[0]);
            Ff7BattleActionReceipt direct = Resolve(slots[1]);
            Ff7BattleActionReceipt bounced = Resolve(0);
            Check(!ignoredReflector.Damage.Missed && schedule.Contacts[0].HitDescription.PreparedContact!.Receipt.Damage == direct.Damage && schedule.Contacts[1].HitDescription.PreparedContact!.Receipt.Damage == bounced.Damage, "A reflected group assigned formula RNG to recipients in a different order from the shared reflection pass.");
            Check(JsonSerializer.Serialize(battle.Capture().Random) == JsonSerializer.Serialize(expectedRandom.Capture()), "A reflected group consumed a different RNG sequence from original-mask evaluation followed by the bounced round.");
            Check(schedule.Contacts[0].Reflections.IsEmpty && schedule.Contacts[1].Reflections is [{ Slot: var reflector, Remaining: 2, Exhausted: false }] && reflector == slots[0], "A reflection transition was attached to an unrelated direct contact.");
            battle.Advance(waitForCommand: true);
            Check(battle.Actors.Single(actor => actor.Actor.Slot == slots[1]).Actor.HitPoints == direct.Target.Hp && battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints == 400 && battle.Actors.Single(actor => actor.Actor.Slot == slots[0]).ReflectionCount == 3, "The first direct contact applied the later bounce or its reflection counter early.");
            battle.Advance(waitForCommand: true);
            Check(battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints == bounced.Target.Hp && battle.Actors.Single(actor => actor.Actor.Slot == slots[0]).ReflectionCount == 2, "The second reflected contact did not commit its matching receipt and reflection transition.");

            Ff7BattleActionReceipt Resolve(int slot)
            {
                Ff7BattleGuestContactContext context = battle.GuestContactContext(0, slot, description, false);
                return Ff7NativeBattleActionResolver.ResolveGuestContact(context.Source, context.SourceVitals, context.Target, context.TargetVitals, context.Attack, description.StatusRecord, expectedRandom, sameActor: slot == 0);
            }
        }

        void CheckReflectionTransitionOrder()
        {
            Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(seed))!;
            snapshot.Actors[0] = snapshot.Actors[0] with { ReflectionCount = 3, Actor = snapshot.Actors[0].Actor with { Statuses = snapshot.Actors[0].Actor.Statuses | Ff7NativeBattleStatuses.Reflect } };
            (Ff7BattleSession prepared, Probe source) = Start(snapshot: snapshot);
            Ff7BattleSnapshot retained = prepared.Capture();
            Ff7BattleActionState action = retained.Action!;
            Ff7BattleGuestCompositeState composite = action.GuestComposite!;
            ImmutableArray<Ff7BattleGuestCompositeContact> contacts = composite.Contacts
                .SetItem(0, composite.Contacts[0] with { Reflections = [new(0, 1, false)] })
                .SetItem(1, composite.Contacts[1] with { Reflections = [new(0, 2, false)] })
                .SetItem(2, composite.Contacts[2] with { Reflections = [new(0, 0, true)] })
                .SetItem(3, composite.Contacts[3] with { Reflections = [new(0, 2, false)] });
            Ff7BattleSession battle = Create(retained with { Action = action with { GuestComposite = composite with { Contacts = contacts } } });
            battle.BindGuestAttack(source.Restore());
            battle.Advance(waitForCommand: true);
            Check(battle.Actors[0].ReflectionCount == 1, "The first settled path did not consume its prepared reflection count.");
            battle.Advance(waitForCommand: true);
            Check(battle.Actors[0].ReflectionCount == 1, "A later-settling path increased a reflector's already consumed counter.");
            battle.Advance(waitForCommand: true);
            Check(battle.Actors[0].ReflectionCount is null && (battle.Actors[0].Actor.Statuses & Ff7NativeBattleStatuses.Reflect) == 0, "An exhausted path retained its Reflect owner.");
            battle.Advance(waitForCommand: true);
            Check(battle.Actors[0].ReflectionCount is null && (battle.Actors[0].Actor.Statuses & Ff7NativeBattleStatuses.Reflect) == 0, "An older prepared path restored an exhausted reflector.");
        }

        void CheckSourceFailure()
        {
            (Ff7BattleSession battle, Probe source) = Start(Ff7BattleGuestCompositeTargetPolicy.RandomSelected, configure: probe => probe.FailSource = true);
            Ff7BattleRng expected = new(kernel[2].Data.Span, source.RandomBeforePreparation!);
            expected.NextByte();
            Check(source.SawSourceFailure && source.SourcePreparations == 0 && source.SourceStock == 100 && source.Commits == 0 && source.Invocation is null && battle.Action is null && battle.GuestAbilityFailure is null, "A failed source preparation retained stock, preparation or an action occurrence.");
            Check(JsonSerializer.Serialize(battle.Capture().Random) == JsonSerializer.Serialize(expected.Capture()), "Source failure consumed target or formula RNG beyond its one actual source roll.");
            Check(battle.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints == 25 && battle.ReadyPlayer != 0, "Source failure changed recipient vitals or retained the completed source turn.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Probe(Guid encounter, Ff7BattleGuestCompositeTargetPolicy policy = Ff7BattleGuestCompositeTargetPolicy.Selected, string? failure = null) : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestCompositeActions, IFf7BattleGuestAbilities
    {
        public int ActorSlot => 0;
        public Guid EncounterId => encounter;
        public Ff7BattleGuestInvocation? Invocation { get; private set; }
        public long HostTick { get; private set; }
        public bool ContactPublished => Published != 0;
        public int Published { get; private set; }
        public int SourcePreparations { get; private set; }
        public int Commits { get; private set; }
        public bool Reorder { get; set; }
        public ImmutableArray<int> PresentationOrder { get; set; } = [];
        public bool Group { get; set; }
        public int Repetitions { get; set; } = 6;
        public int SourceStock { get; private set; } = 100;
        public bool FailSource { get; set; }
        public bool SawSourceFailure { get; private set; }
        public Ff7BattleDamageAction Attack { get; set; } = new(0x26, 1, 255) { AlwaysHits = true, FixedDamage = 10 };
        public Ff7BattleRngState? RandomBeforePreparation { get; set; }
        public Func<Ff7BattleRngState>? CaptureRandom { get; set; }
        private ImmutableArray<Ff7BattleGuestCompositeContact> contacts = [];

        public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null;
        public int MagicStock(ushort ability) => SourceStock;
        public IReadOnlyList<ushort> MagicIds => [1];
        public IReadOnlyList<Ff7BattleGuestAbility> Abilities => [new(Ff7BattleCommand.Magic, 1, "Composite", Group ? (byte)6 : (byte)0, SourceStock)];
        public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target) => command == Ff7BattleCommand.Magic && ability == 1 && SupportsMagic(ability, target);
        public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => target.Slot != 3 && SourceStock > 0;
        public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability) => new(new(6, 24, 24, 24, 14, 0, 0, 24, 17), Attack, 0);
        public void PrepareMagic(Ff7BattleGuestPhysicalHit prepared) => throw new InvalidOperationException("Composite actions must use their occurrence owner.");
        public Ff7BattleGuestCompositeAction DescribeCompositeAction(ushort ability) => new([new(DescribeMagic(ability), Repetitions, policy)]);
        public IFf7BattleGuestPreparationTransaction BeginCompositePreparation(ushort ability)
        {
            RandomBeforePreparation = CaptureRandom?.Invoke();
            return new Preparation(this);
        }
        public bool PrepareSource(ushort ability, Func<byte> nextRandomByte, out string? error)
        {
            nextRandomByte();
            SourcePreparations++;
            SourceStock--;
            SawSourceFailure = FailSource;
            error = FailSource ? "The original source failed." : null;
            return !FailSource;
        }
        public string? PrepareCompositeUnsupportedReason(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> values) => failure;
        public void PrepareComposite(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> values) => contacts = values.ToImmutableArray();
        public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
        {
            Invocation = invocation;
            HostTick = invocation.StartedAt;
        }
        public Ff7BattleGuestAttackUpdate Advance(long hostTick)
        {
            HostTick = hostTick;
            if (Published < contacts.Length)
            {
                if (Reorder)
                {
                    return new(null, false) { CompositeContacts = [Published, Published] };
                }
                int ordinal = PresentationOrder.IsEmpty ? Published : PresentationOrder[Published];
                Published++;
                return new(null, false) { CompositeContacts = [ordinal] };
            }
            Invocation = null;
            return new(null, true);
        }
        public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick) => HostTick = hostTick;
        public Probe Restore() => new(encounter, policy, failure) { Invocation = Invocation, HostTick = HostTick, Published = Published, SourcePreparations = SourcePreparations, Commits = Commits, SourceStock = SourceStock, Group = Group, Repetitions = Repetitions, Attack = Attack, PresentationOrder = PresentationOrder, contacts = contacts };

        private sealed class Preparation(Probe owner) : IFf7BattleGuestPreparationTransaction
        {
            private readonly int preparations = owner.SourcePreparations;
            private readonly int stock = owner.SourceStock;
            private bool committed;
            public void Commit()
            {
                committed = true;
                owner.Commits++;
            }
            public void Dispose()
            {
                if (!committed)
                {
                    owner.SourcePreparations = preparations;
                    owner.SourceStock = stock;
                }
            }
        }
    }
}
