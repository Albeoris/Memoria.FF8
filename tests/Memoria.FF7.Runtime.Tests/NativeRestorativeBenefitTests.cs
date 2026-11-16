using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRestorativeBenefitTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP native restorative command benefit: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255], Inventory = [new(2, 1)] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[555];
        Ff7BattleSession initial = new(formation, progress, kernel, materia, 1);
        Ff7BattleSnapshot source = initial.Capture();
        source = source with { Intro = null, Actors = source.Actors.Select(actor => actor with { AwaitingTurn = actor.Actor.Slot == 1, Gauge = actor.Actor.Slot == 1 ? ushort.MaxValue : (ushort)0 }).ToArray() };
        Ff7BattleSession battle = Restore(source);
        Ff7BattleCommandOption command = battle.AutoPlayCommands(1).Single(option => option.Command == Ff7BattleCommand.Item && option.Argument == 2);
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(battle.Capture());
        Check(command.SelectSingle && (command.ValidTargets & (1 << 6)) != 0, "The original available item must admit the ordinary single enemy target.");
        for (int query = 0; query < 4; query++)
        {
            Check(battle.DescribeRestorativeDamage(1, command, 6).GuaranteedHpDamage == 5500, "Original maximum-HP restoration and recovery absorption did not produce the proved current-state damage bound.");
            Check(battle.DescribeRestorativeDamage(1, command, 4).GuaranteedHpDamage == 0, "An opponent without recovery absorption was incorrectly treated as a restorative knockout.");
            Check(battle.DescribeRestorativeDamage(1, command, 1).GuaranteedHpDamage == 0, "Friendly recovery was incorrectly treated as an offensive command.");
            Check(battle.DescribeRestorativeDamage(1, command with { ValidTargets = (ushort)(command.ValidTargets & ~(1 << 6)) }, 6).GuaranteedHpDamage == 0, "A forged target mask bypassed native command availability.");
        }
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(battle.Capture())), "Benefit queries changed native RNG, inventory, HP, MP, queues or clocks.");
        foreach (Ff7BattleSnapshot changed in new[]
        {
            source with { Actors = source.Actors.Select(actor => actor.Actor.Slot == 6 ? actor with { Actor = actor.Actor with { ElementAbsorb = 0, ExtraElementAbsorb = 0 } } : actor).ToArray() },
            source with { Actors = source.Actors.Select(actor => actor.Actor.Slot == 6 ? actor with { Actor = actor.Actor with { ElementNullify = (actor.Actor.ElementNullify ?? 0) | 0x200u } } : actor).ToArray() },
            source with { Actors = source.Actors.Select(actor => actor.Actor.Slot == 6 ? actor with { Actor = actor.Actor with { Conditions = actor.Actor.Conditions | 0x200u } } : actor).ToArray() },
            source with { Inventory = [], Economy = Ff7BattleEconomy.Create(Ff7NativeBattleInventory.Create([], kernel)) }
        })
        {
            Check(Restore(changed).DescribeRestorativeDamage(1, command, 6).GuaranteedHpDamage == 0, "A changed dynamic affinity, immunity or exhausted stock retained a stale guarantee.");
        }

        Check(battle.Submit(1, command.Command, command.Argument, 1 << 6), "The normal native submission rejected its available restorative command and target.");
        Ff7BattleSnapshot queued = battle.Capture();
        Ff7BattleQueuedCommand staged = queued.Queue.Ready.OfType<Ff7BattleQueuedCommand>().Single(packet => packet.ActorSlot == 1);
        Check(staged.CommandId == (byte)Ff7BattleCommand.Item && staged.Argument == 2 && staged.TargetMask == 1 << 6 && !staged.GuestAttack, "The policy command did not reach the ordinary native item queue.");
        Check(JsonSerializer.Serialize(source.Random) == JsonSerializer.Serialize(queued.Random), "Control submission sampled native battle RNG before execution.");
        Check(JsonSerializer.Serialize(source.Inventory) == JsonSerializer.Serialize(queued.Inventory), "Control submission spent native stock before its command transaction executed.");
        Check(JsonSerializer.Serialize(source.Actors.Select(actor => new { actor.Actor.HitPoints, actor.Actor.MagicPoints, actor.Actor.Statuses })) == JsonSerializer.Serialize(queued.Actors.Select(actor => new { actor.Actor.HitPoints, actor.Actor.MagicPoints, actor.Actor.Statuses })), "Control submission changed numeric results before execution.");
        Ff7BattleSession cold = Restore(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(queued))!);
        Check(JsonSerializer.Serialize(queued) == JsonSerializer.Serialize(cold.Capture()), "The queued native restorative command did not retain exact cold ownership.");
        Check(cold.DescribeRestorativeDamage(1, command, 6).GuaranteedHpDamage == 0, "A submitted actor remained selectable for another policy command.");
        Console.WriteLine("PASS native555 restorative availability, conservative query purity, ordinary submission and cold queue ownership; no effect advanced.");

        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, materia, 1, snapshot: snapshot);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
