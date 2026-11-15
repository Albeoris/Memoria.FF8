using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleTheftTests
{
    public static void Run()
    {
        CheckResolver();
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original Bandit theft: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2], Gil = 1000, Inventory = [new(0, 2, 4), new(1, 1, 17)] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleSystemMessages messages = Ff7BattleSystemMessages.Load(installation);
        Fixture? selected = null;
        foreach (int id in new[] { 516, 517, 518, 519 })
        {
            Fixture fixture = new(formations.Single(formation => formation.FormationId == id), progress, kernel, executable.NativeMateriaStatModifiers, messages);
            Check(fixture.Start.Actors.Any(actor => actor.Actor.Slot >= 4), "An affected original encounter did not initialize its native enemies.");
            if (fixture.Start.Actors.Any(actor => actor.Identity == 108))
            {
                Ff7BattleSnapshot contact = Contact(fixture, fixture.Start, 0x01EE, 0);
                Check(contact.LastHit?.AttackId == 0x01EE, "An affected encounter did not execute its original Bandit turn.");
                selected ??= fixture;
            }
            else
            {
                Ff7BattleSession live = fixture.Restore(fixture.Start);
                Ff7BattleSession cold = fixture.Restore(fixture.Start);
                for (int tick = 0; tick < 16; tick++)
                {
                    AdvancePair(live, cold);
                }
            }
        }
        Check(selected is not null, "The original affected encounter directory contains no Bandit.");
        CheckGil(selected!);
        CheckQueuedItem(selected!);
        CheckActualMug(selected!);
        CheckRetirement(selected!, withdrawn: false);
        CheckRetirement(selected!, withdrawn: true);
        CheckInterruptedEscape(selected!);
    }

    private static void CheckResolver()
    {
        Ff7BattleDamageActor stats = new(30, 30, 30, 30, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals vitals = new(1000, 1000, 100, 100, 0);
        foreach (byte effect in new byte[] { 0x06, 0x07 })
        {
            Ff7BattleAttack record = new(255, 255, 0, 0, 65535, 65535, 65535, 3, 255, effect == 6 ? (byte)0 : (byte)0x11, 16, 255, 255, effect, 255, uint.MaxValue, 0, 65535);
            Ff7BattleRng actualRandom = Random();
            Ff7BattleRng expectedRandom = Random();
            Ff7BattleActionReceipt actual = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, actualRandom);
            Ff7BattleAttack plain = record with { AdditionalEffect = byte.MaxValue };
            Ff7BattleActionReceipt expected = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(plain), plain, expectedRandom);
            Check(actual.TheftEffect == effect && (actual with { TheftEffect = null }) == expected, "A theft intent changed the ordinary native damage/status transaction.");
            Check(JsonSerializer.Serialize(actualRandom.Capture()) == JsonSerializer.Serialize(expectedRandom.Capture()), "Publishing a phase4 theft intent consumed an extra random draw.");
            Ff7BattleAttack inaccurate = record with { Accuracy = 0, DamageCalculation = 0x22 };
            Ff7BattleActionReceipt miss = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats with { MagicEvade = 100 }, vitals, Ff7BattleDamageAction.FromAttack(inaccurate), inaccurate, Random());
            Check(miss.Damage.Missed && miss.TheftEffect is null, "A natural early miss published a phase4 theft intent.");
            Ff7BattleAttack physical = record with { DamageCalculation = 0x11, Elements = 8, SpecialFlags = 65531 };
            Ff7BattleActionReceipt late = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats with { NativeConditions = 0x100 }, vitals, Ff7BattleDamageAction.FromAttack(physical), physical, Random());
            Check(late.Damage.Missed && late.TheftEffect == effect, "Late native condition immunity erased an already published theft intent.");
        }
        Ff7BattleRng Random()
        {
            return new(new byte[3988], new(new byte[8]));
        }
    }

    private static void CheckGil(Fixture fixture)
    {
        foreach (var (held, gil, expectedHeld, expectedGil) in new[] { (0, 250u, 250, 0u), (64935, 1000u, 64935, 1000u), (64934, 1000u, 65534, 400u) })
        {
            Ff7BattleSnapshot start = WithGil(fixture.Start, gil);
            start = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot == fixture.Bandit ? actor with { StolenAssets = new((ushort)held) } : actor).ToArray() };
            Ff7BattleSnapshot contact = Contact(fixture, start, 0x01EE, 0);
            Check(contact.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets!.Gil == expectedHeld && Gil(contact) == expectedGil, "Hold-up lost native level*20, Gil clamping, physical source ownership or the strict65535 accumulation cap.");
            Check(contact.Actors.Single(actor => actor.Actor.Slot == 0).StolenAssets is null, "Hold-up assigned the stolen Gil to its target instead of its source.");
        }
        Ff7BattleSnapshot enemyTarget = Contact(fixture, fixture.Start, 0x01EE, fixture.Bandit);
        Check(Gil(enemyTarget) == Gil(fixture.Start) && enemyTarget.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets is null, "Hold-up stole party economy from an enemy target slot.");
    }

    private static void CheckQueuedItem(Fixture fixture)
    {
        Ff7BattleSnapshot start = fixture.Start with { Tick = 1, Escape = fixture.Start.Escape with { CachedRoll = 1 } };
        start = start with { Economy = Ff7BattleEconomy.Enqueue(start.Economy!, 0, new(0x0B, (byte)fixture.Bandit, 0)) };
        Ff7BattleSession live = fixture.Restore(start);
        Ff7BattleSession cold = fixture.Restore(start);
        Ff7BattleSession baseline = fixture.Restore(fixture.Start with { Tick = 1, Escape = start.Escape });
        AdvancePair(live, cold);
        baseline.Advance(waitForCommand: true);
        Ff7BattleSnapshot removed = live.Capture();
        Check(removed.Economy!.Inventory[17].ItemId == ushort.MaxValue && removed.Economy.Inventory[4].Quantity == 2 && removed.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets is null, "Queue0 did not select the cached-WORD slot before assigning the enemy's held item.");
        Check(Pending(removed.Economy, 1) == 1 && Pending(removed.Economy, 0) == 0 && JsonSerializer.Serialize(removed.Random) == JsonSerializer.Serialize(baseline.Capture().Random), "Native item removal reordered economy phases or consumed an extra RNG draw.");
        cold = fixture.Restore(removed);
        for (int tick = 0; tick < 8 && live.Capture().Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets is null; tick++)
        {
            AdvancePair(live, cold);
        }
        Ff7BattleSnapshot held = live.Capture();
        Check(held.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets?.Item == 1 && Pending(held.Economy!, 1) == 0 && Pending(held.Economy!, 2) == 1, "Presentation queue1 lost source-owned item assignment or its later queue2 notification.");
        Check(!HasMessage(held, fixture.Messages.FormatItemMessage(0x53, 1)), "Theft notification ran before a normal action-completion status phase.");
        Ff7BattleSnapshot completed = FinishStatus(fixture, held);
        Check(Pending(completed.Economy!, 2) == 0 && HasMessage(completed, fixture.Messages.FormatItemMessage(0x53, 1)), "A normal action-completion status phase omitted the original theft notification.");
        Check(completed.Inventory.All(item => item.ItemId != 1), "Publishing a held-item notification removed or returned the item a second time.");
    }

    private static void CheckActualMug(Fixture fixture)
    {
        Ff7BattleRng candidates = fixture.CreateRandom(fixture.Start.Random);
        Ff7BattleSnapshot prepared = fixture.Start;
        Ff7BattleSnapshot? actual = null;
        for (int candidate = 0; candidate < 64; candidate++)
        {
            prepared = fixture.Start with { Random = candidates.Capture() };
            Ff7BattleSnapshot contact = Contact(fixture, prepared, 0x01EF, 0);
            if (contact.LastHit is { Damage.Missed: false, Damage.Damage: > 0 })
            {
                actual = contact;
                break;
            }
            candidates.NextWord();
        }
        Ff7BattleSnapshot admitted = actual ?? throw new InvalidOperationException("No bounded native RNG candidate admitted an ordinary hit from the unmodified original Mug record.");
        Check(admitted.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints < prepared.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints, $"Original Mug failed to retain its ordinary physical damage: hit={JsonSerializer.Serialize(admitted.LastHit)}, target={JsonSerializer.Serialize(admitted.Actors.Single(actor => actor.Actor.Slot == 0).Actor)}.");
        Check(Pending(admitted.Economy!, 0) == 1, "Original Mug removed an item directly instead of queuing the native theft command.");
        Ff7BattleSnapshot heldStart = prepared with { Actors = prepared.Actors.Select(actor => actor.Actor.Slot == fixture.Bandit ? actor with { StolenAssets = new(Item: 0) } : actor).ToArray() };
        Ff7BattleSnapshot guarded = Contact(fixture, heldStart, 0x01EF, 0);
        Check(guarded.LastHit?.Damage == admitted.LastHit!.Damage && Pending(guarded.Economy!, 0) == 0 && guarded.Inventory.SequenceEqual(heldStart.Inventory), "An enemy already holding an item lost Mug damage or bypassed its native held-item guard.");
    }

    private static void CheckRetirement(Fixture fixture, bool withdrawn)
    {
        Ff7BattleInventorySlot[] inventory = Ff7NativeBattleInventory.Spend(fixture.Start.Economy!.Inventory, 1);
        Ff7BattleSnapshot start = fixture.Start with
        {
            Inventory = Ff7NativeBattleInventory.Project(inventory),
            Economy = fixture.Start.Economy with { Inventory = inventory },
            WithdrawnMask = withdrawn ? (ushort)(1 << fixture.Bandit) : (ushort)0,
            Actors = fixture.Start.Actors.Select(actor => actor.Actor.Slot == fixture.Bandit ? actor with { StolenAssets = new(200, 1), Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1, PreviousStatuses = actor.Actor.Statuses & ~1u } } : actor).ToArray()
        };
        Ff7BattleSnapshot retired = FinishStatus(fixture, start);
        if (withdrawn)
        {
            Check(Gil(retired) == 1000 && retired.Inventory.All(item => item.ItemId != 1) && Pending(retired.Economy!, 0) == 0, "A withdrawn enemy returned stolen assets through ordinary retirement.");
            return;
        }
        Ff7BattleSnapshot revived = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot == fixture.Bandit ? actor with { Actor = actor.Actor with { HitPoints = 1, Statuses = actor.Actor.Statuses & ~1u } } : actor).ToArray() };
        Ff7BattleSnapshot living = FinishStatus(fixture, revived);
        Check(Gil(living) == 1000 && living.Inventory.All(item => item.ItemId != 1) && living.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets == new Ff7BattleStolenAssets(200, 1), "An actor revived before status completion incorrectly returned its still-owned stolen assets.");
        Check(Gil(retired) == 1200 && retired.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets is null && Pending(retired.Economy!, 0) == 1, "Native retirement failed to clear and queue its physical stolen-asset owner exactly once.");
        Check(retired.Economy!.RegisteredReturns.All(item => item is null) && retired.Economy.AcquisitionRead == retired.Economy.AcquisitionWrite, "A newly queued item return bypassed native return registration and status-phase flushing.");
        Ff7BattleSnapshot repeated = FinishStatus(fixture, retired);
        Check(Gil(repeated) == 1200 && Pending(repeated.Economy!, 0) <= 1 && repeated.Economy!.RegisteredReturns.Count(item => item == 1) <= 1, "A repeated status phase duplicated retirement of the same actor.");
        Ff7BattleSession live = fixture.Restore(retired);
        Ff7BattleSession cold = fixture.Restore(retired);
        AdvancePair(live, cold);
        Ff7BattleSnapshot registered = live.Capture();
        Check(registered.Economy!.RegisteredReturns.Contains((ushort)1) && registered.Inventory.All(item => item.ItemId != 1), "Queue0 return did not retain its registration boundary before acquisition.");
        Ff7BattleSnapshot terminal = registered with { Outcome = Ff7BattleOutcome.Victory, TerminalFrames = 49, Action = null, Queue = Ff7BattleCommandQueueState.Create(), Actors = registered.Actors.Select(actor => actor.Actor.Slot >= 4 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray() };
        live = fixture.Restore(terminal);
        cold = fixture.Restore(terminal);
        Check(!live.ResultReady, "A terminal battle skipped the native16-pass economy drain after its old presentation wait.");
        bool ring = false;
        for (int tick = 0; tick < 256 && !live.ResultReady; tick++)
        {
            AdvancePair(live, cold);
            Ff7BattleSnapshot current = live.Capture();
            Check(current.Economy!.TerminalPasses >= 16 || !live.ResultReady, "Terminal settlement became ready before completing all native passes.");
            if (!ring && current.Economy!.AcquisitionRead != current.Economy.AcquisitionWrite)
            {
                Check(current.Inventory.All(item => item.ItemId != 1), "Flushing a registered return acquired it in the same native status phase.");
                cold = fixture.Restore(current);
                ring = true;
            }
        }
        Ff7BattleSnapshot settled = live.Capture();
        Check(ring && live.ResultReady && settled.Economy!.TerminalPasses == 16 && Gil(settled) == 1200 && settled.Inventory.Single(item => item.ItemId == 1) == new Ff7InventorySlot(1, 1, 0), "Terminal returns failed their16-pass, first-hole or once-only economy contract.");
        string before = JsonSerializer.Serialize(settled);
        for (int tick = 0; tick < 8; tick++)
        {
            AdvancePair(live, cold);
        }
        Check(before == JsonSerializer.Serialize(live.Capture()), "A completed terminal economy duplicated returned assets or continued advancing.");
    }

    private static void CheckInterruptedEscape(Fixture fixture)
    {
        Ff7BattleInventorySlot[] inventory = Ff7NativeBattleInventory.Spend(fixture.Start.Economy!.Inventory, 1);
        Ff7BattleEconomyState economy = Ff7BattleEconomy.Enqueue(fixture.Start.Economy with { Inventory = inventory }, 1, new(2, (byte)fixture.Bandit, 1));
        Ff7BattleSnapshot escaped = fixture.Start with { Outcome = Ff7BattleOutcome.Escaped, TerminalFrames = 8, Economy = economy, Inventory = Ff7NativeBattleInventory.Project(inventory), Action = null, Actors = fixture.Start.Actors.Select(actor => actor.Actor.Slot == fixture.Bandit ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1, PreviousStatuses = actor.Actor.Statuses & ~1u } } : actor).ToArray() };
        Ff7BattleSession live = fixture.Restore(escaped);
        Ff7BattleSession cold = fixture.Restore(escaped);
        for (int tick = 0; tick < 128 && !live.ResultReady; tick++)
        {
            AdvancePair(live, cold);
        }
        Ff7BattleSnapshot ended = live.Capture();
        Check(live.ResultReady && ended.Economy!.TerminalPasses == 16 && ended.Actors.Single(actor => actor.Actor.Slot == fixture.Bandit).StolenAssets?.Item == 1 && Pending(ended.Economy, 2) == 1, "Interrupted escape failed to finish queue1 while retaining the undrained native notification.");
        Check(ended.Inventory.All(item => item.ItemId != 1) && Gil(ended) == Gil(fixture.Start) && !HasMessage(ended, fixture.Messages.FormatItemMessage(0x53, 1)), "Interrupted escape invented a theft notification or returned stolen assets.");
    }

    private static Ff7BattleSnapshot Contact(Fixture fixture, Ff7BattleSnapshot start, ushort attackId, int target)
    {
        SceneBattleAttack attack = fixture.Data.Attacks.Single(value => value.Id == attackId);
        Ff7BattleSnapshot queued = start with { Queue = Ff7BattleCommandQueue.Enqueue(start.Queue, new(2, fixture.Bandit, 0x20, (ushort)attack.Slot, (ushort)(1 << target), attackId)) };
        Ff7BattleSession live = fixture.Restore(queued);
        Ff7BattleSession cold = fixture.Restore(queued);
        for (int tick = 0; tick < 1024; tick++)
        {
            AdvancePair(live, cold);
            if (live.LastHit?.AttackId == attackId)
            {
                return live.Capture();
            }
        }
        throw new InvalidOperationException($"Original theft action {attackId:X4} did not reach contact in formation{fixture.Formation.FormationId}.");
    }

    private static Ff7BattleSnapshot FinishStatus(Fixture fixture, Ff7BattleSnapshot snapshot)
    {
        Ff7BattleQueuedCommand command = new(2, 0, (byte)Ff7BattleCommand.Defend, 0, 0);
        Ff7BattleSnapshot pending = snapshot with { Action = new(command, snapshot.Tick, -1, Applied: true), Queue = Ff7BattleCommandQueueState.Create() };
        Ff7BattleSession live = fixture.Restore(pending);
        Ff7BattleSession cold = fixture.Restore(pending);
        for (int tick = 0; tick < 16 && live.Action is not null; tick++)
        {
            AdvancePair(live, cold);
        }
        Check(live.Action is null, "The isolated ordinary action-completion callback did not reach its native status phase.");
        return live.Capture();
    }

    private static void AdvancePair(Ff7BattleSession live, Ff7BattleSession cold)
    {
        live.Advance(waitForCommand: true);
        cold.Advance(waitForCommand: true);
        Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed native theft queues, actor owners, RNG or economy publication.");
    }

    private static int Pending(Ff7BattleEconomyState economy, int index)
    {
        Ff7BattleEconomyQueue queue = economy.Queues[index];
        return (queue.Write - queue.Read) & 127;
    }

    private static bool HasMessage(Ff7BattleSnapshot snapshot, string message)
    {
        return snapshot.Messages?.Pending.Any(entry => entry.Text == message) == true || snapshot.Messages?.Visible == message;
    }

    private static uint Gil(Ff7BattleSnapshot snapshot)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AiGlobalMemory!.AsSpan(0x38));
    }

    private static Ff7BattleSnapshot WithGil(Ff7BattleSnapshot snapshot, uint amount)
    {
        byte[] globals = snapshot.AiGlobalMemory!.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(globals.AsSpan(0x38), amount);
        return snapshot with { AiGlobalMemory = globals };
    }

    private sealed class Fixture
    {
        private readonly Ff7Progress progress;
        private readonly IReadOnlyList<KernelSection> kernel;
        private readonly IReadOnlyList<short> modifiers;

        public Fixture(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7BattleSystemMessages messages)
        {
            Formation = formation;
            this.progress = progress;
            this.kernel = kernel;
            this.modifiers = modifiers;
            Messages = messages;
            Data = SceneBattleData.Read(formation);
            Ff7BattleSession initial = new(formation, progress, kernel, modifiers, 1, systemMessages: messages);
            Ff7BattleSnapshot start = initial.Capture();
            Start = start with { Queue = Ff7BattleCommandQueueState.Create(), Actors = start.Actors.Select(actor => actor with { Gauge = 0, AwaitingTurn = false, Actor = actor.Actor with { Level = actor.Identity == 108 ? (byte)30 : actor.Actor.Level, PhysicalEvasion = 0, HitPoints = actor.Actor.Slot < 3 ? 9999u : actor.Actor.HitPoints, MaxHitPoints = actor.Actor.Slot < 3 ? 9999u : actor.Actor.MaxHitPoints } }).ToArray() };
        }

        public SceneFormation Formation { get; }
        public SceneBattleData Data { get; }
        public Ff7BattleSystemMessages Messages { get; }
        public Ff7BattleSnapshot Start { get; }
        public int Bandit => Start.Actors.First(actor => actor.Identity == 108).Actor.Slot;

        public Ff7BattleSession Restore(Ff7BattleSnapshot snapshot)
        {
            return new(Formation, progress, kernel, modifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!, systemMessages: Messages);
        }

        public Ff7BattleRng CreateRandom(Ff7BattleRngState state)
        {
            return new(kernel[2].Data.Span, state);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
