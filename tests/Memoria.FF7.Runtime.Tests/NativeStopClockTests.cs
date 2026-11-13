using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStopClockTests
{
    private const uint Stop = 0x400;

    public static void Run()
    {
        GlobalClock();
        AdmissionAndRemoval();
        InstalledContinuation();
        Console.WriteLine("PASS native Stop global clock, duration, status transaction and JSON continuation.");
    }

    private static void GlobalClock()
    {
        Ff7BattleStatusTimers missing = JsonSerializer.Deserialize<Ff7BattleStatusTimers>("{\"Remainder\":8100,\"Sleep\":50,\"Poison\":3}");
        Ff7BattleStatusTimers restored = Ff7NativeBattleStatusClock.Apply(missing, Stop, Stop);
        Check(restored.Stop == 30 && restored.Remainder == 8100 && restored.Sleep == 0 && restored.Poison == 0, "Older snapshots must restore the missing Stop duration without resetting the actor clock phase.");
        const uint others = Ff7NativeBattleActionResolver.Sleep | Ff7NativeBattleActionResolver.Poison | Ff7NativeBattleStatuses.Regen | Ff7NativeBattleStatuses.DeathSentence;
        Ff7BattleStatusTimers timers = Ff7NativeBattleStatusClock.Apply(default, 0, Stop | others) with { Remainder = 8100, FixedRemainder = 4050, HitPointRemainder = 32760 };
        foreach (byte speed in new byte[] { 0, 128, 255 })
        {
            Ff7BattleGlobalStatusClock phase = default;
            int updates = 0;
            uint statuses = Stop | others;
            Ff7BattleStatusTimers current = timers;
            while ((statuses & Stop) != 0)
            {
                bool tick = Ff7NativeBattleStatusClock.AdvanceGlobalClock(ref phase, speed);
                Ff7BattleStatusTick next = Ff7NativeBattleStatusClock.Advance(current, statuses, speed, maximumHitPoints: 320, globalTick: tick);
                Check(next.Timers.Remainder == 8100 && next.Timers.FixedRemainder == 4050 && next.Timers.HitPointRemainder == 32760 && next.Timers.Sleep == 100 && next.Timers.Poison == 10 && next.Timers.Regen == 127 && next.Timers.DeathSentence == 60 && next.HitPointDelta == 0 && !next.DeathSentenceDue && !next.PoisonDue, "Stop must freeze actor clocks, healing and periodic callbacks through the final global expiry sample.");
                Check(next == Ff7NativeBattleStatusClock.Advance(RoundTrip(current), statuses, speed, maximumHitPoints: 320, globalTick: tick), "A real JSON timer round-trip must preserve Stop continuation.");
                current = next.Timers;
                statuses = next.Statuses;
                updates++;
                Check(updates <= 5000, "The independent global clock must expire Stop even when the actor is otherwise completely frozen.");
            }
            Check(updates == 2 * ((15 * 8192 + Ff7NativeAtb.BaseRate(speed) - 1) / Ff7NativeAtb.BaseRate(speed)) && current.Stop == 0 && statuses == others, "Stop must last exactly thirty global Q13 units across the native alternating buffers at the selected battle speed.");
        }
        foreach (uint modifier in new uint[] { 0, 0x100, 0x200, 0x300, Ff7NativeBattleStatuses.Imprisoned })
        {
            Ff7BattleStatusTick next = Ff7NativeBattleStatusClock.Advance(timers with { Stop = 1 }, Stop | others | modifier, 128, globalTick: true);
            Check((next.Statuses & Stop) == 0 && next.Timers.Stop == 0 && next.Timers.Remainder == 8100, "Haste, Slow and capture clock projection must not change or own Stop's global duration.");
        }
        Ff7BattleStatusTimers partial = timers with { Stop = 7 };
        Check(Ff7NativeBattleStatusClock.Apply(partial, Stop | others, Stop | others).Stop == 7, "Reapplying an already present Stop status must not restart its counter.");
        Ff7BattleStatusTimers cured = Ff7NativeBattleStatusClock.Apply(partial, Stop | others, others);
        Check(cured.Stop == 0 && Ff7NativeBattleStatusClock.Apply(cured, others, Stop | others).Stop == 30, "Curing Stop must clear its counter and a later fresh application must initialize thirty units.");
        Check(Ff7NativeBattleStatusClock.Apply(partial, Stop | others, Stop | others | 1).Stop == 0, "KO must clear the Stop counter rather than let it continue on a dead actor.");
        ExpectInvalid(() => Ff7NativeBattleStatusClock.Advance(timers with { Stop = 31 }, Stop, 128));
        ExpectInvalid(() =>
        {
            Ff7BattleGlobalStatusClock phase = new(8192, 0, 0);
            Ff7NativeBattleStatusClock.AdvanceGlobalClock(ref phase, 128);
        });
        Ff7BattleGlobalStatusClock uneven = new(8150, 1000, 0);
        Check(Ff7NativeBattleStatusClock.AdvanceGlobalClock(ref uneven, 128) && uneven == new Ff7BattleGlobalStatusClock((ushort)(8150 + Ff7NativeAtb.BaseRate(128) - 8192), 1000, 1), "A global crossing must retain its own buffer's fractional phase and select the opposite producer buffer.");
        Ff7BattleGlobalStatusClock saved = RoundTrip(uneven);
        Check(!Ff7NativeBattleStatusClock.AdvanceGlobalClock(ref saved, 128) && saved.FirstRemainder == uneven.FirstRemainder && saved.SecondRemainder == 1000 + Ff7NativeAtb.BaseRate(128) && saved.Buffer == 0, "Cold continuation must retain the other buffer's independent phase instead of merging both fractions.");
    }

    private static void AdmissionAndRemoval()
    {
        Ff7BattleDamageActor stats = new(10, 20, 25, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals source = new(486, 486, 0, 0, 0);
        Ff7BattleDamageAction action = new(0x22, 8, 255);
        Ff7BattleAttack record = Ff7GuestBattleStatuses.ToHostStatusRecord(0, 8, 255)!;
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleActionReceipt Hit(Ff7BattleActionVitals target, Ff7BattleAttack status, byte[]? defenses = null) => Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action, status, Random(), originStatusResistances: defenses);
        Ff7BattleActionReceipt applied = Hit(source, record);
        Check((applied.AddedStatuses & Stop) != 0 && Ff7NativeBattleStatusClock.Apply(default, 0, applied.Target.Statuses).Stop == 30, "A shared source Stop producer must initialize the host timer through the ordinary receipt.");
        Check((Hit(source with { StatusImmunities = Stop }, record).Target.Statuses & Stop) == 0 && (Hit(source with { Statuses = Ff7NativeBattleStatuses.Resist }, record).Target.Statuses & Stop) == 0, "Host immunity and Resist must reject Stop before any duration is created.");
        byte[] defenses = Enumerable.Repeat((byte)100, 40).ToArray();
        defenses[11] = 200;
        Check((Hit(source, record, defenses).Target.Statuses & Stop) == 0, "An incoming Stop operation must honor the source actor's original Stop defense row.");
        Ff7BattleActionReceipt cure = Hit(source with { Statuses = Stop }, record with { StatusChange = 0x7F });
        Check((cure.RemovedStatuses & Stop) != 0 && (cure.Target.Statuses & Stop) == 0, "The shared ordinary cure transaction must remove Stop.");
        Ff7BattleActionReceipt killed = Ff7NativeBattleActionResolver.ApplyPreparedHpDamage(source, source with { Statuses = Stop }, new(false, false, 486));
        Check(killed.Target.Statuses == 1 && (killed.RemovedStatuses & Stop) != 0, "KO must clear Stop in the shared damage transaction.");
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed Stop session: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, modifiers, 1, snapshot: snapshot);
        Ff7BattleSession seeded = Create();
        seeded.BindGuestVitals(new(Guid.NewGuid(), new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17) { OriginBattleStatuses = 8 });
        Ff7BattleSnapshot initial = seeded.Capture();
        Ff7BattleSnapshot setup = initial with { GlobalStatusClock = new(8150, 1000, 0), Actors = initial.Actors.Select(actor => actor with { Gauge = 0, AwaitingTurn = false, Actor = actor.Actor with { Statuses = Stop }, Timers = new(7100, 0, 0) { Stop = 1, FixedRemainder = 3000 } }).ToArray() };
        Ff7BattleSession live = Create(RoundTrip(setup));
        Ff7BattleSession cold = Create(RoundTrip(live.Capture()));
        ushort pausedGauge = live.Capture().Actors[0].Gauge;
        live.Advance(waitForCommand: true);
        Check(live.Capture().GlobalStatusClock == setup.GlobalStatusClock && live.Capture().Actors.All(actor => actor.Timers.Stop == 1 && actor.Gauge == pausedGauge), "Paused battle time must preserve the global phase and every Stop counter.");
        live = Create(RoundTrip(setup));
        for (int update = 0; update < 4; update++)
        {
            live.Advance();
            cold.Advance();
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "An installed stopped session must continue identically through expiry after JSON restoration.");
        }
        Check(live.Capture().Actors.All(actor => (actor.Actor.Statuses & Stop) == 0 && actor.Timers.Stop == 0 && actor.Gauge > 0) && (live.GetGuestOriginBattleStatuses(0) & 8) == 0, "The global callback must release both native and mapped source actors and resume ATB without replaying an action.");
        JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(setup))!.AsObject();
        old.Remove("GlobalStatusClock");
        foreach (JsonNode? actor in old["Actors"]!.AsArray())
        {
            actor!["Timers"]!.AsObject().Remove("Stop");
        }
        Ff7BattleSession legacy = Create(old.Deserialize<Ff7BattleSnapshot>()!);
        Check(JsonSerializer.Serialize(legacy.Capture().Random) == JsonSerializer.Serialize(setup.Random), "Restoring absent clock fields must preserve the saved RNG state.");
        // The first presentation refreshes the ordinary escape roll even when the escape buttons are released.
        legacy.Advance(waitForCommand: true);
        string randomBefore = JsonSerializer.Serialize(legacy.Capture().Random);
        legacy.Advance();
        Check(legacy.Capture().Actors.All(actor => actor.Timers.Stop == 30 && actor.Gauge == 0 && actor.Timers.Remainder == 7100), "Absent new fields must restore the Stop duration while preserving actor progress.");
        Check(legacy.Capture().GlobalStatusClock == new Ff7BattleGlobalStatusClock((ushort)Ff7NativeAtb.BaseRate(setup.NativeBattleSpeed!.Value), 0, 1), "An absent global clock must resume from both zero phases and selector zero.");
        Check(JsonSerializer.Serialize(legacy.Capture().Random) == randomBefore, "The Stop clock sample must not consume RNG.");
        ExpectInvalid(() => Create(setup with { GlobalStatusClock = new(8192, 0, 0) }));
        ExpectInvalid(() => Create(setup with { GlobalStatusClock = new(0, 8192, 0) }));
        ExpectInvalid(() => Create(setup with { GlobalStatusClock = new(0, 0, 2) }));
        Ff7BattleSnapshot fullStop = setup with { NativeBattleSpeed = 255, GlobalStatusClock = default(Ff7BattleGlobalStatusClock), Actors = setup.Actors.Select(actor => actor with { Gauge = 65500, Timers = actor.Timers with { Stop = 30 } }).ToArray() };
        Ff7BattleSession allStopped = Create(RoundTrip(fullStop));
        Ff7BattleTurnAdvanceResult result = allStopped.AdvanceTurn();
        Check(result.AdvancedSteps >= 4551 && result.Boundary is Ff7BattleTurnAdvanceBoundary.PlayerReady or Ff7BattleTurnAdvanceBoundary.QueuedExecution && allStopped.Capture().Actors.Any(actor => (actor.Actor.Statuses & Stop) == 0), "The ordinary bounded turn advance must cross a full all-stopped duration at the slowest native battle speed and reach readiness.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A corrupt native status clock was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
