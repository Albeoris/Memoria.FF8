using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGuestFuryTests
{
    public static void Run()
    {
        Check((Ff7GuestBattleStatuses.SupportedHost & Ff7GuestBattleStatuses.SupportedNativeOnly) == 0, "Native-only persistent statuses must remain outside the common source projection mask.");
        Check(Ff7GuestBattleStatuses.ToOrigin(Ff7GuestBattleStatuses.SupportedNativeOnly) == 0 && Ff7GuestBattleStatuses.ToOriginBattle(Ff7GuestBattleStatuses.SupportedNativeOnly) == 0, "Host Fury/Sadness must not acquire FFVIII saved, battle or Aura aliases.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original native-only guest statuses: select original FFVII resources.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress;
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 57);
        Ff7BattleAttack peck = SceneBattleData.Read(formation).Attacks.Single(attack => attack.Id == 0x1B7).Data;
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleDamageActor stats = new(20, 30, 30, 0, 0, 0, 0, 20, 20);
        Ff7BattleActionVitals vitals = new(486, 486, 0, 0, 0);
        Ff7BattleRng nativeRandom = Random();
        Ff7BattleRng guestRandom = Random();
        Ff7BattleActionReceipt native = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(peck), peck, nativeRandom);
        Ff7BattleAttack projected = Ff7GuestBattleStatuses.ProjectIncoming(peck)!;
        Ff7BattleActionReceipt guest = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(peck), projected, guestRandom, originStatusResistances: Enumerable.Repeat((byte)200, 40).ToArray());
        Check(projected.StatusMask == Ff7NativeBattleStatuses.Fury && guest.Target.Statuses == Ff7NativeBattleStatuses.Fury && guest.Target.Hp < vitals.Hp, "Original1B7 must retain both physical damage and its host-only Fury when its recipient is a guest.");
        Check(native == guest && JsonSerializer.Serialize(nativeRandom.Capture()) == JsonSerializer.Serialize(guestRandom.Capture()), "The incoming native Fury payload must retain its original status/damage RNG draws and ignore unrelated FFVIII resistance rows.");
        Check(guest.AddedOriginBattleStatuses == 0 && guest.AddedOriginSavedStatuses == 0 && guest.RemovedOriginBattleStatuses == 0 && guest.RemovedOriginSavedStatuses == 0, "Incoming native Fury must not mutate either source-native status bank.");
        Ff7BattleActionVitals furious = vitals with { Statuses = Ff7NativeBattleStatuses.Fury };
        Ff7BattleActionVitals sad = vitals with { Statuses = Ff7NativeBattleStatuses.Sadness };
        Ff7BattleDamageAction physical = new(0x11, 16, 100);
        Check(!Contact(vitals, vitals, physical, 192).Damage.Missed && Contact(furious, vitals, physical, 192).Damage.Missed, "The existing host numerical bridge must apply guest-source Fury accuracy exactly once.");
        int normalDamage = Contact(vitals, vitals, physical with { Accuracy = 255 }, 0).Damage.Damage;
        int sadDamage = Contact(vitals, sad, physical with { Accuracy = 255 }, 0).Damage.Damage;
        int nativeSadDamage = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, sad, physical with { Accuracy = 255 }, null, Random()).Damage.Damage;
        Check(sadDamage == nativeSadDamage && sadDamage < normalDamage, "Guest-target Sadness must use the existing host defense modifier exactly once.");

        Ff7BattleActionReceipt Contact(Ff7BattleActionVitals source, Ff7BattleActionVitals target, Ff7BattleDamageAction action, byte value)
            => Ff7NativeBattleActionResolver.ResolveGuestContact(stats, source, stats, target, action, null, Random(value));

        Ff7BattleSession Create(Ff7Progress facts, Ff7BattleSnapshot? snapshot = null) => new(formation, facts, kernel, executable.NativeMateriaStatModifiers, 501, snapshot: snapshot);
        Ff7BattleGuestVitalsBinding binding = new(Guid.NewGuid(), new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17);
        foreach (uint status in new[] { Ff7NativeBattleStatuses.Fury, Ff7NativeBattleStatuses.Sadness })
        {
            Ff7BattleSession start = Create(progress);
            start.BindGuestVitals(binding);
            Ff7BattleSnapshot bound = start.Capture();
            Ff7BattleSnapshot produced = bound with
            {
                Actors = bound.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = status } } : actor).ToArray(),
                GuestOriginStatuses = null,
                GuestProjectedBattleMask = null
            };
            Ff7BattleSession warm = Create(progress, Cold(produced));
            Ff7BattleSession cold = Create(progress, Cold(produced));
            warm.BindGuestVitals(binding);
            cold.BindGuestVitals(binding);
            warm.ReprojectGuestVitals(new(486, 6, 14, 24, 17));
            cold.ReprojectGuestVitals(new(486, 6, 14, 24, 17));
            warm.Advance(waitForCommand: true);
            cold.Advance(waitForCommand: true);
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()) && (warm.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7GuestBattleStatuses.SupportedNativeOnly) == status, "Saved native-only guest facts must survive missing optional projection state, rebind, stat refresh and an independent first cold callback.");
            Check(warm.GetOriginBattleStatuses(0) == 0 && warm.GetOriginSavedStatuses(0) == 0 && warm.Actors.Single(actor => actor.Actor.Slot == 0).LimitGauge == 0, "Native-only guest status must preserve the source status banks and source-owned Limit mechanism.");
            Ff7Progress settled = warm.PublishVitals(progress);
            Ff7CharacterState before = progress.Characters.Single(character => character.Id == 0);
            Ff7CharacterState after = settled.Characters.Single(character => character.Id == 0);
            byte[] expected = (byte[])before.NativeRecord.Clone();
            expected[31] = (byte)((expected[31] & ~Ff7GuestBattleStatuses.SupportedNativeOnly) | status);
            Check(after.NativeRecord.SequenceEqual(expected) && JsonSerializer.Serialize(after with { NativeRecord = before.NativeRecord }) == JsonSerializer.Serialize(before), "Guest field settlement must publish only native story-record bits10/20 while preserving host HP/MP/Limit and all other native record facts.");
            Ff7BattleSession next = Create(settled);
            next.BindGuestVitals(binding with { EncounterId = Guid.NewGuid() });
            Check((next.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7GuestBattleStatuses.SupportedNativeOnly) == status && next.GetOriginSavedStatuses(0) == 0 && next.GetOriginBattleStatuses(0) == 0, "The next guest battle must merge recorded host-only statuses without rewriting source-native status banks.");

            Ff7BattleSnapshot victory = warm.Capture() with { Outcome = Ff7BattleOutcome.Victory, RequestFlags = 0x80, Actors = warm.Capture().Actors.Select(actor => actor.Actor.Slot >= 4 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray() };
            Ff7BattleSession completed = Create(progress, Cold(victory));
            Ff7VictoryProjection first = completed.ProjectVictory(progress, 10);
            Ff7VictoryProjection repeated = completed.ProjectVictory(first.Progress, 10);
            Check(JsonSerializer.Serialize(first.Progress) == JsonSerializer.Serialize(repeated.Progress) && (first.Progress.Characters.Single(character => character.Id == 0).NativeRecord[31] & Ff7GuestBattleStatuses.SupportedNativeOnly) == status, "Once-only victory publication must preserve native-only guest field statuses without replaying the settlement.");
        }
        Console.WriteLine("PASS original incoming guest Fury/RNG, host-only accuracy/Sadness defense, source-bank separation, guest cold/refresh, native field persistence and once-only settlement");
    }

    private static Ff7BattleRng Random(byte value = 0)
    {
        byte[] kernel = new byte[3988];
        kernel.AsSpan(0xE1C, 256).Fill(value);
        return new(kernel, new(new byte[8]));
    }

    private static Ff7BattleSnapshot Cold(Ff7BattleSnapshot snapshot) => JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
