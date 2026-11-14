using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class RewardSuppressionTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed reward suppression: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 292);
        Ff7Progress progress = initial.Progress with { Party = [9, 10, 255] };
        Ff7BattleSession entering = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 101, requestFlags: 0xA0, requestMode: 1);
        Ff7BattleSnapshot finished = entering.Capture() with
        {
            Outcome = Ff7BattleOutcome.Victory,
            TerminalFrames = 45,
            Inventory = [new(0, 1)],
            Actors = entering.Actors.Select(actor => actor.Actor.Slot >= 4
                ? actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | Ff7NativeBattleActionResolver.Death } }
                : actor with { Actor = actor.Actor with { HitPoints = actor.Actor.HitPoints - 5, MagicPoints = (ushort)(actor.Actor.MagicPoints - 1) } }).ToArray()
        };
        Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 101, snapshot: finished);
        string before = JsonSerializer.Serialize(battle.Capture());
        Ff7Progress published = battle.PublishVitals(progress);
        Ff7VictoryProjection result = battle.ProjectVictory(progress, 12);
        Ff7VictoryProjection repeated = battle.ProjectVictory(result.Progress, 12);
        Check(result.Reward.Gil == 0 && result.Reward.Experience == 0 && result.Reward.AbilityPoints == 0 && result.Reward.Items.Length == 0, "Native request80 must suppress every enemy reward accumulator.");
        Check(result.Characters.Count == 0 && result.Materia.Count == 0 && result.Items.Count == 0, "A suppressed result must not execute growth or AP projection.");
        Check(JsonSerializer.Serialize(published) == JsonSerializer.Serialize(result.Progress with { CompletedBattles = published.CompletedBattles }), "Reward suppression changed battle vitals, consumed inventory, progress or RNG.");
        Check(result.Progress.Inventory.SequenceEqual(finished.Inventory) && result.Progress.Characters.Single(character => character.Id == 6).HitPoints == finished.Actors.Single(actor => actor.Identity == 9).Actor.HitPoints, "A suppressed result must still publish consumed inventory and native battle vitals.");
        Check(result.Progress.CompletedBattles.Length == progress.CompletedBattles.Length + 1 && JsonSerializer.Serialize(result.Progress) == JsonSerializer.Serialize(repeated.Progress), "A suppressed victory must retain exactly one completion receipt.");
        Ff7BattleReward preview = battle.PreviewVictory();
        Check(preview.Gil == 0 && preview.Experience == 0 && preview.AbilityPoints == 0 && preview.Items.Length == 0 && before == JsonSerializer.Serialize(battle.Capture()), "Suppressed preview must remain empty and preserve live battle state.");
        Ff7BattleSession ordinary = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 101, snapshot: finished with { RequestFlags = 0x20 });
        Ff7BattleReward ordinaryReward = ordinary.PreviewVictory();
        Check(ordinaryReward.Gil == 1400 && ordinaryReward.Experience == 900 && ordinaryReward.AbilityPoints == 110, "The independent request20 flag must not suppress native enemy rewards.");
        Console.WriteLine("Original request80 reward suppression, battle publication, preview and once-only receipt passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
