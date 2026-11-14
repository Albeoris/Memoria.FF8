using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OriginMagicSequenceTests
{
    private const uint Double = 0x20000;
    private const uint Triple = 0x40000;

    public static void Run()
    {
        DamageScalingAndFloat();
        InstalledSequences();
        Console.WriteLine("PASS source-owned Magic sequences, per-contact Triple scaling and semantic Float affinity.");
    }

    private static void DamageScalingAndFloat()
    {
        Ff7BattleDamageActor actor = new(20, 50, 80, 10, 10, 0, 0, 20, 20);
        Ff7BattleDamageAction fixedHit = new(0x26, 1, 255) { FixedDamage = 5, AlwaysHits = true };
        Ff7BattleDamageResult Hit(Ff7BattleDamageAction action, Ff7BattleDamageActor? target = null)
        {
            return Ff7NativeBattleDamage.Resolve(actor, target ?? actor, action, new(new byte[3988], new(new byte[8])));
        }
        Check(Hit(fixedHit).Damage == 5 && Hit(fixedHit with { DamageScalePercent = 75 }).Damage == 3, "Triple must floor each positive damage contact independently to 75 percent.");
        Check(Enumerable.Range(0, 3).Sum(_ => Hit(fixedHit with { DamageScalePercent = 75 }).Damage) == 9, "Three Triple contacts must round separately rather than scaling an aggregated total.");
        Check(Hit(fixedHit with { FixedDamage = 15000, DamageScalePercent = 75 }).Damage == 7499, "Triple scaling must follow the ordinary native damage cap.");
        Check(Hit(fixedHit with { Healing = true, DamageScalePercent = 75 }).Damage == -5, "Triple must preserve full-strength curative contacts.");
        Check(Hit(fixedHit with { Affinity = Ff7BattleDamageAffinity.Absorb, DamageScalePercent = 75 }).Damage == -5, "Triple must preserve native absorbed healing.");
        Check(Hit(new(0x50, 0, 255, HasStatusOperation: true, AddsStatuses: true) { DamageScalePercent = 75 }).Damage == 0, "Triple must not turn a status-only operation into HP damage.");
        Ff7BattleDamageActor floating = actor with { OriginBattleStatuses = Ff7GuestBattleStatuses.OriginFloat };
        Check(Hit(fixedHit with { NativeElements = 0x08 }, floating).Damage == 0, "Float must nullify native Earth through the common elemental affinity calculation.");
        Check(Hit(fixedHit with { NativeElements = 0x10 }, floating).Damage == 5, "Float must not alias the native Poison element.");
        Check(Hit(fixedHit with { NativeElements = 0x18 }, floating).Damage == 0, "An Earth component must use the existing composite Nullify precedence.");
        Check(Hit(fixedHit with { NativeElements = 0x08, IgnoreNativeAffinity = true }, floating).Damage == 5, "Float must retain the native explicit affinity bypass.");
        Ff7BattleDamageAction restored = RoundTrip(fixedHit with { DamageScalePercent = 75 });
        Check(Hit(restored).Damage == 3, "A queued Triple damage descriptor must retain its scale through JSON.");
    }

    private static void InstalledSequences()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed native Magic sequences: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7CharacterState[] characters = original.Progress.Characters.ToArray();
        byte[] record = (byte[])characters[0].NativeRecord.Clone();
        record[28] = 3;
        record[29] = 3;
        record.AsSpan(64, 64).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x31);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(72), 0x17);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(76), 0x31);
        characters[0] = characters[0] with { NativeRecord = record };
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255], Characters = characters };
        Fixture fixture = new(formations[300], progress, kernel, modifiers);
        Ff7BattleSpell spell = Ff7NativeBattleSpells.Read(characters[0], kernel).First(value => value.Attack.DamageCalculation == 0x22 && value.Attack.CastingCost > 0);
        ushort ability = spell.AttackId;
        int cost = spell.Attack.CastingCost;
        int? nativeFullDamage = null;
        foreach ((uint statuses, int count) in new[] { (0u, 1), (Double, 2), (Triple, 3), (Double | Triple, 3) })
        {
            Ff7BattleSession battle = fixture.Open(fixture.Ready(0, statuses));
            Check(battle.GuestMagicCastCounts(0, ability).SequenceEqual(new[] { count }), "The source's ordinary Magic must expose exactly its owned cast count, with Triple taking precedence.");
            ushort[] targets = Enumerable.Range(0, count).Select(index => (ushort)(index % 2 == 0 ? 0x10 : 0x20)).ToArray();
            Check(battle.SubmitMagicSequence(0, ability, targets), "A native FFVII caster must admit one chosen spell with independently chosen ordered targets.");
            List<Ff7BattleHit> hits = fixture.Complete(ref battle);
            Check(hits.Select(hit => hit.Target).SequenceEqual(targets.Select(mask => mask == 0x10 ? 4 : 5)) && hits.All(hit => hit.Source == 0 && hit.AttackId == ability), "The sequence must publish the same chosen spell once per ordered target.");
            Check(battle.Actors[0].Actor.MagicPoints == 100 - cost * count && battle.Capture().GuestMagicSequences.IsEmpty, "Each actual cast must debit native MP once and release the sequence after its last cast.");
            if (count == 1)
            {
                nativeFullDamage = hits[0].Damage.Damage;
                Check(nativeFullDamage > 0, "The native spell baseline must produce genuine positive damage.");
            }
            else
            {
                Check(hits[0].Damage.Damage == (count == 3 ? nativeFullDamage * 75 / 100 : nativeFullDamage), "The first original spell contact must retain full Double damage and apply Triple's final 75 percent scale.");
            }
        }
        foreach (int selected in new[] { 1, 2 })
        {
            Ff7BattleSession partial = fixture.Open(fixture.Ready(0, Triple));
            Check(partial.SubmitMagicSequence(0, ability, Enumerable.Repeat((ushort)0x10, selected).ToArray()), "Explicit partial confirmation must retain the selected casts under Triple.");
            List<Ff7BattleHit> hits = fixture.Complete(ref partial);
            Check(hits.Count == selected && hits[0].Damage.Damage == nativeFullDamage * 75 / 100 && partial.Actors[0].Actor.MagicPoints == 100 - selected * cost, "A partially confirmed Triple must neither expand again nor lose its per-cast scale or MP ownership.");
        }
        Ff7BattleSession wrongCount = fixture.Open(fixture.Ready(0, Double));
        string before = JsonSerializer.Serialize(wrongCount.Capture());
        Check(!wrongCount.SubmitMagicSequence(0, ability, new ushort[] { 0x10, 0x20, 0x10 }) && JsonSerializer.Serialize(wrongCount.Capture()) == before, "Double must reject a three-target submission without consuming readiness, MP or RNG.");
        Ff7BattleSession grouped = fixture.Open(fixture.Ready(0, Double));
        Check(grouped.Commands(0).Single(option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability).GroupUses == 1, "The original linked All fixture must offer exactly one group cast.");
        Check(grouped.SubmitMagicSequence(0, ability, new ushort[] { 0x30, 0x10 }, new[] { true, false }), "Each cast must retain its own independent group selection.");
        List<Ff7BattleHit> groupHits = fixture.Complete(ref grouped);
        Check(groupHits.Select(hit => hit.Target).SequenceEqual(new[] { 5, 4 }) && grouped.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints < 30000 && grouped.Actors.Single(actor => actor.Actor.Slot == 5).Actor.HitPoints < 30000, "An All cast followed by a single-target cast must preserve both group contacts and the independently selected final target.");
        Check(grouped.Actors[0].Actor.MagicPoints == 100 - 2 * cost && grouped.Actors[0].AllUses?.GetValueOrDefault((byte)ability) == 0, "The mixed group sequence must consume two spell costs and one native All use.");
        Ff7BattleSession tooManyGroups = fixture.Open(fixture.Ready(0, Double));
        before = JsonSerializer.Serialize(tooManyGroups.Capture());
        Check(!tooManyGroups.SubmitMagicSequence(0, ability, new ushort[] { 0x30, 0x30 }, new[] { true, true }) && JsonSerializer.Serialize(tooManyGroups.Capture()) == before, "A sequence exceeding native All uses must reject before spending any resources.");
        foreach ((string reason, Func<Ff7BattleSnapshot, Ff7BattleSnapshot> interrupt) in new (string, Func<Ff7BattleSnapshot, Ff7BattleSnapshot>)[]
        {
            ("source owner loss", snapshot => snapshot with { GuestOriginStatuses = [] }),
            ("source KO", snapshot => snapshot with { Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = 1 } } : actor).ToArray() }),
            ("insufficient later MP", snapshot => snapshot with { Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { MagicPoints = 0 } } : actor).ToArray() })
        })
        {
            Ff7BattleSession battle = fixture.Open(fixture.Ready(0, Triple));
            Check(battle.SubmitMagicSequence(0, ability, new ushort[] { 0x10, 0x20, 0x10 }), "The interrupted Triple fixture must admit all selected targets initially.");
            List<Ff7BattleHit> hits = fixture.Complete(ref battle, interrupt);
            Check(hits.Count == 1 && battle.Capture().GuestMagicSequences.IsEmpty, $"{reason} must cancel remaining casts after the first actual contact.");
            if (reason == "source owner loss")
            {
                Check(battle.Actors[0].Actor.MagicPoints == 100 - cost, "Canceled casts must not spend MP after their caster loses the source repetition owner.");
            }
        }
        Ff7BattleSession automatic = fixture.Open(fixture.Ready(0, Double));
        Check(automatic.Submit(0, Ff7BattleCommand.Magic, ability, 0x10), "Direct native Magic submission must remain available under Double.");
        Check(fixture.Complete(ref automatic).Select(hit => hit.Target).SequenceEqual(new[] { 4, 4 }), "Direct native Magic must repeat its selected default target under the source count.");
        Ff7BattleSnapshot incoming = fixture.Ready(4, Double);
        Ff7BattleSession unaffected = fixture.Open(incoming);
        Check(unaffected.Submit(0, Ff7BattleCommand.Magic, ability, 0x10), "An ordinary cast must admit a target with Double.");
        Check(fixture.Complete(ref unaffected).Count == 1, "Target Double must never repeat an unrelated incoming spell.");
        InstalledInherentGroupMagic(formations[300], progress, kernel, modifiers);
        InstalledEnemyMagic(formations, progress, kernel, modifiers);
    }

    private static void InstalledInherentGroupMagic(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers)
    {
        Ff7CharacterState[] characters = progress.Characters.ToArray();
        byte[] record = (byte[])characters[0].NativeRecord.Clone();
        record.AsSpan(64, 64).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x48 | 0xFFFFFFu << 8);
        characters[0] = characters[0] with { NativeRecord = record };
        Ff7Progress equipped = progress with { Characters = characters };
        Ff7BattleSpell spell = Ff7NativeBattleSpells.Read(characters[0], kernel).Single(value => value.Attack.DamageCalculation == 0x22 && (value.Attack.TargetFlags & 4) != 0 && (value.Attack.TargetFlags & 8) == 0);
        Fixture fixture = new(formation, equipped, kernel, modifiers);
        Ff7BattleSnapshot setup = fixture.Ready(0, Double);
        setup = setup with { Actors = setup.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { MagicPoints = 300, MaxMagicPoints = 300 } } : actor).ToArray() };
        Ff7BattleSession battle = fixture.Open(setup);
        Ff7BattleCommandOption option = battle.Commands(0).Single(choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == spell.AttackId);
        ushort enemies = (ushort)(option.ValidTargets & 0x3F0);
        Check(!option.SelectSingle && option.GroupUses == 0 && enemies == 0x30, "The original inherently grouped Magic fixture must select both opponents without any linked All uses.");
        Check(battle.SubmitMagicSequence(0, spell.AttackId, new ushort[] { enemies, enemies }), "Double must admit two inherently grouped casts without an explicit All selection.");
        List<Ff7BattleHit> hits = fixture.Complete(ref battle);
        Check(hits.Select(hit => hit.Target).SequenceEqual(new[] { 5, 5 }) && hits.All(hit => hit.AttackId == spell.AttackId), "Both repeated inherently grouped casts must retain their complete native target group.");
        Check(battle.Actors.Where(actor => actor.Actor.Slot is 4 or 5).All(actor => actor.Actor.HitPoints < 30000) && battle.Actors[0].Actor.MagicPoints == 300 - 2 * spell.Attack.CastingCost, "Two inherently grouped casts must apply both groups and pay the original MP cost twice.");
        Check(battle.Actors[0].AllUses?.GetValueOrDefault(spell.AttackId) is null or 0 && battle.Capture().GuestMagicSequences.IsEmpty, "Inherent group targeting must neither require All nor retain a stale continuation.");
    }

    private static void InstalledEnemyMagic(IReadOnlyList<SceneFormation> formations, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers)
    {
        var selected = formations.Select(formation => (Formation: formation, Data: SceneBattleData.Read(formation)))
            .SelectMany(value => value.Data.Enemies.SelectMany(enemy => enemy.Attacks.SelectMany(binding => value.Data.Attacks.Where(attack => attack.Id == binding.AttackId).Select(attack => (value.Formation, Enemy: enemy, Attack: attack)))))
            .First(value => value.Attack.Id >= 0x100 && value.Attack.Data.DamageCalculation == 0x22 && value.Attack.Data.Power > 0 && value.Attack.Data.CastingCost is > 0 and <= 30 && Ff7BattleDamageAction.FromAttack(value.Attack.Data) is { NativePhysicalCategory: false, HasStatusOperation: false } && value.Attack.Data.AdditionalEffect == byte.MaxValue && (value.Attack.Data.TargetFlags & 4) == 0);
        Fixture fixture = new(selected.Formation, progress, kernel, modifiers);
        int source = selected.Enemy.FormationSlot + 4;
        Ff7BattleSnapshot setup = fixture.Ready(source, Triple);
        setup = setup with
        {
            Actors = setup.Actors.Select(actor => actor with { AwaitingTurn = false, Gauge = 0, Actor = actor.Actor with { Statuses = actor.Actor.Slot == source ? 0u : actor.Actor.Statuses } }).ToArray(),
            Queue = Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueueState.Create(), new(2, source, 0x20, (ushort)selected.Attack.Slot, 1, selected.Attack.Id))
        };
        Ff7BattleSession battle = fixture.Open(setup);
        List<Ff7BattleHit> hits = fixture.Complete(ref battle);
        Check(hits.Count == 3 && hits.All(hit => hit.Source == source && hit.Target == 0 && hit.AttackId == selected.Attack.Id), "An original nonphysical enemy Magic command must repeat from its caster's Triple owner.");
        Check(battle.Actors.Single(actor => actor.Actor.Slot == source).Actor.MagicPoints == 100 - 3 * selected.Attack.Data.CastingCost, "Original enemy Magic must pay its original MP cost once per actual cast.");
        Ff7BattleQueuedCommand originalCommand = new(2, source, 0x20, (ushort)selected.Attack.Slot, 1, selected.Attack.Id);
        Ff7BattleSnapshot duplicates = setup with
        {
            Actors = setup.Actors.Select(actor => actor.Actor.Slot == source ? actor with { Actor = actor.Actor with { MagicPoints = 300, MaxMagicPoints = 300 } } : actor).ToArray(),
            Queue = Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueueState.Create(), originalCommand), originalCommand)
        };
        Ff7BattleSession twice = fixture.Open(duplicates);
        HashSet<long> occurrences = [];
        List<Ff7BattleHit> repeated = fixture.Complete(ref twice, pendingSource: source, invocationIds: occurrences);
        Check(repeated.Count == 6 && repeated.All(hit => hit.Source == source && hit.Target == 0 && hit.AttackId == selected.Attack.Id), "Two identical queued original enemy commands must each own their three Triple contacts.");
        Check(occurrences.Count == 2 && twice.Capture().NextMagicSequenceId == 2, "Identical original commands must receive two distinct deterministic sequence occurrence IDs.");
        Check(twice.Actors.Single(actor => actor.Actor.Slot == source).Actor.MagicPoints == 300 - 6 * selected.Attack.Data.CastingCost, "Both original occurrences must debit MP once for each of their six actual casts.");
        Ff7BattleSession canceled = fixture.Open(duplicates);
        List<Ff7BattleHit> remaining = fixture.Complete(ref canceled, snapshot => snapshot with { GuestOriginStatuses = [] }, pendingSource: source);
        Check(remaining.Count == 2 && remaining.All(hit => hit.Source == source && hit.AttackId == selected.Attack.Id), "Losing Triple must cancel the active occurrence's remaining casts while preserving a separately queued identical original command.");
        Check(canceled.Actors.Single(actor => actor.Actor.Slot == source).Actor.MagicPoints == 300 - 2 * selected.Attack.Data.CastingCost, "A separately queued original command must pay once after the earlier repetition owner is canceled.");
    }

    private sealed class Fixture(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers)
    {
        public Ff7BattleSession Open(Ff7BattleSnapshot? snapshot = null)
        {
            return new(formation, progress, kernel, modifiers, 1, snapshot: snapshot);
        }

        public Ff7BattleSnapshot Ready(int originSlot, uint statuses)
        {
            Ff7BattleSnapshot entry = Open().Capture();
            return entry with
            {
                Actors = entry.Actors.Select(actor => actor with
                {
                    AwaitingTurn = actor.Actor.Slot == 0,
                    Gauge = actor.Actor.Slot == 0 ? ushort.MaxValue : (ushort)0,
                    Actor = actor.Actor with { HitPoints = 30000, MaxHitPoints = 30000, MagicPoints = 100, MaxMagicPoints = 100, Statuses = actor.Actor.Slot == 0 ? 0u : 0x400u }
                }).ToArray(),
                GuestOriginStatuses = statuses == 0 ? [] : [new(originSlot, statuses)]
            };
        }

        public List<Ff7BattleHit> Complete(ref Ff7BattleSession battle, Func<Ff7BattleSnapshot, Ff7BattleSnapshot>? afterFirstContact = null, int? pendingSource = null, ISet<long>? invocationIds = null)
        {
            Ff7BattleSession cold = Open(RoundTrip(battle.Capture()));
            List<Ff7BattleHit> hits = [];
            long? lastTick = battle.LastHit?.Tick;
            for (int frame = 0; frame < 240; frame++)
            {
                battle.Advance(waitForCommand: true);
                cold = Open(RoundTrip(cold.Capture()));
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A queued native Magic sequence must preserve source, chosen targets, costs, RNG and next cast after every JSON callback.");
                if (battle.LastHit is { } hit && hit.Tick != lastTick)
                {
                    hits.Add(hit);
                    lastTick = hit.Tick;
                    if (hits.Count == 1 && afterFirstContact is not null)
                    {
                        Ff7BattleSnapshot interrupted = afterFirstContact(battle.Capture());
                        battle = Open(RoundTrip(interrupted));
                        cold = Open(RoundTrip(interrupted));
                    }
                }
                Ff7BattleSnapshot current = battle.Capture();
                foreach (Ff7BattleGuestMagicSequence sequence in current.GuestMagicSequences)
                {
                    if (sequence.InvocationId is long id)
                    {
                        invocationIds?.Add(id);
                    }
                }
                bool sourcePending = pendingSource is int source && current.Queue.Ready.OfType<Ff7BattleQueuedCommand>().Concat(current.Queue.Slots.OfType<Ff7BattleQueueEntry>().Select(entry => entry.Command)).Any(command => command.ActorSlot == source && command.Priority >= 2 && command.CommandId is 2 or 0x20);
                if (hits.Count > 0 && current.Action is null && current.GuestMagicSequences.IsEmpty && !sourcePending)
                {
                    return hits;
                }
            }
            Ff7BattleSnapshot remaining = battle.Capture();
            throw new InvalidOperationException($"The bounded native Magic sequence did not complete or cancel: hits={hits.Count}, outcome={remaining.Outcome}, sourceHP={remaining.Actors[0].Actor.HitPoints}, sequences={remaining.GuestMagicSequences.Length}, action={remaining.Action?.Command.CommandId}, next={remaining.GuestMagicSequences.FirstOrDefault()?.Next}, queued={remaining.Queue.Slots.Count(entry => entry is not null)}.");
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
