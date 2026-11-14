using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeFuryTests
{
    public static void Run()
    {
        TestOpposedStatuses();
        TestExistingConsumers();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Fury producer: select original FFVII resources.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 57);
        SceneBattleData scene = SceneBattleData.Read(formation);
        Ff7BattleAttack peck = scene.Attacks.Single(attack => attack.Id == 0x1B7).Data;
        Check(peck is { Accuracy: 95, DamageCalculation: 0x11, Power: 32, StatusChange: 2, StatusMask: Ff7NativeBattleStatuses.Fury, AdditionalEffect: byte.MaxValue }, "Original scene14 Flaming Peck must retain its physical damage and separate eight-threshold Fury operand.");
        Ff7BattleDamageActor sourceStats = Stats() with { IsEnemy = true };
        Ff7BattleActionVitals source = new(1000, 1000, 100, 100, 0);
        Ff7BattleActionReceipt admitted = Ff7NativeBattleActionResolver.Resolve(sourceStats, source, Stats(), source, Ff7BattleDamageAction.FromAttack(peck), peck, Random());
        Check(admitted.AddedStatuses == Ff7NativeBattleStatuses.Fury && admitted.Target.Hp < source.Hp && !admitted.Damage.Missed, "Original Flaming Peck must resolve both ordinary HP damage and its admitted Fury operation.");
        Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.Resolve(sourceStats, source, Stats(), source with { StatusImmunities = Ff7NativeBattleStatuses.Fury }, Ff7BattleDamageAction.FromAttack(peck), peck, Random());
        Check(immune.AddedStatuses == 0 && immune.Target.Hp == admitted.Target.Hp, "Native Fury immunity must suppress the status without suppressing the physical hit.");

        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot);
        Ff7BattleSnapshot entry = Create().Capture();
        Ff7BattleSession warm = Create(Cold(entry));
        Ff7BattleSession cold = Create(Cold(entry));
        for (int callback = 0; callback < 3000 && warm.LastHit?.AttackId != 0x1B7; callback++)
        {
            warm.Advance();
            cold.Advance();
        }
        Check(warm.LastHit is { AttackId: 0x1B7 } && JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Original formation57 initialization and AI turn must reach Flaming Peck with identical independent cold continuation.");
        Ff7BattleSnapshot fury = warm.Capture() with { Actors = warm.Capture().Actors.Select(actor => actor.Actor.Slot == 2 ? actor with { Actor = actor.Actor with { Statuses = (actor.Actor.Statuses & ~Ff7NativeBattleStatuses.Sadness) | Ff7NativeBattleStatuses.Fury } } : actor).ToArray() };
        Ff7BattleSession persisted = Create(Cold(fury));
        Ff7Progress published = persisted.PublishVitals(progress);
        Ff7CharacterState tifa = published.Characters.Single(character => character.Id == 2);
        Check((tifa.NativeRecord[31] & 0x30) == Ff7NativeBattleStatuses.Fury && (Ff7NativeBattlePlayerStats.Create(tifa, kernel, executable.NativeMateriaStatModifiers).Actor.Statuses & 0x30) == Ff7NativeBattleStatuses.Fury, "An authored produced-Fury continuation must survive native vitals publication and the next battle's saved-status projection.");
        Console.WriteLine("PASS original Flaming Peck Fury production, resistance, opposed-status ordering, existing accuracy/Limit consumers, formation57 AI turn, JSON continuation and field persistence");
    }

    private static void TestOpposedStatuses()
    {
        const uint sadness = Ff7NativeBattleStatuses.Sadness;
        const uint fury = Ff7NativeBattleStatuses.Fury;
        Check(Status(0, fury).Target.Statuses == fury && Status(fury, fury).Target.Statuses == fury, "Native Fury addition must admit and retain its active bit.");
        Ff7BattleActionReceipt cancelSadness = Status(sadness, fury);
        Ff7BattleActionReceipt cancelFury = Status(fury, sadness);
        Check(cancelSadness.Target.Statuses == 0 && cancelSadness.RemovedStatuses == sadness && cancelSadness.AddedStatuses == 0, "Adding Fury to current Sadness must cancel both rather than replace Sadness with Fury.");
        Check(cancelFury.Target.Statuses == 0 && cancelFury.RemovedStatuses == fury && cancelFury.AddedStatuses == 0, "Adding Sadness to current Fury must cancel both in the reciprocal native row.");
        Check(Status(0, sadness | fury).Target.Statuses == 0, "Simultaneous native opposed additions stage both opposite removals.");
        Check(Status(sadness, fury, immunity: fury).Target.Statuses == 0, "The native opposed-add rule must run before ordinary add/remove immunity filtering.");
        Check(Status(sadness, fury, immunity: sadness).Target.Statuses == sadness, "Immunity to the staged opposite removal must retain the old status without admitting the canceled new bit.");
        Check(Status(fury, fury, 0x40).Target.Statuses == 0, "Native removal mode must clear Fury directly.");
        Check(Status(sadness, fury, 0x80).Target.Statuses == (sadness | fury), "Native toggle mode must not enter the add-only opposed-pair finalizer.");
        Check(Status(0, fury, immunity: fury).Target.Statuses == 0, "Native ordinary immunity must reject a Fury addition.");
    }

    private static Ff7BattleActionReceipt Status(uint initial, uint mask, byte mode = 0, uint immunity = 0)
    {
        Ff7BattleAttack record = new(255, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, (byte)(mode | 63), byte.MaxValue, 0, mask, 0, ushort.MaxValue);
        Ff7BattleActionVitals vitals = new(100, 100, 10, 10, initial, immunity);
        return Ff7NativeBattleActionResolver.Resolve(Stats(), vitals, Stats(), vitals, Ff7BattleDamageAction.FromAttack(record), record, Random());
    }

    private static void TestExistingConsumers()
    {
        Ff7BattleDamageActor stats = Stats() with { Dexterity = 0 };
        Ff7BattleDamageAction physical = new(0x11, 16, 100);
        Check(!Ff7NativeBattleDamage.Resolve(stats, stats, physical, Random(192)).Missed && Ff7NativeBattleDamage.Resolve(stats with { Statuses = Ff7NativeBattleStatuses.Fury }, stats, physical, Random(192)).Missed, "Produced Fury must reach the existing physical accuracy reduction.");
        Ff7BattleDamageAction magic = new(0x22, 16, 100);
        Check(!Ff7NativeBattleDamage.Resolve(stats, stats, magic, Random(224)).Missed && Ff7NativeBattleDamage.Resolve(stats with { Statuses = Ff7NativeBattleStatuses.Fury }, stats, magic, Random(224)).Missed, "Produced Fury must reach the existing magic accuracy reduction.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, Ff7NativeBattleStatuses.Fury, 140) == 80 && Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, Ff7NativeBattleStatuses.Sadness, 140) == 20, "The existing native Fury/Sadness Limit multipliers must remain double/half.");
    }

    private static Ff7BattleDamageActor Stats() => new(20, 30, 30, 20, 0, 0, 0, 20, 20);

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
