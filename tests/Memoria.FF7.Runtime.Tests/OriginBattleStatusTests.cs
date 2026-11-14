using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class OriginBattleStatusTests
{
    public static void Run()
    {
        const uint supported = Ff7GuestBattleStatuses.SupportedOwnedOriginBattle;
        Ff7BattleDamageActor stats = new(20, 80, 80, 10, 10, 0, 0, 20, 20);
        Ff7BattleActionVitals living = new(100, 200, 0, 0, 0);
        Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));
        Ff7BattleDamageAction operation = new(0x22, 0, 255, HasStatusOperation: true, AddsStatuses: true) { AlwaysHits = true, GuestOriginBattleStatuses = supported, GuestOriginStatusAttack = 200 };
        Ff7BattleActionReceipt Hit(Ff7BattleDamageAction action, uint present = 0, byte[]? resistance = null, uint hostStatuses = 0)
            => Ff7NativeBattleActionResolver.ResolveGuestContact(stats, living, stats, living with { Statuses = hostStatuses }, action, null, Random(), originStatusResistances: resistance, originBattleStatuses: present);
        Ff7BattleActionReceipt applied = Hit(operation);
        Check(applied.AddedOriginBattleStatuses == supported && applied.Target.Statuses == 0 && applied.Damage.Damage == 0, "Approved source statuses must publish independently without borrowing native host bits.");
        Check(Hit(operation, supported).AddedOriginBattleStatuses == 0, "Reapplication must preserve the existing status and its timer without another status roll.");
        byte[] resistances = Enumerable.Repeat((byte)100, 40).ToArray();
        foreach (int index in new[] { 16, 21, 25, 26 })
        {
            resistances[index] = 200;
        }
        Check(Hit(operation, resistance: resistances).AddedOriginBattleStatuses == 0 && Hit(operation with { GuestOriginStatusAttack = 255 }, resistance: resistances).AddedOriginBattleStatuses == supported, "Aura, Float and multicast must address their separate original resistance rows and guaranteed bypass.");
        foreach (uint blocked in new[] { Ff7NativeBattleActionResolver.Death, 0x4000u, Ff7NativeBattleStatuses.Resist, Ff7NativeBattleStatuses.Peerless })
        {
            Check(Hit(operation, hostStatuses: blocked).AddedOriginBattleStatuses == 0, "General host knockout and immunity must reject new source status owners.");
        }
        Ff7BattleActionReceipt exclusive = Hit(operation with { GuestOriginBattleStatuses = Ff7GuestBattleStatuses.OriginAura }, Ff7GuestBattleStatuses.OriginCurse);
        Check(exclusive.AddedOriginBattleStatuses == Ff7GuestBattleStatuses.OriginAura && exclusive.RemovedOriginBattleStatuses == Ff7GuestBattleStatuses.OriginCurse, "Aura application must replace the previous source Curse group without aliasing either status.");
        Ff7BattleActionReceipt cured = Hit(operation with { Healing = true }, supported | 0x10000);
        Check(cured.RemovedOriginBattleStatuses == supported && cured.AddedOriginBattleStatuses == 0, "A source cure removes only the explicit approved status mask and preserves unrelated source state.");
        Ff7BattleAttack dispel = new(255, 0, 0, 0, 0, 0, 0, 0, 0, 0x22, 0, 0, 127, 255, 0, 0x30178700, 0, ushort.MaxValue);
        Ff7BattleDamageAction native = Ff7BattleDamageAction.FromAttack(dispel) with { AlwaysHits = true };
        Ff7BattleActionReceipt Native(Ff7BattleAttack record, uint statuses = 0, bool item = false)
            => Ff7NativeBattleActionResolver.Resolve(stats, living, stats, living with { Statuses = statuses }, native with { IsItem = item }, record, Random(), originBattleStatuses: supported | 0x10000);
        Check(Native(dispel).RemovedOriginBattleStatuses == supported, "Native beneficial Dispel must remove approved applied source statuses, preserving unknown source bits.");
        Check(Native(dispel with { StatusMask = 0x170000 }).RemovedOriginBattleStatuses == 0 && Native(dispel with { StatusMask = 8 }).RemovedOriginBattleStatuses == 0, "Debarrier and harmful-status cures must not acquire the broader beneficial source dispel operation.");
        Check(Native(dispel, Ff7NativeBattleStatuses.Resist).RemovedOriginBattleStatuses == 0 && Native(dispel, Ff7NativeBattleStatuses.Peerless).RemovedOriginBattleStatuses == 0, "The pre-contact native immunity owner must also protect source statuses during a native dispel.");
        Check(Native(dispel, Ff7NativeBattleStatuses.Resist, true).RemovedOriginBattleStatuses == supported && Native(dispel, Ff7NativeBattleStatuses.Peerless, true).RemovedOriginBattleStatuses == 0, "Item status removal bypasses Resist but retains native Peerless immunity for both status owners.");
        Check(Native(dispel with { SpecialFlags = (ushort)(ushort.MaxValue & ~0x80) }, Ff7NativeBattleStatuses.Peerless).RemovedOriginBattleStatuses == supported, "The original ignore-immunity flag must remove the source beneficial category through the same operation.");
        Check(Ff7NativeBattleStatuses.RemovesBeneficialStatuses(dispel) && !Ff7NativeBattleStatuses.RemovesBeneficialStatuses(dispel with { StatusChange = 63 }), "The beneficial removal category requires cure mode; additions with the same mask are not a dispel.");
        Check(RoundTrip(applied).AddedOriginBattleStatuses == supported && RoundTrip(cured).RemovedOriginBattleStatuses == supported, "Source application and cure receipts must survive real JSON serialization.");
        InstalledClock();
        Console.WriteLine("PASS approved origin status ownership, resistance, reapplication, cure, exclusivity, native Dispel and JSON clocks.");
    }

    private static void InstalledClock()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed origin status clocks: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, modifiers, 1, snapshot: snapshot);
        ReadOnlyMemory<byte> magic = kernel.Single(section => section.Type == 1).Data;
        Ff7BattleAttack dispel = Enumerable.Range(0, magic.Length / Ff7BattleAttack.RecordSize).Select(index => Ff7BattleAttack.Read(magic.Span.Slice(index * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize))).First(record => record.StatusChange >> 6 == 1 && record.StatusMask == 0x30178700);
        Ff7BattleDamageActor stats = new(20, 80, 80, 10, 10, 0, 0, 20, 20);
        Ff7BattleActionVitals vitals = new(100, 100, 100, 100, 0);
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(dispel);
        Ff7BattleRng random = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle());
        Ff7BattleActionReceipt native = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, action, dispel, random, originBattleStatuses: Ff7GuestBattleStatuses.SupportedOwnedOriginBattle);
        Check(!native.Damage.Missed && native.RemovedOriginBattleStatuses == Ff7GuestBattleStatuses.SupportedOwnedOriginBattle, "The installed original native beneficial dispel record must produce the semantic source cure transaction.");
        Ff7BattleSnapshot initial = Create().Capture();
        int[] slots = [0, initial.Actors.First(actor => actor.Actor.Slot >= 4).Actor.Slot];
        uint all = Ff7GuestBattleStatuses.SupportedOwnedOriginBattle;
        Ff7BattleSnapshot seeded = initial with { GuestOriginStatuses = slots.Select(slot => new Ff7BattleGuestOriginStatus(slot, all)).ToArray() };
        Ff7BattleSession live = Create(RoundTrip(seeded));
        live.BindOriginStatusDurations(Enumerable.Repeat((short)4, 14).ToArray());
        Check(slots.All(slot => live.GetOriginBattleStatuses(slot) == all), "Native party and enemy occurrences must own source statuses without requiring a guest binding.");
        Ff7BattleSnapshot independent = live.Capture();
        independent.GuestOriginStatuses![0].Timers![8] = 999;
        Check(live.Capture().GuestOriginStatuses![0].Timers![8] == 4, "Captured source timer arrays must not mutate the live encounter.");
        for (int tick = 0; tick < 12; tick++)
        {
            Ff7BattleSession restored = Create(RoundTrip(live.Capture()));
            restored.BindOriginStatusDurations(Enumerable.Repeat((short)4, 14).ToArray());
            live.Advance();
            restored.Advance();
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(restored.Capture()), "A saved source timer must continue identically through native host callbacks.");
        }
        Check(slots.All(slot => live.GetOriginBattleStatuses(slot) == Ff7GuestBattleStatuses.OriginMulticast), "Aura and Float expire through their source clock while the untimed multicast statuses remain owned.");
        Ff7BattleSession cadence = Create(RoundTrip(seeded));
        cadence.BindOriginStatusDurations(Enumerable.Repeat((short)1000, 14).ToArray());
        MethodInfo advance = typeof(Ff7BattleSession).GetMethod("AdvanceGauges", BindingFlags.Instance | BindingFlags.NonPublic)!;
        bool Step(Ff7BattleSession session) => (bool)advance.Invoke(session, [null])!;
        for (int sample = 0; sample < 120; sample++)
        {
            Ff7BattleSession cold = Create(RoundTrip(cadence.Capture()));
            cold.BindOriginStatusDurations(Enumerable.Repeat((short)1000, 14).ToArray());
            Check(Step(cadence), "An admitted virtual time step with pending source timers must not stall between source callbacks.");
            Step(cold);
            Check(JsonSerializer.Serialize(cadence.Capture()) == JsonSerializer.Serialize(cold.Capture()), "JSON continuation must preserve the exact fractional source callback phase.");
        }
        Check(cadence.Capture().OriginStatusClockRemainder == 0 && cadence.Capture().Tick == seeded.Tick && cadence.Capture().GuestOriginStatuses!.All(row => row.Timers![8] == 938 && row.Timers[13] == 938), "One hundred twenty host virtual samples must execute exactly thirty-one normal source callbacks without depending on presentation Tick.");
        JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(cadence.Capture()))!.AsObject();
        old.Remove(nameof(Ff7BattleSnapshot.OriginStatusClockRemainder));
        Ff7BattleSnapshot oldSnapshot = old.Deserialize<Ff7BattleSnapshot>()!;
        Ff7BattleSession compatible = Create(oldSnapshot);
        Check(compatible.Capture().OriginStatusClockRemainder == 0 && JsonSerializer.Serialize(compatible.Capture().GuestOriginStatuses) == JsonSerializer.Serialize(cadence.Capture().GuestOriginStatuses) && JsonSerializer.Serialize(compatible.Capture().Random) == JsonSerializer.Serialize(cadence.Capture().Random), "An absent new fractional phase defaults to zero while retaining every timer, status and RNG owner.");
        foreach (uint held in new[] { Ff7NativeBattleActionResolver.Sleep, 0x400u })
        {
            Ff7BattleSnapshot paused = seeded with { Actors = seeded.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = held }, Timers = Ff7NativeBattleStatusClock.Apply(default, 0, held) } : actor).ToArray() };
            Ff7BattleSession stopped = Create(RoundTrip(paused));
            stopped.BindOriginStatusDurations(Enumerable.Repeat((short)1000, 14).ToArray());
            for (int sample = 0; sample < 120; sample++)
            {
                Step(stopped);
            }
            Check(stopped.Capture().GuestOriginStatuses!.Single(row => row.HostSlot == 0).Timers![8] == 1000 && stopped.Capture().GuestOriginStatuses!.Single(row => row.HostSlot == 0).Timers![13] == 1000, "Sleep and Stop must hold positive source Aura and Float timers across every scheduled callback.");
        }
        Ff7BattleQueuedCommand pending = new(5, 0, (byte)Ff7BattleCommand.Attack, 0, checked((ushort)(1 << slots[1])));
        Ff7BattleSession actionHeld = Create(RoundTrip(seeded with { Action = new(pending, seeded.Tick, 0) }));
        actionHeld.BindOriginStatusDurations(Enumerable.Repeat((short)1000, 14).ToArray());
        for (int sample = 0; sample < 5; sample++)
        {
            Step(actionHeld);
        }
        Check(actionHeld.Capture().OriginStatusClockRemainder == 35 && actionHeld.Capture().GuestOriginStatuses!.All(row => row.Timers![8] == 1000 && row.Timers[13] == 1000), "An active action must hold source timers while the shared callback phase continues.");
        Ff7BattleSession released = Create(RoundTrip(actionHeld.Capture() with { Action = null }));
        released.BindOriginStatusDurations(Enumerable.Repeat((short)1000, 14).ToArray());
        Step(released);
        Step(released);
        Check(released.Capture().GuestOriginStatuses!.All(row => row.Timers![8] == 1000), "Releasing an action pause must not replay skipped source timer callbacks.");
        Step(released);
        Check(released.Capture().GuestOriginStatuses!.All(row => row.Timers![8] == 998 && row.Timers[13] == 998), "The next scheduled source callback must resume with exactly one decrement.");
        Ff7BattleSnapshot invalid = live.Capture() with { GuestOriginStatuses = [new(3, all)] };
        try
        {
            Create(RoundTrip(invalid));
            throw new InvalidOperationException("An origin status row without an actor occurrence was admitted.");
        }
        catch (InvalidDataException)
        {
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
