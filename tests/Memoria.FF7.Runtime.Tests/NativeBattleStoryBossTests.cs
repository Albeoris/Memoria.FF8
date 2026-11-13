using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleStoryBossTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original story boss resources: MEMORIA_FF7_GAME_DIR is unset.");
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
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2], Inventory = [new(1, 30), new(7, 20)] };
        SceneFormation[] aps = Find(37);
        SceneFormation[] reno = Find(48);
        Check(aps.Select(scene => scene.FormationId).SequenceEqual(new[] { 386, 387 }) && reno.Select(scene => scene.FormationId).SequenceEqual(new[] { 412, 413, 414 }), "The original Aps and Reno formation assignments changed.");

        foreach (SceneFormation formation in aps)
        {
            CheckAps(formation);
        }
        foreach (SceneFormation formation in reno)
        {
            CheckReno(formation);
        }

        SceneFormation[] Find(short id) => formations.Where(formation => formation.EnemyRewards.Any(enemy => enemy.EnemyId == id) && SceneBattleData.Read(formation).Enemies.Any(enemy => enemy.Placement.EnemyId == id)).ToArray();
        Ff7BattleSession Open(SceneFormation scene, Ff7BattleSnapshot? snapshot = null) => new(scene, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations);

        void CheckAps(SceneFormation formation)
        {
            Ff7BattleSession battle = Open(formation);
            Ff7BattleSession? restored = null;
            HashSet<ushort> waves = [];
            bool sadness = false;
            for (int step = 0; step < 30000 && !battle.ResultReady; step++)
            {
                battle.Advance(turnBased: true);
                if (restored is not null)
                {
                    restored.Advance(turnBased: true);
                    Check(Snapshot(battle) == Snapshot(restored), "Aps changed after restoring its queued original Tsunami.");
                    restored = null;
                }
                if (battle.Action?.Command.AttackId is 0x144 or 0x145 && battle.Action is { } action && waves.Add(action.Command.AttackId!.Value))
                {
                    restored = Open(formation, Cold(battle));
                }
                sadness |= battle.Actors.Any(actor => actor.Actor.Slot < 3 && (actor.Actor.Statuses & Ff7NativeBattleStatuses.Sadness) != 0);
                if (battle.ReadyPlayer is int slot)
                {
                    SubmitTurn(battle, slot, 4);
                }
            }
            Check(waves.SetEquals(new ushort[] { 0x144, 0x145 }) && sadness && battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory, $"Original Aps {formation.FormationId} did not finish both Tsunamis, Lick and victory. Waves={string.Join(',', waves)}, sadness={sadness}, outcome={battle.Outcome}.");
        }

        void CheckReno(SceneFormation formation)
        {
            using Ff7Session host = Ff7Session.Restore(installation, original.Capture() with { Progress = progress with { FullCombatEnabled = true } });
            host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand((ushort)formation.FormationId)), 0);
            Ff7BattleSession battle = host.Battle!;
            Ff7SessionSave entry = host.Capture();
            Ff7BattleSnapshot entryBattle = battle.Capture();
            for (int index = 0; index < entryBattle.Actors.Length && entryBattle.Actors[index].Actor.Slot < 3; index++)
            {
                Ff7BattleActorState actor = entryBattle.Actors[index];
                entryBattle.Actors[index] = actor with { Actor = actor.Actor with { HitPoints = 2000, MaxHitPoints = 2000 } };
            }
            using Ff7Session combatHost = Ff7Session.Restore(installation, entry with { Battle = entryBattle });
            battle = combatHost.Battle!;
            Check(battle.Capture().ActorModels.Where(model => model.Slot >= 5).All(model => !model.Modifiers!.Visible), "Original Pyramid slots must start with their models hidden.");
            Ff7BattleSession? restored = null;
            int? capturedSlot = null;
            bool released = false;
            bool restoredFade = false;
            int? appearedSlot = null;
            bool retired = false;
            bool restoredEscape = false;
            bool absentClip = false;
            Ff7BattleMotion? lastMotion = null;
            int escapeSteps = 0;
            long? missingClipTick = null;
            for (int step = 0; step < 30000 && !battle.ResultReady; step++)
            {
                battle.Advance(turnBased: true);
                if (restored is not null)
                {
                    restored.Advance(turnBased: true);
                    Check(Snapshot(battle) == Snapshot(restored), "Reno's live animation changed after JSON restore.");
                    restored = null;
                    restoredFade = true;
                }
                if (battle.Action is { Command.ActorSlot: 4, Motion: { Step: { X: 20, Z: -300 }, Remaining: 20 } } && !restoredEscape)
                {
                    using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(combatHost.Capture()))!);
                    battle.Advance(turnBased: true);
                    cold.Battle!.Advance(turnBased: true);
                    Check(Snapshot(battle) == Snapshot(cold.Battle), "Reno's fixed escape movement changed after a cold session restore.");
                    restoredEscape = true;
                }
                Ff7BattleActorModelState renoModel = battle.Capture().ActorModels.Single(model => model.Slot == 4);
                Ff7BattleMotion? currentMotion = battle.Action?.Motion is { WrapHorizontalPosition: true } activeMotion ? activeMotion : renoModel.Motion;
                if (currentMotion is null && lastMotion is { Remaining: 1 } retiringMotion && renoModel.Position == retiringMotion.Position + retiringMotion.Step)
                {
                    currentMotion = retiringMotion.Advance();
                }
                if (currentMotion is { Step: { X: 20, Z: -300 } } motion && lastMotion != motion)
                {
                    Check(lastMotion is null ? motion.Remaining == 35 : motion.Remaining == lastMotion.Remaining - 1 && motion.Position == lastMotion.Position + motion.Step, "Reno's native motion must add its fixed signed offsets once per callback.");
                    lastMotion = motion;
                    escapeSteps++;
                }
                if (battle.Action?.Pose?.MissingClip == 0x10)
                {
                    missingClipTick ??= battle.Tick;
                }
                else if (missingClipTick is long missing && battle.Tick == missing + 4)
                {
                    absentClip = !renoModel.Modifiers!.Visible;
                }
                if (!restoredFade && battle.Capture().ActorModels.Any(model => model.Fade is { Remaining: < 14 }))
                {
                    restored = new(formation, combatHost.Progress, kernel, executable.NativeMateriaStatModifiers, battle.RequestId, snapshot: Cold(battle), animations: animations, cameraScripts: Ff7BattleCameraResources.LoadScripts(installation, battle.Layout));
                }
                if (battle.Actors.FirstOrDefault(actor => actor.Actor.Slot < 3 && (actor.Actor.Statuses & Ff7NativeBattleStatuses.Imprisoned) != 0) is { } captured)
                {
                    capturedSlot ??= captured.Actor.Slot;
                    Check(!battle.Commands(captured.Actor.Slot).Any(), "An imprisoned party member must have no turn menu.");
                }
                if (capturedSlot is int prisoner && battle.LastHit is { Source: < 3, Target: >= 5, Damage.Missed: false } && (Actor(battle, prisoner).Actor.Statuses & Ff7NativeBattleStatuses.Imprisoned) == 0)
                {
                    released = true;
                }
                Ff7BattleActorModelState[] models = battle.Capture().ActorModels;
                appearedSlot ??= models.FirstOrDefault(model => model.Slot >= 5 && model.Modifiers!.Visible && model.Position is not null)?.Slot;
                if (appearedSlot is int modelSlot)
                {
                    Ff7BattleActorModelState pyramidModel = models.Single(model => model.Slot == modelSlot);
                    retired |= released && restoredFade && !pyramidModel.Modifiers!.Visible && pyramidModel.Fade is null && battle.Action is null;
                }
                if (battle.ReadyPlayer is int slot)
                {
                    Ff7BattleActorState? pyramid = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 5 && actor.Actor.HitPoints > 0 && (actor.Actor.Conditions & 8) != 0);
                    SubmitTurn(battle, slot, pyramid?.Actor.Slot ?? 4);
                }
            }
            Check(capturedSlot is not null && retired && restoredEscape && absentClip && escapeSteps == 36 && battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory, $"Original Reno {formation.FormationId} did not finish capture, Pyramid appearance, release and fade retirement. Capture={capturedSlot}, released={released}, appeared={appearedSlot}, fade={restoredFade}, retired={retired}, restoredEscape={restoredEscape}, absentClip={absentClip}, movement={escapeSteps}, outcome={battle.Outcome}, action={battle.Action?.Command}.");
            string before = JsonSerializer.Serialize(combatHost.Capture());
            Ff7VictoryProjection preview = combatHost.PreviewBattleVictory();
            Check(JsonSerializer.Serialize(combatHost.Capture()) == before, "Previewing Reno's victory must preserve the encounter and progress.");
            Check(combatHost.ConfirmBattleVictory() && combatHost.Battle is null, "Reno's victory must publish the field battle result.");
            string settled = JsonSerializer.Serialize(combatHost.Progress);
            Check(settled == JsonSerializer.Serialize(preview.Progress) && !combatHost.ConfirmBattleVictory() && JsonSerializer.Serialize(combatHost.Progress) == settled, "Reno's settlement must agree with the preview and publish only once.");
            combatHost.Host.ResumeEncounter();
        }
    }

    private static void SubmitTurn(Ff7BattleSession battle, int slot, int target)
    {
        Ff7BattleActorState? fallen = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot < 3 && actor.Actor.HitPoints == 0);
        Ff7BattleActorState? injured = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot < 3 && (actor.Actor.Statuses & Ff7NativeBattleStatuses.Imprisoned) == 0 && actor.Actor.HitPoints is > 0 and < 120);
        if (fallen is not null && battle.Submit(slot, Ff7BattleCommand.Item, 7, (ushort)(1 << fallen.Actor.Slot)))
        {
            return;
        }
        if (injured is not null && battle.Submit(slot, Ff7BattleCommand.Item, 1, (ushort)(1 << injured.Actor.Slot)))
        {
            return;
        }
        if (battle.Submit(slot, Ff7BattleCommand.Attack, 0, (ushort)(1 << target)))
        {
            return;
        }
        Check(battle.Submit(slot, Ff7BattleCommand.Defend, 0, (ushort)(1 << slot)), "A story boss turn must admit a valid party command.");
    }

    private static Ff7BattleActorState Actor(Ff7BattleSession battle, int slot) => battle.Actors.Single(actor => actor.Actor.Slot == slot);
    private static string Snapshot(Ff7BattleSession battle) => JsonSerializer.Serialize(battle.Capture());
    private static Ff7BattleSnapshot Cold(Ff7BattleSession battle) => JsonSerializer.Deserialize<Ff7BattleSnapshot>(Snapshot(battle))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
