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
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(formation => formation.FormationId == 300);
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
