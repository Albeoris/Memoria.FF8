using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleUvOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_BATTLE_UV_SAVE") ?? throw new InvalidOperationException("Select the original pre-battle field fixture.");
        using FileStream input = File.OpenRead(path);
        using Ff7Session source = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        foreach (int formationId in new[] { 716, 717, 718 })
        {
            SceneFormation formation = formations.Single(formation => formation.FormationId == formationId);
            Ff7BattleSession battle = new(formation, source.Progress, kernel, materia, 1, animations: animations);
            int icicle = SceneBattleData.Read(formation).Enemies.Single(enemy => enemy.Placement.EnemyId == 217).FormationSlot + 4;
            Ff7BattleSnapshot initialized = battle.Capture();
            Ff7BattleActorModelState actor = initialized.ActorModels.Single(model => model.Slot == icicle);
            initialized.ActorModels[Array.IndexOf(initialized.ActorModels, actor)] = actor with { Action = 5, Reaction = true, Ready = false, Pose = null, PendingSince = -1 };
            battle = new(formation, source.Progress, kernel, materia, 1, snapshot: initialized, animations: animations);
            Ff7BattleSession? fork = null;
            bool wroteUv = false;
            int comparisons = 0;
            int elapsed = 0;
            for (; elapsed < 120 && comparisons < 10; elapsed++)
            {
                battle.Advance(waitForCommand: true);
                if (fork is not null)
                {
                    fork.Advance(waitForCommand: true);
                    Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(fork.Capture()), "A private UV callback fork changed its next native update.");
                    comparisons++;
                }
                Ff7BattleSnapshot current = battle.Capture();
                wroteUv |= current.ActorModels.Any(model => model.NativeUvOffset is not null);
                if (fork is null && current.SecondaryEffects?.Slots.Any(callback => callback is Ff7BattleActorUvAnimationCallback) == true && wroteUv)
                {
                    fork = new(formation, source.Progress, kernel, materia, 1, snapshot: current, animations: animations);
                }
            }
            Console.WriteLine($"Original UV formation{formationId}: ticks={elapsed}, uv={wroteUv}, privateForkSteps={comparisons}, action={battle.Action?.Animation}, outcome={battle.Outcome}");
            Check(wroteUv && comparisons == 10, "An original icicle formation did not run and retain its native actor UV callback.");
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
