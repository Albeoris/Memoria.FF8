using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleRubyPhaseTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Ruby Weapon phase resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        CheckParalysisClock();
        SceneFormation[] selected = formations.Where(formation => formation.EnemyRewards.Any(enemy => enemy.EnemyId == 11) && SceneBattleData.Read(formation).Enemies.Any(enemy => enemy.Placement.EnemyId == 11)).ToArray();
        Check(selected.Select(formation => formation.FormationId).SequenceEqual(new[] { 982, 983 }), "Ruby Weapon's original scene allocations changed.");
        foreach (SceneFormation formation in selected)
        {
            RunSingleSurvivor(formation);
            RunThreeSurvivors(formation);
        }

        void RunSingleSurvivor(SceneFormation formation)
        {
            Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
            Ff7BattleSession Open(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);
            Ff7BattleSession battle = Open();
            Check((Actor(battle, 4).Actor.Conditions & 0x300) == 0x300 && (Actor(battle, 5).Actor.Conditions & 8) == 0, "Ruby Weapon must start protected with hidden tentacles.");
            AdvanceUntil(battle, session => session.Action?.Command.AttackId == 0x3D0);
            Check((Actor(battle, 4).Actor.Conditions & 0x300) == 0 && (Actor(battle, 5).Actor.Conditions & 8) != 0 && (Actor(battle, 6).Actor.Conditions & 8) != 0, "One available character must drive the original phase and expose both tentacles.");
            AdvanceUntil(battle, session => session.Action?.Command.AttackId == 0x3CE && session.Capture().ActorModels.Any(model => model.LinkedAction));
            Ff7BattleSession restored = Open(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            bool reaction = false;
            for (int step = 0; step < 400 && battle.Action is not null; step++)
            {
                Tick(battle);
                Tick(restored);
                Ff7BattleSnapshot current = battle.Capture();
                reaction |= current.PrimaryEffects?.TargetReactions.Any(hit => hit.Target == 0) == true;
                Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(restored.Capture()), "Ruby Weapon's linked target reaction diverged after cold restore.");
            }
            Check(reaction && battle.Action is null, "The tentacle's F7 callback must publish a reaction for the original primary target and retire.");
        }

        void RunThreeSurvivors(SceneFormation formation)
        {
            Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
            Ff7BattleSession battle = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, animations: animations);
            AdvanceUntil(battle, session => session.Action?.Command.AttackId == 0x3C7 && (Actor(session, 1).Actor.Statuses & Ff7NativeBattleStatuses.Paralysis) != 0);
            Check((Actor(battle, 4).Actor.Conditions & 0x300) == 0x300 && (Actor(battle, 5).Actor.Conditions & 8) == 0, "Three available characters must keep Ruby Weapon in its opening phase.");
            Check(Actor(battle, 1).Timers.Paralysis == 20, "Ruby Weapon's original attack must initialize the native twenty-tick Paralysis timer.");
        }
    }

    private static void CheckParalysisClock()
    {
        const uint paralysis = Ff7NativeBattleStatuses.Paralysis;
        Ff7BattleStatusTimers started = Ff7NativeBattleStatusClock.Apply(default, 0, paralysis);
        Check(started.Paralysis == 20, "The original Paralysis timer must initialize to twenty.");
        Ff7BattleStatusTimers due = started with { Remainder = 8191, Paralysis = 1 };
        Ff7BattleStatusTick stopped = Ff7NativeBattleStatusClock.Advance(due, paralysis | 0x400, 0, globalTick: true);
        Check(stopped.Timers.Paralysis == 1 && (stopped.Statuses & paralysis) != 0, "The global tick must not expire Paralysis while its variable clock is stopped.");
        Ff7BattleStatusTick expired = Ff7NativeBattleStatusClock.Advance(JsonSerializer.Deserialize<Ff7BattleStatusTimers>(JsonSerializer.Serialize(due)), paralysis, 0);
        Check(expired.Timers.Paralysis == 0 && (expired.Statuses & paralysis) == 0, "The next variable tick must expire restored Paralysis.");
    }

    private static Ff7BattleActorState Actor(Ff7BattleSession battle, int slot) => battle.Actors.Single(actor => actor.Actor.Slot == slot);

    private static void AdvanceUntil(Ff7BattleSession battle, Func<Ff7BattleSession, bool> reached)
    {
        for (int step = 0; step < 1500; step++)
        {
            Tick(battle);
            if (reached(battle))
            {
                return;
            }
        }
        throw new InvalidOperationException($"Ruby Weapon did not reach the expected phase by tick {battle.Tick}.");
    }

    private static void Tick(Ff7BattleSession battle)
    {
        battle.Advance(turnBased: true);
        if (battle.ReadyPlayer is int slot)
        {
            Check(battle.Submit(slot, Ff7BattleCommand.Defend, 0, (ushort)(1 << slot)), "Ruby Weapon test party could not Defend.");
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
