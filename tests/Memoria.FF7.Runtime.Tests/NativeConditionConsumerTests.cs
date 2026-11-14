using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeConditionConsumerTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed condition consumers: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleAttack life2 = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(8 * 28, 28));
        Check(life2.CastingCost == 100, "Original Life2 cost changed.");
        Ff7BattleActionVitals source = new(100, 100, 225, 460, 0);
        Check(Ff7NativeBattleActionResolver.TrySpendCastingCost(source, life2, out Ff7BattleActionVitals protectedSource, 0x400) && protectedSource == source, "Native cost bypass must preserve the recorded225MP without refunding earlier actions.");
        Ff7BattleActionVitals zero = source with { Mp = 0, Statuses = 0x80 };
        Check(Ff7NativeBattleActionResolver.TrySpendCastingCost(zero, life2, out Ff7BattleActionVitals admitted, 0x400) && admitted == zero, "Native cost bypass must precede both MP and Silence admission.");
        Check(!Ff7NativeBattleActionResolver.TrySpendCastingCost(zero, life2, out _) && Ff7NativeBattleActionResolver.TrySpendCastingCost(source, life2, out Ff7BattleActionVitals spent) && spent.Mp == 125, "Ordinary source MP admission and subtraction changed.");
        TestImmunity(kernel);
        TestOriginalCaveStatus(installation, kernel);
        TestOriginalLife2(installation, kernel);
        Console.WriteLine("Native source cost bypass, category/status immunity, RNG and original Life2 zero-MP cold continuation passed.");
    }

    private static void TestImmunity(IReadOnlyList<KernelSection> kernel)
    {
        const uint paralysis = 0x02000000;
        Ff7BattleAttack original = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x1B * 28, 28));
        Ff7BattleAttack status = original with { Accuracy = 255, DamageCalculation = 0x22, Power = 16, StatusChange = 63, StatusMask = paralysis, AdditionalEffect = byte.MaxValue, SpecialFlags = ushort.MaxValue };
        Ff7BattleDamageActor stats = new(50, 40, 40, 20, 20, 0, 0, 20, 20);
        Ff7BattleActionVitals source = new(1000, 1000, 200, 500, 0);
        foreach (bool physical in new[] { false, true })
        {
            Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(status) with { NativePhysicalCategory = physical };
            Ff7BattleActionVitals target = new(1000, 1000, 200, 500, 0);
            Ff7BattleRng baselineRandom = Random();
            Ff7BattleActionReceipt baseline = Ff7NativeBattleActionResolver.Resolve(stats, source, stats, target, action, status, baselineRandom);
            Check(!baseline.Damage.Missed && (baseline.AddedStatuses & paralysis) != 0, "Unprotected native action must publish its admitted status.");
            foreach (uint condition in new uint[] { 0x100, 0x200 })
            {
                bool matches = condition == (physical ? 0x100u : 0x200u);
                Ff7BattleRng candidate = Random();
                Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = condition }, target, action, status, candidate);
                Check(JsonSerializer.Serialize(candidate.Capture()) == JsonSerializer.Serialize(baselineRandom.Capture()), "Category immunity changed preceding hit/formula RNG.");
                Check(matches ? receipt.Target == target && receipt.Damage is { Missed: true, Damage: 0 } && receipt.AddedStatuses == 0 && receipt.RemovedStatuses == 0 && receipt.NativeResultMarker is null : receipt == baseline, "Native target category immunity must clear damage and pending status publication only for its category.");
            }
            foreach (byte mode in new byte[] { 1, 2 })
            {
                Ff7BattleAttack removal = status with { StatusChange = (byte)(mode << 6 | 63) };
                Ff7BattleActionVitals affected = target with { Statuses = paralysis };
                Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = physical ? 0x100u : 0x200u }, affected, action, removal, Random());
                Check(receipt.Target == affected && receipt.Damage.Missed && receipt.AddedStatuses == 0 && receipt.RemovedStatuses == 0, "Category immunity must clear pending removal and toggle masks as well as additions.");
            }
            foreach (Ff7BattleActionVitals unchanged in new[] { target with { Statuses = paralysis }, target with { StatusImmunities = paralysis } })
            {
                Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = physical ? 0x100u : 0x200u }, unchanged, action, status, Random());
                Check(receipt.Damage.Missed && receipt.Target == unchanged, "Native conditional miss must use pre-immunity proposals even for an already-present or ordinarily immune status.");
            }
            Ff7BattleAttack numeric = status with { StatusMask = 0, StatusChange = byte.MaxValue };
            Ff7BattleDamageAction numericAction = Ff7BattleDamageAction.FromAttack(numeric) with { NativePhysicalCategory = physical };
            Ff7BattleActionReceipt numericReceipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = physical ? 0x100u : 0x200u }, target, numericAction, numeric, Random());
            Check(!numericReceipt.Damage.Missed && numericReceipt.Damage.Damage == 0 && numericReceipt.Target == target, "Pure category-immune numeric contact must retain the native zero non-miss result.");
            Ff7BattleDamageAction earth = numericAction with { NativeElements = 8 };
            Ff7BattleActionReceipt earthReceipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = physical ? 0x100u : 0x200u }, target, earth, numeric, Random());
            Check(earthReceipt.Damage.Missed && earthReceipt.Target == target, "Native Earth action flag must retain its category-immunity miss result without status proposals.");
        }
        Ff7BattleAttack recovery = status with { DamageCalculation = 8, StatusMask = 0, StatusChange = byte.MaxValue };
        Ff7BattleDamageAction recoveryAction = Ff7BattleDamageAction.FromAttack(recovery);
        Ff7BattleActionVitals injured = new(100, 1000, 20, 500, 0);
        Ff7BattleActionReceipt recovered = Ff7NativeBattleActionResolver.Resolve(stats, source, stats, injured, recoveryAction, recovery, Random());
        Check(recovered.Target.Hp == 1000 && recovered.Target.Mp == 500 && recovered.NativeResultMarker == -3, "Ordinary full recovery contract changed.");
        Ff7BattleActionReceipt blockedRecovery = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = 0x200 }, injured, recoveryAction, recovery, Random());
        Check(blockedRecovery.Target == injured && blockedRecovery.Damage.Damage == 0 && blockedRecovery.NativeResultMarker is null, "The common native final-publication gate must also suppress full recovery proposals.");
        Ff7BattleDamageAction mixed = Ff7BattleDamageAction.FromAttack(status) with { GuestOriginBattleStatuses = Ff7GuestBattleStatuses.OriginAura, GuestOriginStatusAttack = 200, ParameterOperation = new(Ff7GuestBattleStatuses.OriginDefenseReduction, 18, 50, 50) };
        Ff7BattleDamageActor savedFacts = stats with { OriginSavedStatuses = Ff7OriginSavedStatusRules.Zombie };
        Ff7BattleActionVitals mixedTarget = new(3264, 3264, 225, 460, 0);
        byte[] resistances = new byte[40];
        Ff7BattleRng mixedBaselineRandom = Random();
        Ff7BattleActionReceipt mixedBaseline = Ff7NativeBattleActionResolver.ResolveGuestContact(savedFacts, source, savedFacts, mixedTarget, mixed, status, mixedBaselineRandom, originStatusResistances: resistances);
        Check((mixedBaseline.AddedStatuses & paralysis) != 0 && mixedBaseline.AddedOriginBattleStatuses != 0 && mixedBaseline.AddedParameterStatus is not null, "Mixed regression must exercise native, origin and parameter proposals.");
        Ff7BattleRng mixedProtectedRandom = Random();
        Ff7BattleActionReceipt mixedProtected = Ff7NativeBattleActionResolver.ResolveGuestContact(savedFacts, source, savedFacts with { NativeConditions = 0x200 }, mixedTarget, mixed, status, mixedProtectedRandom, originStatusResistances: resistances);
        Check(JsonSerializer.Serialize(mixedBaselineRandom.Capture()) == JsonSerializer.Serialize(mixedProtectedRandom.Capture()), "Condition immunity must wait until all mixed-contact proposal RNG has completed.");
        Check(mixedProtected.Damage.Missed && mixedProtected.Target.Hp == mixedTarget.Hp && mixedProtected.Target.Statuses == mixedTarget.Statuses && mixedProtected.AddedStatuses == 0 && mixedProtected.AddedOriginBattleStatuses == 0 && mixedProtected.AddedParameterStatus is null, "The shared finalizer must suppress every mixed-contact proposal.");
        Check(mixedProtected.Source.OriginSavedStatuses == Ff7OriginSavedStatusRules.Zombie && mixedProtected.Target.OriginSavedStatuses == Ff7OriginSavedStatusRules.Zombie, "An immune mixed contact must preserve saved facts supplied by the actor statistics owner.");
        Ff7BattleActionVitals paidSource = source with { Hp = 900, Mp = 125 };
        Ff7BattleActionReceipt paidContact = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, paidSource, stats with { NativeConditions = 0x200 }, mixedTarget, mixed, status, Random(), originStatusResistances: resistances);
        Check(paidContact.Source == paidSource, "Target immunity must preserve independently paid source HP/MP costs supplied before target resolution.");

        Ff7BattleRng Random()
        {
            return new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle(1));
        }
    }

    private static void TestOriginalCaveStatus(Ff7Installation installation, IReadOnlyList<KernelSection> kernel)
    {
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        PcFieldFile cave = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find("nvdun2").Single()));
        HashSet<int> selected = PcFieldEncounterTable.Read(cave.GetSection(PcFieldSectionKind.Encounters).Span).Where(table => table.Enabled).SelectMany(table => table.Standard.Concat(table.Special)).Where(encounter => encounter.Frequency > 0).Select(encounter => (int)encounter.Formation).ToHashSet();
        SceneBattleAttack[] attacks = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Where(formation => selected.Contains(formation.FormationId)).SelectMany(formation =>
        {
            SceneBattleData data = SceneBattleData.Read(formation);
            HashSet<ushort> selectors = data.Enemies.SelectMany(enemy => enemy.Attacks).Select(attack => attack.AttackId).ToHashSet();
            return data.Attacks.Where(attack => selectors.Contains(attack.Id));
        }).Where(attack => (attack.Data.StatusMask & 0x02000000) != 0 && Ff7NativeBattleStatuses.ReadRecordMode(attack.Data) is 0 or 2).DistinctBy(attack => attack.Id).ToArray();
        Check(attacks.Length > 0, "Original cave encounters must contain the reached Paralysis source action.");
        Ff7BattleDamageActor stats = new(50, 40, 40, 20, 20, 0, 0, 20, 20);
        Ff7BattleActionVitals source = new(1000, 1000, 200, 500, 0);
        Ff7BattleActionVitals target = new(3264, 3264, 225, 460, 0);
        foreach (SceneBattleAttack attack in attacks)
        {
            bool exercised = false;
            for (uint seed = 1; seed <= 128; seed++)
            {
                Ff7BattleRng baselineRandom = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle(seed));
                Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(attack.Data);
                Ff7BattleActionReceipt baseline = Ff7NativeBattleActionResolver.Resolve(stats, source, stats, target, action, attack.Data, baselineRandom);
                if ((baseline.AddedStatuses & 0x02000000) == 0)
                {
                    continue;
                }
                Ff7BattleRng protectedRandom = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle(seed));
                Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(stats, source, stats with { NativeConditions = 0x710 }, target, action, attack.Data, protectedRandom);
                Check(receipt.Target == target && receipt.AddedStatuses == 0 && receipt.RemovedStatuses == 0 && receipt.Damage.Missed && receipt.Damage.Damage == 0, $"Original cave attack {attack.Id} must not publish Paralysis against category immunity.");
                Check(JsonSerializer.Serialize(baselineRandom.Capture()) == JsonSerializer.Serialize(protectedRandom.Capture()), "Original cave immunity changed status/hit/formula RNG.");
                exercised = true;
                break;
            }
            Check(exercised, $"Original cave attack {attack.Id} never exercised its recorded Paralysis contact.");
        }
    }

    private static void TestOriginalLife2(Ff7Installation installation, IReadOnlyList<KernelSection> kernel)
    {
        using Ff7Session initial = Ff7Session.NewGame(installation);
        Ff7Progress progress = initial.Progress with { Party = [9, 10, 255] };
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 292);
        Ff7BattleSession entry = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 0xA0, requestMode: 1);
        Ff7BattleSnapshot snapshot = entry.Capture();
        snapshot = snapshot with
        {
            Actors = snapshot.Actors.Select(actor => actor.Identity == 10
                ? actor with { Actor = actor.Actor with { MagicPoints = 0 } }
                : actor.Identity == 9 ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 } } : actor).ToArray(),
            Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, new(2, 1, (byte)Ff7BattleCommand.Magic, 8, 1))
        };
        Ff7BattleSession warm = Restore(snapshot);
        Ff7BattleSession cold = Restore(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        bool completed = false;
        for (int tick = 0; tick < 1000; tick++)
        {
            warm.Advance(waitForCommand: true);
            cold.Advance(waitForCommand: true);
            if (warm.LastHit is { Source: 1, AttackId: 8 } && warm.Action is null)
            {
                completed = true;
                break;
            }
        }
        Check(completed && warm.Actors.Single(actor => actor.Identity == 9).Actor.HitPoints > 0 && warm.Actors.Single(actor => actor.Identity == 10).Actor.MagicPoints == 0, "Original Life2 must execute with native cost bypass at zero MP.");
        Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Original zero-MP Life2 changed across JSON continuation.");

        Ff7BattleSession Restore(Ff7BattleSnapshot state)
        {
            return new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: state);
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
