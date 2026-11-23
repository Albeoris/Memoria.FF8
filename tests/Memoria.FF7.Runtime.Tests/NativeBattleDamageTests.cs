using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleDamageTests
{
    public static void Run()
    {
        PhysicalRolls();
        InstalledOpeningAttacks();
        InstalledAireTamStorm();
        InstalledLevelDeath();
    }

    public static void InstalledAdrenaline()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneBattleData data = SceneBattleData.Read(SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[900]);
        Ff7BattleAttack record = data.Attacks.Single(attack => attack.Id == 0x349).Data;
        const uint haste = Ff7NativeBattleStatuses.Haste;
        const uint slow = Ff7NativeBattleActionResolver.Slow;
        const uint barrier = Ff7NativeBattleStatuses.Barrier;
        Check(record is { DamageCalculation: 0x50, Power: 0, StatusChange: 63, StatusMask: 0x00010100, AdditionalEffect: byte.MaxValue, SpecialFlags: 0xFF7F }, "The installed Adrenaline record changed.");
        Ff7BattleDamageActor actor = Ff7NativeBattleDamage.FromEnemy(data.Enemies[0]);
        Ff7BattleActionVitals vitals = new(10000, 10000, 100, 100, slow, slow);
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record);
        Ff7BattleRngState seed = Ff7BattleRng.BeginBattle();
        Ff7BattleRng random = new(kernel[2].Data.Span, seed);
        Ff7BattleActionReceipt applied = Ff7NativeBattleActionResolver.Resolve(actor, vitals, actor, vitals, action, record, random, sameActor: true);
        Check(applied.Target.Statuses == (haste | barrier) && applied.RemovedStatuses == slow && applied.Target.Hp == vitals.Hp, "Adrenaline must bypass ordinary immunity, apply both statuses, and clear Slow without dealing damage.");
        Check(Ff7NativeBattleStatusClock.Apply(default, vitals.Statuses, applied.Target.Statuses).Barrier == 127, "Adrenaline must initialize the native Barrier timer.");

        Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.Resolve(actor, vitals, actor, vitals, action, record with { SpecialFlags = ushort.MaxValue }, random, sameActor: true);
        Check(immune.Target.Statuses == (slow | barrier) && immune.RemovedStatuses == 0, "Ordinary immunity to one time status must preserve the opposite status and still admit Barrier.");
        Check(JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(seed), "Adrenaline must not advance the native damage or status RNG.");
    }

    private static void PhysicalRolls()
    {
        byte[] kernel = new byte[3988];
        kernel[0xE1C] = 255;
        kernel[0xE1D] = 255;
        kernel[0xE1E] = 255;
        Ff7BattleDamageActor attacker = new(4, 32, 0, 0, 4, 0, 0, 0, 0);
        Ff7BattleDamageActor target = new(4, 0, 0, 0, 0, 0, 0, 0, 0, IsEnemy: true);
        Ff7BattleDamageAction action = new(0x11, 16, 100);
        Ff7BattleRng random = new(kernel, new(new byte[8]));
        Check(Ff7NativeBattleDamage.Resolve(attacker, target, action, random) == new Ff7BattleDamageResult(true, false, 0), "A native physical hit roll equal to accuracy must miss.");
        Check(random.Capture() is { WordCounter: 1, Stream: 0 } && random.Capture().Indices[0] == 4, "A physical miss consumes damage variance and skips critical randomness.");

        kernel[0xE1D] = 0;
        kernel[0xE1E] = 0;
        byte[] indices = [0, 6, 0, 0, 0, 0, 0, 0];
        random = new(kernel, new(indices));
        Check(Ff7NativeBattleDamage.Resolve(attacker, target, action, random) == new Ff7BattleDamageResult(false, true, 67), "A native critical roll equal to its threshold must double base damage before variance.");
        Check(random.Capture() is { WordCounter: 2, Stream: 1 } && random.Capture().Indices.SequenceEqual(new byte[] { 4, 8, 0, 0, 0, 0, 0, 0 }), "A native physical critical changed byte and word ownership.");

        Ff7BattleRng restored = new(kernel, JsonSerializer.Deserialize<Ff7BattleRngState>(JsonSerializer.Serialize(random.Capture()))!);
        Check(Ff7NativeBattleDamage.Resolve(attacker, target, action, random) == Ff7NativeBattleDamage.Resolve(attacker, target, action, restored), "The next damage result changed after restoring native RNG state.");
        Check(JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(restored.Capture()), "The next damage result advanced a different RNG stream after restoration.");
    }

    private static void InstalledOpeningAttacks()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<short> materiaModifiers = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        KernelInitialData initial = KernelInitialData.Read(kernel[3].Data.Span);
        Ff7CharacterState[] initialParty = initial.Characters.Take(3).Select(character => new Ff7CharacterState(character.Id, "", character.Level, character.Experience, character.CurrentHp, character.CurrentMp, character.Raw.ToArray())).ToArray();
        Ff7BattlePlayerStats[] party = initialParty.Select(character => Ff7NativeBattlePlayerStats.Create(character, kernel, materiaModifiers)).ToArray();

        Ff7BattlePlayerStats cloud = party[0];
        Check(cloud == new Ff7BattlePlayerStats(new(6, 36, 23, 6, 14, 0, 0, 24, 17) { EquippedMateriaCount = 2 }, new(0x11, 16, 96, CanBeBlinded: true) { NativePhysicalCategory = true, NativeElements = 0x400 }, 0x23, 0x400) { AttackFlags = 0xFFFB }, $"Original Cloud actor and weapon projection changed: {cloud}.");
        Check(party[1] == new Ff7BattlePlayerStats(new(1, 29, 11, 5, 13, 0, 0, 21, 9), new(0x11, 16, 97, LongRange: true, CanBeBlinded: true) { NativePhysicalCategory = true, NativeElements = 0x2000 }, 0x03, 0x2000) { AttackFlags = 0xFFFB }, $"Original Barret actor and weapon projection changed: {party[1]}.");
        Check(party[2] == new Ff7BattlePlayerStats(new(1, 24, 11, 7, 14, 0, 0, 19, 10, CriticalBonus: 2), new(0x11, 16, 99, CanBeBlinded: true) { NativePhysicalCategory = true, NativeElements = 0x800 }, 0x23, 0x800) { AttackFlags = 0xFFFB }, $"Original Tifa actor and weapon projection changed: {party[2]}.");

        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession battle = new(formations[324], progress, kernel, materiaModifiers, 1);
        Ff7BattleAiActor cloudVitals = battle.Actors.Single(row => row.Actor.Slot == 0).Actor;
        Ff7BattleAiActor barretVitals = battle.Actors.Single(row => row.Actor.Slot == 1).Actor;
        Check(cloudVitals is { HitPoints: 302, MaxHitPoints: 302, MagicPoints: 54, MaxMagicPoints: 56 } && barretVitals is { HitPoints: 222, MaxHitPoints: 222, MagicPoints: 15, MaxMagicPoints: 15 }, "Original battle entry must refresh and clamp party HP/MP from equipped base stats, without filling the adjusted MP cap.");
        Ff7BattleSnapshot saved = battle.Capture();
        saved.Actors[0] = saved.Actors[0] with { Actor = cloudVitals with { HitPoints = 201, MagicPoints = 45 } };
        Ff7BattleSession cold = new(formations[324], progress, kernel, materiaModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(saved)));
        Check(cold.Actors.Single(row => row.Actor.Slot == 0).Actor is { HitPoints: 201, MaxHitPoints: 302, MagicPoints: 45, MaxMagicPoints: 56 }, "Cold battle restore must retain its recorded current and maximum vitals.");

        SceneBattleData opening = SceneBattleData.Read(formations[300]);
        Ff7BattleDamageActor mp = Ff7NativeBattleDamage.FromEnemy(opening.Enemies[0]);
        Ff7BattleDamageAction mpAttack = Ff7BattleDamageAction.FromAttack(opening.Attacks.Single(attack => attack.Id == 272).Data);
        CheckInstalledHit(kernel[2], mp, cloud.Actor, mpAttack, 4, 2, 45);
        CheckInstalledHit(kernel[2], cloud.Actor, mp, cloud.Attack, 39, 2, 45);
        byte[] backRowRecord = initialParty[0].NativeRecord.ToArray();
        backRowRecord[32] &= 0xFE;
        Ff7BattlePlayerStats backRowCloud = Ff7NativeBattlePlayerStats.Create(initialParty[0] with { NativeRecord = backRowRecord }, kernel, materiaModifiers);
        Check(backRowCloud.Actor.BackRow, "A clear native row bit must place the actor in back.");
        CheckInstalledHit(kernel[2], backRowCloud.Actor, mp, backRowCloud.Attack, 19, 2, 45);
        CheckInstalledHit(kernel[2], mp, backRowCloud.Actor, mpAttack, 4, 2, 45);

        Ff7BattleAttack boltRecord = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x21 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        Ff7BattleDamageAction bolt = Ff7BattleDamageAction.FromAttack(boltRecord);
        Ff7BattleDamageAction cure = Ff7BattleDamageAction.FromAttack(Ff7BattleAttack.Read(kernel[1].Data.Span[..Ff7BattleAttack.RecordSize]));
        Ff7BattleDamageAction potion = Ff7BattleDamageAction.FromAttack(Ff7BattleAttack.Read(kernel[4].Data.Span[..Ff7BattleAttack.RecordSize]));
        CheckInstalledHit(kernel[2], cloud.Actor, mp, bolt, 83, 0, 44);
        CheckInstalledHit(kernel[2], cloud.Actor, cloud.Actor, cure, -271, 0, 44);
        CheckInstalledHit(kernel[2], cloud.Actor, cloud.Actor, potion, -100, 0, 43);

        SceneBattleData reactor = SceneBattleData.Read(formations[324]);
        SceneBattleEnemy scorpion = reactor.Enemies.Single();
        Ff7BattleDamageAffinity scorpionAffinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(scorpion, boltRecord.Elements);
        Check(scorpionAffinity == Ff7BattleDamageAffinity.Double, "The original Guard Scorpion Lightning weakness changed.");
        CheckInstalledHit(kernel[2], cloud.Actor, Ff7NativeBattleDamage.FromEnemy(scorpion), bolt with { Affinity = scorpionAffinity }, 68, 0, 44);

        SceneBattleData corridor = SceneBattleData.Read(formations[337]);
        Ff7BattleDamageActor chuseTank = Ff7NativeBattleDamage.FromEnemy(corridor.Enemies[0]);
        Ff7BattleDamageAction rollingClaw = Ff7BattleDamageAction.FromAttack(corridor.Attacks.Single(attack => attack.Id == 0x125).Data);
        CheckInstalledHit(kernel[2], chuseTank, cloud.Actor with { CurrentHitPoints = 302 }, rollingClaw, 37, 0, 43);

        SceneBattleData subway = SceneBattleData.Read(formations[341]);
        Ff7BattleDamageAction bubbles = Ff7BattleDamageAction.FromAttack(subway.Attacks.Single(attack => attack.Id == 0x127).Data);
        Check(bubbles.HasStatusOperation && bubbles.AddsStatuses, "The original status-only action lost its add-status descriptor.");
        CheckInstalledHit(kernel[2], Ff7NativeBattleDamage.FromEnemy(subway.Enemies.First(enemy => enemy.Placement.EnemyId == 27)), cloud.Actor, bubbles, 0, 0, 41);
        CheckInstalledHit(kernel[2], Ff7NativeBattleDamage.FromEnemy(scorpion), cloud.Actor, Ff7BattleDamageAction.FromAttack(reactor.Attacks.Single(attack => attack.Id == 0x11F).Data), 0, 0, 41);
    }

    private static void InstalledAireTamStorm()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneBattleData data = SceneBattleData.Read(SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[984]);
        Ff7BattleAttack record = data.Attacks.Single(attack => attack.Id == 0x3E1).Data;
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record);
        Ff7BattleDamageActor attacker = Ff7NativeBattleDamage.FromEnemy(data.Enemies[0]);
        const uint dispelled = 0x10178700;
        const uint poison = 8;
        Ff7BattleDamageActor player = new(99, 255, 255, 255, 255, 0, 0, 255, 255);
        Ff7BattleActionVitals source = new(1_000_000, 1_000_000, 100, 100, 0);
        foreach ((byte count, int damage) in new (byte, int)[] { (0, 0), (1, 1111), (8, 8888), (9, 9999), (16, 9999) })
        {
            Ff7BattleRngState seed = Ff7BattleRng.BeginBattle();
            Ff7BattleRng random = new(kernel[2].Data.Span, seed);
            Ff7BattleActionReceipt hit = Ff7NativeBattleActionResolver.Resolve(attacker, source, player with { EquippedMateriaCount = count }, new(9999, 9999, 999, 999, dispelled | poison), action, record, random);
            Check(hit.Damage == new Ff7BattleDamageResult(false, false, damage) && hit.RemovedStatuses == dispelled && (hit.Target.Statuses & poison) != 0 && hit.Target.Hp == 9999 - damage &&
                JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(seed), $"Original Aire Tam Storm failed materia count {count}, dispel, damage cap or RNG ownership. {hit} rng={JsonSerializer.Serialize(random.Capture())}");
        }
        Ff7BattleRng resistRandom = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle());
        Ff7BattleActionReceipt resisted = Ff7NativeBattleActionResolver.Resolve(attacker, source, player, new(9999, 9999, 999, 999, dispelled | 0x20000000), action, record, resistRandom);
        Check(resisted.RemovedStatuses == 0x20000000 && resisted.Target.Statuses == dispelled, "Native Resist protects the other statuses from the same dispel contact.");
    }

    private static void InstalledLevelDeath()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneBattleData data = SceneBattleData.Read(SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[869]);
        Ff7BattleAttack record = data.Attacks.Single(attack => attack.Id == 0x31E).Data;
        Check(record is { DamageCalculation: 0x83, Accuracy: 4, Power: 0, StatusChange: 63, StatusMask: 1, Elements: 0x2000 }, "The installed level-based Death attack record changed.");

        SceneBattleEnemy enemy = data.Enemies.First(enemy => enemy.Placement.EnemyId == 295);
        Ff7BattleDamageActor attacker = Ff7NativeBattleDamage.FromEnemy(enemy);
        Ff7BattleDamageActor player = new(40, 0, 0, 0, 0, 0, 0, 0, 0);
        Ff7BattleActionVitals source = new(enemy.MaxHitPoints, enemy.MaxHitPoints, enemy.MaxMagicPoints, enemy.MaxMagicPoints, 0);
        Ff7BattleActionVitals target = new(101, 300, 89, 100, 0);
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record);
        Ff7BattleRngState seed = Ff7BattleRng.BeginBattle();
        Ff7BattleRng random = new(kernel[2].Data.Span, seed);

        Ff7BattleActionReceipt matched = Ff7NativeBattleActionResolver.Resolve(attacker, source, player, target, action, record, random);
        Check(matched.Damage == new Ff7BattleDamageResult(false, false, 0) && matched.AddedStatuses == Ff7NativeBattleActionResolver.Death && matched.SuppressNumericPopup, "The matching level must admit Death with zero numeric damage.");

        Ff7BattleActionReceipt missed = Ff7NativeBattleActionResolver.Resolve(attacker, source, player with { Level = 41 }, target, action, record, random);
        Check(missed.Damage.Missed && missed.Target == target && missed.AddedStatuses == 0, "A nonmatching level must discard the pending Death status.");

        Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.Resolve(attacker, source, player, target with { StatusImmunities = Ff7NativeBattleActionResolver.Death }, action, record, random);
        Check(immune.Target.Hp == target.Hp && immune.AddedStatuses == 0, "Death immunity must protect a matching level.");

        Ff7BattleActionVitals shielded = target with { Statuses = Ff7NativeBattleStatuses.Shield };
        Ff7BattleActionReceipt nullified = Ff7NativeBattleActionResolver.Resolve(attacker, source, player, shielded, action, record, random);
        Check(nullified.Damage.Missed && nullified.Target == shielded && nullified.AddedStatuses == 0, "Nullification must clear the pending status transaction.");

        Ff7BattleDamageAction fractional = action with { Power = 12, Split = true, Affinity = Ff7BattleDamageAffinity.Half };
        Check(Ff7NativeBattleDamage.Resolve(attacker, player with { CurrentHitPoints = target.Hp }, fractional, random) == new Ff7BattleDamageResult(false, false, 9), "A powered level formula must scale current HP, split, then apply affinity.");
        Check(Ff7NativeBattleDamage.Resolve(attacker, player with { Level = 41, CurrentMagicPoints = target.Mp }, fractional with { Accuracy = 0, TargetsMagicPoints = true }, random) == new Ff7BattleDamageResult(false, false, 8), "A zero divisor skips the level check, and the MP variant must use current MP.");
        Check(JsonSerializer.Serialize(random.Capture()) == JsonSerializer.Serialize(seed), "The level predicate, fraction, and guaranteed status must not advance native RNG.");
    }

    private static void CheckInstalledHit(KernelSection randomTable, Ff7BattleDamageActor attacker, Ff7BattleDamageActor target, Ff7BattleDamageAction action, int expectedDamage, uint wordCount, byte firstStreamIndex)
    {
        Ff7BattleRng random = new(randomTable.Data.Span, Ff7BattleRng.BeginBattle());
        Ff7BattleDamageResult result = Ff7NativeBattleDamage.Resolve(attacker, target, action, random);
        Check(result == new Ff7BattleDamageResult(false, false, expectedDamage), $"Native formula 0x{action.Formula:X2} produced {result} with the original RNG table.");
        Ff7BattleRngState state = random.Capture();
        Check(state.WordCounter == wordCount && state.Indices[0] == firstStreamIndex, $"Native formula 0x{action.Formula:X2} advanced different original RNG entries.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
