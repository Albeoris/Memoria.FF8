using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class SpecialEnemyInitializationTests
{
    private static readonly HashSet<short> Selected = [0, 1, 2, 4, 5, 6, 7, 8, 9, 12, 14, 15, 49, 50, 54, 65, 70, 78, 92, 138, 150, 153, 165, 166, 199, 213, 214, 217, 228, 229, 236, 241, 242, 243, 255, 256, 259, 260, 288, 300, 307, 308, 310, 319, 332, 339, 340, 341, 342, 343, 344, 347, 349, 350, 353, 354, 355, 356, 357, 358];
    private sealed record Boundary(Ff7BattleAiActor[] Actors, Dictionary<int, byte[]> Locals, Ff7BattleRngState Random, byte[] GlobalMemory, byte[] SaveBank1)
    {
        public byte[] FormationMemory { get; init; } = [];
        public Ff7BattleAiCommand[] Commands { get; init; } = [];
        public Ff7BattleAiTransfer[] Transfers { get; init; } = [];
    }

    public static void Run()
    {
        Ff7EnemyExtensionDocument document = Ff7EnemyExtensionDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "enemy-extensions.json")));
        Ff7EnemyExtensionCatalog catalog = new([document]);
        Check(Selected.Count == 60 && Selected.All(id => catalog.Find(id)?.ScaleLevel is not null), "Every selected identity needs an explicit level policy in the bundled content.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed special enemy matrix: MEMORIA_FF7_GAME_DIR unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> scenes = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        CheckIndependentChannels(scenes.Single(scene => scene.FormationId == 300), original.Progress, kernel, executable.NativeMateriaStatModifiers);
        SceneFormation[] selected = scenes.Where(scene => scene.EnemyRewards.Any(enemy => Selected.Contains(enemy.EnemyId)) && SceneBattleData.Read(scene).Enemies.Any(enemy => Selected.Contains(enemy.Placement.EnemyId))).ToArray();
        HashSet<short> reached = selected.SelectMany(scene => SceneBattleData.Read(scene).Enemies).Select(enemy => enemy.Placement.EnemyId).Where(Selected.Contains).ToHashSet();
        Check(Selected.SetEquals(reached), $"Selected native placement coverage changed: {string.Join(",", Selected.Except(reached))}.");
        List<string> failures = [];
        int sessions = 0;
        int callbacks = 0;
        Attempt("final duel native callback queue and termination", () => CheckFinalDuel(scenes, original.Progress, kernel, executable.NativeMateriaStatModifiers, catalog));
        Attempt("Schizo pre-action callback continuation", () => CheckPreActionScripts(scenes, original.Progress, kernel, executable.NativeMateriaStatModifiers, catalog));
        foreach (SceneFormation scene in selected)
        {
            Attempt($"formation {scene.FormationId} initialization/restore", () =>
            {
                Ff7BattleSession battle = new(scene, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [100]);
                CheckProjection(SceneBattleData.Read(scene), battle, catalog);
                Ff7BattleSnapshot snapshot = RoundTrip(battle.Capture());
                Ff7BattleSession restored = new(scene, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, enemyExtensions: Ff7EnemyExtensionCatalog.Empty, enemyExtensionPartyLevels: [1]);
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Resolved generation, actor state or RNG changed on restore.");
                for (int frame = 0; frame < 16; frame++)
                {
                    battle.Advance(turnBased: true);
                    restored.Advance(turnBased: true);
                }
                Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Bounded session continuation diverged after JSON restore.");
                sessions++;
            });
            Attempt($"formation {scene.FormationId} relative HP branches", () => CheckRelativeHpBranches(scene, kernel));
            foreach (bool scaled in new[] { false, true })
            {
                Attempt($"formation {scene.FormationId} native callbacks ({(scaled ? "grown" : "baseline")})", () => callbacks += CheckCallbacks(scene, kernel, scaled, original.Progress.Gil));
            }
        }
        if (failures.Count != 0)
        {
            throw new InvalidOperationException($"Special enemy matrix: {sessions}/{selected.Length} sessions; {callbacks} callbacks; {failures.Count} failures.\n{string.Join("\n", failures)}");
        }
        Console.WriteLine($"Special enemy matrix: 60 IDs, {selected.Length} linked formations, {callbacks} native callbacks, JSON continuation and 32 independent policy combinations passed.");

        void Attempt(string name, Action test)
        {
            try
            {
                test();
            }
            catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException)
            {
                failures.Add($"{name}: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private static void CheckPreActionScripts(IReadOnlyList<SceneFormation> scenes, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7EnemyExtensionCatalog catalog)
    {
        SceneFormation formation = scenes.Single(scene => scene.FormationId == 740);
        Ff7BattleSession initial = new(formation, progress, kernel, modifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [100]);
        Ff7BattleSnapshot before = initial.Capture();
        Ff7BattleSnapshot queued = before with { Queue = Ff7BattleCommandQueue.Enqueue(before.Queue, new(2, 0, (byte)Ff7BattleCommand.Defend, 0, 1)) };
        Ff7BattleSession battle = new(formation, progress, kernel, modifiers, 1, snapshot: queued);
        Ff7BattleSession restored = new(formation, progress, kernel, modifiers, 1, snapshot: RoundTrip(queued));
        battle.Advance(turnBased: true);
        restored.Advance(turnBased: true);
        Check(battle.Actors.Where(actor => actor.Identity is 228 or 229).All(actor => actor.Actor.HurtAction == 1), "The original paired-head callback 7 must run before the queued action.");
        Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Pre-action scripts changed their continuation after JSON restore.");
    }

    private static void CheckFinalDuel(IReadOnlyList<SceneFormation> scenes, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7EnemyExtensionCatalog catalog)
    {
        foreach (int formationId in new[] { 925, 927 })
        {
            SceneFormation formation = scenes.Single(scene => scene.FormationId == formationId);
            Ff7Progress party = progress with { Party = [0, 255, 255] };
            Ff7BattleSession initial = new(formation, party, kernel, modifiers, 1, enemyExtensions: catalog, enemyExtensionPartyLevels: [100]);
            Ff7BattleSnapshot snapshot = initial.Capture();
            Ff7BattleActorState sephiroth = snapshot.Actors.Single(actor => actor.Identity == 332);
            Check(sephiroth.Actor.HitPoints == 1 && initial.EnemyExtension(sephiroth.Actor.Slot)!.DrawSlots.Length == 0, "The symbolic final enemy must retain its original HP and empty Draw pool.");
            Ff7BattleCommandQueueState queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, new(0, sephiroth.Actor.Slot, 0x22, 2, (ushort)(1 << sephiroth.Actor.Slot)));
            snapshot = snapshot with { Queue = queue };
            Ff7BattleSession battle = new(formation, party, kernel, modifiers, 1, snapshot: snapshot);
            Ff7BattleSession restored = new(formation, party, kernel, modifiers, 1, snapshot: RoundTrip(snapshot));
            int doneInActions = 0;
            long lastDoneInStart = -1;
            for (int frame = 0; frame < 2048 && !battle.ResultReady; frame++)
            {
                battle.Advance(turnBased: true);
                restored.Advance(turnBased: true);
                Ff7BattleActionState? active = battle.Capture().Action;
                if (active?.Command.AttackId == 0x3C0 && active.StartedAt != lastDoneInStart)
                {
                    lastDoneInStart = active.StartedAt;
                    doneInActions++;
                }
            }
            Ff7BattleSnapshot completed = battle.Capture();
            Check(battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory, $"The final general counter must clear battle participation, drain the native queue and finish. Formation {formationId}; tick {completed.Tick}; outcome {completed.Outcome}; terminal {completed.TerminalFrames}; intro {JsonSerializer.Serialize(completed.Intro)}; action {JsonSerializer.Serialize(completed.Action)}; actors {JsonSerializer.Serialize(completed.Actors.Select(actor => new { actor.Identity, actor.Actor.Slot, actor.Actor.HitPoints, actor.Actor.Statuses, actor.Actor.Conditions }))}; queue {JsonSerializer.Serialize(completed.Queue.Slots.Where(entry => entry is not null))}; ready {completed.Queue.ReadyRead}/{completed.Queue.ReadyWrite}; globals {Convert.ToHexString(completed.AiGlobalMemory ?? [])}.");
            // Native hit masks exclude the acting unit, so the self-directed Done-in cannot recursively invoke the general counter.
            Check(doneInActions == 1, $"The final general counter must queue exactly one Done-in action, received {doneInActions}.");
            Check(completed.AiGlobalMemory is not null && (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(completed.AiGlobalMemory.AsSpan(0x22)) & 8) != 0, "The final queue must execute original callback 15 before publishing the outcome.");
            Check(completed.Queue.Slots.All(entry => entry is null) && completed.Queue.ReadyRead == completed.Queue.ReadyWrite && completed.Action is null, "The final native callback queue must drain before termination.");
            Check(JsonSerializer.Serialize(completed) == JsonSerializer.Serialize(restored.Capture()), "The final native callback chain diverged after JSON restore.");
            Ff7BattleReward reward = battle.PreviewVictory();
            Check(reward.Experience == 0 && reward.Gil == 0 && reward.AbilityPoints == 0 && reward.Items.Length == 0, "The final encounter must retain its zero original rewards.");
        }
    }

    private static void CheckIndependentChannels(SceneFormation native, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers)
    {
        SceneFormation scene = native with { EnemyRewards = native.EnemyRewards.Select(enemy => enemy with { Experience = 17, Gil = 23, AbilityPoints = 3, Items = [new(0, 63)] }).ToArray() };
        SceneBattleData baseline = SceneBattleData.Read(scene);
        foreach (int flags in Enumerable.Range(0, 32))
        {
            bool level = (flags & 1) != 0;
            bool hp = (flags & 2) != 0;
            bool draw = (flags & 4) != 0;
            bool reward = (flags & 8) != 0;
            bool item = (flags & 16) != 0;
            Ff7EnemyExtensionDelta[] deltas = baseline.Enemies.Select(enemy => new Ff7EnemyExtensionDelta
            {
                EnemyId = enemy.Placement.EnemyId,
                ScaleLevel = level,
                Growth = hp ? new() { ["hp"] = [1000000, 1000000, 0, 0] } : null,
                DrawTiers = draw ? [new(0, [new(1, 0)]), new(1, [new(2, 0)])] : null,
                RewardGrowth = reward ? new() { ["exp"] = [1000000, 1000000, 0, 0] } : null,
                ItemReplacements = item ? [new(1, "drop", 0, 1)] : null
            }).DistinctBy(delta => delta.EnemyId).ToArray();
            Ff7BattleSession battle = new(scene, progress, kernel, modifiers, 1, enemyExtensions: new([new(1, "independent-channels", deltas)]), enemyExtensionPartyLevels: [100]);
            foreach (SceneBattleEnemy enemy in baseline.Enemies)
            {
                Ff7ResolvedEnemyExtension resolved = battle.EnemyExtension(enemy.FormationSlot + 4)!;
                Check(resolved.Level == (level ? Math.Max(100, (int)enemy.Level) : enemy.Level), $"Independent level policy failed for combination {flags}.");
                uint expectedHp = hp && level ? Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.MaxHitPoints, 100, enemy.Level, [1000000, 1000000, 0, 0], int.MaxValue) : enemy.MaxHitPoints;
                Check(resolved.Hp == expectedHp, $"HP depends on an unrelated channel for combination {flags}.");
                Check(resolved.Experience == (reward && level ? 34u : 17u), $"EXP depends on an unrelated channel for combination {flags}.");
                Check(resolved.DrawSlots.Select(slot => slot.MagicId).SequenceEqual(draw ? new byte[] { level ? (byte)2 : (byte)1 } : []), $"Draw depends on an unrelated channel for combination {flags}.");
                Check(resolved.Items[0].ItemId == (item && level ? 1 : 0) && resolved.Items[0].Rate == 63, $"Item identity or native probability changed for combination {flags}.");
            }
        }
    }

    private static void CheckProjection(SceneBattleData native, Ff7BattleSession battle, Ff7EnemyExtensionCatalog catalog)
    {
        foreach (SceneBattleEnemy enemy in native.Enemies)
        {
            Ff7EnemyExtensionDelta? delta = catalog.Find(enemy.Placement.EnemyId);
            if (delta is null)
            {
                continue;
            }
            Ff7ResolvedEnemyExtension resolved = battle.EnemyExtension(enemy.FormationSlot + 4)!;
            byte level = delta.ScaleLevel == true ? (byte)Math.Max(100, (int)enemy.Level) : enemy.Level;
            Check(resolved.Level == level, $"Enemy {enemy.Placement.EnemyId} level policy changed.");
            uint hp = Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.MaxHitPoints, level, enemy.Level, delta.Growth?.GetValueOrDefault("hp"), int.MaxValue);
            Check(resolved.Hp == hp, $"Enemy {enemy.Placement.EnemyId} HP projection changed.");
            Ff7EnemyDrawSlot[] expected = delta.DrawTiers?.LastOrDefault(tier => tier.MinimumDelta <= level - enemy.Level)?.Slots ?? [];
            Check(resolved.DrawSlots.SequenceEqual(expected), $"Enemy {enemy.Placement.EnemyId} Draw policy changed.");
            uint experience = Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.Rewards.Experience, level, enemy.Level, delta.RewardGrowth?.GetValueOrDefault("exp"), uint.MaxValue);
            uint gil = Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.Rewards.Gil, level, enemy.Level, delta.RewardGrowth?.GetValueOrDefault("gil"), uint.MaxValue);
            uint ap = Ff7EnemyExtensionCatalog.ScaleUnsigned(enemy.Rewards.AbilityPoints, level, enemy.Level, delta.RewardGrowth?.GetValueOrDefault("ap"), ushort.MaxValue);
            Check(resolved.Experience == experience && resolved.Gil == gil && resolved.AbilityPoints == ap, $"Enemy {enemy.Placement.EnemyId} reward policy changed.");
            for (int itemIndex = 0; itemIndex < enemy.Rewards.Items.Count; itemIndex++)
            {
                SceneItemSlot item = enemy.Rewards.Items[itemIndex];
                string channel = (item.Rate & 128) == 0 ? "drop" : "steal";
                Ff7EnemyItemReplacement? replacement = delta.ItemReplacements?.Where(value => value.Channel == channel && value.FromItemId == item.ItemId && value.MinimumDelta <= level - enemy.Level).OrderBy(value => value.MinimumDelta).LastOrDefault();
                Check(resolved.Items[itemIndex].ItemId == (replacement?.ToItemId ?? item.ItemId) && resolved.Items[itemIndex].Rate == item.Rate, $"Enemy {enemy.Placement.EnemyId} changed its item channel or native rate.");
            }
        }
    }

    private static int CheckCallbacks(SceneFormation scene, IReadOnlyList<KernelSection> kernel, bool scaled, uint gil)
    {
        SceneBattleData data = SceneBattleData.Read(scene);
        if (scaled)
        {
            data = data with { Enemies = data.Enemies.Select(enemy => enemy with { MaxHitPoints = checked(enemy.MaxHitPoints * 2), MaxMagicPoints = enemy.MaxMagicPoints }).ToArray() };
        }
        Ff7BattleAiActor[] actors = CreateActors(data);
        byte[] globals = new byte[64];
        byte layout = Ff7NativeAtb.ResolveLayout(scene.Layout, scene.BattleFlags, false);
        globals[4] = (byte)Ff7NativeAtb.BattleTypeForLayout(layout);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x28), (ushort)scene.FormationId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(globals.AsSpan(0x2C), scene.BattleFlags);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(globals.AsSpan(0x38), gil);
        actors = Ff7NativeAtb.ApplyFormationConditions(layout, actors, data);
        Boundary state = new(actors, [], Ff7BattleRng.BeginBattle(), globals, new byte[256]);
        int count = 0;
        foreach (SceneBattleEnemy enemy in data.Enemies)
        {
            state = Execute(state, enemy.FormationSlot + 4, 0);
        }
        if (data.FormationScripts is not null)
        {
            state = Execute(state, 3, 0);
        }
        for (int turn = 0; turn < 3; turn++)
        {
            foreach (SceneBattleEnemy enemy in data.Enemies)
            {
                state = Execute(state, enemy.FormationSlot + 4, 1);
            }
        }
        foreach (SceneBattleEnemy enemy in data.Enemies)
        {
            int slot = enemy.FormationSlot + 4;
            foreach (byte callback in new byte[] { 2, 3, 4, 5, 6, 7 })
            {
                if (enemy.Scripts is null || enemy.Scripts[callback].IsEmpty)
                {
                    continue;
                }
                Boundary hit = state with { Actors = state.Actors.Select(actor => actor.Slot == slot ? actor with { HitPoints = callback == 3 ? 0 : Math.Max(1, actor.MaxHitPoints / 4), Statuses = callback == 3 ? actor.Statuses | 1u : actor.Statuses, LastAttackerMask = 1 } : actor).ToArray() };
                if (enemy.Placement.EnemyId == 258 && callback == 3)
                {
                    hit = hit with { Actors = hit.Actors.Select(actor => actor.EnemyId is 259 or 260 ? actor with { ElementHalve = 1, CaptureMask = actor.EnemyId == 259 ? 1u : 2u } : actor).ToArray() };
                }
                Boundary restored = RoundTrip(hit);
                Check(JsonSerializer.Serialize(Execute(hit, slot, callback)) == JsonSerializer.Serialize(Execute(restored, slot, callback)), $"Actor {slot}, callback {callback} diverged after JSON restore.");
            }
        }
        return count;

        Boundary Execute(Boundary before, int slot, byte callback)
        {
            Ff7BattleRng random = new(kernel.Single(section => section.Type == 2).Data.Span, before.Random);
            Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(before.Actors, 0);
            Ff7BattleAiResult result;
            try
            {
                Ff7BattleAiContext context = new(slot, before.Actors, masks.Targetable, masks.KnockedOut, masks.ExcludedOpponents, callback, GlobalMemory: before.GlobalMemory, SaveBank1: before.SaveBank1);
                result = slot == 3 ? Ff7BattleAi.Run(data.FormationScripts![callback].Span, context, before.FormationMemory, data, random, kernel.Single(section => section.Type == 1).Data) : Ff7BattleAi.RunActorCallback(context, before.Locals.GetValueOrDefault(slot), data, random, kernel.Single(section => section.Type == 1).Data);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException)
            {
                short identity = slot == 3 ? (short)-1 : data.Enemies.Single(enemy => enemy.FormationSlot + 4 == slot).Placement.EnemyId;
                throw new InvalidOperationException($"Enemy {identity}, actor {slot}, callback {callback}: {exception.Message}", exception);
            }
            foreach (Ff7BattleAiCommand command in result.Commands)
            {
                Check(command.ActorSlot is >= 0 and < 10 && command.ActorSlot != 3 && (masks.Allocated & (1 << command.ActorSlot)) != 0 && (command.TargetMask & ~masks.Allocated) == 0, $"Actor {slot}, callback {callback} references absent source {command.ActorSlot} or targets 0x{command.TargetMask:X4}.");
                if (command.CommandId == 0x20 && command.AttackId >= 0x100)
                {
                    Check(data.Attacks.Any(attack => attack.Id == command.AttackId && attack.Slot == command.SceneAttackSlot), $"Actor {slot}, callback {callback} changed its native scene selector.");
                }
            }
            short callbackEnemy = slot == 3 ? (short)-1 : data.Enemies.Single(enemy => enemy.FormationSlot + 4 == slot).Placement.EnemyId;
            if (callbackEnemy == 258 && callback == 3)
            {
                int right = data.Enemies.Single(enemy => enemy.Placement.EnemyId == 259).FormationSlot + 4;
                int left = data.Enemies.Single(enemy => enemy.Placement.EnemyId == 260).FormationSlot + 4;
                Check(result.Commands.Any(command => command.ActorSlot == right && command.AttackId == 0x2F6 && command.TargetMask == 1) && result.Commands.Any(command => command.ActorSlot == left && command.AttackId == 0x2F7 && command.TargetMask == 2), "Carry Armor's original death script must redirect each release to its capturing arm and captive.");
            }
            if (callbackEnemy == 332 && callback == 1)
            {
                Check(result.Commands.Any(command => command.ActorSlot == 0 && command.CommandId == 0x01 && command.TargetMask == (1 << slot)), "The final original script must redirect Cloud's Attack command against Sephiroth.");
            }
            Dictionary<int, byte[]> locals = new(before.Locals);
            if (slot >= 4)
            {
                locals[slot] = result.LocalMemory;
            }
            count++;
            foreach (Ff7BattleAiTransfer transfer in result.TransferUpdates)
            {
                Check(before.Actors.Any(actor => actor.Slot == transfer.SourceSlot) && before.Actors.Any(actor => actor.Slot == transfer.DestinationSlot), "Native successor transfer referenced an absent actor.");
            }
            byte[] saveBank1 = before.SaveBank1.ToArray();
            foreach (var write in result.SaveBank1Writes)
            {
                saveBank1[write.Key] = write.Value;
            }
            return new(before.Actors.Select(actor => result.ActorUpdates.SingleOrDefault(update => update.Slot == actor.Slot) ?? actor).ToArray(), locals, random.Capture(), result.GlobalMemory, saveBank1) { FormationMemory = slot == 3 ? result.LocalMemory : before.FormationMemory, Commands = result.Commands, Transfers = result.TransferUpdates };
        }
    }

    private static Ff7BattleAiActor[] CreateActors(SceneBattleData data)
    {
        List<Ff7BattleAiActor> actors = [new(0, 0, 8, 100, 100, 1000, 1000, Variant: 0x10, Level: 50, Strength: 30, Magic: 30, Speed: 30, HitEffect: 8), new(1, 0, 8, 100, 100, 1000, 1000, Variant: 0x11, Level: 50, Strength: 30, Magic: 30, Speed: 30, HitEffect: 8), new(2, 0, 8, 100, 100, 1000, 1000, Variant: 0x12, Level: 50, Strength: 30, Magic: 30, Speed: 30, HitEffect: 8)];
        foreach (Ff7NativeEnemyState native in Ff7NativeEnemyInitialization.Create(data))
        {
            SceneBattleEnemy enemy = data.Enemies.Single(item => item.FormationSlot + 4 == native.ActorSlot);
            uint Elements(byte rate) => enemy.Affinities.Where(affinity => affinity.Type < 16 && affinity.Rate == rate).Aggregate(0u, (mask, affinity) => mask | (1u << affinity.Type));
            actors.Add(new(native.ActorSlot, native.Statuses, native.Conditions, native.MagicPoints, native.MaxMagicPoints, native.HitPoints, native.MaxHitPoints, native.Defense, native.MagicDefense, IdleAction: native.IdleAction, HurtAction: native.HurtAction, Variant: (byte)data.Enemies.Count(other => other.FormationSlot < enemy.FormationSlot && other.Placement.EnemyId == native.EnemyId), Level: enemy.Level, EnemyId: (ushort)native.EnemyId, Strength: enemy.Strength, Magic: enemy.Magic, Speed: enemy.Speed, FormationRow: (byte)enemy.Placement.Row, ElementHalve: Elements(4), ElementNullify: Elements(5), ElementAbsorb: Elements(6), ElementWeakness: Elements(2), PreviousStatuses: native.Statuses, CaptureMask: 0, PhysicalEvasion: enemy.Evasion, HitEffect: 2));
        }
        return actors.ToArray();
    }

    private static void CheckRelativeHpBranches(SceneFormation scene, IReadOnlyList<KernelSection> kernel)
    {
        SceneBattleData data = SceneBattleData.Read(scene);
        foreach (SceneBattleEnemy enemy in data.Enemies.Where(enemy => enemy.Placement.EnemyId is 228 or 229 or 259 or 260))
        {
            foreach (uint multiplier in new uint[] { 1, 2 })
            {
                foreach (int quarter in new[] { 1, 2, 3, 4 })
                {
                    Ff7BattleAiActor[] actors = CreateActors(data).Select(actor => actor.Slot >= 4 ? actor with { MaxHitPoints = checked(actor.MaxHitPoints * multiplier), HitPoints = checked(actor.MaxHitPoints * multiplier) / 4 * (uint)quarter, LastAttackerMask = 1 } : actor).ToArray();
                    Ff7BattleRng random = new(kernel.Single(section => section.Type == 2).Data.Span, Ff7BattleRng.BeginBattle());
                    Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(actors, 0);
                    Ff7BattleAiResult initialized = Ff7BattleAi.RunActorCallback(new(enemy.FormationSlot + 4, actors, masks.Targetable, 0, Callback: 0, GlobalMemory: new byte[64], SaveBank1: new byte[256]), [], data, random, kernel.Single(section => section.Type == 1).Data);
                    actors = actors.Select(actor => initialized.ActorUpdates.SingleOrDefault(update => update.Slot == actor.Slot) ?? actor).ToArray();
                    Ff7BattleAiResult result = Ff7BattleAi.RunActorCallback(new(enemy.FormationSlot + 4, actors, masks.Targetable, 0, Callback: 2, GlobalMemory: initialized.GlobalMemory, SaveBank1: new byte[256]), initialized.LocalMemory, data, random, kernel.Single(section => section.Type == 1).Data);
                    if (enemy.Placement.EnemyId is 259 or 260)
                    {
                        byte expected = quarter switch { 1 => 2, 2 => 8, 3 => 32, _ => 128 };
                        Check(result.LocalMemory[12] == expected, $"Enemy {enemy.Placement.EnemyId} HP-quarter branch changed at {quarter}/4 with growth {multiplier}.");
                    }
                    else
                    {
                        Check((result.LocalMemory[28] & 1) == (quarter <= 3 ? 1 : 0), $"Schizo's three-quarter branch changed with growth {multiplier}.");
                    }
                }
            }
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
