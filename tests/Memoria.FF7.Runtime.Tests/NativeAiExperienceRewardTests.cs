using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAiExperienceRewardTests
{
    private sealed record CallbackBoundary(Ff7BattleAiActor[] Actors, Dictionary<int, byte[]> Locals, Ff7BattleRngState Random, byte[] Global, byte[] SaveBank);

    public static void Run()
    {
        CheckScalarContracts();
        CheckRewardProjection();
        CheckInstalledArea();
    }

    private static void CheckScalarContracts()
    {
        SceneBattleData formation = SyntheticData();
        foreach (ushort address in new ushort[] { 0x42E0, 0x42C0 })
        {
            Ff7BattleAiActor party = new(0, 0, 0, 0, 0, 100, 100);
            Ff7BattleAiActor enemy = new(4, 0x20000000, 8, 0, 0, 100, 100);
            uint resource = address == 0x42E0 ? 15u : 7u;
            byte lo = (byte)address;
            byte hi = (byte)(address >> 8);
            byte[] read = [0x13, 0, 0, 0x02, 0x60, 0x20, 0x03, lo, hi, 0x80, 0x90, 0x73];
            Check(BinaryPrimitives.ReadUInt32LittleEndian(Run(read, [party, enemy], [], formation).LocalMemory) == resource, "An absent enemy reward must read its original resource value.");
            Ff7BattleAiActor zero = WithReward(enemy, address, 0);
            Check(BinaryPrimitives.ReadUInt32LittleEndian(Run(read, [party, zero], [], formation).LocalMemory) == 0, "An explicit zero reward was replaced by resource metadata.");
            Ff7BattleAiActor high = WithReward(enemy, address, 0xF1234567);
            Check(BinaryPrimitives.ReadUInt32LittleEndian(Run(read, [party, high], [], formation).LocalMemory) == 0x234567 && Reward(high, address) == 0xF1234567, "Native three-byte stack transport must clip the operand while preserving the owned DWORD.");
            Check(Reward(RoundTrip(high), address) == 0xF1234567, "The actor's full DWORD was truncated by JSON restoration.");
            Check(BinaryPrimitives.ReadUInt32LittleEndian(Run(read, [party, enemy], [], formation, owner: 0).LocalMemory) == 0, "An absent party reward did not retain its native cleared value.");
            byte[] memory = new byte[128];
            BinaryPrimitives.WriteUInt32LittleEndian(memory, 0xFEDCBA98);
            byte[] store = [0x12, 0x60, 0x20, 0x13, lo, hi, 0x03, 0, 0, 0x90, 0x73];
            Ff7BattleAiResult changed = Run(store, [party, enemy], memory, formation);
            Ff7BattleAiActor updated = changed.ActorUpdates.Single();
            Check(updated.Slot == 4 && Reward(updated, address) == 0xDCBA98 && updated.Statuses == enemy.Statuses, "A reward store lost native three-byte stack transport or changed the actor/status owner.");
            byte[] opponentStore = [0x12, 0x70, 0x20, 0x13, lo, hi, 0x03, 0, 0, 0x90, 0x73];
            Check(Run(opponentStore, [party, enemy], memory, formation).ActorUpdates.Single() is { Slot: 0 }, "Reward stores ignored the native opponent selector.");
            Ff7BattleAiActor cold = JsonSerializer.Deserialize<Ff7BattleAiActor>(JsonSerializer.Serialize(updated))!;
            Check(Reward(cold, address) == 0xDCBA98 && Run(read, [party, cold], [], formation).LocalMemory.SequenceEqual(Run(read, [party, updated], [], formation).LocalMemory), "A mutable reward lost ownership across JSON restoration.");
            BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(4), 0x7FFFFF);
            byte[] compare = [0x12, 0, 0, 0x03, lo, hi, 0x03, 0x20, 0, 0x44, 0x90, 0x73];
            Check(BinaryPrimitives.ReadUInt16LittleEndian(Run(compare, [party, WithReward(enemy, address, 0xABF12345)], memory, formation).LocalMemory) == 0x10, "Reward comparison signed the native unsigned three-byte operand or lost its actor selector.");
            foreach (var alias in new[] { (Width: (byte)0, Offset: 31, Expected: 1u), (Width: (byte)1, Offset: 24, Expected: 0xF1u), (Width: (byte)2, Offset: 16, Expected: 0xF123u) })
            {
                ushort at = checked((ushort)(address + alias.Offset));
                byte[] partialRead = [0x13, 0, 0, 0x02, 0x60, 0x20, alias.Width, (byte)at, (byte)(at >> 8), 0x80, 0x90, 0x73];
                Check(BinaryPrimitives.ReadUInt32LittleEndian(Run(partialRead, [party, high], [], formation).LocalMemory) == alias.Expected, "A reward bit, byte or WORD alias read the wrong part of its DWORD.");
            }
            foreach (var alias in new[] { (Width: (byte)0, Offset: 31, Value: 0u, Expected: 0x71234567u), (Width: (byte)1, Offset: 8, Value: 0xABu, Expected: 0xF123AB67u), (Width: (byte)2, Offset: 16, Value: 0x2222u, Expected: 0x22224567u) })
            {
                ushort at = checked((ushort)(address + alias.Offset));
                BinaryPrimitives.WriteUInt32LittleEndian(memory, alias.Value);
                byte[] partialStore = [0x12, 0x60, 0x20, (byte)(0x10 | alias.Width), (byte)at, (byte)(at >> 8), 0x03, 0, 0, 0x90, 0x73];
                Check(Reward(Run(partialStore, [party, high], memory, formation).ActorUpdates.Single(), address) == alias.Expected, "A partial reward store failed to preserve the untouched DWORD bits.");
            }
            ExpectUnsupported(() => Run([0x60, 0x10, 0x13, lo, hi, 0x60, 1, 0x90, 0x73], [party, enemy], [], formation));
        }
    }

    private static void CheckRewardProjection()
    {
        SceneFormation formation = SyntheticFormation();
        Ff7BattleRng random = Random();
        string before = JsonSerializer.Serialize(random.Capture());
        Ff7BattleReward baseline = Ff7DebugVictory.ResolveRewards(formation, random);
        Check(baseline is { Experience: 30, Gil: 14, AbilityPoints: 4 }, "Default rewards changed their original per-instance resource projection.");
        Ff7BattleReward changed = Ff7DebugVictory.ResolveRewards(formation, random, experienceReward: instance => instance.Row == 0 ? 0u : 40u, gilReward: instance => instance.Row == 0 ? 11u : 0u);
        Check(changed is { Experience: 40, Gil: 11, AbilityPoints: 4 } && changed.Items.Length == 0, "Mutable per-actor XP/Gil changed AP, items, explicit zero or repeated enemy-instance ownership.");
        Ff7BattleReward selected = Ff7DebugVictory.ResolveRewards(formation, random, instance => instance.Row == 0, _ => 3, _ => 5);
        Check(selected is { Experience: 3, Gil: 5, AbilityPoints: 2 }, "Undefeated instances contributed mutable rewards.");
        Check(JsonSerializer.Serialize(random.Capture()) == before, "Reward overrides consumed randomness beyond the existing item-drop contract.");
    }

    private static void CheckInstalledArea()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AI_REWARD_SAVE");
        if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original area reward AI: MEMORIA_FF7_GAME_DIR or MEMORIA_FF7_AI_REWARD_SAVE is unset.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7WorldProcess world = source.World ?? throw new InvalidDataException("The area reward fixture requires original world terrain.");
        Ff7Installation installation = Ff7Installation.Discover(game);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation, world.WorldId);
        byte[] encounters = resources.ReadRequired("enc_w.bin");
        int terrain = world.Entities.Single(entity => entity.Model == world.ControlledModel).Terrain;
        int region = Math.Min(15, terrain >> 9 & 31);
        HashSet<int> ids = [];
        for (int record = 0; record < 4; record++)
        {
            int offset = 0xA0 + (region * 4 + record) * 32;
            if ((encounters[offset] & 1) == 0)
            {
                continue;
            }
            for (int entry = 0; entry < 14; entry++)
            {
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(encounters.AsSpan(offset + 2 + entry * 2));
                if ((value >> 10) != 0)
                {
                    ids.Add(value & 1023);
                }
            }
        }
        Check(ids.Count > 0, "The original encounter area has no enabled weighted formations to validate.");
        if (world.BattleState?.Pending is { } pending)
        {
            Check(ids.Contains(pending.Formation), "The derived encounter matrix does not cover the original pending formation.");
        }
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleSystemMessages messages = Ff7BattleSystemMessages.Load(installation);
        foreach (int id in ids.Order())
        {
            SceneFormation formation = formations.Single(item => item.FormationId == id);
            Ff7BattleSession battle = new(formation, source.Progress, kernel, executable.NativeMateriaStatModifiers, long.MaxValue - id, systemMessages: messages);
            Ff7BattleSnapshot snapshot = battle.Capture();
            Check(snapshot.Actors.All(actor => actor.Actor.ExperienceReward is not null && actor.Actor.GilReward is not null), "An installed initialization left mutable reward ownership unknown.");
            Ff7BattleSession cold = new(formation, source.Progress, kernel, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: RoundTrip(snapshot), systemMessages: messages);
            Check(JsonSerializer.Serialize(snapshot) == JsonSerializer.Serialize(cold.Capture()), "Installed init reward ownership changed across JSON restoration.");
            Ff7BattleSnapshot legacy = snapshot with { Actors = snapshot.Actors.Select(actor => actor with { Actor = actor.Actor with { ExperienceReward = null, GilReward = null } }).ToArray() };
            Ff7BattleSession legacyCold = new(formation, source.Progress, kernel, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: RoundTrip(legacy), systemMessages: messages);
            Ff7BattleSnapshot backfilled = legacyCold.Capture();
            Check(backfilled.Actors.All(actor => actor.Actor.ExperienceReward is not null && actor.Actor.GilReward is not null) && JsonSerializer.Serialize(backfilled.Random) == JsonSerializer.Serialize(snapshot.Random) && backfilled.Actors.All(actor => actor.AiMemory.SequenceEqual(snapshot.Actors.Single(original => original.Actor.Slot == actor.Actor.Slot).AiMemory)), "An old reward snapshot replayed native initialization or failed to backfill absent ownership.");
            Ff7BattleSnapshot zero = snapshot with { Actors = snapshot.Actors.Select(actor => actor with { Actor = actor.Actor with { ExperienceReward = 0, GilReward = 0 } }).ToArray() };
            Ff7BattleSession zeroCold = new(formation, source.Progress, kernel, executable.NativeMateriaStatModifiers, snapshot.RequestId, snapshot: RoundTrip(zero), systemMessages: messages);
            Check(zeroCold.Capture().Actors.All(actor => actor.Actor.ExperienceReward == 0 && actor.Actor.GilReward == 0), "Cold compatibility replaced an explicit native zero with resource rewards.");
            if (world.BattleState?.Pending?.Formation == id)
            {
                CheckInstalledRewardConsumer(battle, source.Progress, source.FieldGeneration, kernel, executable.NativeMateriaStatModifiers, messages);
            }
            battle.Advance(waitForCommand: true);
            cold.Advance(waitForCommand: true);
            Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The callback after installed battle restoration diverged.");
            CallbackBoundary boundary = new(snapshot.Actors.Select(actor => actor.Actor).ToArray(), snapshot.Actors.ToDictionary(actor => actor.Actor.Slot, actor => actor.AiMemory), snapshot.Random, snapshot.AiGlobalMemory!, snapshot.AiSaveBank1!);
            for (int turn = 0; turn < 2; turn++)
            {
                foreach (SceneBattleEnemy enemy in battle.Data.Enemies)
                {
                    CallbackBoundary restored = RoundTrip(boundary);
                    CallbackBoundary actual = Advance(enemy.FormationSlot + 4, boundary);
                    Check(JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(Advance(enemy.FormationSlot + 4, restored)), "An installed native main callback changed mutable rewards or RNG after restoration.");
                    boundary = actual;
                }
            }
            CallbackBoundary Advance(int slot, CallbackBoundary current)
            {
                Ff7BattleRng working = new(kernel[2].Data.Span, current.Random);
                Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(current.Actors, snapshot.ImprisonedType.GetValueOrDefault());
                Ff7BattleAiResult result = Ff7BattleAi.RunActorCallback(new(slot, current.Actors, masks.Targetable, masks.KnockedOut, masks.ExcludedOpponents, GlobalMemory: current.Global, SaveBank1: current.SaveBank), current.Locals[slot], battle.Data, working, kernel[1].Data);
                Dictionary<int, byte[]> locals = current.Locals.ToDictionary(pair => pair.Key, pair => pair.Value);
                locals[slot] = result.LocalMemory;
                byte[] bank = current.SaveBank.ToArray();
                foreach ((ushort address, byte value) in result.SaveBank1Writes)
                {
                    bank[address] = value;
                }
                return new(current.Actors.Select(actor => result.ActorUpdates.SingleOrDefault(update => update.Slot == actor.Slot) ?? actor).ToArray(), locals, working.Capture(), result.GlobalMemory, bank);
            }
        }
        Ff7ProgressFixtureProjection(source.Progress, source.FieldGeneration, kernel);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Installed reward checks changed their standalone source fixture.");
        Console.WriteLine($"PASS original World area{region}: {ids.Count} formations [{string.Join(',', ids.Order())}], native initialization/two AI turns/independent snapshots and reward ownership.");
    }

    private static void CheckInstalledRewardConsumer(Ff7BattleSession original, Ff7Progress progress, long generation, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7BattleSystemMessages messages)
    {
        Ff7BattleSnapshot snapshot = original.Capture();
        Ff7BattleSnapshot terminal = snapshot with
        {
            Outcome = Ff7BattleOutcome.Victory,
            TerminalFrames = 45,
            Actors = snapshot.Actors.Select(actor => actor.Actor.Slot < 4 ? actor : actor with { Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | Ff7NativeBattleActionResolver.Death, ExperienceReward = (uint)(actor.Actor.Slot - 3) * 11, GilReward = (uint)(actor.Actor.Slot - 3) * 13 } }).ToArray()
        };
        Ff7BattleSession battle = new(original.Formation, progress, kernel, modifiers, snapshot.RequestId, snapshot: RoundTrip(terminal), systemMessages: messages);
        string before = JsonSerializer.Serialize(battle.Capture());
        Ff7BattleReward preview = battle.PreviewVictory();
        uint experience = (uint)terminal.Actors.Where(actor => actor.Actor.Slot >= 4).Sum(actor => actor.Actor.ExperienceReward!.Value);
        uint gil = (uint)terminal.Actors.Where(actor => actor.Actor.Slot >= 4).Sum(actor => actor.Actor.GilReward!.Value);
        Check(preview.Experience == experience && preview.Gil == gil, "The real battle preview ignored mutable per-instance rewards.");
        Ff7VictoryProjection projection = battle.ProjectVictory(progress, generation);
        Check(projection.Reward == preview || JsonSerializer.Serialize(projection.Reward) == JsonSerializer.Serialize(preview), "Atomic battle reward publication differed from its native preview.");
        string published = JsonSerializer.Serialize(projection.Progress);
        Ff7VictoryProjection duplicate = battle.ProjectVictory(projection.Progress, generation);
        Check(JsonSerializer.Serialize(duplicate.Progress) == published && JsonSerializer.Serialize(battle.Capture()) == before, "Preview/once-only battle settlement changed live reward or RNG ownership.");
    }

    private static void Ff7ProgressFixtureProjection(Ff7Progress progress, long generation, IReadOnlyList<KernelSection> kernel)
    {
        Ff7BattleRng random = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle());
        Ff7VictoryProjection first = Ff7DebugVictory.Project(progress, SyntheticFormation(), kernel, generation, long.MaxValue, random, experienceReward: _ => 3, gilReward: _ => 5);
        string before = JsonSerializer.Serialize(first.Progress);
        string randomBefore = JsonSerializer.Serialize(random.Capture());
        Ff7VictoryProjection duplicate = Ff7DebugVictory.Project(first.Progress, SyntheticFormation(), kernel, generation, long.MaxValue, random, experienceReward: _ => 999, gilReward: _ => 999);
        Check(JsonSerializer.Serialize(duplicate.Progress) == before && duplicate.Reward == first.Reward && JsonSerializer.Serialize(random.Capture()) == randomBefore, "A duplicate reward projection reapplied mutable rewards or consumed RNG.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static Ff7BattleAiActor WithReward(Ff7BattleAiActor actor, ushort address, uint value) => address == 0x42E0 ? actor with { ExperienceReward = value } : actor with { GilReward = value };
    private static uint? Reward(Ff7BattleAiActor actor, ushort address) => address == 0x42E0 ? actor.ExperienceReward : actor.GilReward;
    private static Ff7BattleRng Random() => new(new byte[3988], Ff7BattleRng.BeginBattle());
    private static Ff7BattleAiResult Run(byte[] script, Ff7BattleAiActor[] actors, byte[] local, SceneBattleData formation, int owner = 4) => Ff7BattleAi.Run(script, new(owner, actors, 0x11, 0), local, formation, Random());

    private static SceneFormation SyntheticFormation()
    {
        SceneEnemyReward reward = new(1, ReadOnlyMemory<byte>.Empty, 15, 7, 2, [], ReadOnlyMemory<byte>.Empty);
        return new(1, 0, -1, 0, 0, 0, 0, [], [new(1, Vector3.Zero, 0, 0, 0), new(1, Vector3.One, 1, 0, 0)], [reward], ReadOnlyMemory<byte>.Empty);
    }

    private static SceneBattleData SyntheticData()
    {
        SceneFormation source = SyntheticFormation();
        SceneBattleEnemy enemy = new(0, source.Enemies[0], 0, source.EnemyRewards[0], Level: 1, Speed: 1, Luck: 1, Evasion: 1, Strength: 1, Defense: 1, Magic: 1, MagicDefense: 1, MaxHitPoints: 100, MaxMagicPoints: 0, NativeStatusMask: 0, Affinities: [], Attacks: [], Scripts: null);
        return new(1, [], [enemy], [], null);
    }

    private static void ExpectUnsupported(Action action)
    {
        try
        {
            action();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException("An unsupported reward width or selector was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
