using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleTests
{
    public static void Run()
    {
        NativeBattleAiTests.RunContracts();
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
        RunAtb(kernel);
        RunInstalled();
    }

    private static void RunAtb(ReadOnlySpan<byte> kernel)
    {
        const ushort slots = 0x31;
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        ushort[] normal = Ff7NativeAtb.Initialize(slots, 0, false, random);
        Check(normal.Length == 10 && normal[0] == 0xE000 && normal[4] == 0xDF81 && normal[5] == 0xDF80 && normal[3] == 0, "The native ATB initial slot order, relative spread or missing slot changed.");
        Check(random.Capture().WordCounter == 3, "Initial ATB must consume one word per active actor.");
        ushort[] forced = Ff7NativeAtb.Initialize(slots, 2, true, new(kernel, new(new byte[8])));
        Check(forced[0] == 0xFFFE && forced[4] == 0 && forced[5] == 0, "The native forced party-ready mask changed.");
        int denominator = Ff7NativeAtb.SpeedDenominator([6, 7]);
        Check(denominator == 57 && Ff7NativeAtb.BaseRate(128) == 91, "Native battle speed division changed.");
        Check(Ff7NativeAtb.Increment(4, 50, 0, 0, denominator, 128) == 159, "Enemy speed must use its prototype rather than the party offset.");
        Check(Ff7NativeAtb.Increment(0, 20, -50, 0x100, denominator, 128) == 383, "The native signed Dexterity modifier, party offset or Haste rate changed.");
        Check(Ff7NativeAtb.Increment(0, 20, 0, 0x200, denominator, 128) == 111 && Ff7NativeAtb.Increment(0, 20, 0, 0x400, denominator, 128) == 0, "Native Slow or Stop timing changed.");
        ushort gauge = 0xFF00;
        Check(!Ff7NativeAtb.Advance(ref gauge, 80, true, true) && gauge == 0xFF00, "Frozen actors must retain their gauge.");
        Check(Ff7NativeAtb.Advance(ref gauge, 80, true, false) && gauge == ushort.MaxValue && !Ff7NativeAtb.Advance(ref gauge, 80, false, false), "A saturated gauge must emit a single readiness edge.");
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
        NativeBattleAiTests.RunInstalled(formation, kernel, initial);
        NativeBattleAiTests.RunInstalledMatrix(formations, kernel, initial);
        string untouched = JsonSerializer.Serialize(initial);
        RunStartingSpells(initial, kernel);
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

    private static void RunStartingSpells(Ff7Progress progress, IReadOnlyList<KernelSection> kernel)
    {
        Ff7CharacterState cloud = progress.Characters[0];
        IReadOnlyList<Ff7BattleSpell> starting = Ff7NativeBattleSpells.Read(cloud, kernel);
        Check(starting.Select(spell => (spell.AttackId, spell.Attack.CastingCost)).SequenceEqual(new (byte, ushort)[] { (0x1E, 4), (0x21, 4) }), "Opening Cloud's equipped Ice and Bolt were not admitted with their original MP costs.");
        Check(progress.Characters.Skip(1).Take(2).All(character => Ff7NativeBattleSpells.Read(character, kernel).Count == 0), "Unequipped opening party members acquired magic.");
        byte[] record = (byte[])cloud.NativeRecord.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x34 | 1999u << 8);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(72), 0x35);
        IReadOnlyList<Ff7BattleSpell> before = Ff7NativeBattleSpells.Read(cloud with { NativeRecord = record }, kernel);
        Check(before.Select(spell => spell.AttackId).SequenceEqual(new byte[] { 0, 0x1E, 0x21 }) && before[0].Attack.CastingCost == 5, "Native Cure admission or the below-threshold Bolt2 gate changed.");
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x34 | 2000u << 8);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(76), 0x34);
        IReadOnlyList<Ff7BattleSpell> at = Ff7NativeBattleSpells.Read(cloud with { NativeRecord = record }, kernel);
        Check(at.Select(spell => spell.AttackId).SequenceEqual(new byte[] { 0, 0x1E, 0x21, 0x22 }) && at[^1].Attack.CastingCost == 22, "Native AP threshold or deduplicated Bolt2 admission changed.");
    }

    public static void RunOpeningRoster()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed opening battle roster: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        int[] opening = [.. Enumerable.Range(300, 25), 328, 329, 330, .. Enumerable.Range(332, 28), 364, 367, 392, 393, 394, 395];
        short[] arenas = opening.Select(id => formations[id].LocationId).Distinct().Order().ToArray();
        Check(arenas.SequenceEqual(new short[] { 9, 10, 11, 12, 13, 25, 26, 29, 41, 49, 85 }), "The opening arena census differs from the original field resources.");
        SceneBattleData[] battles = opening.Select(id => SceneBattleData.Read(formations[id])).ToArray();
        short[] enemies = battles.SelectMany(battle => battle.Enemies.Select(enemy => enemy.Placement.EnemyId)).Distinct().Order().ToArray();
        Check(enemies.SequenceEqual(new short[] { 16, 17, 18, 19, 20, 21, 22, 23, 24, 26, 27, 29, 30, 31, 32, 33 }), "The opening enemy prototype census changed.");
        foreach (SceneBattleData battle in battles)
        {
            IReadOnlyList<Ff7NativeEnemyState> actors = Ff7NativeEnemyInitialization.Create(battle);
            Check(actors.Count != 0 && actors.Select(actor => actor.ActorSlot).SequenceEqual(battle.Enemies.Select(enemy => enemy.FormationSlot + 4)), "Opening encounter lost a physical actor slot.");
        }

        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        foreach (short arena in arenas)
        {
            string prefix = ModelPrefix(370 + arena);
            Check(ReadMember(prefix + "ac").Length > 0 && ReadMember(prefix + "am").Length > 0, $"Opening arena {arena} is missing its original resources.");
        }
        int clips = 0;
        foreach (string prefix in enemies.Select(enemy => ModelPrefix(enemy)).Concat(["rt", "sb", "ru"]))
        {
            BattleModelData model = BattleModelData.Read(ReadMember(prefix + "aa"), ReadMember(prefix + "da"));
            Check(model.BodyClips.Count > 0, $"Opening battle model {prefix} requires original body animation.");
            Check(BattleActionBank.Read(ReadMember(prefix + "ab")).Scripts.Count > 0, $"Opening battle model {prefix} requires its original action bank.");
            foreach ((BattlePackedClip clip, int index) in model.BodyClips.Concat(model.WeaponClips).Select((clip, index) => (clip, index)))
            {
                if (clip.PackedData.Length == 4)
                {
                    continue;
                }
                try
                {
                    Check(BattleClipDecoder.Decode(clip).Count == clip.FrameCount, "Native clip frame count changed.");
                    clips++;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"Original battle model {prefix}, packed clip {index}, could not initialize.", exception);
                }
            }
        }
        Console.WriteLine($"Original opening startup census: {battles.Length} formations, {arenas.Length} arenas, {enemies.Length} enemy prototypes, 3 player model banks, {clips} decoded body and weapon clips.");

        byte[] ReadMember(string name) => archive.ReadEntry(archive.Find(name).Single());
        static string ModelPrefix(int index) => new(new[] { (char)('a' + index / 26), (char)('a' + index % 26) });
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
        Check(ClipHash(cloud.BodyClips[15]) == "DEBEE1130D80DE807E5E294C90701F1404D66EFB91C99F9329227634F3B38F8A" && ClipHash(cloud.WeaponClips[0]) == "E73FFBCC475A13604423A55ACFEECD0DAFE85B573CDB136FAA9FFA7605216126", "Original Cloud body or weapon packed frame words differ from the native decoder.");
        BattleActionBank cloudActions = BattleActionBank.Read(ReadMember("rtab"));
        Check(cloudActions.Scripts.Count == 74 && cloudActions.Scripts[0].Span.SequenceEqual(cloudActions.Scripts[^1].Span), "Cloud's action table lost its later bank or aliased idle sequence.");

        BattleModelData mp = BattleModelData.Read(ReadMember("aqaa"), ReadMember("aqda"));
        Check(mp.Type == 0 && mp.Bones.Count == 27 && mp.BodyClips.Count == 16 && mp.WeaponClips.Count == 16, "Opening MP model body/weapon animation ownership changed.");
        Check(ClipHash(mp.BodyClips[2]) == "B67B3C90B214680299E716EB7A2F3FAFF82B2BA61FCB2C788DBDF57C1F4D6483" && ClipHash(mp.BodyClips[5]) == "51FBCEDA76F40D250B0B3F349CCD9CB19F5EE3BDC2C9814565F69B199286401A" && ClipHash(mp.BodyClips[8]) == "B472204DB4E4EE7EF3377120D96B948A006546ADC90941AD5FF1921688A3D083", "Original MP action frame words differ from the native decoder.");
        ExpectInvalid(() => BattleClipDecoder.Decode(mp.BodyClips[2] with { PackedData = mp.BodyClips[2].PackedData[..^10] }));
        BattleActionBank mpActions = BattleActionBank.Read(ReadMember("aqab"));
        Check(mpActions.CollisionRadius == 350 && mpActions.Scripts.Count == 32 && mpActions.Scripts[3].Span.SequenceEqual(new byte[] { 0xFC, 0xEA, 0x02, 0xAD, 0x07, 0xB8, 0x01, 0x01, 0x0A, 0xD8, 0x01, 0x10, 0x00, 0xF7, 0x08, 0x03, 0x04, 0x05, 0xE5, 0xEE }), "Opening MP action 3 no longer points to the original bounded sequence.");
        RunOpeningMpActions(mpActions, mp.BodyClips.Count);
        Check(BattleActionBank.Read(ReadMember("obab")).Scripts.Count == 24, "The shorter original enemy action directory was lost.");

        byte[] malformedBank = ReadMember("aqab");
        BinaryPrimitives.WriteUInt32LittleEndian(malformedBank.AsSpan(108), 220);
        ExpectInvalid(() => BattleActionBank.Read(malformedBank));
        byte[] malformedPack = ReadMember("aqda");
        BinaryPrimitives.WriteInt32LittleEndian(malformedPack.AsSpan(12), int.MaxValue);
        ExpectInvalid(() => BattleModelData.Read(ReadMember("aqaa"), malformedPack));
    }

    private static string ClipHash(BattlePackedClip clip)
    {
        using MemoryStream output = new();
        using BinaryWriter words = new(output);
        foreach (BattleClipFrame frame in BattleClipDecoder.Decode(clip))
        {
            words.Write(frame.X);
            words.Write(frame.Y);
            words.Write(frame.Z);
            foreach (BattleRotationWords rotation in frame.Rotations)
            {
                words.Write(rotation.X);
                words.Write(rotation.Y);
                words.Write(rotation.Z);
            }
        }
        return Convert.ToHexString(SHA256.HashData(output.ToArray()));
    }

    private static void RunOpeningMpActions(BattleActionBank bank, int clipCount)
    {
        for (int action = 3; action <= 4; action++)
        {
            IReadOnlyList<BattleActionInstruction> instructions = BattleActionInstructions.ReadUntilIdle(bank.Scripts[action], clipCount);
            BattleActionInstruction hit = instructions.Single(instruction => instruction.Opcode == 0xF7);
            int delay = action == 3 ? 8 : 4;
            Check(hit.Operands.Span[0] == delay && instructions[^1].Opcode == 0xEE, "Original MP action target reaction or idle transfer changed.");
            Ff7BattleTargetCallbacks schedule = Ff7BattleTargetCallbacks.Schedule(0x11, (byte)delay);
            for (int callback = 0; callback < delay; callback++)
            {
                schedule = schedule.Advance(false, out int? early);
                Check(early is null, "Native target reaction fired before the authored delay.");
            }
            Check(schedule.Advance(true, out int? paused) == schedule && paused is null, "Battle pause advanced the native target reaction.");
            Ff7BattleTargetCallbacks cold = JsonSerializer.Deserialize<Ff7BattleTargetCallbacks>(JsonSerializer.Serialize(schedule))!;
            schedule = schedule.Advance(false, out int? first);
            Check(schedule == cold.Advance(false, out int? restored) && first == 0 && restored == first, "Cold target reaction changed the due native target.");
            schedule = schedule.Advance(false, out int? second);
            Check(second == 4 && schedule.RemainingTargets == 0 && schedule.Advance(false, out int? extra) == schedule && extra is null, "Native reactions must consume each target slot once in physical order.");
        }
        ExpectInvalid(() => BattleActionInstructions.ReadUntilIdle(new byte[] { 0xAD, 1 }, clipCount));
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
