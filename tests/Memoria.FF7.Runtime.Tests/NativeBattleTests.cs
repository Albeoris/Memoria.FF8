using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleTests
{
    public static void Run()
    {
        byte[] kernel = new byte[3988];
        for (int index = 0; index < 256; index++)
        {
            kernel[0xE1C + index] = (byte)index;
        }
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        SceneEnemyReward enemy = new(10, ReadOnlyMemory<byte>.Empty, 16, 10, 2, [new(3, 64), new(0, 0), new(1, 63), new(-1, 255)], ReadOnlyMemory<byte>.Empty);
        SceneFormation formation = new(300, 0, -1, 0, 0, 0, 0, [], [new(10, Vector3.Zero, 0, 0, 0), new(10, Vector3.Zero, 0, 0, 0)], [enemy], ReadOnlyMemory<byte>.Empty);
        Ff7BattleReward rewards = Ff7DebugVictory.ResolveRewards(formation, random);
        Check(rewards.Gil == 20 && rewards.Experience == 32 && rewards.AbilityPoints == 4, "Every original enemy instance must contribute rewards.");
        Check(rewards.Items.SequenceEqual(new Ff7RewardItem[] { new(0, 1), new(1, 1) }), "Drop selectors, inclusive zero-rate comparison or first successful slot changed.");
        Check(random.Capture().Indices[0] == 3, "Skipped steal slots must not consume drop randomness.");
        enemy = enemy with { Items = [new(-1, 63), new(1, 63), new(-1, 255), new(-1, 255)] };
        random = new(kernel, new(new byte[8]));
        rewards = Ff7DebugVictory.ResolveRewards(formation with { EnemyRewards = [enemy] }, random);
        Check(rewards.Items.Length == 0 && random.Capture().Indices[0] == 2, "A successful empty native item slot must terminate the drop search.");
        Ff7BattleRngState seeded = Ff7BattleRng.BeginBattle();
        Check(seeded.CrtSeed == 2745024 && seeded.Indices.SequenceEqual(new byte[] { 41, 20, 10, 5, 2, 1, 0, 0 }), "The native CRT seed or per-stream shift changed.");
        random = new(kernel, seeded);
        random.NextWord();
        Ff7BattleRng restored = new(kernel, random.Capture());
        Check(random.NextWord() == restored.NextWord() && JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Native word random continuation changed after restore.");
        RunInstalled();
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneFormation formation = formations.Single(formation => formation.FormationId == 300);
        RunInstalledSceneData(formations, formation, kernel);
        RunInstalledModelData(installation);
        using Ff7Session session = Ff7Session.NewGame(installation);
        Ff7Progress initial = session.Progress;
        string untouched = JsonSerializer.Serialize(initial);
        Ff7Progress result = Ff7DebugVictory.Apply(initial, formation, kernel, 0, 1);
        Check(untouched == JsonSerializer.Serialize(initial), "The battle transaction mutated its incoming saved progression.");
        Ff7BattleReward reward = result.CompletedBattles.Single().Reward;
        Check(reward.Gil == 20 && reward.Experience == 32 && reward.AbilityPoints == 4 && reward.Items.SequenceEqual(new Ff7RewardItem[] { new(0, 1) }), "Original opening formation reward/drop data changed.");
        Ff7CharacterState cloud = result.Characters[0];
        Check(cloud.Level == 7 && cloud.Experience == initial.Characters[0].Experience + 32 && result.Gil == initial.Gil + 20, "Opening native EXP/growth/gil application changed.");
        for (int slot = 0; slot < 16; slot++)
        {
            uint before = BinaryPrimitives.ReadUInt32LittleEndian(initial.Characters[0].NativeRecord.AsSpan(64 + 4 * slot));
            uint after = BinaryPrimitives.ReadUInt32LittleEndian(cloud.NativeRecord.AsSpan(64 + 4 * slot));
            if ((byte)before != 255)
            {
                Check((byte)before == (byte)after && (after >> 8) == (before >> 8) + 4, "Original equipped materia did not receive native AP.");
            }
        }
        Check(ReferenceEquals(result, Ff7DebugVictory.Apply(result, formation, kernel, 0, 1)), "Replayed victory must return the recorded transaction without awarding again.");
        Ff7Progress cold = JsonSerializer.Deserialize<Ff7Progress>(JsonSerializer.Serialize(result))!;
        Check(JsonSerializer.Serialize(Ff7DebugVictory.Apply(result, formation, kernel, 0, 2)) == JsonSerializer.Serialize(Ff7DebugVictory.Apply(cold, formation, kernel, 0, 2)), "Cold reward/RNG continuation changed the following battle.");
        FieldHostRequest fieldRandom = new(long.MaxValue, 0, 7, 0, new FieldRandomCommand());
        Check(session.Host.Execute(fieldRandom).Value == 202 && session.Progress.FieldRandomIndex == 1, "Original field random table/index update changed.");
        Check(session.Host.Execute(fieldRandom).Value == 202 && session.Progress.FieldRandomIndex == 1, "Repeated field RNG receipt consumed a second value.");
        using Ff7Session restoredSession = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Check(restoredSession.Host.Execute(fieldRandom).Value == 202 && restoredSession.Progress.FieldRandomIndex == 1, "Cold field RNG receipt changed its value.");
        Check(restoredSession.Host.Execute(fieldRandom with { Id = long.MaxValue - 1 }).Value == 238 && restoredSession.Progress.FieldRandomIndex == 2, "Cold field RNG next value changed.");
        Check(result.Characters.Skip(1).All(character => character.NativeRecord.SequenceEqual(initial.Characters.Single(original => original.Id == character.Id).NativeRecord)), "Unrecruited characters received automatic rewards.");
    }

    private static void RunInstalledSceneData(IReadOnlyList<SceneFormation> formations, SceneFormation opening, IReadOnlyList<KernelSection> kernel)
    {
        SceneBattleData[] decoded = formations.Select(SceneBattleData.Read).ToArray();
        Check(decoded.Length == 1024 && decoded.Select(formation => formation.FormationId).SequenceEqual(Enumerable.Range(0, 1024)), "The original scene archive must retain all physical formation IDs.");
        SceneBattleData battle = decoded[300];
        Check(battle.Enemies.Count == 2 && battle.Enemies.Select(enemy => enemy.FormationSlot).SequenceEqual(new[] { 0, 1 }) && battle.Enemies.All(enemy => enemy.Placement.EnemyId == 16), "Duplicate native enemies must retain separate formation slots.");
        SceneBattleEnemy first = battle.Enemies[0];
        Check(first.Placement.Position == new Vector3(-500, 0, -1500) && battle.Enemies[1].Placement.Position == new Vector3(500, 0, -1800) && first.InitialConditionFlags == uint.MaxValue, "Opening formation positions and full initial condition flags changed.");
        Check(first.Level == 2 && first.Speed == 50 && first.Strength == 6 && first.Defense == 2 && first.MaxHitPoints == 30 && first.MaxMagicPoints == 0, "Opening enemy native combat parameters changed.");
        Check(first.Attacks.Select(attack => (attack.AttackId, attack.ActionAnimation)).SequenceEqual(new (ushort, byte)[] { (272, 3), (273, 4) }), "Native action animation selectors no longer bind the enemy's attack IDs.");
        Check(battle.Cameras[0] == new SceneCameraPlacement(new Vector3(6173, -1776, 4517), new Vector3(0, -304, 392)), "Signed formation camera placement changed.");
        Check(battle.FormationScripts is null && first.Scripts is { Count: 16 } && first.Scripts[0].IsEmpty && first.Scripts[1].Span[..6].SequenceEqual(new byte[] { 0x11, 0x20, 0, 0x60, 0, 0x90 }), "Opening AI bank must preserve missing initialization and native main script bytes.");
        Check(first.Scripts![1].Span.SequenceEqual(battle.Enemies[1].Scripts![1].Span), "Duplicate placements must load the same prototype AI bytes.");
        IReadOnlyList<Ff7NativeEnemyState> initialized = Ff7NativeEnemyInitialization.Create(battle);
        Check(initialized[0] == new Ff7NativeEnemyState(4, 16, 30, 30, 0, 0, 4, 0, 31, 0, 0, 0, 1) && initialized[1] == initialized[0] with { ActorSlot = 5 }, "PC actor allocation, initial conditions, vitals or defense changed.");
        Ff7NativeEnemyState reactor = Ff7NativeEnemyInitialization.Create(decoded[308]).Single(enemy => enemy.EnemyId == 20);
        Check(reactor.ActorSlot == 6 && reactor.HitPoints == 18 && reactor.Statuses == 0 && reactor.StatusImmunities == 0x0620183C, "Reactor enemy native status complement or allocation changed.");
        Ff7BattleAttack machineGun = battle.Attacks.Single(attack => attack.Id == 272).Data;
        Check(machineGun.Accuracy == 100 && machineGun.ImpactSound == 15 && machineGun.DamageCalculation == 0x11 && machineGun.Power == 16 && machineGun.Elements == 0x2000 && machineGun.StatusMask == uint.MaxValue, "Scene attack effect, arithmetic or native mask fields changed.");
        Ff7BattleAttack fire = Ff7BattleAttack.Read(kernel.Single(section => section.Type == 1).Data.Span.Slice(27 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        Check(fire.CastingCost == 4 && fire.Elements == 1, "The original Fire MP cost or element changed.");

        byte[] invalidScene = opening.RawScene.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalidScene.AsSpan(0xE80), 5);
        ExpectInvalid(() => SceneBattleData.Read(opening with { RawScene = invalidScene }));
        BinaryPrimitives.WriteUInt16LittleEndian(invalidScene.AsSpan(0xE80), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(invalidScene.AsSpan(0xE88), 31);
        ExpectInvalid(() => SceneBattleData.Read(opening with { RawScene = invalidScene }));
        ExpectInvalid(() => SceneBattleData.Read(opening with { RawScene = opening.RawScene[..^1] }));
    }

    private static void RunInstalledModelData(Ff7Installation installation)
    {
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] ReadMember(string name) => archive.ReadEntry(archive.Find(name).Single());
        BattleModelData cloud = BattleModelData.Read(ReadMember("rtaa"), ReadMember("rtda"));
        Check(cloud.Type == 2 && cloud.Bones.Count == 23 && cloud.BodyClips.Count == 52 && cloud.WeaponClips.Count == 42, "Original Cloud body/weapon animation division changed.");
        Check(cloud.BodyClips[0] is { RotationCount: 24, FrameCount: 20, PackedData.Length: 860 } && cloud.WeaponClips[0] is { RotationCount: 1, FrameCount: 20, PackedData.Length: 112 }, "Cloud body and weapon clip bounds changed.");
        BattleActionBank cloudActions = BattleActionBank.Read(ReadMember("rtab"));
        Check(cloudActions.Scripts.Count == 74 && cloudActions.Scripts[0].Span.SequenceEqual(cloudActions.Scripts[^1].Span), "Cloud's action table lost its later bank or aliased idle sequence.");

        BattleModelData mp = BattleModelData.Read(ReadMember("aqaa"), ReadMember("aqda"));
        Check(mp.Type == 0 && mp.Bones.Count == 27 && mp.BodyClips.Count == 16 && mp.WeaponClips.Count == 16, "Opening MP model body/weapon animation ownership changed.");
        BattleActionBank mpActions = BattleActionBank.Read(ReadMember("aqab"));
        Check(mpActions.CollisionRadius == 350 && mpActions.Scripts.Count == 32 && mpActions.Scripts[3].Span.SequenceEqual(new byte[] { 0xFC, 0xEA, 0x02, 0xAD, 0x07, 0xB8, 0x01, 0x01, 0x0A, 0xD8, 0x01, 0x10, 0x00, 0xF7, 0x08, 0x03, 0x04, 0x05, 0xE5, 0xEE }), "Opening MP action 3 no longer points to the original bounded sequence.");
        Check(BattleActionBank.Read(ReadMember("obab")).Scripts.Count == 24, "The shorter original enemy action directory was lost.");

        byte[] malformedBank = ReadMember("aqab");
        BinaryPrimitives.WriteUInt32LittleEndian(malformedBank.AsSpan(108), 220);
        ExpectInvalid(() => BattleActionBank.Read(malformedBank));
        byte[] malformedPack = ReadMember("aqda");
        BinaryPrimitives.WriteInt32LittleEndian(malformedPack.AsSpan(12), int.MaxValue);
        ExpectInvalid(() => BattleModelData.Read(ReadMember("aqaa"), malformedPack));
    }

    private static void ExpectInvalid(Action read)
    {
        try
        {
            read();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new Exception("Invalid FFVII battle resource bounds were accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
