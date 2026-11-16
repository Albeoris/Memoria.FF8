using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class FrogStatusTests
{
    public static void Run()
    {
        CheckActorBranch();
        const uint frog = Ff7NativeBattleStatuses.Frog;
        Ff7BattleAttack record = new(255, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 63, byte.MaxValue, 0, frog, 0, 0x80);
        Ff7BattleDamageActor stats = new(20, 30, 30, 30, 0, 0, 0, 20, 20);
        Ff7BattleActionVitals vitals = new(320, 320, 20, 20, 0);
        Ff7BattleActionReceipt Resolve(Ff7BattleAttack attack, Ff7BattleActionVitals target, Ff7BattleRng? random = null)
        {
            return Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, target, Ff7BattleDamageAction.FromAttack(attack), attack, random ?? Random());
        }
        Check((Ff7NativeBattleStatuses.SupportedRecordStatuses & frog) == 0, "Frog must remain outside the shared origin guest status contract.");
        Check(Resolve(record, vitals).Target.Statuses == frog, "The native status transaction must admit Frog application.");
        Check(Resolve(record with { StatusChange = 127 }, vitals with { Statuses = frog }).Target.Statuses == 0, "The native remove transaction must cure Frog.");
        Check(Resolve(record with { StatusChange = 191 }, vitals).Target.Statuses == frog, "Toggle must apply Frog to an ordinary target.");
        Ff7BattleRng removalRandom = Random(255);
        Check(Resolve(record with { StatusChange = 128 }, vitals with { Statuses = frog }, removalRandom).Target.Statuses == 0 && removalRandom.Capture().Indices.All(index => index == 0), "Existing Frog must take guaranteed252 removal without a status random draw, even with zero base chance.");
        Ff7BattleRng additionRandom = Random(255);
        Check(Resolve(record with { StatusChange = 128 }, vitals, additionRandom).Target.Statuses == 0 && additionRandom.Capture().Indices.Any(index => index != 0), "New Frog application must retain its ordinary probability and draw.");
        foreach (Ff7BattleActionVitals protectedTarget in new[] { vitals with { StatusImmunities = frog }, vitals with { Statuses = Ff7NativeBattleStatuses.Resist }, vitals with { Statuses = Ff7NativeBattleStatuses.Peerless } })
        {
            Check((Resolve(record, protectedTarget).Target.Statuses & frog) == 0, "Native status immunity, Resist and Peerless must prevent Frog.");
        }
        Check(Resolve(record with { StatusChange = 128 }, vitals with { Statuses = frog, StatusImmunities = frog }).Target.Statuses == frog, "Guaranteed toggle removal must still respect native status immunity.");
        Check(Resolve(record with { SpecialFlags = 0 }, vitals with { StatusImmunities = frog }).Target.Statuses == frog, "Native attacks that ignore status immunity must keep their original property.");
        bool guestRejected = false;
        try
        {
            Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, Random());
        }
        catch (NotSupportedException)
        {
            guestRejected = true;
        }
        Check(guestRejected, "Frog must not acquire an implicit origin guest status translation.");
        foreach (byte command in Enumerable.Range(1, 32).Select(value => (byte)value))
        {
            bool expected = command is 1 or 4 or 32;
            Check(Ff7NativeBattleStatuses.CanUseCommand(frog, command, 0) == expected, $"Frog command{command} must follow the native dispatch.");
        }
        Check(Ff7NativeBattleStatuses.CanUseCommand(frog, 2, 10, 7) && Ff7NativeBattleStatuses.CanUseCommand(frog, 21, 10, 7), "Toad must retain both ordinary and W-Magic admission.");
        Check(!Ff7NativeBattleStatuses.CanUseCommand(frog | Ff7NativeBattleStatuses.Silence, 2, 10), "Silence must block Toad even with a zero staged casting cost.");
        Check(!Ff7NativeBattleStatuses.CanUseCommand(frog, 32, 0, 1), "Frog must reject MP-cost enemy actions.");
        foreach (byte command in Enumerable.Range(1, 32).Select(value => (byte)value))
        {
            foreach (int cost in new[] { 0, 1 })
            {
                bool silenced = command is 2 or 3 or 13 or 21 or 22 || command == 32 && cost != 0;
                Check(Ff7NativeBattleStatuses.CanUseCommand(Ff7NativeBattleStatuses.Silence, command, 0, cost) == !silenced, $"Silence command{command}, cost{cost} must match the original dispatch.");
                Check(Ff7NativeBattleStatuses.CanUseCommand(Ff7NativeBattleStatuses.Silence | frog, command, 0, cost, 0x400), "The native source condition400 must bypass both Silence and Frog admission.");
            }
        }
        Installed();
        Console.WriteLine("PASS native Frog mutation, immunity, chance, commands and queued revalidation.");
    }

    private static void Installed()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed Frog commands: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneFormation formation = formations.Single(value => value.FormationId == 300);
        Ff7BattleAttack nativeFrog = SceneBattleData.Read(formations.Single(value => value.FormationId == 116)).Attacks.First(attack => attack.Data.StatusMask == Ff7NativeBattleStatuses.Frog).Data;
        Check(nativeFrog.DamageCalculation == 0x11, "The reached original formation must provide its original Frog action formula.");
        Ff7BattleAttack toad = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(10 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        Check(toad.StatusMask == Ff7NativeBattleStatuses.Frog && Ff7NativeBattleStatuses.ReadRecordMode(toad) == 2, "Native spell10 must toggle Frog through the shared transaction.");
        Ff7BattleDamageActor nativeStats = new(20, 30, 30, 30, 0, 0, 0, 20, 20);
        Ff7BattleActionVitals nativeVitals = new(320, 320, 20, 20, 0);
        Ff7BattleActionReceipt nativeContact = Ff7NativeBattleActionResolver.Resolve(nativeStats, nativeVitals, nativeStats, nativeVitals, Ff7BattleDamageAction.FromAttack(nativeFrog), nativeFrog, Random());
        Check((nativeContact.Target.Statuses & Ff7NativeBattleStatuses.Frog) != 0, "The reached original Frog action must produce its native status without a synthetic effect record.");
        IReadOnlyList<short> modifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255], Inventory = [new(0, 3)] };
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, modifiers, 1, snapshot: snapshot);
        Ff7BattleSnapshot entry = Create().Capture();
        Ff7BattleSnapshot setup = entry with
        {
            Actors = entry.Actors.Select(actor => actor with { Gauge = 0, AwaitingTurn = actor.Actor.Slot == 0, Actor = actor.Actor with { Statuses = actor.Actor.Slot == 0 ? Ff7NativeBattleStatuses.Frog : 0x400U } }).ToArray()
        };
        Ff7BattleSession menu = Create(RoundTrip(setup));
        Check(menu.Commands(0).Any(option => option.Command == Ff7BattleCommand.Attack) && menu.Commands(0).All(option => option.Command is Ff7BattleCommand.Attack or Ff7BattleCommand.Item || option.Command == Ff7BattleCommand.Magic && option.Argument == 10), "The native menu must show Attack and only Frog-compatible commands.");
        Check(!menu.Submit(0, Ff7BattleCommand.Defend, 0, 1) && !menu.Submit(0, Ff7BattleCommand.Change, 0, 1), "Ordinary submission must reject Frog-forbidden commands.");
        Ff7BattleSnapshot staged = setup with { Queue = Ff7BattleCommandQueue.StageReady(entry.Queue, 0, (byte)Ff7BattleCommand.Change, 0, 1), Actors = setup.Actors.Select(actor => actor with { AwaitingTurn = false }).ToArray() };
        Ff7BattleSession live = Create(RoundTrip(staged));
        Ff7BattleSession cold = Create(RoundTrip(staged));
        uint originalConditions = setup.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Conditions;
        for (int frame = 0; frame < 16; frame++)
        {
            live.Advance();
            cold = Create(RoundTrip(cold.Capture()));
            cold.Advance();
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Frog staged rejection must survive cold restoration identically.");
        }
        Check(live.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Conditions == originalConditions, "A status gained after staging must stop the queued row change.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void CheckActorBranch()
    {
        BattleModelData model = new(0, [], [new(1, 1, new byte[5]), new(1, 1, new byte[5])], []);
        BattleActionSequence Sequence(byte[] script) => BattleActionSequence.Read(new(0, ReadOnlyMemory<byte>.Empty, [script]), model, 0);
        BattleActionSequence branch = Sequence([0xCE, 0, 0xEE, 0xCD, 1, 0xEE]);
        BattleActionPoseUpdate party = branch.Advance(new());
        BattleActionPoseUpdate enemy = branch.Advance(new(), enemy: true);
        Check(party.State.ClipIndex == 0 && enemy.State.ClipIndex == 1 && party.Events.Count == 0 && enemy.Events.Count == 0, "CE must immediately select its physical party/enemy branch without an event or frame wait.");
        BattleActionPoseState restored = JsonSerializer.Deserialize<BattleActionPoseState>(JsonSerializer.Serialize(enemy.State))!;
        Check(branch.Advance(restored, enemy: true).State == branch.Advance(enemy.State, enemy: true).State && branch.Advance(restored, enemy: true).State.Completed, "An enemy branch continuation changed across JSON restoration.");
        BattleActionSequence raw = Sequence([0xCE, 0xF4, 0xCD, 0, 0xEE, 0xCD, 1, 0xEE]);
        Check(raw.Advance(new(), enemy: true).State.ClipIndex == 0, "CE scanned typed operands instead of the first raw CD byte.");
        BattleActionSequence absent = Sequence([0xCE, 0, 0xEE]);
        Check(absent.Advance(new()).State.ClipIndex == 0, "A party CE branch required an enemy-only marker.");
        bool rejected = false;
        try
        {
            absent.Advance(new(), enemy: true);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "An enemy CE branch without a CD marker escaped its byte-cursor bounds.");
    }

    private static Ff7BattleRng Random(byte value = 0)
    {
        byte[] kernel = new byte[3988];
        kernel.AsSpan(0xE1C, 256).Fill(value);
        return new(kernel, new(new byte[8]));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
