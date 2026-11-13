using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattlePartyRosterTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original native party battle roster: MEMORIA_FF7_GAME_DIR is unset.");
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
        IReadOnlyList<Ff7BattleAttack> limits = Ff7PcExecutableData.LoadBattleLimits(installation);
        SceneFormation formation = formations[300];
        int[] gaugeDivisors = [200, 195, 200, 160, 202, 200];
        byte[] standardWeapons = [62, 48, 87, 64, 96, 73];
        byte[] learned = [0x8E, 0xA3, 0xB1, 0xAA, 0xAD, 0x9C];
        for (int characterId = 3; characterId <= 8; characterId++)
        {
            Ff7CharacterState character = original.Progress.Characters.Single(character => character.Id == characterId);
            Check(Ff7NativeBattleLimits.ReadGaugeDivisor(character, kernel) == gaugeDivisors[characterId - 3], $"Character {characterId} did not use their original Limit divisor.");
            Check(Ff7NativeBattleLimits.ReadLearnedIds(character, kernel).SequenceEqual(new byte[] { learned[characterId - 3] }), $"Character {characterId} did not retain the first learned Limit.");
            Ff7CharacterState[] characters = original.Progress.Characters.ToArray();
            byte[] record = (byte[])character.NativeRecord.Clone();
            record[28] = standardWeapons[characterId - 3];
            record[30] = byte.MaxValue;
            record.AsSpan(64, 64).Fill(byte.MaxValue);
            characters[Array.IndexOf(characters, character)] = character with { NativeRecord = record };
            Ff7Progress progress = original.Progress with { Party = [characterId, 255, 255], Characters = characters };
            Ff7BattleSession Open(Ff7BattleSnapshot? snapshot = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: snapshot, animations: animations, limits: limits);
            Ff7BattleSnapshot ready = Open().Capture();
            Ff7BattleActorState actor = ready.Actors[0];
            ready.Actors[0] = actor with { AwaitingTurn = true, Gauge = ushort.MaxValue };
            Ff7BattleSession battle = Open(ready);
            Check(battle.Actors[0].Identity == characterId && battle.ReadyPlayer == 0, $"Character {characterId} did not reach the turn menu.");
            Check(battle.Submit(0, Ff7BattleCommand.Attack, 0, 0x10), $"Character {characterId} could not submit their ordinary Attack.");
            Ff7BattleSession? cold = null;
            bool finished = false;
            for (int step = 0; step < 900; step++)
            {
                battle.Advance(waitForCommand: true);
                if (cold is not null)
                {
                    cold.Advance(waitForCommand: true);
                    Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Character {characterId} changed across the live Attack restore.");
                }
                else if (battle.Action?.Pose is { Completed: false } && battle.LastHit is { Source: 0 })
                {
                    cold = Open(JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                }
                if (battle.Action is null && battle.LastHit is { Source: 0, Target: 4 })
                {
                    finished = true;
                    break;
                }
            }
            Check(finished && cold is not null, $"Character {characterId} did not complete the original Attack action and presentation.");
        }
        Console.WriteLine("Original Aerith, Red XIII, Yuffie, Cait Sith, Vincent and Cid initialize, attack, and resume their live animation snapshots.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
