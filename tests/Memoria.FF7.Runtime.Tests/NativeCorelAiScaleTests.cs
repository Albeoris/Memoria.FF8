using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCorelAiScaleTests
{
    public static void Run()
    {
        TestOperands();
        TestPresentation();
        TestSharedTransform();
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original Corel scale callbacks: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 2] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        HashSet<int> selected = [];
        for (int field = 0; field < 10; field++)
        {
            LgpEntry entry = fields.Find($"mtcrl_{field}").Single();
            PcFieldFile file = PcFieldFile.ReadCompressed(fields.ReadEntry(entry));
            foreach (PcFieldEncounterTable table in PcFieldEncounterTable.Read(file.GetSection(PcFieldSectionKind.Encounters).Span).Where(table => table.Enabled))
            {
                selected.UnionWith(table.Standard.Concat(table.Special).Where(encounter => encounter.Frequency != 0).Select(encounter => (int)encounter.Formation));
            }
        }
        Check(selected.Order().SequenceEqual(Enumerable.Range(492, 20).Concat(new[] { 513, 514, 515 })), "Original Corel encounter tables must select their 23 weighted formations, excluding unused formation 512.");

        foreach (int id in selected.Order())
        {
            SceneFormation formation = formations.Single(value => value.FormationId == id);
            Ff7BattleSession Open(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, id + 1, snapshot: snapshot);
            Ff7BattleSession fresh = Open();
            Ff7BattleSnapshot start = fresh.Capture();
            Check(start.Actors.All(actor => actor.Actor.Size == 16), $"Formation {id} did not initialize the native size byte to 16.");
            Ff7BattleSession cold = Open(RoundTrip(start));
            Equal(fresh, cold, $"Formation {id} initialization");
            Ff7BattleSession legacy = Open(HistoricalSnapshot(start));
            Check(legacy.Capture().Actors.All(actor => actor.Actor.Size == 16), $"Formation {id} absent historical size bytes did not restore the native default.");
            Check(legacy.Capture().Actors.All(actor => legacy.GetActorScale(actor.Actor.Slot) == 4096), "Absent historical presentation fields must restore the identity scale.");
            Equal(fresh, legacy, $"Formation {id} historical fields: HP, inventory, AI memory and RNG");
            fresh.Advance();
            cold.Advance();
            legacy.Advance();
            Equal(fresh, cold, $"Formation {id} first cold callback");
            Equal(fresh, legacy, $"Formation {id} historical first callback");
            start = fresh.Capture();
            start = start with { Actors = start.Actors.Select(actor => actor.Actor.Slot < 3 ? actor with { Actor = actor.Actor with { HitPoints = 10000, MaxHitPoints = 10000 } } : actor).ToArray() };
            Ff7BattleSession warm = Open(start);
            cold = Open(RoundTrip(start));
            for (int tick = 0; tick < 6000 && warm.LastHit is null; tick++)
            {
                warm.Advance();
                cold.Advance();
            }
            Check(warm.LastHit is { Source: >= 4 }, $"Formation {id} did not execute its original enemy turn within 6000 callbacks.");
            Equal(warm, cold, $"Formation {id} original enemy turn and RNG");
            cold = Open(RoundTrip(warm.Capture()));
            warm.Advance();
            cold.Advance();
            Equal(warm, cold, $"Formation {id} post-turn cold callback");

            if (id is 500 or 501 or 513)
            {
                TestOriginalCounter(formation, progress, kernel, executable.NativeMateriaStatModifiers, animations);
            }
        }
        Console.WriteLine("Native size operands/selectors/store/comparison and 23 original Corel initialization/turn/JSON continuations passed, including Bomb growth counters and owner isolation.");
    }

    private static void TestOperands()
    {
        SceneBattleData data = new(0, [], [], [], null);
        Ff7BattleAiActor[] actors = [new(0, 0, 8, 0, 0, 100, 100), new(4, 0, 8, 0, 0, 600, 600) { Size = 16 }, new(5, 0, 8, 0, 0, 600, 600) { Size = 248 }];
        Ff7BattleAiContext context = new(4, actors, 0x31, 0);
        Ff7BattleAiResult Run(byte[] script, Ff7BattleAiContext? owner = null) => Ff7BattleAi.Run(script, owner ?? context, [], data, new(new byte[3988], new(new byte[8])));
        byte[] read = [0x11, 0, 0, 0x02, 0x60, 0x20, 0x01, 0x98, 0x40, 0x80, 0x83, 0x90, 0x73];
        Check(Run(read).LocalMemory[0] == 16 && Run(read, context with { ActorSlot = 5 }).LocalMemory[0] == 248, "Self selection must read the owning unsigned size byte.");
        Check(Run(read, context with { ActorSlot = 0 }).LocalMemory[0] == 16, "An absent historical size byte must retain the native default.");
        Check(Run(read, context with { ActorSlot = 5, Actors = actors.Select(actor => actor.Slot == 5 ? actor with { Size = 0 } : actor).ToArray() }).LocalMemory[0] == 0, "An explicitly recorded zero size must remain zero.");
        Ff7BattleAiResult stored = Run([0x12, 0, 0, 0x61, 0x30, 0, 0x90, 0x12, 0, 0, 0x11, 0x98, 0x40, 0x61, 0xF8, 1, 0x90, 0x73], context with { Actors = actors.Select(actor => actor.Slot == 5 ? actor with { Size = 200 } : actor).ToArray() });
        Check(stored.ActorUpdates.Length == 2 && stored.ActorUpdates.All(actor => actor.Size == 248) && stored.ActorUpdates.All(actor => actor.Slot is 4 or 5), "A selected multi-actor size store must truncate to a byte without changing an unselected owner.");
        byte[] compare = [0x11, 0, 0, 0x02, 0x60, 0x20, 0x01, 0x98, 0x40, 0x80, 0x83, 0x60, 0x7F, 0x44, 0x90, 0x73];
        Check(Run(compare, context with { ActorSlot = 5 }).LocalMemory[0] == 1, "Size comparisons must preserve unsigned values above 127.");
        byte[] grow = [0x12, 0x60, 0x20, 0x11, 0x98, 0x40, 0x80, 0x02, 0x60, 0x20, 0x01, 0x98, 0x40, 0x80, 0x60, 8, 0x30, 0x90, 0x73];
        Check(Run(grow).ActorUpdates.Single().Size == 24, "The original ADD8 operand order must grow size 16 to 24.");
        Check(Run(grow, context with { ActorSlot = 5 }).ActorUpdates.Single().Size == 0, "A native size ADD8 and byte store must truncate 256 to zero.");
        foreach (byte width in new byte[] { 0, 2, 3 })
        {
            bool rejected = false;
            try
            {
                Run([0x02, 0x60, 0x20, width, 0x98, 0x40, 0x80, 0x73]);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected, $"Size projection must reject unsupported width {width}.");
        }
    }

    private static Ff7BattleSnapshot HistoricalSnapshot(Ff7BattleSnapshot snapshot)
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(snapshot))!.AsObject();
        json.Remove(nameof(Ff7BattleSnapshot.ActorSizePresentation));
        foreach (JsonNode? actor in json[nameof(Ff7BattleSnapshot.Actors)]!.AsArray())
        {
            actor![nameof(Ff7BattleActorState.Actor)]!.AsObject().Remove(nameof(Ff7BattleAiActor.Size));
        }
        RemoveNativeScale(json);
        return json.Deserialize<Ff7BattleSnapshot>()!;
    }

    private static void RemoveNativeScale(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            owner.Remove(nameof(Ff7BattleActorModelModifiers.NativeScale));
            foreach (JsonNode? child in owner.Select(property => property.Value))
            {
                if (child is not null)
                {
                    RemoveNativeScale(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    RemoveNativeScale(child);
                }
            }
        }
    }

    private static void TestSharedTransform()
    {
        BattleModelData model = new(0, [new(-1, 100, true), new(0, 40, true)], [], []);
        BattleClipFrame frame = new(10, -20, 30, [new(), new(), new()]);
        Vector3 origin = new(1000, 2000, -3000);
        Vector3 point = new(4, 6, 8);
        const float facing = MathF.PI / 2;
        Ff7BattleActorModelModifiers grown = new() { NativeScale = 6144 };
        Check(grown.PoseTransform?.Scale == 1.5f, "NativeScale 6144 must produce a single factor of 1.5 for the shared pose consumer.");
        Vector3 bone = BattleRigidPose.BonePoint(model, frame, origin, facing, 2, point);
        Vector3 weapon = BattleRigidPose.WeaponPosition(frame, origin, facing, point);
        CheckPoint(BattleRigidPose.BonePoint(model, frame, origin, facing, 2, point, grown.PoseTransform), origin + (bone - origin) * 1.5f, "Scaled bone, clip translation and parent attachment");
        CheckPoint(BattleRigidPose.WeaponPosition(frame, origin, facing, point, grown.PoseTransform), origin + (weapon - origin) * 1.5f, "Scaled weapon and clip translation");

        foreach (BattleActorPoseTransform? identity in new BattleActorPoseTransform?[] { null, default(BattleActorPoseTransform), new BattleActorPoseTransform(), new Ff7BattleActorModelModifiers().PoseTransform })
        {
            CheckPoint(BattleRigidPose.BonePoint(model, frame, origin, facing, 2, point, identity), bone, "Default bone transform identity");
            CheckPoint(BattleRigidPose.WeaponPosition(frame, origin, facing, point, identity), weapon, "Default weapon transform identity");
        }
        foreach (BattleActorPoseTransform? zero in new BattleActorPoseTransform?[] { new(Scale: 0), new Ff7BattleActorModelModifiers { NativeScale = 0 }.PoseTransform })
        {
            CheckPoint(BattleRigidPose.BonePoint(model, frame, origin, facing, 2, point, zero), origin, "Explicit zero bone scale with unchanged world placement");
            CheckPoint(BattleRigidPose.WeaponPosition(frame, origin, facing, point, zero), origin, "Explicit zero weapon scale with unchanged world placement");
        }

        static void CheckPoint(Vector3 actual, Vector3 expected, string scenario)
        {
            Check(Vector3.Distance(actual, expected) < 0.001f, $"{scenario}: expected {expected}, got {actual}.");
        }
    }

    private static void TestPresentation()
    {
        Ff7BattleSizePresentation initial = Ff7NativeBattleActorSize.Create();
        Check(initial.Scales.Length == 10 && initial.Scales.All(scale => scale == 4096), "Native size presentation must initialize every physical slot to 4096.");
        Ff7BattleSizePresentation warm = Ff7NativeBattleActorSize.Enqueue(initial, [new(4, 24), new(5, 32)]);
        Ff7BattleSizePresentation cold = RoundTrip(warm);
        for (int callback = 1; callback <= 16; callback++)
        {
            warm = Ff7NativeBattleActorSize.Advance(warm);
            cold = Ff7NativeBattleActorSize.Advance(cold);
            Ff7NativeBattleActorSize.Validate(warm);
            Check(JsonSerializer.Serialize(warm) == JsonSerializer.Serialize(cold), "Size interpolation diverged after JSON restore.");
            Check(warm.ActiveSlot == 4 && warm.Remaining == 16 - callback && warm.Scales[4] == 4096 + callback * 128 && warm.Scales[5] == 4096, "The native 16-step size consumer changed its timing, scale or next queued owner.");
            cold = RoundTrip(warm);
        }
        warm = Ff7NativeBattleActorSize.Advance(warm);
        Check(warm.ActiveSlot is null && warm.Pending.Length == 1 && warm.Scales[5] == 4096, "The seventeenth size callback must clean up before admitting the next packet.");
        warm = Ff7NativeBattleActorSize.Advance(warm);
        Check(warm.ActiveSlot == 5 && warm.Remaining == 15 && warm.Scales[5] == 4352 && warm.Scales[4] == 6144, "The next queued size packet must begin on the following callback and retain the previous owner.");

        foreach ((byte size, short expected) in new (byte, short)[] { (0, 8192), (8, 6144), (128, -24576), (255, 8448) })
        {
            Ff7BattleSizePresentation state = Ff7NativeBattleActorSize.Enqueue(initial, [new(4, size)]);
            for (int callback = 0; callback < 16; callback++)
            {
                state = Ff7NativeBattleActorSize.Advance(state);
            }
            Check(state.Scales[4] == expected && state.Scales[0] == 4096, $"Native size {size} did not retain its positive absolute step, signed target-short arithmetic and owner isolation.");
        }
    }

    private static void TestOriginalCounter(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7BattleAnimationResources animations)
    {
        Ff7BattleSession Open(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, modifiers, formation.FormationId + 1, snapshot: snapshot, animations: animations);
        Ff7BattleSession fresh = Open();
        Ff7BattleSnapshot start = fresh.Capture();
        Ff7BattleSession legacy = Open(HistoricalSnapshot(start));
        Check(legacy.Capture().Actors.All(actor => legacy.ActorModelOutput(actor.Actor.Slot).NativeScale == 4096), "Absent historical model scale fields must restore the native identity modifier.");
        Equal(fresh, legacy, $"Bomb {formation.FormationId} historical model modifiers and progress");
        fresh.Advance(waitForCommand: true);
        legacy.Advance(waitForCommand: true);
        Equal(fresh, legacy, $"Bomb {formation.FormationId} historical model next callback");
        int target = start.Actors.First(actor => actor.Identity == 105).Actor.Slot;
        SceneBattleData data = SceneBattleData.Read(formation);

        foreach ((uint hp, byte stage, byte size, byte expectedStage, byte expectedSize) in new (uint, byte, byte, byte, byte)[] { (451, 0, 16, 0, 16), (450, 0, 16, 1, 24), (301, 1, 24, 1, 24), (300, 1, 24, 2, 32), (300, 2, 32, 2, 32) })
        {
            Ff7BattleAiActor[] actors = start.Actors.Select(actor => actor.Actor.Slot == target ? actor.Actor with { HitPoints = hp, Size = size } : actor.Actor).ToArray();
            byte[] memory = new byte[Ff7BattleAi.LocalMemorySize];
            memory[0] = stage;
            Ff7BattleAiContext context = new(target, actors, start.AiMasks!.Value.Targetable, 0, Callback: 2);
            Ff7BattleRngState seed = start.Random;
            Ff7BattleRng random = new(kernel[2].Data.Span, RoundTrip(seed));
            Ff7BattleAiResult result = Ff7BattleAi.RunActorCallback(context, memory, data, random, kernel[1].Data);
            Ff7BattleRng coldRandom = new(kernel[2].Data.Span, RoundTrip(seed));
            Ff7BattleAiResult cold = Ff7BattleAi.RunActorCallback(RoundTrip(context), RoundTrip(memory), data, coldRandom, kernel[1].Data);
            Check(JsonSerializer.Serialize(result) == JsonSerializer.Serialize(cold) && JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(coldRandom.Capture()), "Original Bomb threshold callback or RNG changed after independent JSON restore.");
            Check(result.LocalMemory[0] == expectedStage, $"Bomb {formation.FormationId} HP {hp} did not retain its stage guard.");
            Ff7BattleAiActor updated = result.ActorUpdates.SingleOrDefault(actor => actor.Slot == target) ?? actors.Single(actor => actor.Slot == target);
            Check(updated.Size == expectedSize && result.ActorUpdates.All(actor => actor.Slot == target), $"Bomb {formation.FormationId} HP {hp} changed the wrong size or owner.");
        }

        Ff7BattleSession warm = Open(start);
        for (int stage = 1; stage <= 2; stage++)
        {
            Ff7BattleSnapshot before = warm.Capture();
            before = before with
            {
                Actors = before.Actors.Select(actor => actor.Actor.Slot == target ? actor with { Actor = actor.Actor with { HitPoints = stage == 1 ? 451U : 301U } } : actor).ToArray(),
                Queue = Ff7BattleCommandQueue.Enqueue(before.Queue, new(2, 0, (byte)Ff7BattleCommand.Attack, 0, (ushort)(1 << target)))
            };
            warm = Open(before);
            Ff7BattleSession cold = Open(RoundTrip(before));
            short initialScale = warm.GetActorScale(target);
            bool observed = false;
            bool restoredTween = false;
            for (int tick = 0; tick < 2000; tick++)
            {
                short previousScale = warm.GetActorScale(target);
                warm.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                if (warm.GetActorScale(target) != previousScale)
                {
                    Check(warm.ActorModelOutput(target).NativeScale == previousScale, "Native model submission must retain the pre-callback scale until the next presentation pass.");
                }
                Equal(warm, cold, $"Bomb {formation.FormationId} stage {stage} hit/size callback");
                if (!restoredTween && warm.GetActorScale(target) != initialScale)
                {
                    cold = Open(RoundTrip(warm.Capture()));
                    restoredTween = true;
                }
                Ff7BattleActorState actor = warm.Capture().Actors.Single(actor => actor.Actor.Slot == target);
                if (actor.Actor.Size == 16 + stage * 8 && warm.Action is null && warm.LastHit is { Source: 0 } hit && hit.Target == target)
                {
                    observed = true;
                    break;
                }
            }
            Ff7BattleActorState grown = warm.Capture().Actors.Single(actor => actor.Actor.Slot == target);
            Check(observed && grown.Actor.HitPoints > 150 && grown.AiMemory[0] == stage, $"Original Bomb {formation.FormationId} nonlethal physical counter did not reach growth stage {stage}.");
            if (formation.FormationId == 501)
            {
                Check(warm.Capture().Actors.Single(actor => actor.Identity == 105 && actor.Actor.Slot != target).Actor.Size == 16, "Growing one Bomb changed the other Bomb's size.");
            }
            cold = Open(RoundTrip(warm.Capture()));
            for (int tick = 0; tick < 256; tick++)
            {
                short previousScale = warm.GetActorScale(target);
                warm.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                if (warm.GetActorScale(target) != previousScale)
                {
                    Check(warm.ActorModelOutput(target).NativeScale == previousScale, "Native size interpolation must advance after model/joint submission.");
                }
                if (!restoredTween && warm.GetActorScale(target) != initialScale)
                {
                    Equal(warm, cold, $"Bomb {formation.FormationId} stage {stage} first size step");
                    cold = Open(RoundTrip(warm.Capture()));
                    restoredTween = true;
                }
                if (restoredTween && warm.Capture().ActorSizePresentation is { ActiveSlot: null, Pending.Length: 0 })
                {
                    break;
                }
            }
            Equal(warm, cold, $"Bomb {formation.FormationId} stage {stage} cold presentation continuation");
            Check(restoredTween && warm.GetActorScale(target) == 4096 + stage * 2048, $"Original Bomb {formation.FormationId} stage {stage} growth: restored={restoredTween}, scale={warm.GetActorScale(target)}, size={grown.Actor.Size}, tick={warm.Tick}, pending={warm.Capture().ActorSizePresentation?.Pending.Length}, active={warm.Capture().ActorSizePresentation?.ActiveSlot}, remaining={warm.Capture().ActorSizePresentation?.Remaining}.");
            Check(warm.ActorModelOutput(target).NativeScale == 4096 + stage * 2048, "Original Bomb presentation must publish its retained scale through the shared model modifiers.");
        }
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void Equal(Ff7BattleSession warm, Ff7BattleSession cold, string scenario)
    {
        Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"{scenario}: full saved state or RNG diverged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
