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
        Check(cloud == new Ff7BattlePlayerStats(new(6, 36, 23, 6, 14, 0, 0, 24, 17), new(0x11, 16, 96, CanBeBlinded: true), 0x23, 0x400), $"Original Cloud actor and weapon projection changed: {cloud}.");
        Check(party[1] == new Ff7BattlePlayerStats(new(1, 29, 11, 5, 13, 0, 0, 21, 9), new(0x11, 16, 97, LongRange: true, CanBeBlinded: true), 0x03, 0x2000), $"Original Barret actor and weapon projection changed: {party[1]}.");
        Check(party[2] == new Ff7BattlePlayerStats(new(1, 24, 11, 7, 14, 0, 0, 19, 10, CriticalBonus: 2), new(0x11, 16, 99, CanBeBlinded: true), 0x23, 0x800), $"Original Tifa actor and weapon projection changed: {party[2]}.");

        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
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
