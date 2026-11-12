using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeElixirActionTests
{
    public static void Run()
    {
        Ff7BattleAttack record = new(255, 0, 0, 0, 0, 0, 0, 0x0F, 0, 0x08, 32, 0, 255, 255, 255, uint.MaxValue, 0x200, ushort.MaxValue);
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record);
        Ff7BattleActionVitals source = new(10, 100, 3, 50, 0);
        Ff7BattleActionVitals target = new(1, 50000, 1, 4000, Ff7NativeBattleActionResolver.Poison);
        Ff7BattleRng random = new(new byte[3988], Ff7BattleRng.BeginBattle());
        string beforeRandom = JsonSerializer.Serialize(random.Capture());
        Ff7BattleActionReceipt healed = Ff7NativeBattleActionResolver.Resolve(default, source, default, target, action, record, random);
        Check(healed.Target == (target with { Hp = 50000, Mp = 4000 }) && healed.Source == source, "Full recovery must restore both native pools directly without numerical caps or source mutation.");
        Check(healed.NativeResultMarker == -3 && healed.Damage == new Ff7BattleDamageResult(false, false, 0), "Full recovery must retain its explicit native result marker instead of a fabricated numeric heal.");
        Check(JsonSerializer.Serialize(random.Capture()) == beforeRandom, "Formula08 must not consume accuracy, variance or status RNG.");
        Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.Resolve(default, source, default(Ff7BattleDamageActor) with { NativeConditions = 0x300 }, target, action with { Affinity = Ff7BattleDamageAffinity.Nullify }, record, random);
        Check(immune.Target == healed.Target && immune.NativeResultMarker == -3, "Native full recovery is a direct vital write before numerical immunity and replaces the prior non-absorb affinity.");
        Ff7BattleActionReceipt self = Ff7NativeBattleActionResolver.Resolve(default, target, default, target, action, record, random, sameActor: true);
        Check(self.Source == self.Target && self.Target.Hp == target.MaxHp && self.Target.Mp == target.MaxMp, "Self recovery must publish the same dual-pool state for source and target.");
        Ff7BattleActionReceipt absorbed = Ff7NativeBattleActionResolver.Resolve(default, source, default, target with { StatusImmunities = 1 }, action with { Affinity = Ff7BattleDamageAffinity.Absorb }, record, random);
        Check(absorbed.Target.Hp == 0 && absorbed.Target.Mp == target.Mp && (absorbed.AddedStatuses & 1) != 0 && absorbed.NativeResultMarker == -2, "Absorbed formula08 must publish its native forced-death result rather than ordinary healing or damage.");
        Ff7BattleActionReceipt dead = Ff7NativeBattleActionResolver.Resolve(default, source, default, target with { Hp = 0, Statuses = 1 }, action, record, random);
        Check(dead.Damage.Missed && dead.Target.Hp == 0 && dead.NativeResultMarker is null, "Native Elixir target flags must not grant revival.");
        InstalledPot();
        Console.WriteLine("PASS native Elixir dual-pool formula and original queued item interaction.");
    }

    private static void InstalledPot()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed queued Elixir interaction: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session session = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleAttack original = Ff7NativeBattleActionResolver.ReadItem(5, kernel);
        Check(original.DamageCalculation == 8 && original.Power == 32 && original.Elements == 0x200, "The installed Elixir must use its original full-recovery formula.");
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 885);
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7Progress progress = session.Progress with { Inventory = [.. session.Progress.Inventory.Where(item => item.ItemId != 5), new(5, 2)] };
        byte[] bank = new byte[256];
        bank[84] = 6;
        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, modifiers, 1, snapshot: saved, initialSaveBank1: bank);
        Ff7BattleSnapshot initial = Create().Capture();
        int pot = initial.Actors.Single(actor => actor.Identity == 310).Actor.Slot;
        Ff7BattleSnapshot ready = initial with { Actors = initial.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with { Gauge = 0, AwaitingTurn = false }).ToArray() };
        Ff7BattleSession battle = Create(ready);
        Check((battle.Actors.Single(actor => actor.Actor.Slot == pot).Actor.Conditions & 0x300) == 0x300, "Original Pot initialization must close both damage gates.");
        Check(battle.Submit(0, Ff7BattleCommand.Item, 5, (ushort)(1 << pot)), "The original Elixir must be selectable for the native enemy recipient.");
        Ff7BattleSnapshot queued = RoundTrip(battle.Capture());
        Check(queued.Inventory.Single(item => item.ItemId == 5).Quantity == 2 && (queued.Actors.Single(actor => actor.Actor.Slot == pot).Actor.Conditions & 0x300) == 0x300, "Queuing an item must neither spend it nor unlock its recipient before the actual action.");
        Ff7BattleSession restored = Create(queued);
        for (int frame = 0; frame < 180 && (battle.Actors.Single(actor => actor.Actor.Slot == pot).Actor.Conditions & 0x300) != 0; frame++)
        {
            battle.Advance(turnBased: true);
            restored.Advance(turnBased: true);
        }
        Check((battle.Actors.Single(actor => actor.Actor.Slot == pot).Actor.Conditions & 0x300) == 0, "A completed original Elixir item receipt must invoke the native counter and open both gates.");
        Check(battle.Capture().Inventory.Single(item => item.ItemId == 5).Quantity == 1, "The queued Elixir must consume one source item exactly once.");
        Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Queued native item continuation must retain the same bank, AI, RNG and inventory after JSON restore.");
        Ff7BattleSession continued = Create(RoundTrip(battle.Capture()));
        for (int frame = 0; frame < 16; frame++)
        {
            continued.Advance(turnBased: true);
        }
        Check(continued.Capture().Inventory.Single(item => item.ItemId == 5).Quantity == 1 && continued.Capture().AiSaveBank1?[84] == 6, "Continuing the committed receipt must not replay its item debit or replace the native bank owner.");
    }

    private static Ff7BattleSnapshot RoundTrip(Ff7BattleSnapshot snapshot)
    {
        return JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot)) ?? throw new InvalidDataException("No restored battle state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
